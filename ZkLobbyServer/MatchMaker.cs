﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using LobbyClient;
using PlasmaShared;
using ZkData;
using Timer = System.Timers.Timer;

namespace ZkLobbyServer
{
    public class MatchMaker
    {
        private const int TimerSeconds = 30;

        private const int BanSeconds = 30;

        private ConcurrentDictionary<string, DateTime> bannedPlayers = new ConcurrentDictionary<string, DateTime>();

        private List<ProposedBattle> invitationBattles = new List<ProposedBattle>();
        private ConcurrentDictionary<string, PlayerEntry> players = new ConcurrentDictionary<string, PlayerEntry>();
        private List<MatchMakerSetup.Queue> possibleQueues = new List<MatchMakerSetup.Queue>();

        private Dictionary<string, int> queuesCounts = new Dictionary<string, int>();
        private ZkLobbyServer server;


        private object tickLock = new object();
        private Timer timer;
        private Dictionary<string, int> ingameCounts = new Dictionary<string, int>();

        public MatchMaker(ZkLobbyServer server)
        {
            this.server = server;
            using (var db = new ZkDataContext())
            {
                possibleQueues.Add(new MatchMakerSetup.Queue()
                {
                    Name = "Teams",
                    Description = "Small teams 2v2 to 4v4 with reasonable skill difference",
                    MaxPartySize = 4,
                    MinSize = 4,
                    MaxSize = 8,
                    Game = server.Game,
                    Mode = AutohostMode.Teams,
                    Maps =
                        db.Resources.Where(
                                x => (x.MapSupportLevel >= MapSupportLevel.MatchMaker) && (x.MapIsTeams == true) && (x.TypeID == ResourceType.Map))
                            .Select(x => x.InternalName)
                            .ToList()
                });

                possibleQueues.Add(new MatchMakerSetup.Queue()
                {
                    Name = "1v1",
                    Description = "Duels with reasonable skill difference",
                    MaxPartySize = 1,
                    MinSize = 2,
                    MaxSize = 2,
                    Game = server.Game,
                    Maps =
                        db.Resources.Where(
                                x => (x.MapSupportLevel >= MapSupportLevel.MatchMaker) && (x.MapIs1v1 == true) && (x.TypeID == ResourceType.Map))
                            .Select(x => x.InternalName)
                            .ToList(),
                    Mode = AutohostMode.Game1v1,
                });
            }
            timer = new Timer(TimerSeconds * 1000);
            timer.AutoReset = true;
            timer.Elapsed += TimerTick;
            timer.Start();

            queuesCounts = CountQueuedPeople(players.Values);
            ingameCounts = CountIngamePeople();
        }


        public async Task AreYouReadyResponse(ConnectedUser user, AreYouReadyResponse response)
        {
            PlayerEntry entry;
            if (players.TryGetValue(user.Name, out entry))
                if (entry.InvitedToPlay)
                {
                    if (response.Ready) entry.LastReadyResponse = true;
                    else
                    {
                        entry.LastReadyResponse = false;
                        await RemoveUser(user.Name);
                    }

                    var invitedPeople = players.Values.Where(x => x?.InvitedToPlay == true).ToList();

                    if ((invitedPeople.Count <= 1) || invitedPeople.All(x => x.LastReadyResponse)) OnTick();
                    else
                    {
                        var readyCounts = CountQueuedPeople(invitedPeople.Where(x => x.LastReadyResponse));

                        var proposedBattles = ProposeBattles(invitedPeople.Where(x => x.LastReadyResponse));

                        await Task.WhenAll(invitedPeople.Select(async (p) =>
                        {
                            var invitedBattle = invitationBattles?.FirstOrDefault(x => x.Players.Contains(p));
                            await
                                server.SendToUser(p.Name,
                                    new AreYouReadyUpdate()
                                    {
                                        QueueReadyCounts = readyCounts,
                                        ReadyAccepted = p.LastReadyResponse == true,
                                        LikelyToPlay = proposedBattles.Any(y => y.Players.Contains(p)),
                                        YourBattleSize = invitedBattle?.Size,
                                        YourBattleReady =
                                            invitedPeople.Count(x => x.LastReadyResponse && (invitedBattle?.Players.Contains(x) == true))
                                    });
                        }));
                    }
                }
        }

