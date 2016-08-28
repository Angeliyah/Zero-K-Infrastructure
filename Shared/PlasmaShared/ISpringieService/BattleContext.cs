using System.Collections.Generic;
using ZkData;

namespace PlasmaShared
{
    public class BattleContext
    {
        public string FounderName;
        public string Map;
        public string Mod;
        public List<PlayerTeam> Players = new List<PlayerTeam>();
        public List<BotTeam> Bots = new List<BotTeam>();
        public string Title;
        public bool IsMission;
        public string EngineVersion;
        public AutohostMode Mode = AutohostMode.None;
        public IDictionary<string, string> ModOptions { get; set; } = new Dictionary<string, string>();
    }
}