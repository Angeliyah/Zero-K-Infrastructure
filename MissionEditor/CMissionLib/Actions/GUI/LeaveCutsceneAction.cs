﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CMissionLib.Actions
{
    /// <summary>
    /// Leaves the current cutscene
    /// </summary>
	[DataContract]
	public class LeaveCutsceneAction : Action
	{
        bool instant = false;

		public LeaveCutsceneAction()
			: base() {}

        /// <summary>
        /// If true, the letterbox effect is removed immediately 
        /// instead of scrolling out from the top/bottom of the screen
        /// </summary>
        [DataMember]
        public bool Instant
        {
            get { return instant; }
            set
            {
                instant = value;
                RaisePropertyChanged("Instant");
            }
        }

        public override LuaTable GetLuaTable(Mission mission)
        {
            var map = new Dictionary<object, object>
				{
					{"instant", Instant},
				};
            return new LuaTable(map);
        }

		public override string GetDefaultName()
		{
			return "Leave Cutscene";
		}
	}
}