public class AccumulationEngine : IWyckoffStructureEngine
{
    private const string arLineTag = "AR_LINE_ACC";
    private const string structureLineTag = "STRUCTURE_LINE_ACC";
    private readonly Strategy strategy;

    public StructureDirection Direction => StructureDirection.Accumulation;
    public StructurePhase Phase { get; private set; }

    public bool IsActive => Phase != StructurePhase.Searching;

    // === Structure Variables (moved from strategy) ===
    private double candidateScLow;
    private int candidateScBar;

    private double arHigh;
    private double arHighLocked;
    private bool arDisplacementReached;

    private double stLow;
    private double structureLow;

    private bool sosTriggered;

    private int lpsAttempts;
    private double attempt1RetraceLow;
    private bool retraceReadyForAttempt2;

    private bool rangeDefined;
    private double lastRangeLow;
    public bool IsTrendContext { get; private set; }
public bool IsInTradePhase => Phase == StructurePhase.InTrade;

    public AccumulationEngine(Strategy strategy)
    {
        this.strategy = strategy;
        Reset();
    }

    public void Reset()
    {
        candidateScLow = double.MaxValue;
        candidateScBar = -1;

        arHigh = double.MinValue;
        arHighLocked = 0;
        arDisplacementReached = false;

        stLow = 0;
        structureLow = 0;
        strategy.RemoveDrawObject(arLineTag);
        strategy.RemoveDrawObject(structureLineTag);

        sosTriggered = false;
        lpsAttempts = 0;
        rangeDefined = false;

        Phase = StructurePhase.Searching;
    }

    private void SearchForSC()
    {
        bool sweep = strategy.Low[0] < strategy.MIN(strategy.Low, 20)[1];

        double range = strategy.High[0] - strategy.Low[0];
        double avgRange =
            strategy.SMA(strategy.High, 20)[0] -
            strategy.SMA(strategy.Low, 20)[0];

        bool expansion = range > avgRange * 1.5;
        bool rejection = (strategy.Close[0] - strategy.Low[0]) / range >= 0.6;

        if (sweep && expansion && rejection)
        {
            candidateScLow = strategy.Low[0];
            candidateScBar = strategy.CurrentBar;

            arHigh = strategy.High[0];
            arDisplacementReached = false;

            Phase = StructurePhase.TrackingAR;

            strategy.Print("SC DETECTED");
        }
    }

    private void TrackARUp()
    {
        if (strategy.High[0] > arHigh)
            arHigh = strategy.High[0];

        if (!arDisplacementReached &&
            arHigh >= candidateScLow + 6.0 &&
            strategy.CurrentBar > candidateScBar)
        {
            arDisplacementReached = true;
            strategy.Print("AR DISPLACEMENT REACHED");
        }

        if (arDisplacementReached)
            CheckForST();
    }

    private void CheckForST()
    {
        double range = arHigh - candidateScLow;
        double retraceLevel = arHigh - (range * 0.618);

        if (strategy.Low[0] <= retraceLevel)
        {
            stLow = strategy.Low[0];

            if (stLow < candidateScLow - 3.0)
            {
                Reset();
                return;
            }

            structureLow = Math.Min(candidateScLow, stLow);
            arHighLocked = arHigh;

            Phase = StructurePhase.WaitingForBreak;

            strategy.Print("AR CONFIRMED");
        }
    }

    private void CheckSOS()
    {
        if (!sosTriggered &&
            strategy.High[0] > arHighLocked)
        {
            sosTriggered = true;
            Phase = StructurePhase.WaitingForLPS;

            strategy.Print("SOS CONFIRMED");
        }
    }

    private void CheckLPS()
    {
        if (!rangeDefined)
            return;

        bool holdsStructure =
            strategy.Low[0] > structureLow - 3.0;

        bool bullishEngulfing =
            strategy.Close[0] > strategy.Open[0] &&
            strategy.Close[1] < strategy.Open[1] &&
            strategy.Close[0] >= strategy.Open[1] &&
            strategy.Open[0] <= strategy.Close[1];

        if (holdsStructure && bullishEngulfing)
        {
            ExecuteTrade();
        }
    }

    public void ExecuteTrade()
    {
        strategy.EnterLong(1, "LPS_T1");
        strategy.EnterLong(1, "LPS_T2");

        Phase = StructurePhase.InTrade;
    }

    public void ProcessBar()
    {
        switch (Phase)
        {
            case StructurePhase.Searching:
                SearchForSC();
                break;

            case StructurePhase.TrackingAR:
                TrackARUp();
                break;

            case StructurePhase.WaitingForBreak:
                CheckSOS();
                break;

            case StructurePhase.WaitingForLPS:
                CheckLPS();
                break;
        }
    }