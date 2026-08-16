namespace TimeServerStressTest;

public static class MultiTestRequestBudget
{
    public static bool TryCreate(IReadOnlyList<StressTestResult> successfulResults, out long maximumRequests)
    {
        maximumRequests = 0;
        var totals = successfulResults
            .Where(result => result.FailedRequests == 0 && result.TotalRequests > 0)
            .Select(result => result.TotalRequests)
            .ToArray();
        if (totals.Length == 0)
        {
            return false;
        }

        maximumRequests = (long)Math.Round(totals.Average());
        return maximumRequests > 0;
    }
}
