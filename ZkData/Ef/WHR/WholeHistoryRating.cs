// Implementation of WHR based on original by Pete Schwamb httpsin//github.com/goshrine/whole_history_rating

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZkData;

namespace Ratings
{

    public class WholeHistoryRating : IRatingSystem{

        const double DecayPerDaySquared = 300;



        IDictionary<int, Player> players;
        List<Game> games;
        double w2; //elo range expand per day squared

        public WholeHistoryRating() {
            w2 = DecayPerDaySquared;
            games = new List<Game>();
            players = new Dictionary<int, Player>();
        }
        

        public double GetPlayerRating(Account account)
        {
            UpdateRatings();
            List<double[]> ratings = getPlayerRatings(account.AccountID);
            return (ratings.Count > 0 ? ratings.Last()[1] : 0) + 1500; //1500 for zk peoplers to feel at home
        }

        public double GetPlayerRatingUncertainty(Account account)
        {
            UpdateRatings();
            List<double[]> ratings = getPlayerRatings(account.AccountID);
            return ratings.Count > 0 ? ratings.Last()[2] : Double.PositiveInfinity;
        }

        public List<double> PredictOutcome(List<List<Account>> teams)
        {
            return teams.Select(t => SetupGame(t, teams.Where(t2 => !t2.Equals(t)).SelectMany(t2 => t2).ToList(), "B", ConvertDate(DateTime.Now)).getBlackWinProbability() * 2 / teams.Count).ToList();
        }

        public void ProcessBattle(SpringBattle battle)
        {
            latestBattle = battle;
            List<Account> winners = battle.SpringBattlePlayers.Where(p => p.IsInVictoryTeam).Select(p => p.Account).ToList();
            List<Account> losers = battle.SpringBattlePlayers.Where(p => !p.IsInVictoryTeam).Select(p => p.Account).ToList();
            createGame(losers, winners, "W", ConvertDate(battle.StartTime));
        }

        //implementation specific

        private SpringBattle latestBattle, lastUpdate;

        private readonly object updateLock = new object();

        public void UpdateRatings()
        {
            if (latestBattle == null)
            {
                Trace.TraceInformation("WHR: No battles to evaluate");
                return;
            }
            if (latestBattle.Equals(lastUpdate))
            {
                Trace.TraceInformation("WHR: Nothing to update");
                return;
            }
            try
            {
                lock (updateLock)
                {
                    if (lastUpdate == null)
                    {
                        Trace.TraceInformation("Initializing all WHR ratings, this will take some time..");
                        Task.Factory.StartNew(() => {
                            runIterations(50);
                            Trace.TraceInformation("WHR Ratings updated");
                        });
                    }
                    else if (latestBattle.StartTime.Subtract(lastUpdate.StartTime).TotalDays > 0.5d)
                    {
                        Trace.TraceInformation("Updating all WHR ratings");
                        Task.Factory.StartNew(() => {
                            runIterations(1);
                            Trace.TraceInformation("WHR Ratings updated");
                        });
                    }
                    else if (!latestBattle.Equals(lastUpdate))
                    {
                        Trace.TraceInformation("Updating WHR ratings for last Battle");
                        Task.Factory.StartNew(() => {
                            List<Player> players = latestBattle.SpringBattlePlayers.Select(p => GetPlayerByAccount(p.Account)).ToList();
                            players.ForEach(p => p.runOneNewtonIteration());
                            players.ForEach(p => p.updateUncertainty());
                            Trace.TraceInformation("WHR Ratings updated");
                        });
                    }
                    else
                    {
                        Trace.TraceInformation("No WHR ratings to update");
                    }

                    lastUpdate = latestBattle;
                }
            }catch(Exception ex)
            {
                Trace.TraceError("Thread error while updating WHR " + ex);
            }
        }

        //private

        private int ConvertDate(DateTime date)
        {
            return (int)date.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
        }

        private Player GetPlayerByAccount(Account acc)
        {
            return getPlayerById(acc.AccountID);
        }

        private Player getPlayerById(int id) {
            if (!players.ContainsKey(id)) {
                players.Add(id, new Player(id, w2));
            }
            return players[id];
        }

        private List<double[]> getPlayerRatings(int id) {
            Player player = getPlayerById(id);
            return player.days.Select(d=> new double[] { d.day, (d.getElo()), ((d.uncertainty * 100)) }).ToList();
        }

        private Game SetupGame(List<Account> black, List<Account> white, string winner, int time_step) {

            // Avoid self-played games (no info)
            if (black.Equals(white)) {
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


            List<Player> white_player = white.Select(p=>GetPlayerByAccount(p)).ToList();
            List<Player> black_player = black.Select(p=>GetPlayerByAccount(p)).ToList();
            Game game = new Game(black_player, white_player, winner, time_step);
            return game;
        }

        private Game createGame(List<Account> black, List<Account> white, string winner, int time_step) {
            Game game = SetupGame(black, white, winner, time_step);
            return game != null ? AddGame(game) : null;
        }

        private Game AddGame(Game game) {
            game.whitePlayers.ForEach(p=>p.AddGame(game));
            game.blackPlayers.ForEach(p=>p.AddGame(game));

            games.Add(game);
            return game;
        }

        private void runIterations(int count) {
            for (int i = 0; i < count; i++) {
                runSingleIteration();
            }
            foreach (Player p in players.Values) {
                p.updateUncertainty();
            }
        }

        private void printStats() {
            double sum = 0;
            int bigger = 0;
            int total = 0;
            double lowest = 0;
            double highest = 0;
            foreach (Player p in players.Values) {
                if (p.days.Count > 0) {
                    total++;
                    double elo = p.days[p.days.Count - 1].getElo();
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

        private void runSingleIteration() {
            foreach (Player p in players.Values) {
                p.runOneNewtonIteration();
            }
        }
    }

}