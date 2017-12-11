// Implementation of WHR based on original by Pete Schwamb httpsin//github.com/goshrine/whole_history_rating

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using ZkData;

namespace Ratings
{

    public class WholeHistoryRating : IRatingSystem
    {

        const float RatingOffset = 1500;

        IDictionary<int, PlayerRating> playerRatings = new ConcurrentDictionary<int, PlayerRating>();
        IDictionary<int, Player> players = new Dictionary<int, Player>();
        SortedDictionary<float, int> sortedPlayers = new SortedDictionary<float, int>();
        IDictionary<int, float> playerKeys = new Dictionary<int, float>();
        Random rand = new Random();
        readonly float w2; //elo range expand per day squared
        public static readonly PlayerRating DefaultRating = new PlayerRating(int.MaxValue, 1, RatingOffset, float.PositiveInfinity);

        private bool runningInitialization = true;
        private readonly RatingCategory category;

        public WholeHistoryRating(RatingCategory category)
        {
            this.category = category;
            w2 = GlobalConst.EloDecayPerDaySquared;

            lock (dbLock)
            {
                var db = new ZkDataContext();
                foreach (var accountRating in db.AccountRatings.Where(x => x.ratingCategory == category).ToList())
                {
                    playerRatings.Add(accountRating.AccountID, new PlayerRating(accountRating.Rank, accountRating.Percentile, accountRating.RealElo, accountRating.LastUncertainty, accountRating.LastGameDate));
                }
            }
        }
        

        public PlayerRating GetPlayerRating(int accountID)
        {
            return playerRatings.ContainsKey(accountID) ? playerRatings[accountID] : DefaultRating;
        }

        public List<float> PredictOutcome(List<ICollection<Account>> teams)
        {
            return teams.Select(t =>
                    SetupGame(t.Select(x => x.AccountID).ToList(),
                            teams.Where(t2 => !t2.Equals(t)).SelectMany(t2 => t2.Select(x => x.AccountID)).ToList(),
                            true,
                            RatingSystems.ConvertDateToDays(DateTime.Now),
                            -1
                    ).getBlackWinProbability() * 2 / teams.Count).ToList();
        }

        private int battlesRegistered = 0;

        public void ProcessBattle(SpringBattle battle)
        {
            latestBattle = battle;
            ICollection<int> winners = battle.SpringBattlePlayers.Where(p => p.IsInVictoryTeam && !p.IsSpectator).Select(p => p.AccountID).ToList();
            ICollection<int> losers = battle.SpringBattlePlayers.Where(p => !p.IsInVictoryTeam && !p.IsSpectator).Select(p => p.AccountID).ToList();
            if (winners.Count > 0 && losers.Count > 0)
            {
                battlesRegistered++;
                createGame(losers, winners, false, RatingSystems.ConvertDateToDays(battle.StartTime), battle.SpringBattleID);
                if (RatingSystems.Initialized)
                {
                    Trace.TraceInformation(battlesRegistered + " battles registered for WHR, latest Battle: " + battle.SpringBattleID );
                    UpdateRatings();
                }
            }
        }

        public List<Account> GetTopPlayers(int count)
        {
            return GetTopPlayers(count, x => true);
        }

        public List<Account> GetTopPlayers(int count, Func<Account, bool> selector)
        {
            if (runningInitialization) return new List<Account>(); // dont block during updates to prevent dosprotector from kicking in
            lock (updateLockInternal) 
            {
                int counter = 0;
                ZkDataContext db = new ZkDataContext();
                List<Account> retval = new List<Account>();
                foreach (var pair in sortedPlayers)
                {
                    Account acc = db.Accounts.Where(a => a.AccountID == pair.Value).FirstOrDefault();
                    if (playerRatings[acc.AccountID].Rank < int.MaxValue && selector.Invoke(acc))
                    {
                        if (counter++ >= count) break;
                        retval.Add(acc);
                    }
                }
                return retval;
            }
        }

        //implementation specific


        private SpringBattle latestBattle, lastUpdate;

        private readonly object updateLock = new object();
        private readonly object updateLockInternal = new object();
        private readonly object dbLock = new object();

