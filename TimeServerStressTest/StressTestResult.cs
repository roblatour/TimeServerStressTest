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
    StressTestStatus Status,
    bool IsSingleRequest = false,
    long ActualSentRequestCount = 0,
    double ActualAchievedSendsPerSecond = 0,
    double MaximumSchedulingLatenessMilliseconds = 0,
    long ConfiguredRequestsPerSecond = 0,
    TimeSpan SendPhaseDuration = default,
    TimeSpan DrainPhaseDuration = default,
    long MatchedResponses = 0,
    long UnmatchedResponses = 0,
    long DuplicateResponses = 0,
    long TimedOutOutstandingRequests = 0,
    double AverageSchedulingLatenessMilliseconds = 0,
    double SenderBlockedByOutstandingWindowDurationMilliseconds = 0,
    StressTestMode TestMode = StressTestMode.Paced,
    double MinimumObservedInterSendGapMicroseconds = 0,
    double MaximumObservedInterSendGapMilliseconds = 0,
    long SendsViolatingMinimumGap = 0,
    double ScheduleRecoveryDurationMilliseconds = 0,
    long LostRequests = 0,
    int GracePeriodMilliseconds = 0,
    int RequiredDrainPeriodMilliseconds = 0)
{
    public TimeSpan Duration => Elapsed > TimeSpan.Zero ? Elapsed : TimeSpan.Zero;

    public double RequestsPerSecond => GetRate(TotalRequests);

    public double SuccessfulRequestsPerSecond => GetRate(SuccessfulRequests);

    public double FailedRequestsPerSecond => GetRate(FailedRequests);

    public double SuccessRate => TotalRequests == 0 ? 0 : (double)SuccessfulRequests / TotalRequests * 100;

    public double AdjustedSuccessfulRequestsPerSecond => RequestsPerSecond * (SuccessRate / 100);

    private double GetRate(long count)
    {
        return count / Math.Max(Duration.TotalSeconds, 0.001);
    }
}
