﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace CMissionLib.Actions
{
    /// <summary>
    /// Enables the player to transfer units to another team
    /// Some game-side Lua code might require this
    /// Not required for <see cref="TransferUnitsAction"/>
    /// </summary>
	[DataContract]
	public class AllowUnitTransfersAction : Action
	{

		public override LuaTable GetLuaTable(Mission mission)
		{
			return new LuaTable();
		}

		public override string GetDefaultName()
		{
			return "Allow Unit Transfers";
		}
	}
}
