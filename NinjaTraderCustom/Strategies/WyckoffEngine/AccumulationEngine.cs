using System.Windows.Media;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;

public class AccumulationEngine : BaseWyckoffEngine
{
    public override StructureDirection Direction => StructureDirection.Accumulation;

    private const string tagPrefix = "ACC_";

    public AccumulationEngine(Strategy strategy)
        : base(strategy)
    {
    }

    // =========================================================
    // CLIMAX (SC)
    // =========================================================

    protected override bool DetectClimax()
    {
        bool sweep = strategy.Low[0] < strategy.MIN(strategy.Low, 20)[1];

        double range = strategy.High[0] - strategy.Low[0];
        if (range <= 0)
            return false;

        double avgRange =
            strategy.SMA(strategy.High, 20)[0] -
            strategy.SMA(strategy.Low, 20)[0];

        bool expansion = range > avgRange * 1.5;
        bool rejection = (strategy.Close[0] - strategy.Low[0]) / range >= 0.6;

        if (sweep && expansion && rejection)
        {
            candidateExtreme = strategy.Low[0];
            candidateBar = strategy.CurrentBar;

            arExtreme = strategy.High[0];

            DrawDot("SC", candidateExtreme, Brushes.Cyan);
            DrawLabel("SC", "SC", candidateExtreme - strategy.TickSize * 4, Brushes.Cyan);

            return true;
        }

        return false;
    }

    // =========================================================
    // AR TRACKING
    // =========================================================

    protected override void TrackAR()
    {
        if (strategy.High[0] > arExtreme)
            arExtreme = strategy.High[0];

        if (!arDisplacementReached &&
            arExtreme >= candidateExtreme + 6.0)
        {
            arDisplacementReached = true;
        }

        if (arDisplacementReached)
            CheckForST();
    }

    protected override void CheckForST()
    {
        double range = arExtreme - candidateExtreme;
        double retraceLevel = arExtreme - (range * 0.618);

        if (strategy.Low[0] <= retraceLevel)
        {
            stExtreme = strategy.Low[0];
            structureExtreme = System.Math.Min(candidateExtreme, stExtreme);
            arLocked = arExtreme;

            DrawHorizontal("STRUCTURE_LOW", structureExtreme, Brushes.Red);
            DrawHorizontal("AR_HIGH", arLocked, Brushes.Goldenrod);
            DrawLabel("AR", "AR", arLocked + strategy.TickSize * 4, Brushes.Goldenrod);
            DrawLabel("ST", "ST", stExtreme - strategy.TickSize * 4, Brushes.Magenta);

            Phase = StructurePhase.WaitingForBreak;
        }
    }

    // =========================================================
    // PRE-SOS RANGE LOW LOCK
    // =========================================================

    protected override void TrackPreSosRangeExtreme()
    {
        if (rangeExtremeLocked)
            return;

        if (strategy.CrossBelow(strategy.EMA(9), strategy.EMA(21), 1))
        {
            phaseRangeExtreme = strategy.MIN(strategy.Low, 10)[0];
            rangeExtremeLocked = true;

            DrawHorizontal("RANGE_LOW", phaseRangeExtreme, Brushes.DodgerBlue);
        }
    }

    // =========================================================
    // SOS (BOS)
    // =========================================================

    protected override void CheckSOS()
    {
        if (!sosTriggered &&
            strategy.High[0] > arLocked)
        {
            sosTriggered = true;
            sosExtreme = strategy.High[0];

            double fullRange = sosExtreme - phaseRangeExtreme;

            discountLevel = sosExtreme - (fullRange * 0.62);
            deepDiscountLevel = sosExtreme - (fullRange * 0.786);

            rangeLocked = true;

            DrawHorizontal("SOS_HIGH", sosExtreme, Brushes.LimeGreen);
            DrawHorizontal("DISC_62", discountLevel, Brushes.Gray);
            DrawHorizontal("DISC_786", deepDiscountLevel, Brushes.DarkGray);
            DrawLabel("SOS", "SOS", sosExtreme + strategy.TickSize * 4, Brushes.LimeGreen);

            DrawBOSArrow();
            DrawTradingRange();

            Phase = StructurePhase.WaitingForLPS;
        }
    }

    // =========================================================
    // ENTRY SIGNAL
    // =========================================================