        public int GetTotalWaiting()
        {
            return queuesCounts?.Sum(x => (int?)x.Value) ?? 0;
        }


        public async Task OnLoginAccepted(ICommandSender client)
        {
            await client.SendCommand(new MatchMakerSetup() { PossibleQueues = possibleQueues });
        }

        public async Task QueueRequest(ConnectedUser user, MatchMakerQueueRequest cmd)
        {
            var banTime = BannedSeconds(user.Name);
            if (banTime != null)
            {
                await user.SendCommand(new MatchMakerStatus() { BannedSeconds = banTime });
                await user.Respond($"Please rest and wait for {banTime}s because you refused previous match");
                return;
            }

            // already invited ignore requests
            PlayerEntry entry;
            if (players.TryGetValue(user.Name, out entry) && entry.InvitedToPlay)
            {
                await user.SendCommand(ToMatchMakerStatus(entry));
                return;
            }

            var wantedQueueNames = cmd.Queues?.ToList() ?? new List<string>();
            var wantedQueues = possibleQueues.Where(x => wantedQueueNames.Contains(x.Name)).ToList();

            if (wantedQueues.Count == 0) // delete
            {
                await RemoveUser(user.Name);
                return;
            }

            var userEntry = players.AddOrUpdate(user.Name,
                (str) => new PlayerEntry(user.User, wantedQueues),
                (str, usr) =>
                {
                    usr.UpdateTypes(wantedQueues);
                    return usr;
                });

            queuesCounts = CountQueuedPeople(players.Values);
            await user.SendCommand(ToMatchMakerStatus(userEntry));

            if (invitationBattles?.Any() != true) OnTick(); // if nobody is invited, we can do tick now to speed up things
        }

        public async Task RemoveUser(string name)
        {
            PlayerEntry entry;
            if (players.TryRemove(name, out entry))
            {
                if (entry.InvitedToPlay) bannedPlayers[entry.Name] = DateTime.UtcNow; // was invited but he is gone now (whatever reason), ban!

                ConnectedUser conUser;
                if (server.ConnectedUsers.TryGetValue(name, out conUser) && (conUser != null))
                {
                    if (entry?.InvitedToPlay == true) await conUser.SendCommand(new AreYouReadyResult() { AreYouBanned = true, IsBattleStarting = false, });

                    await conUser.SendCommand(new MatchMakerStatus() { BannedSeconds = BannedSeconds(name) }); // left queue
                }
            }
        }


        private int? BannedSeconds(string name)
        {
            DateTime banEntry;
            if (bannedPlayers.TryGetValue(name, out banEntry) && (DateTime.UtcNow.Subtract(banEntry).TotalSeconds < BanSeconds)) return (int)(BanSeconds - DateTime.UtcNow.Subtract(banEntry).TotalSeconds);
            else bannedPlayers.TryRemove(name, out banEntry);
            return null;
        }

        private Dictionary<string, int> CountQueuedPeople(IEnumerable<PlayerEntry> sumPlayers)
        {
            var ncounts = possibleQueues.ToDictionary(x => x.Name, x => 0);
            foreach (var plr in sumPlayers.Where(x => x != null)) foreach (var jq in plr.QueueTypes) ncounts[jq.Name]++;
            return ncounts;
        }

        private Dictionary<string, int> CountIngamePeople()
        {
            var ncounts = possibleQueues.ToDictionary(x => x.Name, x => 0);
            foreach (var bat in server.Battles.Values.Where(x => x != null && x.IsMatchMakerBattle && x.IsInGame))
            {
                var plrs = bat.spring?.Context?.LobbyStartContext?.Players.Count(x => !x.IsSpectator) ?? 0;
                if (plrs > 0)
                {
                    var type = possibleQueues.FirstOrDefault(x => x.Mode == bat.Mode && x.MinSize <= plrs && x.MaxSize >= plrs); // reverse determine queue type, not stored in batlte, silly?
                    if (type != null) ncounts[type.Name] += plrs;
                }
            }
            return ncounts;
        }

