﻿using System.Linq;
using System.Threading.Tasks;
using LobbyClient;
using ZkData;
using ZkLobbyServer.autohost;

namespace ZkLobbyServer
{
    public class CmdTitle : ServerBattleCommand
    {
        private string title;
        public override string Help => "[title] - changes room title, e.g. !title All Welcome";
        public override string Shortcut => "title";
        public override BattleCommandAccess Access => BattleCommandAccess.NotIngame;

        public override ServerBattleCommand Create() => new CmdTitle();

        public override string Arm(ServerBattle battle, Say e, string arguments = null)
        {
            if (string.IsNullOrEmpty(arguments)) arguments = $"{battle.FounderName}'s {battle.Mode.Description()}";
            title = arguments;
            return $"Change title to {title}?";
        }


        public override async Task ExecuteArmed(ServerBattle battle, Say e)
        {
            if (title != null)
            {
                await battle.SwitchTitle(title);
                await battle.SayBattle("Title changed to " + title);
            }

        }
    }
}