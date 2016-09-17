﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CMissionLib.Actions
{
    /// <summary>
    /// Transfers units in the specified group to the specified team
    /// Does not require <see cref="AllowUnitTransfersAction"/>
    /// </summary>
	[DataContract]
	public class TransferUnitsAction : Action
	{
		string group = String.Empty;
		Player player;

		public TransferUnitsAction(Player player)
			: base()
		{
			this.player = player;
		}

		[DataMember]
		public string Group
		{
			get { return group; }
			set
			{
				group = value;
				RaisePropertyChanged("Group");
			}
		}

		[DataMember]
		public Player Player
		{
			get { return player; }
			set
			{
				player = value;
				RaisePropertyChanged("Player");
			}
		}

		public override LuaTable GetLuaTable(Mission mission)
		{
			var map = new Dictionary<object, object>
				{
					{"group", group},
					{"player", mission.Players.IndexOf(player)},
				};
			return new LuaTable(map);
		}

		public override string GetDefaultName()
		{
			return "Transfer Units";
		}
	}
}