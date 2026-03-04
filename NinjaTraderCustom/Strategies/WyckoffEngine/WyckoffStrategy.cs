#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion
//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class WyckoffStrategy : Strategy
	{
		private AccumulationEngine accumulation;
		private DistributionEngine distribution;
		private StructureCoordinator coordinator;
		// ==========================
		// SESSION GOVERNANCE
		// ==========================
		private double dailyPnL = 0;
		private double dailyProfitLimit = 2700;
		private double dailyLossLimit = -900;

		private int tradesToday = 0;
		private int maxTradesPerDay = 6;

		private int lpsAttempts = 0;
		private int maxLpsAttempts = 2;

		private DateTime currentSessionDate = Core.Globals.MinDate;
		// ==========================
		// POSITION TRACKING (STEP 2)
		// ==========================
		private double entryPrice = 0;
		private MarketPosition activePosition = MarketPosition.Flat;

		private enum MarketContext
		{
			Range,
			Trend
		}



		private string arLineTag = "AR_LINE";
		private string structureLineTag = "STRUCTURE_LINE";



		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Fast EMA Period", Order = 1, GroupName = "Parameters")]
		public int FastPeriod { get; set; }

		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name = "Slow EMA Period", Order = 2, GroupName = "Parameters")]
		public int SlowPeriod { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Stop Loss (ticks)", Order = 3, GroupName = "Risk")]
		public int StopLossTicks { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Profit Target (ticks)", Order = 4, GroupName = "Risk")]
		public int ProfitTargetTicks { get; set; }

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Name = "WyckoffStrategy_Prod";
				Calculate = Calculate.OnBarClose;

				FastPeriod = 9;
				SlowPeriod = 21;
				StopLossTicks = 12;
				ProfitTargetTicks = 24;
			}
			if (State == State.DataLoaded)
			{
				accumulation = new AccumulationEngine(this);
				distribution = new DistributionEngine(this);

				coordinator = new StructureCoordinator(accumulation, distribution);
			}
		}


		protected override void OnExecutionUpdate(
	Execution execution,
	string executionId,
	double price,
	int quantity,
	MarketPosition marketPosition,
	string orderId,
	DateTime time)
		{
			if (execution.Order.OrderState != OrderState.Filled)
				return;

			// ----------------------------
			// Detect NEW ENTRY
			// ----------------------------
			if (activePosition == MarketPosition.Flat &&
				Position.MarketPosition != MarketPosition.Flat)
			{
				entryPrice = Position.AveragePrice;
				activePosition = Position.MarketPosition;

				Print("NEW POSITION OPENED AT: " + entryPrice);
			}

			// ----------------------------
			// Detect FULL EXIT
			// ----------------------------
			if (activePosition != MarketPosition.Flat &&
				Position.MarketPosition == MarketPosition.Flat)
			{
				Print("POSITION CLOSED");

				activePosition = MarketPosition.Flat;
				entryPrice = 0;
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 50)
				return;
			if (BarsInProgress != 0)
				return;

			// ---------------------------
			// Session reset
			// ---------------------------
			if (currentSessionDate.Date != Time[0].Date)
			{
				currentSessionDate = Time[0].Date;
				tradesToday = 0;
				dailyPnL = 0;
			}

			// ---------------------------
			// Trading hours filter
			// ---------------------------
			if (ToTime(Time[0]) < 083000 || ToTime(Time[0]) > 135900)
				return;

			dailyPnL =
				SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;

			// ============================
			// TRAILING LOGIC (ALWAYS RUN)
			// ============================

			HandleTrailingStops();

			// ---------------------------
			// Entry locking logic
			// ---------------------------
			bool tradingLocked =
				dailyPnL >= dailyProfitLimit ||
				dailyPnL <= dailyLossLimit ||
				tradesToday >= maxTradesPerDay;

			if (tradingLocked)
				return;

			// ---------------------------
			// Structure engine processing
			// ---------------------------
			coordinator.Process();
		}

		private void HandleTrailingStops()
		{
			if (entryPrice <= 0)
				return;

			double atrValue = atr[0];
			double trailDistance = atrValue * AtrMultiplier;

			// LONG
			if (Position.MarketPosition == MarketPosition.Long)
			{
				double newStop = Close[0] - trailDistance;

				if (newStop > entryPrice)
				{
					SetStopLoss("CORE_T2", CalculationMode.Price, newStop, false);
					Print("ATR LONG TRAIL MOVED TO: " + newStop);
				}
			}

			// SHORT
			if (Position.MarketPosition == MarketPosition.Short)
			{
				double newStop = Close[0] + trailDistance;

				if (newStop < entryPrice)
				{
					SetStopLoss("CORE_T2", CalculationMode.Price, newStop, false);
					Print("ATR SHORT TRAIL MOVED TO: " + newStop);
				}
			}
		}


	}
}