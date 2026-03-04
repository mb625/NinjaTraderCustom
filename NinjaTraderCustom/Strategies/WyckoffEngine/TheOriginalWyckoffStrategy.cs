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
// Test change for Git commit
//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies.WyckoffEngine
{
	public class TheOriginalWyckoffStrategy : Strategy
	{
		
		private double candidateScLow = double.MaxValue;
		private int candidateScBar = -1;
		
		private double arHigh = double.MinValue;
		private double arHighLocked = 0;
		
		private double stLow = 0;
		private double structureLow = 0;
		
		private bool arDisplacementReached = false;
		
		private WyckoffState currentState = WyckoffState.SearchingForCandidateSC;
		
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
                Name = "TheOriginalWyckoffStrategy";
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
		}
		
		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, 
    MarketPosition marketPosition, string orderId, DateTime time)
		{
		    if (execution.Order.OrderState != OrderState.Filled)
		        return;
					
			
		}

        protected override void OnBarUpdate()
		{
		    if (CurrentBar < 50)
		        return;
		
		    // ================================
		    //  HARD STRUCTURE INVALIDATION
		    // ================================
		    if ((currentState == WyckoffState.WaitingForSOS ||
			     currentState == WyckoffState.WaitingForLPS ||
			     currentState == WyckoffState.InTrade) &&
			     Low[0] < structureLow - 3.0)
		    {
		        Print("STRUCTURE BROKEN - RESETTING");
		        ResetStructure();
		        return;
		    }
		
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
		
		            Print("CANDIDATE SC DETECTED");
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
		            Print("AR DISPLACEMENT THRESHOLD REACHED");
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
		
		                currentState = WyckoffState.WaitingForSOS;
		
		                Print("AR CONFIRMED VIA 61.8% RETRACE");
		                Print("AR HIGH LOCKED AT: " + arHighLocked);
		                Print("ST LOW: " + stLow);
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
					Draw.Text(this,
					    "AR_disp_" + CurrentBar,
					    "AR",
					    0,
					    arHigh + 2 * TickSize,
					    Brushes.Gold);
		        }
		    }
		
		    // ============================================
		    //  PHASE 3 — WAITING FOR SOS
		    // ============================================
		    if (currentState == WyckoffState.WaitingForSOS)
		    {
		        if (High[0] > arHighLocked)
		        {
		            currentState = WyckoffState.WaitingForLPS;
		            Print("SOS CONFIRMED");
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
		    }
		
		    // ============================================
		    //  PHASE 4 — LPS ENTRY (MULTIPLE ATTEMPTS)
		    // ============================================
		    if (currentState == WyckoffState.WaitingForLPS)
		    {
		        bool holdsStructure = Low[0] > structureLow - 3.0;
		
		        bool bullishEngulfing =
		            Close[0] > Open[0] &&
		            Close[1] < Open[1] &&
		            Close[0] >= Open[1] &&
		            Open[0] <= Close[1];
		
		        if (holdsStructure && bullishEngulfing)
		        {
		            double stopPrice = structureLow - 3.0;
		            double target1 = Close[0] + 4.5;
		            double target2 = Close[0] + 9.0;
					Draw.Text(this,
					    "LPS_" + CurrentBar,
					    "LPS",
					    0,
					    Low[0] - 2 * TickSize,
					    Brushes.Lime);
		
		            EnterLong(1, "LPS_T1");
		            SetStopLoss("LPS_T1", CalculationMode.Price, stopPrice, false);
		            SetProfitTarget("LPS_T1", CalculationMode.Price, target1);
		
		            EnterLong(1, "LPS_T2");
		            SetStopLoss("LPS_T2", CalculationMode.Price, stopPrice, false);
		            SetProfitTarget("LPS_T2", CalculationMode.Price, target2);
		
		            currentState = WyckoffState.InTrade;
		
		            Print("LPS ENTRY EXECUTED");
		        }
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
