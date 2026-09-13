using System.Buffers.Binary;
using System.Collections.Concurrent;
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

public enum StressTestMode
{
    Paced,
    Saturation
}

public sealed record StressSnapshot(
    long TotalRequests,
    long RequestsPerSecond,
    long SuccessfulRequests,
    long FailedRequests,
    TimeSpan Remaining,
    TimeSpan Elapsed = default,
    long ActualSentRequestCount = 0,
    double ActualAchievedSendsPerSecond = 0,
    double MaximumSchedulingLatenessMilliseconds = 0,
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
    bool IsDraining = false,
    long LostRequests = 0,
    int GracePeriodMilliseconds = 0,
    int RequiredDrainPeriodMilliseconds = 0);

public sealed class NtpRequestSchedule
{
    private readonly Func<long> getTimestamp;
    private readonly long startTimestamp;
    private readonly long frequency;
    private readonly int requestsPerSecond;
    private long nextRequestNumber;

    public NtpRequestSchedule(Func<long> getTimestamp, long startTimestamp, long frequency, int requestsPerSecond)
    {
        this.getTimestamp = getTimestamp;
        this.startTimestamp = startTimestamp;
        this.frequency = frequency;
        this.requestsPerSecond = requestsPerSecond;
    }

    public long GetDueTimestamp(long requestNumber) => startTimestamp + requestNumber * frequency / requestsPerSecond;

    public long GetPacedDueTimestamp(long requestNumber, long previousSendTimestamp)
    {
        var dueTimestamp = GetDueTimestamp(requestNumber);
        return previousSendTimestamp < 0
            ? dueTimestamp
            : Math.Max(dueTimestamp, previousSendTimestamp + frequency / requestsPerSecond);
    }

    public bool TryTakeDueRequest(out long requestNumber, out long dueTimestamp)
    {
        requestNumber = nextRequestNumber;
        dueTimestamp = GetDueTimestamp(requestNumber);
        if (getTimestamp() < dueTimestamp)
        {
            return false;
        }

        nextRequestNumber++;
        return true;
    }
}

public sealed class NtpStressRunner
{
    public const int DefaultConcurrentWorkers = 10;
    public const int MaximumConcurrentWorkers = 100;

    public Task<StressSnapshot> RunAsync(NtpEndpoint endpoint, TimeSpan duration, int concurrentWorkers, long maximumRequests, IProgress<StressSnapshot> progress, CancellationToken cancellationToken)
    {
        return RunCoreAsync(endpoint, duration, concurrentWorkers, maximumRequests, 1, StressTestMode.Paced, progress, cancellationToken, completeOutstandingRequests: false, distributeRequestsAcrossWorkers: false);
    }

    public Task<StressSnapshot> RunAsync(NtpEndpoint endpoint, TimeSpan duration, int concurrentWorkers, long maximumRequests, int maximumRequestsPerSecond, IProgress<StressSnapshot> progress, CancellationToken cancellationToken)
    {
        return RunAsync(endpoint, duration, concurrentWorkers, maximumRequests, maximumRequestsPerSecond, StressTestMode.Paced, progress, cancellationToken);
    }

    public Task<StressSnapshot> RunAsync(NtpEndpoint endpoint, TimeSpan duration, int concurrentWorkers, long maximumRequests, int maximumRequestsPerSecond, StressTestMode testMode, IProgress<StressSnapshot> progress, CancellationToken cancellationToken, int gracePeriodMilliseconds = 3_000, int requiredDrainPeriodMilliseconds = 10_000)
    {
        return RunCoreAsync(endpoint, duration, concurrentWorkers, maximumRequests, maximumRequestsPerSecond, testMode, progress, cancellationToken, completeOutstandingRequests: true, distributeRequestsAcrossWorkers: true, gracePeriodMilliseconds, requiredDrainPeriodMilliseconds);
    }

