using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TimeServerStressTest.Tests;

[TestClass]
public sealed class NtpEndpointTests
{
    [TestMethod]
    [DataRow("192.168.1.10", "192.168.1.10", 123)]
    [DataRow("ntp://time.example.com", "time.example.com", 123)]
    [DataRow("https://time.example.com:9123/status", "time.example.com", 9123)]
    [DataRow("time.example.com:8123", "time.example.com", 8123)]
    [DataRow("2001:db8::1", "2001:DB8::1", 123)]
    [DataRow("[2001:db8::1]", "2001:DB8::1", 123)]
    public void TryParse_ValidAddress_ReturnsEndpoint(string value, string expectedHost, int expectedPort)
    {
        var parsed = NtpEndpoint.TryParse(value, out var endpoint);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(endpoint);
        Assert.AreEqual(expectedHost, endpoint.Host);
        Assert.AreEqual(expectedPort, endpoint.Port);
    }

    [TestMethod]
    public void TryParse_ExplicitPort_OverridesAddressPort()
    {
        var parsed = NtpEndpoint.TryParse("time.example.com:8123", 9123, out var endpoint);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(endpoint);
        Assert.AreEqual("time.example.com", endpoint.Host);
        Assert.AreEqual(9123, endpoint.Port);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("http://")]
    [DataRow("time.example.com:70000")]
    public void TryParse_InvalidAddress_ReturnsFalse(string value)
    {
        var parsed = NtpEndpoint.TryParse(value, out var endpoint);

        Assert.IsFalse(parsed);
        Assert.IsNull(endpoint);
    }
}

[TestClass]
public sealed class StressTestResultTests
{
    [TestMethod]
    public void CalculatedMetrics_UsesRunnerElapsedTimeAndRequestTotals()
    {
        var started = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Local);
        var result = new StressTestResult(4, 200, 150, 50, started, started.AddSeconds(20), TimeSpan.FromSeconds(10), StressTestStatus.Completed);

        Assert.AreEqual(TimeSpan.FromSeconds(10), result.Duration);
        Assert.AreEqual(20d, result.RequestsPerSecond);
        Assert.AreEqual(15d, result.SuccessfulRequestsPerSecond);
        Assert.AreEqual(5d, result.FailedRequestsPerSecond);
        Assert.AreEqual(75d, result.SuccessRate);
    }

    [TestMethod]
    public void CalculatedMetrics_ZeroDurationUsesFiniteRate()
    {
        var timestamp = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Local);
        var result = new StressTestResult(1, 1, 1, 0, timestamp, timestamp, TimeSpan.Zero, StressTestStatus.Stopped);

        Assert.AreEqual(TimeSpan.Zero, result.Duration);
        Assert.IsTrue(double.IsFinite(result.RequestsPerSecond));
        Assert.AreEqual(1000d, result.RequestsPerSecond);
    }
}

[TestClass]
public sealed class NtpStressRunnerTests
{
    [TestMethod]
    public void RequestSchedule_FiveThousandRequestsAtOneThousandPerSecond_SpansFiveSecondsWithoutBatches()
    {
        const long frequency = 1_000_000;
        var schedule = new NtpRequestSchedule(() => 0, 10_000, frequency, 1_000);
        var dueTimes = Enumerable.Range(0, 5_000).Select(requestNumber => schedule.GetDueTimestamp(requestNumber)).ToArray();

        Assert.AreEqual(10_000L, dueTimes[0]);
        Assert.AreEqual(10_000L + 4_999 * 1_000, dueTimes[^1]);
        Assert.IsTrue(dueTimes[^1] - dueTimes[0] >= frequency * 499 / 100);
        Assert.IsTrue(dueTimes.Zip(dueTimes.Skip(1)).All(pair => pair.Second - pair.First == 1_000));
    }

