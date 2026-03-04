public abstract class BaseWyckoffEngine : IWyckoffStructureEngine
{
    protected readonly Strategy strategy;

    public abstract StructureDirection Direction { get; }

    public StructurePhase Phase { get; protected set; }

    public bool IsActive => Phase != StructurePhase.Searching;
    public bool IsInTradePhase => Phase == StructurePhase.InTrade;

    // ==============================
    // PHASE A STRUCTURE
    // ==============================

    protected double candidateExtreme;
    protected int candidateBar;

    protected double arExtreme;
    protected double arLocked;
    protected bool arDisplacementReached;

    protected double stExtreme;
    protected double structureExtreme;

    // ==============================
    // RANGE TRACKING
    // ==============================

    protected double phaseRangeExtreme;
    protected bool rangeExtremeLocked;

    protected bool sosTriggered;
    protected double sosExtreme;

    protected double discountLevel;
    protected double deepDiscountLevel;
    protected bool rangeLocked;

    // ==============================
    // REATTEMPT
    // ==============================

    protected int lpsAttempts;
    protected const int maxLpsAttempts = 2;
    protected bool fullStopOutOccurred;

    protected BaseWyckoffEngine(Strategy strategy)
    {
        this.strategy = strategy;
        Reset();
    }

    public virtual void Reset()
    {
        candidateExtreme = 0;
        candidateBar = -1;

        arExtreme = 0;
        arLocked = 0;
        arDisplacementReached = false;

        stExtreme = 0;
        structureExtreme = 0;

        phaseRangeExtreme = 0;
        rangeExtremeLocked = false;

        sosTriggered = false;
        sosExtreme = 0;

        discountLevel = 0;
        deepDiscountLevel = 0;
        rangeLocked = false;

        lpsAttempts = 0;
        fullStopOutOccurred = false;

        Phase = StructurePhase.Searching;
    }

    // =========================================================
    // ABSTRACT DIRECTION-SPECIFIC RULES
    // =========================================================

    protected abstract bool DetectClimax();
    protected abstract void TrackAR();
    protected abstract void CheckForST();
    protected abstract void TrackPreSosRangeExtreme();
    protected abstract void CheckSOS();
    protected abstract bool EntrySignal();

    protected abstract bool StructureInvalidated();

    protected abstract void ExecuteTrade();

    // =========================================================
    // STOP OUT NOTIFICATION
    // =========================================================

    public void NotifyStopOut()
    {
        fullStopOutOccurred = true;
        Phase = StructurePhase.WaitingForLPS;
    }

    // =========================================================
    // MAIN PROCESS LOOP
    // =========================================================

    public void ProcessBar()
    {
        if (Phase != StructurePhase.Searching &&
            StructureInvalidated())
        {
            Reset();
            return;
        }

        switch (Phase)
        {
            case StructurePhase.Searching:
                if (DetectClimax())
                    Phase = StructurePhase.TrackingAR;
                break;

            case StructurePhase.TrackingAR:
                TrackAR();
                break;

            case StructurePhase.WaitingForBreak:
                TrackPreSosRangeExtreme();
                CheckSOS();
                break;

            case StructurePhase.WaitingForLPS:
                CheckLPS();
                break;
        }
    }

    // =========================================================
    // SHARED LPS LOGIC (0.62 / 0.786)
    // =========================================================

    private void CheckLPS()
    {
        if (!rangeLocked)
            return;

        if (lpsAttempts >= maxLpsAttempts)
            return;

        if (!EntrySignal())
            return;

        ExecuteTrade();
        lpsAttempts++;
        fullStopOutOccurred = false;
        Phase = StructurePhase.InTrade;
    }
}