    private async Task<StressSnapshot> RunCoreAsync(NtpEndpoint endpoint, TimeSpan duration, int concurrentWorkers, long maximumRequests, int maximumRequestsPerSecond, StressTestMode testMode, IProgress<StressSnapshot> progress, CancellationToken cancellationToken, bool completeOutstandingRequests, bool distributeRequestsAcrossWorkers, int gracePeriodMilliseconds = 3_000, int requiredDrainPeriodMilliseconds = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(concurrentWorkers, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(concurrentWorkers, MaximumConcurrentWorkers);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRequests, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRequestsPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumRequestsPerSecond, 50000);
        ArgumentOutOfRangeException.ThrowIfLessThan(gracePeriodMilliseconds, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(requiredDrainPeriodMilliseconds, 0);
        var effectiveWorkers = concurrentWorkers + 1;
        var addresses = IPAddress.TryParse(endpoint.Host, out var parsedAddress)
            ? [parsedAddress]
            : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
        if (address is null)
        {
            throw new InvalidOperationException("The Time Server Address could not be resolved.");
        }

        if (distributeRequestsAcrossWorkers)
        {
            return await RunRateLimitedAsync(new IPEndPoint(address, endpoint.Port), duration, effectiveWorkers, maximumRequests, maximumRequestsPerSecond, testMode, progress, cancellationToken, gracePeriodMilliseconds, requiredDrainPeriodMilliseconds).ConfigureAwait(false);
        }

        var serverEndpoint = new IPEndPoint(address, endpoint.Port);
        var startedAt = DateTime.UtcNow;
        var endsAt = startedAt + duration;
        var stopwatch = Stopwatch.StartNew();
        long startedRequests = 0;
        long requestsPerSecond = 0;
        long successfulRequests = 0;
        long failedRequests = 0;

        using var durationCancellation = new CancellationTokenSource();
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, durationCancellation.Token);
        var requestTasks = new System.Collections.Concurrent.ConcurrentBag<Task>();
        var workers = Enumerable.Range(0, effectiveWorkers)
            .Select(workerIndex => Task.Run(() => RunWorkerAsync(workerIndex)))
            .ToArray();
        var monitor = ReportProgressAsync();
        var deadline = CancelAtAsync(endsAt);

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
            await Task.WhenAll(requestTasks).ConfigureAwait(false);
        }
        finally
        {
            runCancellation.Cancel();
            await Task.WhenAll(monitor, deadline).ConfigureAwait(false);
            ReportProgress(endsAt - DateTime.UtcNow);
        }

        return CreateSnapshot(endsAt - DateTime.UtcNow);

