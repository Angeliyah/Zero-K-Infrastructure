using System.Collections.Generic;
using System.Linq;
using LobbyClient;
using PlasmaShared;

namespace Springie.autohost
{
    public class CommandList
    {
        public List<CommandConfig> Commands = new List<CommandConfig>();

        public CommandList()
        {
            AddMissing(new CommandConfig("balance",
                                         1,
                                         "<allycount> - assigns people to <allycount> rank balanced alliances, e.g. !balance - makes 2 random but balanced alliances",
                                         10));

            AddMissing(new CommandConfig("start", 1, " - starts game", 5));

            AddMissing(new CommandConfig("listmaps", 1, "[<filters>..] - lists maps on server, e.g. !listmaps altor div", 10));

            AddMissing(new CommandConfig("listmods", 1, "[<filters>..] - lists games on server, e.g. !listmods absolute 2.23", 5));
            AddMissing(new CommandConfig("map", 3, "[<filters>..] - changes server map, eg. !map altor div"));
            AddMissing(new CommandConfig("mapremote", 0, "[<filters>..] - changes server map, eg. !map altor div"));    // see https://github.com/ZeroK-RTS/Zero-K-Infrastructure/issues/756

            AddMissing(new CommandConfig("forcestart", 3, " - starts game forcibly (ignoring warnings)", 5));

            AddMissing(new CommandConfig("say",
                                         3,
                                         "<text> - says something in game",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));

            AddMissing(new CommandConfig("force",
                                         3,
                                         " - forces game start inside game",
                                         8,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));
            AddMissing(new CommandConfig("kick",
                                         3,
                                         "[<filters>..] - kicks a player",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));


            AddMissing(new CommandConfig("transmit", 0, "Internal command transfer to ingame") { AllowSpecs = true});


            AddMissing(new CommandConfig("exit",
                                         3,
                                         " - exits the game",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));

            AddMissing(new CommandConfig("votemap", 1, "[<mapname>..] - starts vote for new map, e.g. !votemap altored div"));

            AddMissing(new CommandConfig("votekick",
                                         2,
                                         "[<playerame>..] - starts vote to kick a player, e.g. !votekick Licho",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));

            AddMissing(new CommandConfig("votespec",
                                         2,
                                         "[<playername>..] - starts vote to spectate player, e.g. !votespec Licho",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));

            
            AddMissing(new CommandConfig("voteforcestart", 2, " - starts vote to force game to start in lobby"));

            AddMissing(new CommandConfig("voteforce",
                                         2,
                                         " - starts vote to force game to start from game",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));

            AddMissing(new CommandConfig("voteexit",
                                         2,
                                         " - starts vote to exit game",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));

            AddMissing(new CommandConfig("voteresign",
                                                     0,
                                                     " - starts a vote to resign game",
                                                     0,
                                                     new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));

            AddMissing(new CommandConfig("vote",
                                         0,
                                         "<number> - votes for given option (works from battle only), e.g. !vote 1",
                                         0,
                                         new[] { SayPlace.Battle, SayPlace.Game }));
            AddMissing(new CommandConfig("y",
                                         0,
                                         "- votes for given option 1 (works from battle only), e.g. !y; !vote 1",
                                         0,
                                         new[] { SayPlace.Battle, SayPlace.Game }));
            AddMissing(new CommandConfig("n",
                                         0,
                                         "- votes for given option 2 (works from battle only), e.g. !n; !vote 2",
                                         0,
                                         new[] { SayPlace.Battle, SayPlace.Game }));

            AddMissing(new CommandConfig("rehost", 3, "[<modname>..] - rehosts game, e.g. !rehost abosol 2.23 - rehosts AA2.23"));

            AddMissing(new CommandConfig("updaterapidmod", 3, "[<tag>..] - force update host to mod with specified rapid tag"));


            AddMissing(new CommandConfig("adduser", 0, "<pw> - technical command used for mid-game spectator join", 0, new[] { SayPlace.Battle, SayPlace.User }) { AllowSpecs = true});

            AddMissing(new CommandConfig("helpall", 0, "- lists all commands known to Springie (sorted by command level)", 5) { AllowSpecs = true});

            AddMissing(new CommandConfig("setengine", 3, "[version] - sets a new spring version", 2));

            AddMissing(new CommandConfig("springie",
                                         0,
                                         "- responds with basic springie information",
                                         5,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Channel })
                                         { AllowSpecs = true});

            AddMissing(new CommandConfig("endvote", 1, "- ends current poll"));


            AddMissing(new CommandConfig("listoptions", 1, " - lists all mod/map options", 5));

            AddMissing(new CommandConfig("setoptions", 3, "<name>=<value>[,<name>=<value>] - applies mod/map options", 0));

            AddMissing(new CommandConfig("votesetoptions", 1, "<name>=<value>[,<name>=<value>] - starts a vote to apply mod/map options", 0));

            AddMissing(new CommandConfig("resetoptions", 3, " - sets default mod/map options", 0));

            AddMissing(new CommandConfig("voteresetoptions", 1, " - starts a vote to set default mod/map options", 0));

            AddMissing(new CommandConfig("cbalance",
                                         1,
                                         "[<allycount>] - assigns people to allycount random balanced alliances but attempts to put clanmates to same teams",
                                         10));

            AddMissing(new CommandConfig("spawn",
                                         -2,
                                         "<configs> - creates new autohost. Example: !spawn mod=ca:stable,title=My PWN game,password=secret. The following parameters can be specified: map, mod, title, password, maxplayers, owner, handle, mode. Possible values for mode: 0 - none, 3 - 1v1, 4 - FFA, 5 - coop, 6 - teams, 10 - serious.",
                                         0)
                                         { AllowSpecs = true});

            AddMissing(new CommandConfig("setpassword", 3, "<newpassword> - sets server password (needs !rehost to apply)"));

            AddMissing(new CommandConfig("setmaxplayers", 3, "<maxplayers> - sets server size (needs !rehost to apply)"));

            AddMissing(new CommandConfig("setgametitle", 3, "<new title> - sets server game title (needs !rehost to apply)"));

            AddMissing(new CommandConfig("boss",
                                         3,
                                         "<name> - sets <name> as a new boss, use w5ithout parameter to remove any current boss. If there is a boss on server, other non-admin people have their rights reduced"));
 

            AddMissing(new CommandConfig("spec", 3, "<username> - forces player to become spectator", 0));

            AddMissing(new CommandConfig("predict", 0, "predicts chances of victory", 0) { AllowSpecs = true});

            AddMissing(new CommandConfig("specafk", 2, "forces all AFK player to become spectators", 0));

            AddMissing(new CommandConfig("cheats",
                                         3,
                                         "enables/disables .cheats in game",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));
            
            AddMissing(new CommandConfig("hostsay",
                                         3,
                                         "says something as host, useful for /nocost etc",
                                         0,
                                         new[] { SayPlace.User, SayPlace.Battle, SayPlace.Game }));

            AddMissing(new CommandConfig("notify",
                                         0,
                                         "springie notifies you when game ends",
                                         0,
                                         new[]
                                         {
                                             SayPlace.User, SayPlace.Battle, SayPlace.Game,
                                             //SayPlace.Channel // this does silly stuff with !notify in #zk
                                         })
                                         { AllowSpecs = true});

            AddMissing(new CommandConfig("move", 4, "<where> - moves players to a new host"));
            AddMissing(new CommandConfig("votemove", 2, "<where> - moves players to a new host") { AllowSpecs = true});
        }


        void AddMissing(CommandConfig command)
        {
            Commands.Add(command);
        }
    }
}
