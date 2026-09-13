using System.Globalization;
using System.Text;

namespace TimeServerStressTest;

public static class CsvReportExporter
{
    public static void Save(string path, IReadOnlyList<StressTestResult> results)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Concurrent Requests,Total Requests,Requests/Second,Successes,Failures,Losses,Success Rate,Successful Requests/Second,Started,Ended,Configured Requests/Second,Test Mode,Send Phase Duration,Drain Phase Duration,Actual Send Rate,Matched Responses,Unmatched Responses,Duplicate Responses,Timed-Out Outstanding Requests,Maximum Send Lateness (ms),Average Send Lateness (ms),Minimum Observed Inter-Send Gap (us),Maximum Observed Inter-Send Gap (ms),Sends Violating Minimum Gap,Schedule Recovery Duration (ms),Sender Blocked by Outstanding Window Duration (ms),Grace Period (ms),Required Drain Period (ms)");

        foreach (var result in results)
        {
            AppendRow(csv, [
                result.Workers.ToString(CultureInfo.InvariantCulture),
                result.TotalRequests.ToString(CultureInfo.InvariantCulture),
                result.IsSingleRequest ? "N/A" : result.RequestsPerSecond.ToString("N2", CultureInfo.InvariantCulture),
                result.SuccessfulRequests.ToString(CultureInfo.InvariantCulture),
                result.FailedRequests.ToString(CultureInfo.InvariantCulture),
                result.LostRequests.ToString(CultureInfo.InvariantCulture),
                result.SuccessRate.ToString("N2", CultureInfo.InvariantCulture) + "%",
                result.SuccessfulRequestsPerSecond.ToString("N2", CultureInfo.InvariantCulture),
                result.Started.ToString("O", CultureInfo.InvariantCulture),
                result.Ended.ToString("O", CultureInfo.InvariantCulture),
                result.IsSingleRequest ? "N/A" : result.ConfiguredRequestsPerSecond.ToString(CultureInfo.InvariantCulture),
                result.TestMode.ToString(),
                result.SendPhaseDuration.TotalSeconds.ToString("N6", CultureInfo.InvariantCulture),
                result.DrainPhaseDuration.TotalSeconds.ToString("N6", CultureInfo.InvariantCulture),
                result.ActualAchievedSendsPerSecond.ToString("N2", CultureInfo.InvariantCulture),
                result.MatchedResponses.ToString(CultureInfo.InvariantCulture),
                result.UnmatchedResponses.ToString(CultureInfo.InvariantCulture),
                result.DuplicateResponses.ToString(CultureInfo.InvariantCulture),
                result.TimedOutOutstandingRequests.ToString(CultureInfo.InvariantCulture),
                result.MaximumSchedulingLatenessMilliseconds.ToString("N3", CultureInfo.InvariantCulture),
                result.AverageSchedulingLatenessMilliseconds.ToString("N3", CultureInfo.InvariantCulture),
                result.MinimumObservedInterSendGapMicroseconds.ToString("N3", CultureInfo.InvariantCulture),
                result.MaximumObservedInterSendGapMilliseconds.ToString("N3", CultureInfo.InvariantCulture),
                result.SendsViolatingMinimumGap.ToString(CultureInfo.InvariantCulture),
                result.ScheduleRecoveryDurationMilliseconds.ToString("N3", CultureInfo.InvariantCulture),
                result.SenderBlockedByOutstandingWindowDurationMilliseconds.ToString("N3", CultureInfo.InvariantCulture),
                result.GracePeriodMilliseconds.ToString(CultureInfo.InvariantCulture),
                result.RequiredDrainPeriodMilliseconds.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> values)
    {
        csv.AppendLine(string.Join(',', values.Select(Escape)));
    }

    private static string Escape(string value)
    {
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }
}