    protected override bool EntrySignal()
    {
        bool holdsStructure =
            strategy.Low[0] > phaseRangeExtreme - 3.0;

        bool bullishEngulfing =
            strategy.Close[0] > strategy.Open[0] &&
            strategy.Close[1] < strategy.Open[1] &&
            strategy.Close[0] >= strategy.Open[1];

        bool emaCrossUp =
            strategy.CrossAbove(strategy.EMA(9), strategy.EMA(21), 1);

        if (lpsAttempts == 0)
            return strategy.Close[0] <= discountLevel &&
                   holdsStructure &&
                   bullishEngulfing &&
                   emaCrossUp;

        if (lpsAttempts == 1 && fullStopOutOccurred)
            return strategy.Low[0] <= deepDiscountLevel &&
                   holdsStructure &&
                   bullishEngulfing &&
                   emaCrossUp;

        return false;
    }

    protected override bool StructureInvalidated()
    {
        return strategy.Low[0] < structureExtreme - 3.0;
    }

    protected override void ExecuteTrade()
    {
        strategy.EnterLong(1, "CORE_T1");
        strategy.EnterLong(1, "CORE_T2");

        DrawDot("ENTRY", strategy.Close[0], Brushes.Lime);
        DrawLabel("LPS", "LPS", strategy.Close[0] - strategy.TickSize * 6, Brushes.Lime);
    }

    // =========================================================
    // DRAW HELPERS
    // =========================================================

    private void DrawHorizontal(string name, double price, Brush brush)
    {
        string tag = tagPrefix + name;

        strategy.Draw.HorizontalLine(
            strategy,
            tag,
            false,
            price,
            brush
        );
    }

    private void DrawDot(string name, double price, Brush brush)
    {
        string tag = tagPrefix + name + "_" + strategy.CurrentBar;

        strategy.Draw.Dot(
            strategy,
            tag,
            false,
            0,
            price,
            brush
        );
    }

    private void DrawBOSArrow()
    {
        string tag = tagPrefix + "BOS_" + strategy.CurrentBar;

        double arrowPrice = strategy.Low[0] - strategy.TickSize * 4;

        strategy.Draw.ArrowUp(
            strategy,
            tag,
            false,
            0,
            arrowPrice,
            Brushes.LimeGreen
        );

        strategy.Draw.Text(
            strategy,
            tag + "_TXT",
            false,
            "BOS",
            0,
            arrowPrice - strategy.TickSize * 3,
            0,
            Brushes.LimeGreen
        );
    }

    private void DrawTradingRange()
    {
        string tag = tagPrefix + "TR_BOX";

        int startBarsAgo = 20;   // width of the box
        int endBarsAgo = 0;

        strategy.Draw.Rectangle(
            strategy,
            tag,
            false,
            startBarsAgo,
            sosExtreme,
            endBarsAgo,
            phaseRangeExtreme,
            Brushes.Transparent,
            Brushes.DarkSlateBlue,
            2
        );
    }

    private void DrawPhaseShading()
    {
        string tag = tagPrefix + "PHASE_BG";

        Brush phaseBrush = Brushes.Transparent;

        switch (Phase)
        {
            case StructurePhase.Searching:
                phaseBrush = Brushes.DarkRed;
                break;

            case StructurePhase.TrackingAR:
                phaseBrush = Brushes.Orange;
                break;

            case StructurePhase.WaitingForBreak:
                phaseBrush = Brushes.DodgerBlue;
                break;

            case StructurePhase.WaitingForLPS:
                phaseBrush = Brushes.MediumPurple;
                break;

            case StructurePhase.InTrade:
                phaseBrush = Brushes.LimeGreen;
                break;
        }

        double top = strategy.MAX(strategy.High, 50)[0];
        double bottom = strategy.MIN(strategy.Low, 50)[0];

        strategy.Draw.Rectangle(
            strategy,
            tag,
            false,
            50,
            top,
            0,
            bottom,
            Brushes.Transparent,
            phaseBrush,
            1
        );
    }

    private void DrawPhaseLabel()
    {
        string tag = tagPrefix + "PHASE";

        strategy.Draw.TextFixed(
            strategy,
            tag,
            $"ACC Phase: {Phase}",
            TextPosition.TopRight,
            Brushes.White,
            new NinjaTrader.Gui.Tools.SimpleFont("Arial", 14),
            Brushes.Black,
            Brushes.Black,
            0
        );
    }


}