        private void OnTick()
        {
            lock (tickLock)
            {
                try
                {
                    timer.Stop();
                    var realBattles = ResolveToRealBattles();

                    foreach (var bat in realBattles) StartBattle(bat);

                    queuesCounts = CountQueuedPeople(players.Values);
                    ingameCounts = CountIngamePeople();

                    ResetAndSendMmInvitations();
                }
                catch (Exception ex)
                {
                    Trace.TraceError("MatchMaker tick error: {0}", ex);
                }
                finally
                {
                    timer.Start();
                }
            }
        }

        private static List<ProposedBattle> ProposeBattles(IEnumerable<PlayerEntry> users)
        {
            var proposedBattles = new List<ProposedBattle>();

            var usersByWaitTime = users.OrderBy(x => x.JoinedTime).ToList();
            var remainingPlayers = usersByWaitTime.ToList();

            foreach (var user in usersByWaitTime)
                if (remainingPlayers.Contains(user)) // consider only those not yet assigned
                {
                    var battle = TryToMakeBattle(user, remainingPlayers);
                    if (battle != null)
                    {
                        proposedBattles.Add(battle);
                        remainingPlayers.RemoveAll(x => battle.Players.Contains(x));
                    }
                }

            return proposedBattles;
        }

        private void ResetAndSendMmInvitations()
        {
            // generate next battles and send inviatation
            invitationBattles = ProposeBattles(players.Values.Where(x => x != null));
            var toInvite = invitationBattles.SelectMany(x => x.Players).ToList();
            foreach (var usr in players.Values.Where(x => x != null))
                if (toInvite.Contains(usr))
                {
                    usr.InvitedToPlay = true;
                    usr.LastReadyResponse = false;
                }
                else
                {
                    usr.InvitedToPlay = false;
                    usr.LastReadyResponse = false;
                }


            foreach (var conus in server.ConnectedUsers.Values.Where(x => x != null))
            {
                PlayerEntry entry;
                players.TryGetValue(conus.Name, out entry);
                conus.SendCommand(ToMatchMakerStatus(entry));
            }

            server.Broadcast(toInvite.Select(x => x.Name), new AreYouReady() { SecondsRemaining = TimerSeconds });
        }

        private List<ProposedBattle> ResolveToRealBattles()
        {
            var lastMatchedUsers = players.Values.Where(x => x?.InvitedToPlay == true).ToList();

            // force leave those not ready
            foreach (var pl in lastMatchedUsers.Where(x => !x.LastReadyResponse)) RemoveUser(pl.Name);

            var readyUsers = lastMatchedUsers.Where(x => x.LastReadyResponse).ToList();
            var realBattles = ProposeBattles(readyUsers);

            var readyAndStarting = readyUsers.Where(x => realBattles.Any(y => y.Players.Contains(x))).ToList();
            var readyAndFailed = readyUsers.Where(x => !realBattles.Any(y => y.Players.Contains(x))).Select(x => x.Name);

            server.Broadcast(readyAndFailed, new AreYouReadyResult() { IsBattleStarting = false });

            server.Broadcast(readyAndStarting.Select(x => x.Name), new AreYouReadyResult() { IsBattleStarting = true });
            server.Broadcast(readyAndStarting.Select(x => x.Name), new MatchMakerStatus() { });

            foreach (var usr in readyAndStarting)
            {
                PlayerEntry entry;
                players.TryRemove(usr.Name, out entry);
            }

            return realBattles;
        }

        private async Task StartBattle(ProposedBattle bat)
        {
            var battleID = Interlocked.Increment(ref server.BattleCounter);

            var battle = new ServerBattle(server, true);
            battle.UpdateWith(new BattleHeader()
            {
                BattleID = battleID,
                Founder = "#MatchMaker_" + battleID,
                Engine = server.Engine,
                Game = server.Game,
                Title = "MatchMaker " + battleID,
                Mode = bat.Mode,
            });
            server.Battles[battleID] = battle;

            //foreach (var plr in bat.Players) battle.Users[plr.Name] = new UserBattleStatus(plr.Name, plr.LobbyUser) { IsSpectator = false, AllyNumber = 0, };

            // also join in lobby
            await server.Broadcast(server.ConnectedUsers.Keys, new BattleAdded() { Header = battle.GetHeader() });
            foreach (var usr in bat.Players) await server.ForceJoinBattle(usr.Name, battle);

            await battle.StartGame();
        }


