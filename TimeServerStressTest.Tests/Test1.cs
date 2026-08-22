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
    public async Task RunAsync_MultipleWorkers_StartsOneRequestPerWorkerConcurrently()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var receivedRequestsCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        const int workers = 4;
        var receivedRequests = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var receiveTask = Task.Run(async () =>
        {
            for (var count = 0; count < workers; count++)
            {
                await server.ReceiveAsync(receivedRequestsCancellation.Token);
            }

            receivedRequests.SetResult();
        });

        var endpoint = new NtpEndpoint(IPAddress.Loopback.ToString(), ((IPEndPoint)server.Client.LocalEndPoint!).Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromMilliseconds(100),
            workers,
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
    public async Task RunAsync_UsesOneSocketPerWorker()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var remoteEndpoints = new List<IPEndPoint>();
        var responseTask = Task.Run(async () =>
        {
            for (var count = 0; count < 2; count++)
            {
                var request = await server.ReceiveAsync();
                remoteEndpoints.Add(request.RemoteEndPoint);
                await server.SendAsync(CreateNtpServerResponse(), request.RemoteEndPoint);
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
        Assert.AreEqual(remoteEndpoints[0], remoteEndpoints[1]);
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
            await server.SendAsync(CreateNtpServerResponse(), request.RemoteEndPoint);
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
            await server.SendAsync(CreateNtpServerResponse(), request.RemoteEndPoint);
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

    private static byte[] CreateNtpServerResponse()
    {
        var response = new byte[48];
        response[0] = 0x24;
        return response;
    }
}