        async Task RunWorkerAsync(int workerIndex)
        {
            var nextBatchAt = startedAt;
            var batchNumber = 0;

            while (!runCancellation.IsCancellationRequested && nextBatchAt < endsAt)
            {
                try
                {
                    await DelayUntilUtcAsync(nextBatchAt, runCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
                {
                    break;
                }

                var requestsForWorker = distributeRequestsAcrossWorkers
                    ? GetRequestsForWorker(maximumRequestsPerSecond, effectiveWorkers, workerIndex)
                    : maximumRequestsPerSecond;
                for (var request = 0; request < requestsForWorker; request++)
                {
                    if (runCancellation.IsCancellationRequested || !TryStartRequest())
                    {
                        return;
                    }

                    requestTasks.Add(RunRequestAsync());
                }

                nextBatchAt = nextBatchAt.AddSeconds(1);
                batchNumber++;
            }
        }

        async Task RunRequestAsync()
        {
            try
            {
                using var client = new UdpClient(serverEndpoint.AddressFamily);
                client.Connect(serverEndpoint);
                var cancellation = completeOutstandingRequests ? CancellationToken.None : runCancellation.Token;
                var succeeded = await SendRequestAsync(client, cancellation).ConfigureAwait(false);
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
                TimeSpan.FromTicks(Math.Min(stopwatch.Elapsed.Ticks, duration.Ticks)));
        }
    }

    private async Task<StressSnapshot> RunRateLimitedAsync(IPEndPoint serverEndpoint, TimeSpan duration, int workerCount, long maximumRequests, int maximumRequestsPerSecond, StressTestMode testMode, IProgress<StressSnapshot> progress, CancellationToken cancellationToken, int gracePeriodMilliseconds, int requiredDrainPeriodMilliseconds)
    {
        var totalRequests = maximumRequests == 0
            ? checked(duration.Ticks * maximumRequestsPerSecond / TimeSpan.TicksPerSecond)
            : maximumRequests;
        long sentRequests = 0;
        long requestsPerSecond = 0;
        long successfulRequests = 0;
        long failedRequests = 0;
        long nextRequestIdentifier = 0;
        long unmatchedResponses = 0;
        long duplicateResponses = 0;
        long totalSchedulingLatenessStopwatchTicks = 0;
        long maximumSchedulingLatenessStopwatchTicks = 0;
        long minimumInterSendGapStopwatchTicks = long.MaxValue;
        long maximumInterSendGapStopwatchTicks = 0;
        long sendsViolatingMinimumGap = 0;
        long sendPhaseEndTimestamp = 0;
        long drainPhaseEndTimestamp = 0;
        long lastGlobalSendTimestamp = -1;
        long finalized = 0;
        var outstandingRequests = Enumerable.Range(0, workerCount)
            .Select(_ => new ConcurrentDictionary<ulong, long>())
            .ToArray();
        var matchedRequestIdentifiers = new ConcurrentDictionary<ulong, byte>();
        var clients = Enumerable.Range(0, workerCount)
            .Select(_ => CreateClient())
            .ToArray();
        foreach (var client in clients)
        {
            client.Connect(serverEndpoint);
        }

        var startTimestamp = Stopwatch.GetTimestamp();
        using var completionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receivers = clients.Select((client, index) => ReceiveResponsesAsync(client, outstandingRequests[index])).ToArray();
        var monitor = ReportProgressAsync();

        UdpClient CreateClient()
        {
            var client = new UdpClient(serverEndpoint.AddressFamily);
            try
            {
                client.Client.ReceiveBufferSize = 4 * 1024 * 1024;
            }
            catch (SocketException)
            {
            }

            return client;
        }

        try
        {
            await Task.WhenAll(Enumerable.Range(0, workerCount)
                .Select(workerIndex => Task.Run(() => ScheduleRequestsAsync(workerIndex))))
                .ConfigureAwait(false);
            var sendCompletedTimestamp = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref sendPhaseEndTimestamp, sendCompletedTimestamp);
            ReportProgress(TimeSpan.Zero);
            var requiredDrainPeriodStopwatchTicks = requiredDrainPeriodMilliseconds * Stopwatch.Frequency / 1_000;
            var drainDeadlineTimestamp = sendCompletedTimestamp + requiredDrainPeriodStopwatchTicks;
            while (outstandingRequests.Any(outstanding => !outstanding.IsEmpty) && Stopwatch.GetTimestamp() < drainDeadlineTimestamp)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken).ConfigureAwait(false);
            }

