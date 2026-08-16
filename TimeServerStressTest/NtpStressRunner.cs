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

        endpoint = new NtpEndpoint(uri.Host, port);
        return true;
    }

    public static bool TryParse(string? value, int port, out NtpEndpoint? endpoint)
    {
        endpoint = null;

        if (port is < 1 or > 65535 || !TryParseUri(value, out var uri))
        {
            return false;
        }

        endpoint = new NtpEndpoint(uri.Host, port);
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
}

public sealed record StressSnapshot(long TotalRequests, long RequestsPerSecond, long SuccessfulRequests, long FailedRequests, TimeSpan Remaining, TimeSpan Elapsed = default);

public sealed class NtpStressRunner
{
    public const int DefaultConcurrentWorkers = 10;
    public const int MaximumConcurrentWorkers = 100;

    public async Task<StressSnapshot> RunAsync(NtpEndpoint endpoint, TimeSpan duration, int concurrentWorkers, long maximumRequests, IProgress<StressSnapshot> progress, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrentWorkers, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(concurrentWorkers, MaximumConcurrentWorkers);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRequests, 0);
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
        if (address is null)
        {
            throw new InvalidOperationException("The Time Server Address could not be resolved.");
        }

        var serverEndpoint = new IPEndPoint(address, endpoint.Port);
        var stopwatch = Stopwatch.StartNew();
        long startedRequests = 0;
        long successfulRequests = 0;
        long failedRequests = 0;

        using var durationCancellation = new CancellationTokenSource(duration);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCancellation.Token);
        var startWorkers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workers = Enumerable.Range(0, concurrentWorkers)
            .Select(_ => Task.Run(RunWorkerAsync))
            .ToArray();
        var monitor = ReportProgressAsync();
        startWorkers.SetResult();

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        finally
        {
            runCancellation.Cancel();
            await monitor.ConfigureAwait(false);
            ReportProgress(duration - stopwatch.Elapsed);
        }

        return CreateSnapshot(duration - stopwatch.Elapsed);

        async Task RunWorkerAsync()
        {
            await startWorkers.Task.ConfigureAwait(false);

            using var client = new UdpClient(serverEndpoint.AddressFamily);
            client.Connect(serverEndpoint);

            while (!runCancellation.IsCancellationRequested)
            {
                var requestNumber = Interlocked.Increment(ref startedRequests);
                if (maximumRequests > 0 && requestNumber > maximumRequests)
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

        async Task ReportProgressAsync()
        {
            try
            {
                while (!runCancellation.IsCancellationRequested)
                {
                    ReportProgress(duration - stopwatch.Elapsed);
                    await Task.Delay(250, runCancellation.Token).ConfigureAwait(false);
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
            var elapsed = stopwatch.Elapsed;
            var elapsedSeconds = Math.Max(elapsed.TotalSeconds, 0.001);
            var successes = Interlocked.Read(ref successfulRequests);
            var failures = Interlocked.Read(ref failedRequests);
            var completedRequests = successes + failures;
            return new StressSnapshot(
                completedRequests,
                (long)Math.Round(completedRequests / elapsedSeconds),
                successes,
                failures,
                remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                elapsed);
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
