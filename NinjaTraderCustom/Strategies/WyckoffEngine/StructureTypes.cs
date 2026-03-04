namespace NinjaTrader.NinjaScript.Strategies
{
    public enum StructureDirection
    {
        Accumulation,
        Distribution
    }

    public enum StructurePhase
    {
        Searching,
        TrackingAR,
        WaitingForBreak,
        WaitingForLPS,
        InTrade
    }
}