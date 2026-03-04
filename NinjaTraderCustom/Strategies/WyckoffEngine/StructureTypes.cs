namespace NinjaTrader.NinjaScript.Strategies.WyckoffEngine
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

    public interface IWyckoffStructureEngine
    {
        StructureDirection Direction { get; }
        StructurePhase Phase { get; }

        bool IsActive { get; }
        bool IsInTradePhase { get; }

        void ProcessBar();
        void Reset();
    }
}