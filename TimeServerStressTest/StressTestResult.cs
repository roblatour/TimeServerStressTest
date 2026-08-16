namespace TimeServerStressTest;

public enum StressTestStatus
{
    Completed,
    Stopped
}

public sealed record StressTestResult(
    int Workers,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    DateTime Started,
    DateTime Ended,
    TimeSpan Elapsed,
    StressTestStatus Status)
{
    public TimeSpan Duration => Elapsed > TimeSpan.Zero ? Elapsed : TimeSpan.Zero;

    public double RequestsPerSecond => GetRate(TotalRequests);

    public double SuccessfulRequestsPerSecond => GetRate(SuccessfulRequests);

    public double FailedRequestsPerSecond => GetRate(FailedRequests);

    public double SuccessRate => TotalRequests == 0 ? 0 : (double)SuccessfulRequests / TotalRequests * 100;

    private double GetRate(long count)
    {
        return count / Math.Max(Duration.TotalSeconds, 0.001);
    }
}