        private void TimerTick(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            OnTick();
        }


        private MatchMakerStatus ToMatchMakerStatus(PlayerEntry entry)
        {
            var ret = new MatchMakerStatus()
            {
                QueueCounts = queuesCounts,
                IngameCounts = ingameCounts,
            };
            if (entry != null)
            {
                ret.JoinedQueues = entry.QueueTypes.Select(x => x.Name).ToList();
                ret.CurrentEloWidth = entry.EloWidth;
                ret.JoinedTime = entry.JoinedTime;
                ret.BannedSeconds = BannedSeconds(entry.Name);
            }
            return ret;
        }

        private static ProposedBattle TryToMakeBattle(PlayerEntry player, IList<PlayerEntry> otherPlayers)
        {
            var playersByElo =
                otherPlayers.Where(x => x != player)
                    .OrderBy(x => Math.Abs(x.LobbyUser.EffectiveMmElo - player.LobbyUser.EffectiveMmElo))
                    .ThenBy(x => x.JoinedTime)
                    .ToList();

            var testedBattles = player.GenerateWantedBattles();

            foreach (var other in playersByElo)
                foreach (var bat in testedBattles)
                    if (bat.CanBeAdded(other))
                    {
                        bat.AddPlayer(other);
                        if (bat.Players.Count == bat.Size) return bat;
                    }

            return null;
        }


        public class PlayerEntry
        {
            public bool InvitedToPlay;
            public bool LastReadyResponse;
            public int EloWidth => (int)Math.Min(400, 100 + DateTime.UtcNow.Subtract(JoinedTime).TotalSeconds / 30 * 50);
            public DateTime JoinedTime { get; private set; } = DateTime.UtcNow;
            public User LobbyUser { get; private set; }
            public string Name => LobbyUser.Name;
            public List<MatchMakerSetup.Queue> QueueTypes { get; private set; }


            public PlayerEntry(User user, List<MatchMakerSetup.Queue> queueTypes)
            {
                QueueTypes = queueTypes;
                LobbyUser = user;
            }

            public List<ProposedBattle> GenerateWantedBattles()
            {
                var ret = new List<ProposedBattle>();
                foreach (var qt in QueueTypes) for (var i = qt.MaxSize; i >= qt.MinSize; i--) if (i%2 == 0) ret.Add(new ProposedBattle(i, this, qt.Mode));
                return ret;
            }

            public void UpdateTypes(List<MatchMakerSetup.Queue> queueTypes)
            {
                QueueTypes = queueTypes;
            }
        }


        public class ProposedBattle
        {
            private PlayerEntry owner;
            public List<PlayerEntry> Players = new List<PlayerEntry>();
            public int Size;
            public int MaxElo { get; private set; } = int.MinValue;
            public int MinElo { get; private set; } = int.MaxValue;
            public AutohostMode Mode { get; private set; }

            public ProposedBattle(int size, PlayerEntry initialPlayer, AutohostMode mode)
            {
                Size = size;
                Mode = mode;
                owner = initialPlayer;
                AddPlayer(initialPlayer);
            }

            public void AddPlayer(PlayerEntry player)
            {
                Players.Add(player);
                var elo = GetElo(player);
                MinElo = Math.Min(MinElo, elo);
                MaxElo = Math.Max(MaxElo, elo);
            }

            public bool CanBeAdded(PlayerEntry other)
            {
                if (!other.GenerateWantedBattles().Any(y => (y.Size == Size) && (y.Mode == Mode))) return false;
                var widthMultiplier = Math.Max(1.0, 1.0 + (Size - 4) * 0.1);
                var width = owner.EloWidth * widthMultiplier;

                var elo = GetElo(other);
                if ((elo - MinElo > width) || (MaxElo - elo > width)) return false;

                return true;
            }

            private int GetElo(PlayerEntry entry) => entry.LobbyUser.EffectiveMmElo;
        }
    }
}