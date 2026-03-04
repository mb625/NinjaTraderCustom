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
	public class WyckoffStrategy_Prod : Strategy
	{
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
		
		private double candidateScLow = double.MaxValue;
		private int candidateScBar = -1;
		
		private double arHigh = double.MinValue;
		private double arHighLocked = 0;
		private bool arDisplacementReached = false;
		
		private double stLow = 0;
		private double structureLow = 0;
		
		private bool sosTriggered = false;
		
		private double entryPrice = 0;
		private bool runnerStopMoved = false;	
		private bool fullStopOutOccurred = false;
		private bool retraceReadyForAttempt2 = false;
		private double attempt1RetraceLow = double.MaxValue;
		private MarketContext currentContext = MarketContext.Range;
		private double lastRangeLow = 0;
		private double lastSosHigh = 0;
		private bool rangeDefined = false;

		private enum MarketContext
		{
		    Range,
		    Trend
		}
		
		private WyckoffState currentState = WyckoffState.SearchingForCandidateSC;
		
		private string arLineTag = "AR_LINE";
		private string structureLineTag = "STRUCTURE_LINE";
			
		
		private enum WyckoffState
		{
		    SearchingForCandidateSC,
		    TrackingAR,
		    WaitingForSOS,
		    WaitingForLPS,
		    InTrade
		}

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Fast EMA Period", Order=1, GroupName="Parameters")]
        public int FastPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name="Slow EMA Period", Order=2, GroupName="Parameters")]
        public int SlowPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Stop Loss (ticks)", Order=3, GroupName="Risk")]
        public int StopLossTicks { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Profit Target (ticks)", Order=4, GroupName="Risk")]
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
        }
		
		private void ResetStructure()
		{
		    candidateScLow = double.MaxValue;
		    candidateScBar = -1;
		
		    arHigh = double.MinValue;
		    arHighLocked = 0;
		    arDisplacementReached = false;
		
		    stLow = 0;
		    structureLow = 0;
		
		    currentState = WyckoffState.SearchingForCandidateSC;
			RemoveDrawObject(arLineTag);
			RemoveDrawObject(structureLineTag);
			sosTriggered = false;
			lpsAttempts = 0;
			rangeDefined = false;
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
		
		    // --------------------------------------
		    // Detect T1 Profit Target Filled
		    // --------------------------------------
		    if (execution.Order.Name == "LPS_T1" &&
		        execution.Order.OrderAction == OrderAction.Sell &&
		        price >= entryPrice + 4.5)
		    {
		        if (!runnerStopMoved)
		        {
		            double newStop = entryPrice + 1.0;
		
		            SetStopLoss("LPS_T2", CalculationMode.Price, newStop, false);
		
		            runnerStopMoved = true;
		
		            Print("T1 HIT - RUNNER STOP MOVED TO +1");
		        }
		    }
		
		    // --------------------------------------
		    // Detect Stop Out Of Full Position
		    // --------------------------------------
		    if (execution.Order.OrderAction == OrderAction.Sell &&
		        price <= entryPrice - 3.0)
		    {
		        fullStopOutOccurred = true;
		
		        Print("FULL STOP OUT DETECTED");
		    }
		
		    // --------------------------------------
		    // Position Fully Closed
		    // --------------------------------------
		    if (Position.MarketPosition == MarketPosition.Flat)
		    {
		        tradesToday++;
		
		        dailyPnL =
		            SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
		
		        Print("TRADE CLOSED");
		        Print("Trades Today: " + tradesToday);
		        Print("Daily PnL: " + dailyPnL);
		
		        // Only allow second attempt if FULL stop occurred
		        if (!fullStopOutOccurred)
		        {
		            lpsAttempts = maxLpsAttempts;  // disable further attempts
		        }
		
		        fullStopOutOccurred = false;
		    }
		}

        protected override void OnBarUpdate()
		{
		    if (CurrentBar < 50)
		        return;
			
			bool tradingLocked =
		    dailyPnL >= dailyProfitLimit ||
		    dailyPnL <= dailyLossLimit ||
		    tradesToday >= maxTradesPerDay;			
			
			// ==========================
			// NEW SESSION RESET
			// ==========================
			if (currentSessionDate.Date != Time[0].Date)
			{
			    currentSessionDate = Time[0].Date;
			    tradesToday = 0;
			    dailyPnL = 0;
			}
			
			// ==========================
			// TRADING HOURS FILTER
			// 07:00 to 13:59 NY
			// ==========================
			if (ToTime(Time[0]) < 083000 || ToTime(Time[0]) > 135900)
			    return;
			
		
			
			
		    // ================================
		    //  HARD STRUCTURE INVALIDATION
		    // ================================
		    if ((currentState == WyckoffState.WaitingForSOS ||
			     currentState == WyckoffState.WaitingForLPS ||
			     currentState == WyckoffState.InTrade) &&
			     Low[0] < structureLow - 3.0)
		    {
		        Print("=================================");
				Print("STRUCTURE BROKEN - RESETTING");
				Print("Time: " + Time[0]);
				Print("Bar: " + CurrentBar);
				Print("Structure Low: " + structureLow);
				Print("Current Low: " + Low[0]);
				Print("=================================");
		        ResetStructure();
		        return;
		    }
			
			// ==========================
			// DAILY PNL LIMITS
			// ==========================
			dailyPnL = SystemPerformance.AllTrades.TradesPerformance.Currency.CumProfit;
			
		
		
		    // ============================================
		    //  PHASE 1 — SEARCHING FOR CANDIDATE SC
		    // ============================================
		    if (currentState == WyckoffState.SearchingForCandidateSC)
		    {
		        bool sweep = Low[0] < MIN(Low, 20)[1];
		
		        double range = High[0] - Low[0];
		        double avgRange = SMA(High, 20)[0] - SMA(Low, 20)[0];
		        bool expansion = range > avgRange * 1.5;
		
		        bool rejection = (Close[0] - Low[0]) / range >= 0.6;
		
		        if (sweep && expansion && rejection)
		        {
		            candidateScLow = Low[0];
		            candidateScBar = CurrentBar;
		
		            arHigh = High[0];
		            arDisplacementReached = false;
		
		            currentState = WyckoffState.TrackingAR;
		
		            Print("=================================");
					Print("SC DETECTED");
					Print("Time: " + Time[0]);
					Print("Bar: " + CurrentBar);
					Print("SC Low: " + candidateScLow);
					Print("=================================");
					Draw.Text(this,
					    "SC_" + CurrentBar,
					    "SC",
					    0,
					    candidateScLow - 2 * TickSize,
					    Brushes.DeepSkyBlue);
		        }
		    }
		
		    // ============================================
		    //  PHASE 2 — TRACKING AR EXPANSION
		    // ============================================
		    if (currentState == WyckoffState.TrackingAR)
		    {
		        // Update AR high dynamically
		        if (High[0] > arHigh)
		            arHigh = High[0];
		
		        // Require minimum displacement
		        if (!arDisplacementReached &&
		            arHigh >= candidateScLow + 6.0 &&
		            CurrentBar > candidateScBar)
		        {
		            arDisplacementReached = true;
		            Print("=================================");
					Print("AR DISPLACEMENT THRESHOLD REACHED");
					Print("Time: " + Time[0]);
					Print("Bar: " + CurrentBar);
					Print("Current AR High: " + arHigh);
					Print("Distance From SC: " + (arHigh - candidateScLow));
					Print("=================================");
					// Draw final AR
					Draw.Text(this,
						"AR_final_" + CurrentBar,
						"AR",
						0,
						arHighLocked + 2 * TickSize,
						Brushes.Gold);
						
					// Draw ST
					Draw.Text(this,
						"ST_" + CurrentBar,
						"ST",
						0,
						stLow - 2 * TickSize,
						Brushes.MediumPurple);
		        }
		
		        // After displacement, monitor 61.8% retrace
		        if (arDisplacementReached)
		        {
		            double range = arHigh - candidateScLow;
		            double retraceLevel = arHigh - (range * 0.618);
		
		            if (Low[0] <= retraceLevel)
		            {
		                // ST defined automatically
		                stLow = Low[0];
		
		                // Apply 3 point tolerance
		                if (stLow < candidateScLow - 3.0)
		                {
		                    Print("ST TOO DEEP - RESETTING STRUCTURE");
		                    ResetStructure();
		                    return;
		                }
		
		                // Confirm SC (adaptive)
		                structureLow = Math.Min(candidateScLow, stLow);
		
		                // Freeze AR permanently
		                arHighLocked = arHigh;
						
						// Remove previous lines if they exist
						RemoveDrawObject(arLineTag);
						RemoveDrawObject(structureLineTag);
						
						// Draw new AR line
						Draw.HorizontalLine(this,
						    arLineTag,
						    arHighLocked,
						    Brushes.Gold);
						
						// Draw new Structure line
						Draw.HorizontalLine(this,
						    structureLineTag,
						    structureLow,
						    Brushes.DeepSkyBlue);
		
		                currentState = WyckoffState.WaitingForSOS;
		
		                Print("=================================");
						Print("AR CONFIRMED");
						Print("Time: " + Time[0]);
						Print("Bar: " + CurrentBar);
						Print("SC Low: " + candidateScLow);
						Print("AR High Locked: " + arHighLocked);
						Print("Range: " + (arHighLocked - candidateScLow));
						Print("=================================");
		                Print("=================================");
						Print("ST CONFIRMED");
						Print("Time: " + Time[0]);
						Print("Bar: " + CurrentBar);
						Print("ST Low: " + stLow);
						Print("Distance Below SC: " + (candidateScLow - stLow));
						Print("=================================");
						
		            }
		        }
		    }
		
		    // ============================================
		    //  PHASE 3 — WAITING FOR SOS
		    // ============================================
		    if (currentState == WyckoffState.WaitingForSOS)
			{
			    if (!sosTriggered &&
			        High[0] > arHighLocked)   // 🔴 Just require break of AR
			    {
			        sosTriggered = true;
			
			        // Context classification
			        double arRange = arHighLocked - structureLow;
			
			        bool largeRange = arRange >= 6.0;
			        bool steepBreakout = (High[0] - Low[0]) >= 3.0;
			
			        if (largeRange && steepBreakout)
			        {
			            currentContext = MarketContext.Trend;
			            Print("CONTEXT: TREND");
			        }
			        else
			        {
			            currentContext = MarketContext.Range;
			            Print("CONTEXT: RANGE");
			        }
			
			        currentState = WyckoffState.WaitingForLPS;
			        Print("AR BROKEN — WAITING FOR LPS");
			
			        // Draw labels if desired
			        Draw.Text(this,
			            "AR_final_" + CurrentBar,
			            "AR",
			            0,
			            arHighLocked + 2 * TickSize,
			            Brushes.Gold);
			
			        Draw.Text(this,
			            "ST_" + CurrentBar,
			            "ST",
			            0,
			            stLow - 2 * TickSize,
			            Brushes.MediumPurple);
			    }
			}
			
			// ============================================
			// TREND RUNNER TRAILING LOGIC
			// ============================================
			if (currentContext == MarketContext.Trend &&
			    currentState == WyckoffState.InTrade &&
			    Position.MarketPosition == MarketPosition.Long &&
			    runnerStopMoved)
			{
			    // Trail below previous bar low by 1 point
			    double newTrailStop = Low[1] - 1.0;
			
			    // Only move stop higher (never loosen)
			    if (newTrailStop > entryPrice + 1.0)
			    {
			        SetStopLoss("LPS_T2", CalculationMode.Price, newTrailStop, false);
			        Print("TREND TRAIL MOVED TO: " + newTrailStop);
			    }
			}
			
			// ============================================
			// STRUCTURAL EMA CROSS (ONLY BEFORE SOS)
			// ============================================
			if (!rangeDefined &&
			    !sosTriggered &&
			    currentState != WyckoffState.InTrade &&
			    CrossAbove(EMA(FastPeriod), EMA(SlowPeriod), 1))
			{
			    lastRangeLow = MIN(Low, 20)[0];
			    rangeDefined = true;
			
			    Print("STRUCTURAL RANGE LOW LOCKED: " + lastRangeLow);
			}
		
		    // ============================================
			//  PHASE 4 — LPS ENTRY (CLEAN VERSION)
			// ============================================
			if (currentState == WyckoffState.WaitingForLPS)
			{
				if (!rangeDefined)
    				return;
			    bool holdsStructure = Low[0] > structureLow - 3.0;
			
			    bool bullishEngulfing =
			        Close[0] > Open[0] &&
			        Close[1] < Open[1] &&
			        Close[0] >= Open[1] &&
			        Open[0] <= Close[1];
				
				bool bullishOrderFlow =
    				CrossAbove(EMA(FastPeriod), EMA(SlowPeriod), 1);
			
			    tradingLocked =
			        dailyPnL >= dailyProfitLimit ||
			        dailyPnL <= dailyLossLimit ||
			        tradesToday >= maxTradesPerDay;
				
				// Calculate 61.8 retrace level
				double totalRange = arHighLocked - lastRangeLow;
				double retraceLevel = arHighLocked - (totalRange * 0.618);
				bool inDiscountZone = Close[0] <= retraceLevel;
				// ============================================
				// REQUIRE RETRACE INTO 0.618 AFTER SOS
				// ============================================
				
				// If price retraces again to 61.8 zone after stopout
				if (lpsAttempts == 1 && fullStopOutOccurred)
				{
				    if (Low[0] <= retraceLevel &&
				        Low[0] < attempt1RetraceLow)
				    {
				        retraceReadyForAttempt2 = true;
				
				        Print("DEEPER RETRACE CONFIRMED FOR ATTEMPT 2");
				        Print("Attempt1 Low: " + attempt1RetraceLow);
				        Print("Current Low: " + Low[0]);
				    }
				}
			
			    // ============================================
				// RANGE CONTEXT
				// ============================================
				if (currentContext == MarketContext.Range)
				{
				    bool validAttempt1 = lpsAttempts == 0;
				
				    bool validAttempt2 =
				        lpsAttempts == 1 &&
				        fullStopOutOccurred &&
				        retraceReadyForAttempt2;
				
				    if (!tradingLocked &&
				        holdsStructure &&
						rangeDefined &&
					    inDiscountZone &&
					    bullishOrderFlow &&
				        bullishEngulfing &&
				        (validAttempt1 || validAttempt2))
				    {
				        if (lpsAttempts == 0)
				            attempt1RetraceLow = Low[0];
				
				        lpsAttempts++;
				        retraceReadyForAttempt2 = false;
				        fullStopOutOccurred = false;
				
				        entryPrice = Close[0];
				        runnerStopMoved = false;
				
				        double initialStop = entryPrice - 3.0;
				        double target1 = entryPrice + 4.5;
				        double target2 = entryPrice + 9.0;
				
				        EnterLong(1, "LPS_T1");
				        SetStopLoss("LPS_T1", CalculationMode.Price, initialStop, false);
				        SetProfitTarget("LPS_T1", CalculationMode.Price, target1);
				
				        EnterLong(1, "LPS_T2");
				        SetStopLoss("LPS_T2", CalculationMode.Price, initialStop, false);
				        SetProfitTarget("LPS_T2", CalculationMode.Price, target2);
				
				        currentState = WyckoffState.InTrade;
				
				        Print("RANGE LPS ENTRY");
				    }
				}
				
				// ============================================
				// TREND CONTEXT
				// ============================================
				else if (currentContext == MarketContext.Trend)
				{
				    bool validTrendAttempt = lpsAttempts == 0;
				
				    if (!tradingLocked &&
				        holdsStructure &&
						rangeDefined &&
					    inDiscountZone &&
					    bullishOrderFlow &&
				        bullishEngulfing &&
				        validTrendAttempt)
				    {
				        lpsAttempts = maxLpsAttempts;  // disable reattempts
				
				        entryPrice = Close[0];
				        runnerStopMoved = false;
				
				        double initialStop = entryPrice - 3.0;
				        double target1 = entryPrice + 4.5;
				
				        EnterLong(1, "LPS_T1");
				        SetStopLoss("LPS_T1", CalculationMode.Price, initialStop, false);
				        SetProfitTarget("LPS_T1", CalculationMode.Price, target1);
				
				        EnterLong(1, "LPS_T2");
				        SetStopLoss("LPS_T2", CalculationMode.Price, initialStop, false);
				
				        currentState = WyckoffState.InTrade;
				
				        Print("TREND LPS ENTRY");
				    }
				}
			
			        Print("=================================");
			        Print("LPS ENTRY EXECUTED");
			        Print("Time: " + Time[0]);
			        Print("Attempt #: " + lpsAttempts);
			        Print("Trades Today: " + tradesToday);
			        Print("Daily PnL: " + dailyPnL);
			        Print("=================================");
			   }
			
		
		    // ============================================
		    //  AFTER TRADE — RETURN TO LPS
		    // ============================================
		    if (currentState == WyckoffState.InTrade &&
		        Position.MarketPosition == MarketPosition.Flat)
		    {
		        currentState = WyckoffState.WaitingForLPS;
		        Print("Trade complete - structure still valid");
		    }	    
			
			
			
		}
	}
}
