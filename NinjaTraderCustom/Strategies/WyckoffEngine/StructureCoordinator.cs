using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies.WyckoffEngine
{
    public class StructureCoordinator
    {
        private readonly AccumulationEngine accumulation;
        private readonly DistributionEngine distribution;

        public enum StructureDirection
        {
            None,
            Accumulation,
            Distribution
        }

        public StructureDirection ActiveDirection { get; private set; }

        public StructureCoordinator(Strategy strategy)
        {
            accumulation = new AccumulationEngine(strategy);
            distribution = new DistributionEngine(strategy);

            ActiveDirection = StructureDirection.None;
        }

        public void Reset()
        {
            accumulation.Reset();
            distribution.Reset();

            ActiveDirection = StructureDirection.None;
        }

        public void ProcessBar()
        {
            accumulation.ProcessBar();
            distribution.ProcessBar();

            if (accumulation.IsActive)
                ActiveDirection = StructureDirection.Accumulation;
            else if (distribution.IsActive)
                ActiveDirection = StructureDirection.Distribution;
            else
                ActiveDirection = StructureDirection.None;
        }

        public bool IsInTradePhase()
        {
            return accumulation.IsInTradePhase || distribution.IsInTradePhase;
        }
    }
}
/*
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class StructureCoordinator
    {
        public StructureDirection ActiveDirection { get; private set; }
        public AccumulationEngine Accumulation => accumulation;
        public DistributionEngine Distribution => distribution;
        private readonly IWyckoffStructureEngine accumulation;
        private readonly IWyckoffStructureEngine distribution;

        public StructureDirection ActiveDirection { get; private set; }

        public StructureCoordinator(
            IWyckoffStructureEngine accumulation,
            IWyckoffStructureEngine distribution)
        {
            this.accumulation = accumulation;
            this.distribution = distribution;
            ActiveDirection = StructureDirection.None;
        }

        public void Process()
        {
            if (ActiveDirection == StructureDirection.None)
            {
                accumulation.ProcessBar();
                distribution.ProcessBar();

                if (accumulation.HasConfirmedBreak())
                    Activate(StructureDirection.Accumulation);

                else if (distribution.HasConfirmedBreak())
                    Activate(StructureDirection.Distribution);
            }
            else if (ActiveDirection == StructureDirection.Accumulation)
            {
                accumulation.ProcessBar();

                if (accumulation.IsInvalidated())
                    ResetAll();
            }
            else if (ActiveDirection == StructureDirection.Distribution)
            {
                distribution.ProcessBar();

                if (distribution.IsInvalidated())
                    ResetAll();
            }
        }

        private void Activate(StructureDirection direction)
        {
            ActiveDirection = direction;

            if (direction == StructureDirection.Accumulation)
                distribution.Reset();
            else
                accumulation.Reset();
        }

        private void ResetAll()
        {
            accumulation.Reset();
            distribution.Reset();
            ActiveDirection = StructureDirection.None;
        }
    }
    */