    [TestMethod]
    public void RequestSchedule_WhenOverdue_EmitsOnlyTheNextRequest()
    {
        long timestamp = 100_000;
        var schedule = new NtpRequestSchedule(() => timestamp, 0, 1_000, 100);

        Assert.IsTrue(schedule.TryTakeDueRequest(out var firstRequestNumber, out _));
        Assert.AreEqual(0L, firstRequestNumber);

        timestamp = 10_000;
        Assert.IsTrue(schedule.TryTakeDueRequest(out var overdueRequestNumber, out _));
        Assert.AreEqual(1L, overdueRequestNumber);
    }

    [TestMethod]
    public void GetRequestsForWorker_FiveSecondsAtOneThousandRequestsPerSecond_AssignsRemainderToOriginalWorker()
    {
        const int durationSeconds = 5;
        const int maximumRequestsPerSecond = 1000;
        const int concurrentWorkers = 20;
        const int workerCount = concurrentWorkers + 1;
        var requestsPerWorker = Enumerable.Range(0, workerCount)
            .Select(workerIndex => NtpStressRunner.GetRequestsForWorker(maximumRequestsPerSecond, workerCount, workerIndex) * durationSeconds)
            .ToArray();

        Assert.AreEqual(durationSeconds * maximumRequestsPerSecond, requestsPerWorker.Sum());
        Assert.AreEqual(300, requestsPerWorker[0]);
        Assert.AreEqual(235, requestsPerWorker[1]);
        Assert.AreEqual(60, Enumerable.Range(0, 20).Sum(interval => NtpStressRunner.GetRequestsForInterval(60, 20, interval)));
        Assert.AreEqual(47, Enumerable.Range(0, 20).Sum(interval => NtpStressRunner.GetRequestsForInterval(47, 20, interval)));
    }

