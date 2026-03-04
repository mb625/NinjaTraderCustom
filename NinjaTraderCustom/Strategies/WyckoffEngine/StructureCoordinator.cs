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