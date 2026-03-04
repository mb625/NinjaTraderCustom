public class DistributionEngine : IWyckoffStructureEngine
{
    private readonly Strategy strategy;

    public StructureDirection Direction => StructureDirection.Distribution;
    public StructurePhase Phase { get; private set; }

    public bool IsActive => Phase != StructurePhase.Searching;

    // ===== STRUCTURE VARIABLES =====
    private double candidateBcHigh;
    private int candidateBcBar;

    private double arLow;
    private double arLowLocked;
    private bool arDisplacementReached;

    private double utHigh;
    private double structureHigh;

    private bool sowTriggered;

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

        sowTriggered = false;

        Phase = StructurePhase.Searching;
    }

    private void SearchForBC()
    {
        bool sweep =
            strategy.High[0] > strategy.MAX(strategy.High, 20)[1];

        double range =
            strategy.High[0] - strategy.Low[0];

        double avgRange =
            strategy.SMA(strategy.High, 20)[0] -
            strategy.SMA(strategy.Low, 20)[0];

        bool expansion =
            range > avgRange * 1.5;

        bool rejection =
            (strategy.High[0] - strategy.Close[0]) / range >= 0.6;

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

    private void TrackARDown()
    {
        if (strategy.Low[0] < arLow)
            arLow = strategy.Low[0];

        if (!arDisplacementReached &&
            arLow <= candidateBcHigh - 6.0 &&
            strategy.CurrentBar > candidateBcBar)
        {
            arDisplacementReached = true;
            strategy.Print("AR DOWN DISPLACEMENT CONFIRMED");
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

            if (utHigh > candidateBcHigh + 3.0)
            {
                Reset();
                return;
            }

            structureHigh = Math.Max(candidateBcHigh, utHigh);
            arLowLocked = arLow;

            Phase = StructurePhase.WaitingForBreak;

            strategy.Print("AR CONFIRMED — WAITING FOR SOW");
        }
    }

    private void CheckSOW()
    {
        if (!sowTriggered &&
            strategy.Low[0] < arLowLocked)
        {
            sowTriggered = true;
            Phase = StructurePhase.WaitingForLPS;

            strategy.Print("SOW CONFIRMED");
        }
    }
    private void CheckLPSY()
    {
        bool holdsStructure =
            strategy.High[0] < structureHigh + 3.0;

        bool bearishEngulfing =
            strategy.Close[0] < strategy.Open[0] &&
            strategy.Close[1] > strategy.Open[1] &&
            strategy.Close[0] <= strategy.Open[1] &&
            strategy.Open[0] >= strategy.Close[1];

        if (holdsStructure && bearishEngulfing)
        {
            ExecuteTrade();
        }
    }

    public void ExecuteTrade()
    {
        strategy.EnterShort(1, "LPSY_T1");
        strategy.EnterShort(1, "LPSY_T2");

        Phase = StructurePhase.InTrade;
    }

    public void ProcessBar()
    {
        switch (Phase)
        {
            case StructurePhase.Searching:
                SearchForBC();
                break;

            case StructurePhase.TrackingAR:
                TrackARDown();
                break;

            case StructurePhase.WaitingForBreak:
                CheckSOW();
                break;

            case StructurePhase.WaitingForLPS:
                CheckLPSY();
                break;
        }
    }

    private void SearchForBC() { }
    private void TrackARDown() { }
    private void CheckSOW() { }
    private void CheckLPSY() { }

    public bool HasConfirmedBreak()
        => Phase == StructurePhase.WaitingForLPS;

    public bool IsInvalidated()
        => strategy.High[0] > structureHigh + 3;

    public bool WantsToTrade()
        => Phase == StructurePhase.WaitingForLPS;

    public void ExecuteTrade()
    {
        strategy.EnterShort(1, "LPSY");
        Phase = StructurePhase.InTrade;
    }
}