    [TestMethod]
    [DataRow(10L, 3, 0, "0,3,6,9")]
    [DataRow(10L, 3, 1, "1,4,7")]
    [DataRow(10L, 3, 2, "2,5,8")]
    [DataRow(2L, 5, 4, "")]
    public void GetRequestNumbersForWorker_AssignsDeterministicRoundRobinSequence(long totalRequests, int workerCount, int workerIndex, string expectedRequestNumbers)
    {
        var requestNumbers = NtpStressRunner.GetRequestNumbersForWorker(totalRequests, workerCount, workerIndex).ToArray();
        var expected = string.IsNullOrEmpty(expectedRequestNumbers)
            ? Array.Empty<long>()
            : expectedRequestNumbers.Split(',').Select(long.Parse).ToArray();

        CollectionAssert.AreEqual(expected, requestNumbers);
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_SendsTheScheduledTotalWithoutFailures()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        const int maximumRequestsPerSecond = 100;
        using var receiveCancellation = new CancellationTokenSource();
        var responseTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var request = await server.ReceiveAsync(receiveCancellation.Token);
                    await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException) when (receiveCancellation.IsCancellationRequested)
            {
            }
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var stopwatch = Stopwatch.StartNew();
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            20,
            0,
            maximumRequestsPerSecond,
            new Progress<StressSnapshot>(),
            CancellationToken.None);
        stopwatch.Stop();

        receiveCancellation.Cancel();
        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(3));
        Assert.AreEqual(maximumRequestsPerSecond, snapshot.TotalRequests);
        Assert.AreEqual(snapshot.TotalRequests, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
        Assert.AreEqual(TimeSpan.FromSeconds(1), snapshot.SendPhaseDuration);
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_CountsDuplicateMatchingResponseOnce()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var responseTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            var response = CreateNtpServerResponse(request.Buffer);
            await server.SendAsync(response, request.RemoteEndPoint);
            await server.SendAsync(response, request.RemoteEndPoint);
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            0,
            0,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(1, snapshot.TotalRequests);
        Assert.AreEqual(1, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_CountsReplyReceivedDuringDrainPhase()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var responseTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            0,
            1,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(1, snapshot.MatchedResponses);
        Assert.AreEqual(1, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.TimedOutOutstandingRequests);
        Assert.IsTrue(snapshot.DrainPhaseDuration >= TimeSpan.FromMilliseconds(50));
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_CountsReplyReceivedAfterOneSecondDuringDrainPhase()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var responseTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(1_100));
            await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            0,
            1,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(1, snapshot.MatchedResponses);
        Assert.AreEqual(1, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.TimedOutOutstandingRequests);
        Assert.IsTrue(snapshot.DrainPhaseDuration >= TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_ReplyAfterGracePeriodIsFailure()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var responseTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
        });
        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);

        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            0,
            1,
            1,
            StressTestMode.Paced,
            new Progress<StressSnapshot>(),
            CancellationToken.None,
            100);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(0, snapshot.SuccessfulRequests);
        Assert.AreEqual(1, snapshot.FailedRequests);
        Assert.AreEqual(0, snapshot.LostRequests);
        Assert.AreEqual(100, snapshot.GracePeriodMilliseconds);
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_AtDrainTimeout_ClassifiesOutstandingRequestOnce()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var receiveTask = server.ReceiveAsync();
        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var stopwatch = Stopwatch.StartNew();
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            0,
            1,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await receiveTask.WaitAsync(TimeSpan.FromSeconds(1));
        stopwatch.Stop();

        Assert.AreEqual(1, snapshot.ActualSentRequestCount);
        Assert.AreEqual(0, snapshot.SuccessfulRequests);
        Assert.AreEqual(1, snapshot.TimedOutOutstandingRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
        Assert.AreEqual(1, snapshot.LostRequests);
        Assert.AreEqual(10_000, snapshot.RequiredDrainPeriodMilliseconds);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(400), stopwatch.Elapsed);
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_DoesNotCountUnknownOriginateTimestamp()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var responseTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(CreateNtpServerResponse(new byte[48]), request.RemoteEndPoint);
            await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            0,
            0,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(1, snapshot.TotalRequests);
        Assert.AreEqual(1, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
    }

    [TestMethod]
    public async Task RunAsync_ConcurrentRequests_StartsBaseAndRequestedConcurrentWorkers()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var receivedRequestsCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        const int concurrentRequests = 3;
        const int totalWorkers = concurrentRequests + 1;
        var receivedRequests = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var receiveTask = Task.Run(async () =>
        {
            for (var count = 0; count < totalWorkers; count++)
            {
                await server.ReceiveAsync(receivedRequestsCancellation.Token);
            }

            receivedRequests.SetResult();
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            concurrentRequests,
            0,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await receivedRequests.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await receiveTask;

        Assert.AreEqual(0, snapshot.TotalRequests);
        Assert.AreEqual(0, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
    }

    [TestMethod]
    public async Task RunAsync_OneConcurrentRequest_UsesBaseAndConcurrentSockets()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var remoteEndpoints = new List<IPEndPoint>();
        var responseTask = Task.Run(async () =>
        {
            for (var count = 0; count < 2; count++)
            {
                var request = await server.ReceiveAsync();
                remoteEndpoints.Add(request.RemoteEndPoint);
                await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
            }
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(2),
            1,
            2,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask;

        Assert.AreEqual(2, snapshot.SuccessfulRequests);
        Assert.AreNotEqual(remoteEndpoints[0], remoteEndpoints[1]);
    }

    [TestMethod]
    public async Task RunAsync_DoesNotCountCanceledOutstandingRequestsAsFailures()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var stopwatch = Stopwatch.StartNew();
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromMilliseconds(100),
            1,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);
        stopwatch.Stop();

        Assert.AreEqual(0, snapshot.TotalRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
        Assert.IsTrue(snapshot.Elapsed < TimeSpan.FromSeconds(1));
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task RunAsync_SingleRequestTimeoutCountsAsFailure()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var receiveTask = server.ReceiveAsync();
        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);

        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(2),
            1,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await receiveTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(1, snapshot.TotalRequests);
        Assert.AreEqual(0, snapshot.SuccessfulRequests);
        Assert.AreEqual(1, snapshot.FailedRequests);
    }

    [TestMethod]
    public async Task RunAsync_RejectsInvalidNtpResponse()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var responseTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(new byte[48], request.RemoteEndPoint);
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(2),
            1,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask;

        Assert.AreEqual(0, snapshot.SuccessfulRequests);
        Assert.AreEqual(1, snapshot.FailedRequests);
    }

    [TestMethod]
    public async Task RunAsync_ZeroWorkersUsesOneWorker()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var responseTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(2),
            0,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask;

        Assert.AreEqual(1, snapshot.TotalRequests);
        Assert.AreEqual(1, snapshot.SuccessfulRequests);
    }

    [TestMethod]
    public async Task RunAsync_AcceptsValidNtpResponse()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var responseTask = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(2),
            1,
            1,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask;

        Assert.AreEqual(1, snapshot.TotalRequests);
        Assert.AreEqual(1, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_FiveThousandAtOneThousandPerSecondCompletesNearFiveSeconds()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        const int requestCount = 5_000;
        var responseTask = Task.Run(async () =>
        {
            for (var count = 0; count < requestCount; count++)
            {
                var request = await server.ReceiveAsync();
                await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
            }
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(5),
            20,
            requestCount,
            1_000,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(requestCount, snapshot.ActualSentRequestCount);
        Assert.IsTrue(snapshot.Elapsed >= TimeSpan.FromSeconds(4.5));
        Assert.IsTrue(snapshot.Elapsed <= TimeSpan.FromSeconds(7));
        Assert.IsTrue(snapshot.ActualAchievedSendsPerSecond >= 990);
        Assert.IsTrue(snapshot.ActualAchievedSendsPerSecond <= 1_000);
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_SpreadsSendsInsteadOfClusteringThemIntoIntervals()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        const int requestsPerSecond = 200;
        var receivedAt = new List<long>();
        var responseTask = Task.Run(async () =>
        {
            for (var count = 0; count < requestsPerSecond; count++)
            {
                var request = await server.ReceiveAsync();
                receivedAt.Add(Stopwatch.GetTimestamp());
                await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
            }
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            3,
            requestsPerSecond,
            requestsPerSecond,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        var tenMillisecondTicks = Stopwatch.Frequency / 100;
        var maximumRequestsInTenMilliseconds = receivedAt
            .Select(timestamp => receivedAt.Count(other => other >= timestamp && other < timestamp + tenMillisecondTicks))
            .Max();

        Assert.AreEqual(requestsPerSecond, snapshot.ActualSentRequestCount);
        Assert.IsTrue(receivedAt[^1] - receivedAt[0] >= Stopwatch.Frequency * 8 / 10);
        Assert.IsTrue(maximumRequestsInTenMilliseconds < 6);
    }

    [TestMethod]
    public void RequestSchedule_AfterSchedulingDelay_DelaysNextPacedRequestByOneInterval()
    {
        const long frequency = 1_000_000;
        var schedule = new NtpRequestSchedule(() => 0, 0, frequency, 100);

        var nextDueTimestamp = schedule.GetPacedDueTimestamp(2, 900_000);

        Assert.AreEqual(910_000L, nextDueTimestamp);
    }

    [TestMethod]
    public async Task RunAsync_RateLimitedRequests_CorrelatesRepliesAcrossRoundRobinSockets()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        const int concurrentWorkers = 3;
        const int requestCount = 40;
        var remoteEndpoints = new HashSet<IPEndPoint>();
        var responseTask = Task.Run(async () =>
        {
            for (var count = 0; count < requestCount; count++)
            {
                var request = await server.ReceiveAsync();
                remoteEndpoints.Add(request.RemoteEndPoint);
                await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
            }
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            concurrentWorkers,
            requestCount,
            requestCount,
            new Progress<StressSnapshot>(),
            CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(requestCount, snapshot.ActualSentRequestCount);
        Assert.AreEqual(requestCount, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
        Assert.AreEqual(concurrentWorkers + 1, remoteEndpoints.Count);
    }

    [TestMethod]
    public void RequestSchedule_AfterSchedulerPause_PreservesAbsoluteDueTimes()
    {
        const long frequency = 1_000_000;
        const int requestsPerSecond = 1_000;
        long timestamp = 0;
        var schedule = new NtpRequestSchedule(() => timestamp, 0, frequency, requestsPerSecond);

        Assert.AreEqual(1_000L, schedule.GetDueTimestamp(1));

        timestamp = 900_000;

        Assert.AreEqual(901_000L, schedule.GetDueTimestamp(901));
        Assert.AreEqual(4_999_000L, schedule.GetDueTimestamp(4_999));
    }

    [TestMethod]
    public async Task RunAsync_SaturationMode_IsReportedAndCorrelatesReplies()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        const int requestCount = 100;
        var responseTask = Task.Run(async () =>
        {
            for (var count = 0; count < requestCount; count++)
            {
                var request = await server.ReceiveAsync();
                await server.SendAsync(CreateNtpServerResponse(request.Buffer), request.RemoteEndPoint);
            }
        });
        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);

        var snapshot = await new NtpStressRunner().RunAsync(endpoint, TimeSpan.FromSeconds(1), 0, 0, requestCount, StressTestMode.Saturation, new Progress<StressSnapshot>(), CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(StressTestMode.Saturation, snapshot.TestMode);
        Assert.AreEqual(requestCount, snapshot.ActualSentRequestCount);
        Assert.AreEqual(requestCount, snapshot.SuccessfulRequests);
        Assert.AreEqual(0, snapshot.FailedRequests);
    }

    private static byte[] CreateNtpServerResponse(ReadOnlySpan<byte> request)
    {
        var response = new byte[48];
        response[0] = 0x24;
        request.Slice(40, 8).CopyTo(response.AsSpan(24, 8));
        return response;
    }
}

[TestClass]
public sealed class NtpFunctionalTests
{
    private const string Secret = "5630344f527233585a77537a4f30364d55654f62656241654a33596e3071796e";

    [TestMethod]
    public void KeyFile_ValidMultipleKeys_DecodesHexSecret()
    {
        Assert.IsTrue(NtpKeyFile.TryParse($"1 SHA256 {Secret}\r\n\r\n65535 SHA256 {Secret}\n", out var keys));
        Assert.HasCount(2, keys);
        Assert.AreEqual(1, keys[0].Id);
        Assert.AreEqual("V04ORr3XZwSzO06MUeObebAeJ3Yn0qyn", System.Text.Encoding.ASCII.GetString(keys[0].Value));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0 SHA256 5630344f527233585a77537a4f30364d55654f62656241654a33596e3071796e")]
    [DataRow("65536 SHA256 5630344f527233585a77537a4f30364d55654f62656241654a33596e3071796e")]
    [DataRow(" 1 SHA256 5630344f527233585a77537a4f30364d55654f62656241654a33596e3071796e")]
    [DataRow("1  SHA256 5630344f527233585a77537a4f30364d55654f62656241654a33596e3071796e")]
    [DataRow("1 SHA1 5630344f527233585a77537a4f30364d55654f62656241654a33596e3071796e")]
    public void KeyFile_InvalidInput_IsRejected(string content)
    {
        Assert.IsFalse(NtpKeyFile.TryParse(content, out _));
    }

    [TestMethod]
    public void AuthenticatedResponse_MustMatchIdAndDigest()
    {
        NtpKeyFile.TryParse($"1 SHA256 {Secret}", out var keys);
        var request = NtpFunctionalRunner.CreateRequest(4);
        var response = new byte[48];
        response[0] = 0x24;
        response[1] = 1;
        request.AsSpan(40, 8).CopyTo(response.AsSpan(24, 8));
        request.AsSpan(40, 8).CopyTo(response.AsSpan(40, 8));
        var signed = NtpFunctionalRunner.ApplyAuthentication(response, keys[0]);
        Assert.IsTrue(NtpFunctionalRunner.ValidResponse(signed, request, keys[0], true));
        signed[^1] ^= 1;
        Assert.IsFalse(NtpFunctionalRunner.ValidResponse(signed, request, keys[0], true));
    }

    [TestMethod]
    public void InterleavedRequest_UsesPreviousExchangeTimestamps()
    {
        var first = NtpFunctionalRunner.CreateRequest(4);
        var reply = new byte[48];
        first.AsSpan(40, 8).CopyTo(reply.AsSpan(24, 8));
        first.AsSpan(40, 8).CopyTo(reply.AsSpan(32, 8));
        first.AsSpan(40, 8).CopyTo(reply.AsSpan(40, 8));
        var second = NtpFunctionalRunner.CreateInterleavedRequest(first, reply);

        Assert.IsTrue(second.AsSpan(24, 8).SequenceEqual(reply.AsSpan(32, 8)));
        Assert.IsTrue(second.AsSpan(40, 8).SequenceEqual(first.AsSpan(40, 8)));
        Assert.IsFalse(second.AsSpan(32, 8).SequenceEqual(new byte[8]));
        var interleavedReply = new byte[48];
        interleavedReply[0] = 0x24;
        interleavedReply[1] = 1;
        second.AsSpan(32, 8).CopyTo(interleavedReply.AsSpan(24, 8));
        reply.AsSpan(40, 8).CopyTo(interleavedReply.AsSpan(40, 8));
        Assert.IsTrue(NtpFunctionalRunner.ValidResponse(interleavedReply, second, null, false, reply));
        interleavedReply[40] ^= 1;
        Assert.IsFalse(NtpFunctionalRunner.ValidResponse(interleavedReply, second, null, false, reply));
    }

    [TestMethod]
    [DataRow("192.168.1.10", AddressFamily.InterNetwork, true)]
    [DataRow("169.254.1.1", AddressFamily.InterNetwork, false)]
    [DataRow("127.0.0.1", AddressFamily.InterNetwork, false)]
    [DataRow("2001:db8::1", AddressFamily.InterNetworkV6, true)]
    [DataRow("fd00::1", AddressFamily.InterNetworkV6, true)]
    [DataRow("fe80::1", AddressFamily.InterNetworkV6, false)]
    [DataRow("::1", AddressFamily.InterNetworkV6, false)]
    public void UsableAddress_RequiresRoutableAddressOfRequestedFamily(string value, AddressFamily family, bool expected)
    {
        Assert.AreEqual(expected, NtpFunctionalRunner.HasUsableAddress([IPAddress.Parse(value)], family));
    }

    [TestMethod]
    [DataRow(true, false, 9, "IPv4", "IPv4")]
    [DataRow(false, true, 9, "IPv6", "IPv6")]
    [DataRow(true, true, 18, "IPv4", "IPv6")]
    public void SelectedTestNames_IncludeOnlyConnectedFamilies(bool ipv4, bool ipv6, int count, string firstFamily, string lastFamily)
    {
        var families = new List<AddressFamily>();
        if (ipv4)
        {
            families.Add(AddressFamily.InterNetwork);
        }

        if (ipv6)
        {
            families.Add(AddressFamily.InterNetworkV6);
        }

        var names = NtpFunctionalRunner.GetTestNames(families);
        Assert.HasCount(count, names);
        StringAssert.StartsWith(names[0], firstFamily);
        StringAssert.StartsWith(names[^1], lastFamily);
    }

    [TestMethod]
    public void FunctionalPdf_ContainsResultTableButNotStressChart()
    {
        var path = Path.GetTempFileName();
        try
        {
            var when = new DateTime(2026, 9, 27);
            PdfReportExporter.SaveFunctional(path,
                [new FunctionalTestResult("IPv4 NTPv3 Standard", FunctionalTestStatus.Pass), new FunctionalTestResult("IPv4 NTPv4 Standard", FunctionalTestStatus.Fail)],
                new NtpEndpoint("localhost", 123), when, when.AddSeconds(1), "Test note");
            var pdf = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path));
            StringAssert.Contains(pdf, "NTP Functional Test Results");
            StringAssert.Contains(pdf, "IPv4 NTPv4 Standard");
            StringAssert.Contains(pdf, "36 510 Td (1)");
            StringAssert.Contains(pdf, "36 490 Td (2)");
            StringAssert.Contains(pdf, "Time Server Port: 123");
            Assert.IsFalse(pdf.Contains("/Im0", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