        public void UpdateRatings()
        {
            if (latestBattle == null)
            {
                //Trace.TraceInformation("WHR: No battles to evaluate");
                return;
            }
            if (latestBattle.Equals(lastUpdate))
            {
                //Trace.TraceInformation("WHR: Nothing to update");
                return;
            }
            lock (updateLock)
            {
                Action updateAction = null;
                if (lastUpdate == null)
                {
                    updateAction = (() => {
                        Trace.TraceInformation("Initializing WHR ratings for " + battlesRegistered + " battles, this will take some time..");
                        runIterations(50);
                        UpdateRankings(players.Values);
                    });
                }
                else if (latestBattle.StartTime.Subtract(lastUpdate.StartTime).TotalDays > 0.5d)
                {
                    updateAction = (() =>
                    {
                        Trace.TraceInformation("Updating all WHR ratings");
                        runIterations(1);
                        UpdateRankings(players.Values);
                    });
                }
                else if (!latestBattle.Equals(lastUpdate))
                {
                    updateAction = (() =>
                    {
                        Trace.TraceInformation("Updating WHR ratings for last Battle");
                        IEnumerable<Player> players = latestBattle.SpringBattlePlayers.Where(p => !p.IsSpectator).Select(p => getPlayerById(p.AccountID));
                        players.ForEach(p => p.runOneNewtonIteration());
                        players.ForEach(p => p.updateUncertainty());
                        UpdateRankings(players);
                    });
                }
                else
                {
                    //Trace.TraceInformation("No WHR ratings to update");
                    return;
                }
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        runningInitialization = true;
                        lock (updateLockInternal)
                        {
                            DateTime start = DateTime.Now;
                            updateAction.Invoke();
                            Trace.TraceInformation("WHR Ratings updated in " + DateTime.Now.Subtract(start).TotalSeconds + " seconds, " + (GC.GetTotalMemory(false) / (1 << 20)) + "MiB total memory allocated");
                        }
                        runningInitialization = false;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Thread error while updating WHR " + ex);
                    }
                });
                lastUpdate = latestBattle;
            }

        }
        
        public void SaveToDB()
        {
            lock (dbLock)
            {
                var db = new ZkDataContext();
                foreach (var playerRating in playerRatings)
                {
                    var accountRating = db.AccountRatings.Where(r => r.AccountID == playerRating.Key && r.ratingCategory == category).FirstOrDefault();
                    if (accountRating != null)
                    {
                        accountRating.UpdateFromRatingSystem(playerRating.Value);
                    }
                    else
                    {
                        accountRating = new AccountRating(playerRating.Key, category);
                        accountRating.UpdateFromRatingSystem(playerRating.Value);
                        db.AccountRatings.InsertOnSubmit(accountRating);
                    }
                }
                db.SaveChanges();
            }
        }

        public string DebugPlayer(Account player)
        {
            if (!RatingSystems.Initialized) return "";
            if (!players.ContainsKey(player.AccountID)) return "Unknown player";
            string debugString = "";
            foreach (PlayerDay d in players[player.AccountID].days)
            {
                debugString +=
                    d.day + ";" +
                    d.getElo() + ";" +
                    d.uncertainty * 100 + ";" +
                    d.wonGames.Select(g =>
                        g.whitePlayers.Select(p => p.id.ToString()).Aggregate("", (x, y) => x + "," + y) + "/" +
                        g.blackPlayers.Select(p => p.id.ToString()).Aggregate("", (x, y) => x + "," + y) + "/" +
                        (g.blackWins ? "Second" : "First") + "/" +
                        g.id
                    ).Aggregate("", (x, y) => x + "|" + y) + "\r\n";
            }
            return debugString;
        }

        //private
        

        //Runs in O(N log(N)) for all players
        private void UpdateRankings(IEnumerable<Player> players)
        {
            try
            {
                foreach (var p in players)
                {
                    if (p.days.Count == 0)
                    {
                        Trace.TraceWarning("No rating days for player " + p.id);
                        continue;
                    }
                    float elo = p.days.Last().getElo() + RatingOffset;
                    float lastUncertainty = p.days.Last().uncertainty * 100;
                    int lastDay = p.days.Last().day;
                    playerRatings[p.id] = new PlayerRating(int.MaxValue, 1, elo, lastUncertainty, lastDay);
                    float rating = -playerRatings[p.id].Elo + 0.001f * (float)rand.NextDouble();
                    if (playerKeys.ContainsKey(p.id)) sortedPlayers.Remove(playerKeys[p.id]);
                    playerKeys[p.id] = rating;
                    sortedPlayers[rating] = p.id;
                }
                float[] playerUncertainties = new float[playerRatings.Count];
                int index = 0;
                float DynamicMaxUncertainty = GlobalConst.MinimumDynamicMaxLadderUncertainty;
                foreach (var pair in playerRatings)
                {
                    playerUncertainties[index++] = pair.Value.Uncertainty;
                }
                Array.Sort(playerUncertainties);
                DynamicMaxUncertainty = Math.Max(DynamicMaxUncertainty, playerUncertainties[Math.Min(playerUncertainties.Length, GlobalConst.LadderSize) - 1] + 0.01f);
                int activePlayers = Math.Max(1, ~Array.BinarySearch(playerUncertainties, DynamicMaxUncertainty));
                int rank = 0;
                foreach (var pair in sortedPlayers)
                {
                    if (playerRatings[pair.Value].Uncertainty <= DynamicMaxUncertainty)
                    {
                        rank++;
                        playerRatings[pair.Value] = new PlayerRating(rank, (float)rank / activePlayers, playerRatings[pair.Value].RealElo, playerRatings[pair.Value].Uncertainty);
                    }
                    else if (playerRatings[pair.Value].Rank < int.MaxValue)
                    {
                        playerRatings[pair.Value] = new PlayerRating(int.MaxValue, 1, playerRatings[pair.Value].RealElo, playerRatings[pair.Value].Uncertainty);
                    }
                }
            }
            catch (Exception ex)
            {
                string dbg = "Failed to update rankings " + ex + "\nPlayers: ";
                foreach (var p in players)
                {
                    dbg += p.id + " (" + p.days.Count + " days), ";
                }
                Trace.TraceError(dbg);
            }
        }

        private Player GetPlayerByAccount(Account acc)
        {
            return getPlayerById(acc.AccountID);
        }

        private Player getPlayerById(int id)
        {
            if (!players.ContainsKey(id))
            {
                players.Add(id, new Player(id, w2));
            }
            return players[id];
        }

        private List<float[]> getPlayerRatings(int id)
        {
            Player player = getPlayerById(id);
            return player.days.Select(d => new float[] { d.day, (d.getElo()), ((d.uncertainty * 100)) }).ToList();
        }

        private Game SetupGame(ICollection<int> black, ICollection<int> white, bool blackWins, int time_step, int id)
        {

            // Avoid self-played games (no info)
            if (black.Equals(white))
            {
                Trace.TraceError("White == Black");
                return null;
            }
            if (white.Count < 1)
            {
                Trace.TraceError("White empty");
                return null;
            }
            if (black.Count < 1)
            {
                Trace.TraceError("Black empty");
                return null;
            }


            List<Player> white_player = white.Select(p => getPlayerById(p)).ToList();
            List<Player> black_player = black.Select(p => getPlayerById(p)).ToList();
            Game game = new Game(black_player, white_player, blackWins, time_step, id);
            return game;
        }

        private Game createGame(ICollection<int> black, ICollection<int> white, bool blackWins, int time_step, int id)
        {
            Game game = SetupGame(black, white, blackWins, time_step, id);
            return game != null ? AddGame(game) : null;
        }

        private Game AddGame(Game game)
        {
            game.whitePlayers.ForEach(p => p.AddGame(game));
            game.blackPlayers.ForEach(p => p.AddGame(game));

            return game;
        }

        private void runIterations(int count)
        {
            for (int i = 0; i < count; i++)
            {
                runSingleIteration();
            }
            foreach (Player p in players.Values)
            {
                p.updateUncertainty();
            }
            SaveToDB();
        }

        private void printStats()
        {
            float sum = 0;
            int bigger = 0;
            int total = 0;
            float lowest = 0;
            float highest = 0;
            foreach (Player p in players.Values)
            {
                if (p.days.Count > 0)
                {
                    total++;
                    float elo = p.days[p.days.Count - 1].getElo();
                    sum += elo;
                    if (elo > 0) bigger++;
                    lowest = Math.Min(lowest, elo);
                    highest = Math.Max(highest, elo);
                }
            }
            Trace.TraceInformation("Lowest eloin " + lowest);
            Trace.TraceInformation("Highest eloin " + highest);
            Trace.TraceInformation("sum eloin " + sum);
            Trace.TraceInformation("Average eloin " + (sum / total));
            Trace.TraceInformation("Amount > 0in " + bigger);
            Trace.TraceInformation("Amount < 0in " + (total - bigger));
        }

        private void runSingleIteration()
        {
            foreach (Player p in players.Values)
            {
                p.runOneNewtonIteration();
            }
        }
    }

}
