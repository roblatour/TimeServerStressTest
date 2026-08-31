using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TimeServerStressTest;

public sealed record NtpEndpoint(string Host, int Port)
{
    public const int DefaultPort = 123;

    public static bool TryParse(string? value, out NtpEndpoint? endpoint)
    {
        endpoint = null;

        if (!TryParseUri(value, out var uri))
        {
            return false;
        }

        var port = uri.IsDefaultPort || uri.Port == -1 ? DefaultPort : uri.Port;
        if (port is < 1 or > 65535)
        {
            return false;
        }

        endpoint = new NtpEndpoint(NormalizeHost(uri.Host), port);
        return true;
    }

    public static bool TryParse(string? value, int port, out NtpEndpoint? endpoint)
    {
        endpoint = null;

        if (port is < 1 or > 65535 || !TryParseUri(value, out var uri))
        {
            return false;
        }

        endpoint = new NtpEndpoint(NormalizeHost(uri.Host), port);
        return true;
    }

    private static bool TryParseUri(string? value, out Uri uri)
    {
        uri = null!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var input = value.Trim();
        if (IPAddress.TryParse(input.Trim('[', ']'), out var address))
        {
            var host = address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]" : address.ToString();
            uri = new Uri($"ntp://{host}");
            return true;
        }

        if (!input.Contains("://", StringComparison.Ordinal))
        {
            input = $"ntp://{input}";
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var parsedUri) && !string.IsNullOrWhiteSpace(parsedUri.Host))
        {
            uri = parsedUri;
            return true;
        }

        return false;
    }

    private static string NormalizeHost(string host)
    {
        var normalizedHost = host.Trim('[', ']');
        return IPAddress.TryParse(normalizedHost, out var address) && address.AddressFamily == AddressFamily.InterNetworkV6
            ? address.ToString().ToUpperInvariant()
            : normalizedHost;
    }
}

public sealed record StressSnapshot(long TotalRequests, long RequestsPerSecond, long SuccessfulRequests, long FailedRequests, TimeSpan Remaining, TimeSpan Elapsed = default);

public sealed class NtpStressRunner
{
    public const int DefaultConcurrentWorkers = 10;
    public const int MaximumConcurrentWorkers = 100;

    public async Task<StressSnapshot> RunAsync(NtpEndpoint endpoint, TimeSpan duration, int concurrentWorkers, long maximumRequests, IProgress<StressSnapshot> progress, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrentWorkers, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(concurrentWorkers, MaximumConcurrentWorkers);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRequests, 0);
        var effectiveWorkers = concurrentWorkers + 1;
        var addresses = IPAddress.TryParse(endpoint.Host, out var parsedAddress)
            ? [parsedAddress]
            : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
        if (address is null)
        {
            throw new InvalidOperationException("The Time Server Address could not be resolved.");
        }

        var serverEndpoint = new IPEndPoint(address, endpoint.Port);
        var startedAt = GetNextUtcSecond();
        await DelayUntilUtcAsync(startedAt, cancellationToken).ConfigureAwait(false);

        var endsAt = startedAt + duration;
        var stopwatch = Stopwatch.StartNew();
        long startedRequests = 0;
        long requestsPerSecond = 0;
        long successfulRequests = 0;
        long failedRequests = 0;

        using var durationCancellation = new CancellationTokenSource();
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCancellation.Token);
        var workers = Enumerable.Range(0, effectiveWorkers)
            .Select(_ => Task.Run(RunWorkerAsync))
            .ToArray();
        var monitor = ReportProgressAsync();
        var deadline = CancelAtAsync(endsAt);

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        finally
        {
            runCancellation.Cancel();
            await Task.WhenAll(monitor, deadline).ConfigureAwait(false);
            ReportProgress(endsAt - DateTime.UtcNow);
        }

        return CreateSnapshot(endsAt - DateTime.UtcNow);

        async Task RunWorkerAsync()
        {
            using var client = new UdpClient(serverEndpoint.AddressFamily);
            client.Connect(serverEndpoint);

            while (!runCancellation.IsCancellationRequested)
            {
                if (!TryStartRequest())
                {
                    break;
                }

                try
                {
                    var succeeded = await SendRequestAsync(client, runCancellation.Token).ConfigureAwait(false);
                    if (succeeded)
                    {
                        Interlocked.Increment(ref successfulRequests);
                    }
                    else
                    {
                        Interlocked.Increment(ref failedRequests);
                    }
                }
                catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        bool TryStartRequest()
        {
            if (maximumRequests == 0)
            {
                Interlocked.Increment(ref startedRequests);
                return true;
            }

            while (true)
            {
                var currentRequestCount = Interlocked.Read(ref startedRequests);
                if (currentRequestCount >= maximumRequests)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref startedRequests, currentRequestCount + 1, currentRequestCount) == currentRequestCount)
                {
                    return true;
                }
            }
        }

        async Task CancelAtAsync(DateTime endTime)
        {
            try
            {
                await DelayUntilUtcAsync(endTime, runCancellation.Token).ConfigureAwait(false);
                durationCancellation.Cancel();
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task ReportProgressAsync()
        {
            var previousRequestCount = 0L;

            try
            {
                while (!runCancellation.IsCancellationRequested)
                {
                    await DelayUntilNextUtcSecondAsync(runCancellation.Token).ConfigureAwait(false);
                    var requestCount = Interlocked.Read(ref startedRequests);
                    Interlocked.Exchange(ref requestsPerSecond, requestCount - previousRequestCount);
                    previousRequestCount = requestCount;
                    ReportProgress(endsAt - DateTime.UtcNow);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        void ReportProgress(TimeSpan remaining)
        {
            progress.Report(CreateSnapshot(remaining));
        }

        StressSnapshot CreateSnapshot(TimeSpan remaining)
        {
            var successes = Interlocked.Read(ref successfulRequests);
            var failures = Interlocked.Read(ref failedRequests);
            var completedRequests = successes + failures;
            return new StressSnapshot(
                completedRequests,
                Interlocked.Read(ref requestsPerSecond),
                successes,
                failures,
                remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                stopwatch.Elapsed);
        }
    }

    private static Task DelayUntilNextUtcSecondAsync(CancellationToken cancellationToken)
    {
        return DelayUntilUtcAsync(GetNextUtcSecond(), cancellationToken);
    }

    private static DateTime GetNextUtcSecond()
    {
        var now = DateTime.UtcNow;
        var ticksUntilNextSecond = TimeSpan.TicksPerSecond - now.Ticks % TimeSpan.TicksPerSecond;
        return now.AddTicks(ticksUntilNextSecond);
    }

    private static async Task DelayUntilUtcAsync(DateTime targetTime, CancellationToken cancellationToken)
    {
        while (true)
        {
            var remaining = targetTime - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> SendRequestAsync(UdpClient client, CancellationToken cancellationToken)
    {
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCancellation.CancelAfter(TimeSpan.FromSeconds(1));

        try
        {
            var request = new byte[48];
            request[0] = 0x23;

            await client.SendAsync(request, requestCancellation.Token).ConfigureAwait(false);
            var response = await client.ReceiveAsync(requestCancellation.Token).ConfigureAwait(false);
            return IsValidNtpServerResponse(response.Buffer);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    internal static bool IsValidNtpServerResponse(ReadOnlySpan<byte> response)
    {
        if (response.Length < 48)
        {
            return false;
        }

        var version = (response[0] >> 3) & 0x07;
        var mode = response[0] & 0x07;
        return version is >= 1 and <= 4 && mode == 4;
    }
}
