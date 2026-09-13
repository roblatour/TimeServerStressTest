namespace TimeServerStressTest;

public sealed class IncrementNumericUpDown : NumericUpDown
{
    public override void UpButton()
    {
        Value = Math.Min(Maximum, (decimal)(Math.Floor((double)Value / 1000) + 1) * 1000);
    }

    public override void DownButton()
    {
        Value = Value <= 1000 ? Minimum : (decimal)Math.Floor((double)(Value - 1) / 1000) * 1000;
    }
}
