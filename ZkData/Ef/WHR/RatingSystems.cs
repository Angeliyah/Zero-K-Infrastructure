﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ZkData;
using System.Data.Entity;

namespace Ratings
{
    public class RatingSystems
    {
        public static Dictionary<RatingCategory, WholeHistoryRating> whr = new Dictionary<RatingCategory, WholeHistoryRating>();

        public static readonly IEnumerable<RatingCategory> ratingCategories = Enum.GetValues(typeof(RatingCategory)).Cast<RatingCategory>();

        public static readonly bool DisableRatingSystems = false;

        private static HashSet<int> processedBattles = new HashSet<int>();

        public static bool Initialized { get; private set; }

        private static object processingLock = new object();

        public static void Init()
        {
            if (DisableRatingSystems) return;
            Initialized = false;
            ratingCategories.ForEach(category => whr[category] = new WholeHistoryRating(MiscVar.GetValue("WHR_" + category.ToString())));

            Task.Factory.StartNew(() => {
                lock (processingLock)
                {
                    ZkDataContext data = new ZkDataContext();
                    DateTime minStartTime = DateTime.Now.AddYears(-5);
                    foreach (SpringBattle b in data.SpringBattles
                            .Where(x => x.StartTime > minStartTime)
                            .Include(x => x.ResourceByMapResourceID)
                            .Include(x => x.SpringBattlePlayers)
                            .Include(x => x.SpringBattleBots)
                            .AsNoTracking()
                            .OrderBy(x => x.SpringBattleID))
                    {
                        ProcessResult(b);
                    }
                    whr.Values.ForEach(w => w.UpdateRatings());
                    Initialized = true;
                }
            });
        }

        public static void BackupToDB()
        {
            if (DisableRatingSystems) return;
            Trace.TraceInformation("Backing up ratings...");
            ratingCategories.ForEach(category => MiscVar.SetValue("WHR_" + category.ToString(), whr[category].SerializeJSON()));
        }

        public static void BackupToDB(IRatingSystem ratingSystem)
        {
            if (DisableRatingSystems) return;
            Trace.TraceInformation("Backing up rating system...");
            ratingCategories.Where(category => whr[category].Equals(ratingSystem)).ForEach(category => MiscVar.SetValue("WHR_" + category.ToString(), whr[category].SerializeJSON()));
        }

        public static IRatingSystem GetRatingSystem(RatingCategory category)
        {
            if (DisableRatingSystems) return null;
            if (!whr.ContainsKey(category))
            {
                Trace.TraceError("WHR: Unknown category " + category);
                return whr[RatingCategory.MatchMaking];
            }
            return whr[category];
        }

        private static int latestBattle;

        public static void ProcessResult(SpringBattle battle)
        {
            if (DisableRatingSystems) return;
            lock (processingLock)
            {
                int battleID = -1;
                try
                {
                    battleID = battle.SpringBattleID;
                    if (processedBattles.Contains(battleID)) return;
                    processedBattles.Add(battleID);
                    ratingCategories.Where(c => IsCategory(battle, c)).ForEach(c => whr[c].ProcessBattle(battle));
                    latestBattle = battleID;
                }
                catch (Exception ex)
                {
                    Trace.TraceError("WHR: Error processing battle (B" + battleID + ")" + ex);
                }
            }
        }

        private static Dictionary<int, Tuple<int, int, int>> factionCache = new Dictionary<int, Tuple<int, int, int>>();

        public static Tuple<int, int> GetPlanetwarsFactionStats(int factionID)
        {
            try
            {
                int count, skill;
                if (!factionCache.ContainsKey(factionID) || factionCache[factionID].Item1 != latestBattle)
                {
                    var maxAge = DateTime.UtcNow.AddDays(-7);
                    IEnumerable<Account> accounts;
                    var rating = RatingCategory.Planetwars;
                    if (GlobalConst.PlanetWarsMode == PlanetWarsModes.PreGame)
                    {
                        rating = RatingCategory.MatchMaking;
                        accounts = GetRatingSystem(rating).GetTopPlayers(int.MaxValue, x => x.LastLogin > maxAge && x.FactionID == factionID);
                    }
                    else
                    {
                        accounts = GetRatingSystem(rating).GetTopPlayers(int.MaxValue, x => x.PwAttackPoints > 0 && x.FactionID == factionID);
                    }
                    count = accounts.Count();
                    skill = count > 0 ? (int)Math.Round(accounts.Average(x => x.GetRating(rating).Elo)) : 1500;
                    factionCache[factionID] = new Tuple<int, int, int>(latestBattle, count, skill);
                }
                count = factionCache[factionID].Item2;
                skill = factionCache[factionID].Item3;
                return new Tuple<int, int>(count, skill);
            }catch(Exception ex)
            {
                Trace.TraceError("WHR failed to calculate faction stats " + ex);
                return new Tuple<int, int>(-1, -1);
            }
        }

        public static int ConvertDateToDays(DateTime date)
        {
            return (int)(date.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays / 1);
        }
        public static DateTime ConvertDaysToDate(int days)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(days);
        }

        private static bool IsCategory(SpringBattle battle, RatingCategory category)
        {
            int battleID = -1;
            try
            {
                if (battle.HasBots) return false;
                battleID = battle.SpringBattleID;
                switch (category)
                {
                    case RatingCategory.Casual:
                        return !(battle.IsMission || battle.HasBots || (battle.PlayerCount < 2) || (battle.ResourceByMapResourceID?.MapIsSpecial == true)
                            || battle.Duration < GlobalConst.MinDurationForElo);
                    case RatingCategory.MatchMaking:
                        return battle.IsMatchMaker;
                    case RatingCategory.Planetwars:
                        return battle.Mode == PlasmaShared.AutohostMode.Planetwars; //how?
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("WHR: Error while checking battle category (B" + battleID + ")" + ex);
            }
            return false;
        }
    }

    public enum RatingCategory
    {
        Casual, MatchMaking, Planetwars
    }
}
