using System;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies.WyckoffEngine
{
    public class DistributionEngine : BaseWyckoffEngine
    {
        public override StructureDirection Direction => StructureDirection.Distribution;

        public DistributionEngine(Strategy strategy) : base(strategy)
        {
        }

        protected override bool DetectClimax()
        {
            return false;
        }

        protected override void TrackAR()
        {
        }

        protected override void CheckForST()
        {
        }

        protected override void TrackPreSosRangeExtreme()
        {
        }

        protected override void CheckSOS()
        {
        }

        protected override bool EntrySignal()
        {
            return false;
        }

        protected override bool StructureInvalidated()
        {
            return false;
        }

        protected override void ExecuteTrade()
        {
        }
    }
}
/*
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class DistributionEngine : BaseWyckoffEngine
    {
        private const string arLineTag = "AR_LINE_DIST";
        private const string structureLineTag = "STRUCTURE_LINE_DIST";

        private readonly Strategy strategy;

        public StructureDirection Direction => StructureDirection.Distribution;
        public StructurePhase Phase { get; private set; }

        public bool IsActive => Phase != StructurePhase.Searching;
        public bool IsInTradePhase => Phase == StructurePhase.InTrade;

        // =========================================================
        // PHASE A — BUYING CLIMAX (BC)
        // =========================================================

        private double candidateBcHigh;
        private int candidateBcBar;

        private double arLow;
        private double arLowLocked;
        private bool arDisplacementReached;

        private double utHigh;
        private double structureHigh;

        // =========================================================
        // PHASE B/C — PRE-SOW LOWER HIGH (EMA STRUCTURE)
        // =========================================================

        private double phaseRangeHigh;
        private bool rangeHighLocked;

        // =========================================================
        // SOW + RANGE FINALIZATION
        // =========================================================

        private bool sowTriggered;
        private double sowLow;

        private double premiumLevel;      // 0.62
        private double deepPremiumLevel;  // 0.786
        private bool rangeLocked;

        // =========================================================
        // REATTEMPT LOGIC
        // =========================================================

        private int lpsyAttempts;
        private const int maxLpsyAttempts = 2;
        private bool fullStopOutOccurred;

        // =========================================================

        public DistributionEngine(Strategy strategy)
        {
            this.strategy = strategy;
            Reset();
        }

        public void Reset()
        {
            candidateBcHigh = double.MinValue;
            candidateBcBar = -1;

            arLow = double.MaxValue;
            arLowLocked = 0;
            arDisplacementReached = false;

            utHigh = 0;
            structureHigh = 0;

            phaseRangeHigh = 0;
            rangeHighLocked = false;

            sowTriggered = false;
            sowLow = 0;

            premiumLevel = 0;
            deepPremiumLevel = 0;
            rangeLocked = false;

            lpsyAttempts = 0;
            fullStopOutOccurred = false;

            strategy.RemoveDrawObject(arLineTag);
            strategy.RemoveDrawObject(structureLineTag);

            Phase = StructurePhase.Searching;
        }

        // =========================================================
        // PHASE 1 — SEARCH FOR BC
        // =========================================================

        private void SearchForBC()
        {
            bool sweep = strategy.High[0] > strategy.MAX(strategy.High, 20)[1];

            double barRange = strategy.High[0] - strategy.Low[0];
            double avgRange =
                strategy.SMA(strategy.High, 20)[0] -
                strategy.SMA(strategy.Low, 20)[0];

            bool expansion = barRange > avgRange * 1.5;
            bool rejection =
                (strategy.High[0] - strategy.Close[0]) / barRange >= 0.6;

            if (sweep && expansion && rejection)
            {
                candidateBcHigh = strategy.High[0];
                candidateBcBar = strategy.CurrentBar;

                arLow = strategy.Low[0];
                arDisplacementReached = false;

                Phase = StructurePhase.TrackingAR;

                strategy.Print("BC DETECTED");
            }
        }

        // =========================================================
        // PHASE 2 — TRACK AR DOWN
        // =========================================================

        private void TrackAR()
        {
            if (strategy.Low[0] < arLow)
                arLow = strategy.Low[0];

            if (!arDisplacementReached &&
                arLow <= candidateBcHigh - 6.0 &&
                strategy.CurrentBar > candidateBcBar)
            {
                arDisplacementReached = true;
                strategy.Print("AR DOWN DISPLACEMENT REACHED");
            }

            if (arDisplacementReached)
                CheckForUT();
        }

        private void CheckForUT()
        {
            double range = candidateBcHigh - arLow;
            double retraceLevel = arLow + (range * 0.618);

            if (strategy.High[0] >= retraceLevel)
            {
                utHigh = strategy.High[0];

                // Structural invalidation
                if (utHigh > candidateBcHigh + 3.0)
                {
                    Reset();
                    return;
                }

                structureHigh = Math.Max(candidateBcHigh, utHigh);
                arLowLocked = arLow;

                Phase = StructurePhase.WaitingForBreak;

                strategy.Print("UT CONFIRMED");
            }
        }

        // =========================================================
        // PHASE B/C — LOCK PRE-SOW LOWER HIGH
        // =========================================================

        private void TrackPreSowLowerHigh()
        {
            if (rangeHighLocked)
                return;

            // Pullback begins when fast EMA crosses ABOVE slow EMA
            if (strategy.CrossAbove(strategy.EMA(9), strategy.EMA(21), 1))
            {
                phaseRangeHigh = strategy.MAX(strategy.High, 10)[0];
                rangeHighLocked = true;

                strategy.Print("PRE-SOW RANGE HIGH LOCKED: " + phaseRangeHigh);
            }
        }

        // =========================================================
        // PHASE 3 — SOW
        // =========================================================

        private void CheckSOW()
        {
            if (!sowTriggered &&
                strategy.Low[0] < arLowLocked)
            {
                sowTriggered = true;
                sowLow = strategy.Low[0];

                // 🔥 LIVE BOS ARROW (Break of Structure)
                strategy.Draw.ArrowDown(
                    strategy,
                    "DIST_BOS_" + strategy.CurrentBar,
                    false,
                    0,
                    strategy.High[0] + 2,
                    Brushes.Red);

                strategy.Print("SOW CONFIRMED (BOS)");

                if (rangeHighLocked)
                {
                    double fullRange = phaseRangeHigh - sowLow;

                    premiumLevel = sowLow + (fullRange * 0.62);
                    deepPremiumLevel = sowLow + (fullRange * 0.786);

                    rangeLocked = true;

                    strategy.Print("RANGE LOCKED (DIST)");
                    strategy.Print("HIGH: " + phaseRangeHigh);
                    strategy.Print("LOW: " + sowLow);
                    strategy.Print("0.62: " + premiumLevel);
                    strategy.Print("0.786: " + deepPremiumLevel);
                }

                Phase = StructurePhase.WaitingForLPS;
            }
        }

        // =========================================================
        // PHASE 4 — LPSY ENTRY LOGIC
        // =========================================================

        private void CheckLPSY()
        {
            if (!rangeLocked)
                return;

            if (lpsyAttempts >= maxLpsyAttempts)
                return;

            bool holdsStructure =
                strategy.High[0] < phaseRangeHigh + 3.0;

            bool bearishEngulfing =
                strategy.Close[0] < strategy.Open[0] &&
                strategy.Close[1] > strategy.Open[1] &&
                strategy.Close[0] <= strategy.Open[1] &&
                strategy.Open[0] >= strategy.Close[1];

            bool emaCrossDown =
                strategy.CrossBelow(strategy.EMA(9), strategy.EMA(21), 1);

            // ======================
            // FIRST ATTEMPT (.62)
            // ======================
            if (lpsyAttempts == 0)
            {
                bool inPremium = strategy.Close[0] >= premiumLevel;

                if (inPremium &&
                    holdsStructure &&
                    bearishEngulfing &&
                    emaCrossDown)
                {
                    ExecuteTrade();
                }

                return;
            }

            // ======================
            // SECOND ATTEMPT (.786 REQUIRED)
            // ======================
            if (lpsyAttempts == 1 && fullStopOutOccurred)
            {
                bool reachedDeepPremium =
                    strategy.High[0] >= deepPremiumLevel;

                if (reachedDeepPremium &&
                    holdsStructure &&
                    bearishEngulfing &&
                    emaCrossDown)
                {
                    ExecuteTrade();
                }
            }
        }

        // =========================================================
        // EXECUTION
        // =========================================================

        private void ExecuteTrade()
        {
            strategy.EnterShort(1, "CORE_T1");
            strategy.EnterShort(1, "CORE_T2");

            lpsyAttempts++;
            fullStopOutOccurred = false;

            Phase = StructurePhase.InTrade;

            strategy.Print("LPSY ENTRY EXECUTED - ATTEMPT " + lpsyAttempts);
        }

        public void NotifyStopOut()
        {
            fullStopOutOccurred = true;
            Phase = StructurePhase.WaitingForLPS;

            strategy.Print("SHORT STOP OUT - READY FOR REATTEMPT");
        }

        // =========================================================
        // VISUALIZATION
        // =========================================================

        private void DrawStructure()
        {
            if (arLowLocked > 0)
            {
                strategy.Draw.HorizontalLine(
                    strategy,
                    arLineTag,
                    arLowLocked,
                    Brushes.DodgerBlue);
            }

            if (rangeLocked)
            {
                strategy.Draw.HorizontalLine(
                    strategy,
                    structureLineTag + "_HIGH",
                    phaseRangeHigh,
                    Brushes.Red);

                strategy.Draw.HorizontalLine(
                    strategy,
                    structureLineTag + "_LOW",
                    sowLow,
                    Brushes.DarkRed);

                strategy.Draw.HorizontalLine(
                    strategy,
                    structureLineTag + "_62",
                    premiumLevel,
                    Brushes.Orange);

                strategy.Draw.HorizontalLine(
                    strategy,
                    structureLineTag + "_786",
                    deepPremiumLevel,
                    Brushes.Goldenrod);
            }
        }

        private void DrawPhaseLabel()
        {
            strategy.Draw.TextFixed(
                strategy,
                "DIST_PHASE",
                $"DIST Phase: {Phase}",
                TextPosition.TopRight,
                Brushes.White,
                new Gui.Tools.SimpleFont("Arial", 14),
                Brushes.Black,
                Brushes.Black,
                0);
        }

        // =========================================================
        // PROCESS BAR
        // =========================================================

        public void ProcessBar()
        {
            // Structural invalidation
            if (Phase != StructurePhase.Searching &&
                strategy.High[0] > structureHigh + 3.0)
            {
                strategy.Print("DISTRIBUTION STRUCTURE BROKEN - RESET");
                Reset();
                return;
            }

            switch (Phase)
            {
                case StructurePhase.Searching:
                    SearchForBC();
                    break;

                case StructurePhase.TrackingAR:
                    TrackAR();
                    break;

                case StructurePhase.WaitingForBreak:
                    TrackPreSowLowerHigh();
                    CheckSOW();
                    break;

                case StructurePhase.WaitingForLPS:
                    CheckLPSY();
                    break;
            }

            // 🔥 Always draw structure + phase
            DrawStructure();
            DrawPhaseLabel();
        }
    }
}
*/