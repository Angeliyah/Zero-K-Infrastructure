﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;

namespace CMissionLib.Actions
{
	/// <summary>
	/// Give orders to the specified units
	/// </summary>
	[DataContract]
	public class GiveOrdersAction : Action
	{
		ObservableCollection<string> groups = new ObservableCollection<string>();
		ObservableCollection<IOrder> orders;
        bool queue = false;

		public GiveOrdersAction()
			: this(new ObservableCollection<IOrder>()) {}

		public GiveOrdersAction(IEnumerable<IOrder> orders)
		{
			this.orders = new ObservableCollection<IOrder>(orders);
		}

		[DataMember]
		public ObservableCollection<IOrder> Orders
		{
			get { return orders; }
			set
			{
				orders = value;
				RaisePropertyChanged("Orders");
			}
		}

		/// <summary>
		/// The unit groups whose members will be given the orders
		/// </summary>
		[DataMember]
		public ObservableCollection<string> Groups
		{
			get { return groups; }
			set
			{
				groups = value;
				RaisePropertyChanged("Groups");
			}
		}

		/// <summary>
		/// If true, the first order is added to unit's order queue (as if Shift was pressed)
		/// Subsequent orders within this action are always queued
		/// </summary>
        [DataMember]
        public bool Queue
        {
            get { return queue; }
            set
            {
                queue = value;
                RaisePropertyChanged("Queue");
            }
        }

		public override LuaTable GetLuaTable(Mission mission)
		{
			var map = new Dictionary<object, object>
				{
					{"orders", LuaTable.CreateArray(orders.Select(o => o.GetLuaMap(mission)).ToArray())},
					{"groups", LuaTable.CreateSet(groups)},
                    {"queue", queue}
				};
			return new LuaTable(map);
		}

		public override string GetDefaultName()
		{
			return "Give Orders";
		}
	}
}