﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Steamworks;
using ZkData;

namespace ChobbyLauncher
{
    // see handling corrupted state exceptions https://msdn.microsoft.com/en-us/magazine/dd419661.aspx?f=255&MSPPError=-2147217396
    public class SteamClientHelper : IDisposable
    {
        public enum OverlayOption
        {
            LobbyInvite,
            Friends,
            Community,
            Players,
            Settings,
            OfficialGameGroup,
            Achievements
        }

        private Callback<GameLobbyJoinRequested_t> lobbyJoinRequestCallback;
        private Callback<GameOverlayActivated_t> overlayActivatedCallback;

        private int tickCounter;
        private Timer timer;
        public bool IsOnline { get; private set; }

        public event Action<bool> OverlayActivated = (b) => { };
        public event Action SteamOffline = () => { };
        public event Action SteamOnline = () => { };


        public void Dispose()
        {
            try
            {
                if (timer != null) timer.Dispose();
                if (IsOnline) SteamAPI.Shutdown();
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        public void ConnectToSteam()
        {
            TimerOnElapsed(this);
            timer = new Timer(TimerOnElapsed, null, 100, 100);
        }


        public void CreateLobbyAsync(Action<ulong?> onCreated)
        {
            if (IsOnline)
            {
                var onLobbyCreated = new CallResult<LobbyCreated_t>();
                var callID = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 16);
                onLobbyCreated.Set(callID,
                    (t, failure) =>
                    {
                        if (!failure && (t.m_eResult == EResult.k_EResultOK)) onCreated?.Invoke(t.m_ulSteamIDLobby);
                        else onCreated?.Invoke((ulong?)null);
                    });
            }
        }

        private byte[] GetClientAuthToken()
        {
            var buf = new byte[256];
            uint ticketSize;
            SteamUser.GetAuthSessionTicket(buf, buf.Length, out ticketSize);
            var truncArray = new byte[ticketSize];
            Array.Copy(buf, truncArray, truncArray.Length);
            return truncArray;
        }


        public string GetClientAuthTokenHex()
        {
            if (IsOnline) return GetClientAuthToken().ToHex();
            else return null;
        }


        public List<ulong> GetFriends()
        {
            if (IsOnline)
            {
                var ret = new List<ulong>();
                var cnt = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
                for (var i = 0; i < cnt; i++) ret.Add(SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate).m_SteamID);
                return ret;
            }
            return null;
        }


        public ulong? GetLobbyOwner(ulong lobbyID)
        {
            if (IsOnline) return SteamMatchmaking.GetLobbyOwner(new CSteamID(lobbyID)).m_SteamID;
            return null;
        }

        public string GetMyName()
        {
            if (IsOnline) return SteamFriends.GetPersonaName();
            return null;
        }

        public ulong GetSteamID()
        {
            if (IsOnline) return SteamUser.GetSteamID().m_SteamID;
            return 0;
        }


        public void InviteFriendToGame(ulong lobbyID, ulong friendID)
        {
            if (IsOnline) SteamMatchmaking.InviteUserToLobby(new CSteamID(lobbyID), new CSteamID(friendID));
        }

        public event Action<ulong> JoinFriendRequest = (steamID) => { };

        public void OpenOverlaySection(OverlayOption option)
        {
            if (IsOnline) SteamFriends.ActivateGameOverlay(option.ToString());
        }

        public void OpenOverlayWebsite(string url)
        {
            if (IsOnline) SteamFriends.ActivateGameOverlayToWebPage(url);
        }



        [HandleProcessCorruptedStateExceptions]
        private void TimerOnElapsed(object sender)
        {
            try
            {
                if (tickCounter % 300 == 0)
                    if (!IsOnline)
                        if (SteamAPI.Init() && SteamAPI.IsSteamRunning())
                        {
                            IsOnline = true;

                            lobbyJoinRequestCallback = new Callback<GameLobbyJoinRequested_t>(t => { JoinFriendRequest(t.m_steamIDFriend.m_SteamID); });
                            overlayActivatedCallback = new Callback<GameOverlayActivated_t>(t => { OverlayActivated(t.m_bActive != 0); });

                            SteamOnline();
                        }
                if (IsOnline)
                    if (SteamAPI.IsSteamRunning()) SteamAPI.RunCallbacks();
                    else
                    {
                        IsOnline = false;
                        SteamOffline();
                    }
            }
            catch (DllNotFoundException ex)
            {
                Trace.TraceWarning("Error initializing steam, disabling susbystem: {0} library not found", ex.Message);
                if (timer != null) timer.Dispose();
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }

            tickCounter++;
        }
    }
}