            Interlocked.Exchange(ref finalized, 1);
            var lostRequests = outstandingRequests.Sum(outstanding => outstanding.Count);
            foreach (var outstanding in outstandingRequests)
            {
                outstanding.Clear();
            }
            Interlocked.Exchange(ref drainPhaseEndTimestamp, Stopwatch.GetTimestamp());
            completionCancellation.Cancel();
            await Task.WhenAll(receivers).ConfigureAwait(false);
            ReportProgress(TimeSpan.Zero);
            return CreateSnapshot(TimeSpan.Zero, lostRequests);
        }
        finally
        {
            completionCancellation.Cancel();
            foreach (var client in clients)
            {
                client.Dispose();
            }

            monitorCancellation.Cancel();
            await Task.WhenAll(monitor, Task.WhenAll(receivers)).ConfigureAwait(false);
            foreach (var outstanding in outstandingRequests)
            {
                outstanding.Clear();
            }
        }

        async Task ScheduleRequestsAsync(int workerIndex)
        {
            var schedule = new NtpRequestSchedule(Stopwatch.GetTimestamp, startTimestamp, Stopwatch.Frequency, maximumRequestsPerSecond);
            var sendWindowEndTimestamp = startTimestamp + duration.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond;
            foreach (var requestNumber in GetRequestNumbersForWorker(totalRequests, workerCount, workerIndex))
            {
                var sendTiming = await WaitForSendTimestampAsync(requestNumber).ConfigureAwait(false);
                if (sendTiming is null)
                {
                    break;
                }

                var (scheduledTimestamp, actualTimestamp, previousSendTimestamp) = sendTiming.Value;
                if (maximumRequests == 0 && actualTimestamp >= sendWindowEndTimestamp)
                {
                    break;
                }

                var latenessStopwatchTicks = Math.Max(0, actualTimestamp - scheduledTimestamp);
                UpdateMaximum(ref maximumSchedulingLatenessStopwatchTicks, latenessStopwatchTicks);
                Interlocked.Add(ref totalSchedulingLatenessStopwatchTicks, latenessStopwatchTicks);

                if (previousSendTimestamp >= 0)
                {
                    var interSendGapStopwatchTicks = Math.Max(0, actualTimestamp - previousSendTimestamp);
                    UpdateMinimum(ref minimumInterSendGapStopwatchTicks, interSendGapStopwatchTicks);
                    UpdateMaximum(ref maximumInterSendGapStopwatchTicks, interSendGapStopwatchTicks);
                    if (testMode == StressTestMode.Paced && interSendGapStopwatchTicks < Stopwatch.Frequency / maximumRequestsPerSecond)
                    {
                        Interlocked.Increment(ref sendsViolatingMinimumGap);
                    }
                }

                var requestIdentifier = unchecked((ulong)Interlocked.Increment(ref nextRequestIdentifier));
                if (requestIdentifier == 0)
                {
                    throw new InvalidOperationException("NTP request identifiers have been exhausted.");
                }

                var request = new byte[48];
                request[0] = 0x23;
                BinaryPrimitives.WriteUInt64BigEndian(request.AsSpan(40, sizeof(ulong)), requestIdentifier);
                outstandingRequests[workerIndex].TryAdd(requestIdentifier, actualTimestamp);

                try
                {
                    await clients[workerIndex].SendAsync(request, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref sentRequests);
                }
                catch (SocketException)
                {
                    outstandingRequests[workerIndex].TryRemove(requestIdentifier, out _);
                }
            }

            async Task<(long ScheduledTimestamp, long ActualTimestamp, long PreviousSendTimestamp)?> WaitForSendTimestampAsync(long requestNumber)
            {
                if (testMode == StressTestMode.Saturation)
                {
                    var saturationDueTimestamp = schedule.GetDueTimestamp(requestNumber);
                    if (maximumRequests == 0 && saturationDueTimestamp >= sendWindowEndTimestamp)
                    {
                        return null;
                    }

                    await DelayUntilStopwatchTimestampAsync(saturationDueTimestamp, cancellationToken).ConfigureAwait(false);
                    var actualTimestamp = Stopwatch.GetTimestamp();
                    if (maximumRequests == 0 && actualTimestamp >= sendWindowEndTimestamp)
                    {
                        return null;
                    }

                    var previousSendTimestamp = Interlocked.Exchange(ref lastGlobalSendTimestamp, actualTimestamp);
                    return (saturationDueTimestamp, actualTimestamp, previousSendTimestamp);
                }

                while (true)
                {
                    var previousSendTimestamp = Interlocked.Read(ref lastGlobalSendTimestamp);
                    var pacedDueTimestamp = schedule.GetPacedDueTimestamp(requestNumber, previousSendTimestamp);
                    if (maximumRequests == 0 && pacedDueTimestamp >= sendWindowEndTimestamp)
                    {
                        return null;
                    }

                    await DelayUntilStopwatchTimestampAsync(pacedDueTimestamp, cancellationToken).ConfigureAwait(false);
                    var actualTimestamp = Stopwatch.GetTimestamp();
                    if (maximumRequests == 0 && actualTimestamp >= sendWindowEndTimestamp)
                    {
                        return null;
                    }

                    if (Interlocked.CompareExchange(ref lastGlobalSendTimestamp, actualTimestamp, previousSendTimestamp) == previousSendTimestamp)
                    {
                        return (pacedDueTimestamp, actualTimestamp, previousSendTimestamp);
                    }
                }
            }
        }

        async Task ReceiveResponsesAsync(UdpClient client, ConcurrentDictionary<ulong, long> outstanding)
        {
            try
            {
                while (!completionCancellation.IsCancellationRequested)
                {
                    var response = await client.ReceiveAsync(completionCancellation.Token).ConfigureAwait(false);
                    var responseTimestamp = Stopwatch.GetTimestamp();
                    if (IsValidNtpServerResponse(response.Buffer))
                    {
                        var requestIdentifier = BinaryPrimitives.ReadUInt64BigEndian(response.Buffer.AsSpan(24, sizeof(ulong)));
                        if (Volatile.Read(ref finalized) == 0 && outstanding.TryRemove(requestIdentifier, out var sentTimestamp))
                        {
                            matchedRequestIdentifiers.TryAdd(requestIdentifier, 0);
                            if (responseTimestamp - sentTimestamp <= gracePeriodMilliseconds * Stopwatch.Frequency / 1_000)
                            {
                                Interlocked.Increment(ref successfulRequests);
                            }
                            else
                            {
                                Interlocked.Increment(ref failedRequests);
                            }
                        }
                        else if (matchedRequestIdentifiers.ContainsKey(requestIdentifier))
                        {
                            Interlocked.Increment(ref duplicateResponses);
                        }
                        else
                        {
                            Interlocked.Increment(ref unmatchedResponses);
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref unmatchedResponses);
                    }
                }
            }
            catch (OperationCanceledException) when (completionCancellation.IsCancellationRequested)
            {
            }
            catch (SocketException)
            {
            }
        }

        async Task ReportProgressAsync()
        {
            var previousRequestCount = 0L;

            try
            {
                while (!monitorCancellation.IsCancellationRequested)
                {
                    await DelayUntilNextUtcSecondAsync(monitorCancellation.Token).ConfigureAwait(false);
                    var requestCount = Interlocked.Read(ref sentRequests);
                    Interlocked.Exchange(ref requestsPerSecond, requestCount - previousRequestCount);
                    previousRequestCount = requestCount;
                    var elapsedStopwatchTicks = Stopwatch.GetTimestamp() - startTimestamp;
                    var remainingStopwatchTicks = Math.Max(0, duration.Ticks * Stopwatch.Frequency / TimeSpan.TicksPerSecond - elapsedStopwatchTicks);
                    ReportProgress(TimeSpan.FromSeconds((double)remainingStopwatchTicks / Stopwatch.Frequency));
                }
            }
            catch (OperationCanceledException) when (monitorCancellation.IsCancellationRequested)
            {
            }
        }

        void ReportProgress(TimeSpan remaining)
        {
            progress.Report(CreateSnapshot(remaining));
        }

        StressSnapshot CreateSnapshot(TimeSpan remaining, long lostRequests = 0)
        {
            var successes = Interlocked.Read(ref successfulRequests);
            var failures = Interlocked.Read(ref failedRequests);
            var sent = Interlocked.Read(ref sentRequests);
            var sendPhaseTimestamp = Interlocked.Read(ref sendPhaseEndTimestamp);
            var scheduledSendWindowEndTimestamp = startTimestamp + sent * Stopwatch.Frequency / maximumRequestsPerSecond;
            var sendPhaseDuration = sendPhaseTimestamp == 0
                ? TimeSpan.FromSeconds(Math.Min(duration.TotalSeconds, Math.Max(0, (double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency)))
                : maximumRequests == 0
                    ? duration
                : TimeSpan.FromSeconds((double)(Math.Max(sendPhaseTimestamp, scheduledSendWindowEndTimestamp) - startTimestamp) / Stopwatch.Frequency);
            var drainPhaseTimestamp = Interlocked.Read(ref drainPhaseEndTimestamp);
            var drainPhaseDuration = drainPhaseTimestamp == 0 || sendPhaseTimestamp == 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds((double)(drainPhaseTimestamp - sendPhaseTimestamp) / Stopwatch.Frequency);
            return new StressSnapshot(
                sent,
                Interlocked.Read(ref requestsPerSecond),
                successes,
                failures,
                remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
                sendPhaseDuration,
                sent,
                sent / Math.Max(sendPhaseDuration.TotalSeconds, 0.001),
                (double)Interlocked.Read(ref maximumSchedulingLatenessStopwatchTicks) * 1000 / Stopwatch.Frequency,
                sendPhaseDuration,
                drainPhaseDuration,
                successes,
                Interlocked.Read(ref unmatchedResponses),
                Interlocked.Read(ref duplicateResponses),
                lostRequests,
                sent == 0 ? 0 : (double)Interlocked.Read(ref totalSchedulingLatenessStopwatchTicks) * 1000 / Stopwatch.Frequency / sent,
                0,
                testMode,
                minimumInterSendGapStopwatchTicks == long.MaxValue ? 0 : (double)minimumInterSendGapStopwatchTicks * 1_000_000 / Stopwatch.Frequency,
                (double)Interlocked.Read(ref maximumInterSendGapStopwatchTicks) * 1000 / Stopwatch.Frequency,
                Interlocked.Read(ref sendsViolatingMinimumGap),
                0,
                Interlocked.Read(ref sendPhaseEndTimestamp) != 0 && Volatile.Read(ref finalized) == 0,
                lostRequests,
                gracePeriodMilliseconds,
                requiredDrainPeriodMilliseconds);
        }
    }

    private static void UpdateMaximum(ref long target, long candidate)
    {
        var current = Interlocked.Read(ref target);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref target, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    private static void UpdateMinimum(ref long target, long candidate)
    {
        var current = Interlocked.Read(ref target);
        while (candidate < current)
        {
            var observed = Interlocked.CompareExchange(ref target, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    internal static IEnumerable<long> GetRequestNumbersForWorker(long totalRequests, int workerCount, int workerIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalRequests);
        ArgumentOutOfRangeException.ThrowIfLessThan(workerCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(workerIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(workerIndex, workerCount);

        for (long requestNumber = workerIndex; requestNumber < totalRequests; requestNumber += workerCount)
        {
            yield return requestNumber;
            if (requestNumber > totalRequests - workerCount)
            {
                yield break;
            }
        }
    }

    internal static int GetRequestsForWorker(int totalRequestsPerSecond, int workerCount, int workerIndex)
    {
        var requestsPerWorker = totalRequestsPerSecond / workerCount;
        return workerIndex == 0 ? requestsPerWorker + totalRequestsPerSecond % workerCount : requestsPerWorker;
    }

    internal static int GetRequestsForInterval(int requestsPerSecond, int intervalsPerSecond, int interval)
    {
        var requestsPerInterval = requestsPerSecond / intervalsPerSecond;
        return interval < requestsPerSecond % intervalsPerSecond ? requestsPerInterval + 1 : requestsPerInterval;
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

    private static async Task DelayUntilStopwatchTimestampAsync(long targetTimestamp, CancellationToken cancellationToken)
    {
        var coarseDelayThreshold = Stopwatch.Frequency / 500;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingStopwatchTicks = targetTimestamp - Stopwatch.GetTimestamp();
            if (remainingStopwatchTicks <= 0)
            {
                return;
            }

            if (remainingStopwatchTicks > coarseDelayThreshold)
            {
                var coarseDelayTicks = remainingStopwatchTicks - Stopwatch.Frequency / 1000;
                await Task.Delay(TimeSpan.FromSeconds((double)coarseDelayTicks / Stopwatch.Frequency), cancellationToken).ConfigureAwait(false);
                continue;
            }

            while (Stopwatch.GetTimestamp() < targetTimestamp)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.SpinWait(64);
            }

            return;
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
