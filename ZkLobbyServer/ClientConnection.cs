﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using LobbyClient;
using RestSharp.Extensions;
using ZkData;
using Timer = System.Timers.Timer;

namespace ZkLobbyServer
{
    public class ClientConnection:ICommandSender
    {
        string Name
        {
            get
            {
                if (connectedUser != null) return connectedUser.Name;
                else return null;
            }
        }
        ConnectedUser connectedUser;
        private bool loginAttempted;

        readonly int number;

        readonly ZkLobbyServer server;

        ITransport transport;
        public string RemoteEndpointIP
        {
            get { return transport.RemoteEndpointAddress; }
        }


        public ClientConnection(ITransport transport, ZkLobbyServer server)
        {
            this.server = server;
            number = Interlocked.Increment(ref server.ClientCounter);
            this.transport = transport;

            transport.ConnectAndRun(OnCommandReceived, OnConnected, OnConnectionClosed).ConfigureAwait(false);
        }

        public async Task OnCommandReceived(string line)
        {
            try
            {
                dynamic obj = server.Serializer.DeserializeLine(line);
                if (obj is Login || obj is Register) await Process(obj);
                else await connectedUser.Process(obj);
            }
            catch (Exception ex)
            {
                var message = string.Format("{0} error processing line {1} : {2}", this, line, ex);
                Trace.TraceError(message);
                SendCommand(new Say() { Place = SayPlace.MessageBox, Target = Name, User = Name, Text = message });
            }
        }

        public async Task OnConnected()
        {
            //Trace.TraceInformation("{0} connected", this);
            await SendCommand(new Welcome() { Engine = server.Engine, Game = server.Game, Version = server.Version });
        }


        public async Task OnConnectionClosed(bool wasRequested)
        {
            var reason = wasRequested ? "quit" : "connection failed";
            if (!string.IsNullOrEmpty(Name)) await connectedUser.RemoveConnection(this, reason);
            //Trace.TraceInformation("{0} {1}", this, reason);
        }


        public async Task Process(Login login)
        {
            loginAttempted = true;
            var ret = await Task.Run(()=>server.LoginChecker.Login(login, RemoteEndpointIP));
            if (ret.LoginResponse.ResultCode == LoginResponse.Code.Ok)
            {
                var user = ret.User;
                //Trace.TraceInformation("{0} login: {1}", this, response.ResultCode.Description());
                
                await this.SendCommand(user); // send self to self first

                connectedUser = server.ConnectedUsers.GetOrAdd(user.Name, (n) => new ConnectedUser(server, user));
                connectedUser.User = user;
                connectedUser.Connections.TryAdd(this, true);


                // mutually syncs users based on visibility rules
                await server.TwoWaySyncUsers(Name, server.ConnectedUsers.Keys);

                await SendCommand(ret.LoginResponse); // login accepted


                foreach (var b in server.Battles.Values)
                {
                    if (b != null)
                    {
                        await
                            SendCommand(new BattleAdded()
                            {
                                Header = b.GetHeader()
                            });

                        foreach (var u in b.Users.Keys.Where(x=> x!=null && server.CanUserSee(Name, x))) await SendCommand(new JoinedBattle() { BattleID = b.BattleID, User = u });
                    }
                }


                await server.OfflineMessageHandler.SendMissedMessages(this, SayPlace.User, Name, user.AccountID);

                var defChans = await server.ChannelManager.GetDefaultChannels(user.AccountID); 
                defChans.AddRange(server.Channels.Where(x=>x.Value.Users.ContainsKey(user.Name)).Select(x=>x.Key)); // add currently connected channels to list too
                
                foreach (var chan in defChans.ToList().Distinct()) {
                    await connectedUser.Process(new JoinChannel() {
                        ChannelName = chan,
                        Password = null
                    });
                }


                await SendCommand(new FriendList() { Friends = connectedUser.Friends.ToList() });
                await SendCommand(new IgnoreList() { Ignores = connectedUser.Ignores.ToList() });

                await server.MatchMaker.OnLoginAccepted(connectedUser);
            }
            else
            {
                await SendCommand(ret.LoginResponse);
                if (ret.LoginResponse.ResultCode == LoginResponse.Code.Banned) transport.RequestClose();
            }
        }



        public async Task Process(Register register)
        {
            var response = new RegisterResponse();
            if (!Account.IsValidLobbyName(register.Name) || string.IsNullOrEmpty(register.PasswordHash)) response.ResultCode = RegisterResponse.Code.InvalidCharacters;
            else if (server.ConnectedUsers.ContainsKey(register.Name)) response.ResultCode = RegisterResponse.Code.AlreadyConnected;
            else
            {
                await Task.Run(() =>
                {
                    using (var db = new ZkDataContext())
                    {
                        var acc = db.Accounts.FirstOrDefault(x => x.Name == register.Name);
                        if (acc != null) response.ResultCode = RegisterResponse.Code.InvalidName;
                        else
                        {
                            if (string.IsNullOrEmpty(register.PasswordHash)) response.ResultCode = RegisterResponse.Code.InvalidPassword;
                            else
                            {
                                acc = new Account() { Name = register.Name };
                                acc.SetPasswordHashed(register.PasswordHash);
                                acc.SetName(register.Name);
                                acc.SetAvatar();
                                db.Accounts.Add(acc);
                                db.SaveChanges();

                                response.ResultCode = RegisterResponse.Code.Ok;
                            }
                        }
                    }
                });
            }

            //Trace.TraceInformation("{0} login: {1}", this, response.ResultCode.Description());
            await SendCommand(response);
        }

        public void RequestClose()
        {
            transport.RequestClose();
        }

        public async Task SendCommand<T>(T data)
        {
            try
            {
                var line = server.Serializer.SerializeToLine(data);
                await SendLine(line);
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0} error sending {1} : {2}", this, data, ex);
            }
        }


        public async Task SendLine(string line)
        {
            try
            {
                await transport.SendLine(line);
            }
            catch (Exception ex)
            {
                Trace.TraceError("{0} error sending {1} : {2}", this, line, ex);
            }
        }


        public override string ToString()
        {
            return string.Format("[{0} {1}:{2} {3}]", number, transport.RemoteEndpointAddress, transport.RemoteEndpointPort, Name);
        }

    }
}