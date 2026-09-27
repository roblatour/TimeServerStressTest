using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TimeServerStressTest;

internal enum FunctionalTestStatus { Pass, Fail, CouldNotRun }

internal sealed record FunctionalTestResult(string Name, FunctionalTestStatus Status);

internal sealed record SymmetricKey(int Id, byte[] Value);

internal static class NtpKeyFile
{
    private static readonly Regex KeyLine = new(@"^([0-9]{1,5}) SHA256 ([A-Za-z0-9]{64})$", RegexOptions.CultureInvariant);

    internal static bool TryParse(string content, out IReadOnlyList<SymmetricKey> keys)
    {
        var parsed = new List<SymmetricKey>();
        var ids = new HashSet<int>();
        foreach (var line in content.Split('\n'))
        {
            var value = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var match = KeyLine.Match(value);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var id) || id is < 1 or > 65535 || !ids.Add(id))
            {
                keys = [];
                return false;
            }

            var secret = match.Groups[2].Value;
            parsed.Add(new SymmetricKey(id, IsHex(secret) ? Convert.FromHexString(secret) : Encoding.ASCII.GetBytes(secret)));
        }

        keys = parsed;
        return parsed.Count > 0;
    }

    private static bool IsHex(string value) => value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}

internal sealed class NtpFunctionalRunner
{
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BetweenTests = TimeSpan.FromMilliseconds(100);
    private static long sequence;

    internal static IReadOnlyList<string> TestNames { get; } = new[] { "IPv4", "IPv6" }
        .SelectMany(family => new[]
        {
            "NTPv3 Standard", "NTPv4 Standard", "NTPv4 Interleaved", "NTPv4 Symmetric-key standard",
            "NTPv4 Symmetric-key interleaved", "NTPv4 Invalid symmetric-key", "NTPv4 Missing symmetric-key",
            "NTPv4 Truncated MAC", "NTPv4 Mini saturated stress test"
        }.Select(test => $"{family} {test}"))
        .ToArray();

    internal static IReadOnlyList<AddressFamily> GetAvailableFamilies()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses.Select(unicast => unicast.Address))
            .ToArray();
        return new[] { AddressFamily.InterNetwork, AddressFamily.InterNetworkV6 }
            .Where(family => HasUsableAddress(addresses, family))
            .ToArray();
    }

    internal static bool HasUsableAddress(IEnumerable<IPAddress> addresses, AddressFamily family) =>
        addresses.Any(address => address.AddressFamily == family && !IPAddress.IsLoopback(address) &&
            (family == AddressFamily.InterNetworkV6
                ? !address.IsIPv6LinkLocal && !address.IsIPv6Multicast
                : address.GetAddressBytes() is not [169, 254, _, _]));

    internal static IReadOnlyList<string> GetTestNames(IReadOnlyCollection<AddressFamily> families) =>
        TestNames.Where((_, index) => families.Contains(index < 9 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6)).ToArray();

    internal async Task RunAsync(NtpEndpoint endpoint, SymmetricKey? key, IReadOnlyCollection<AddressFamily> families, IProgress<FunctionalTestResult> progress, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = IPAddress.TryParse(endpoint.Host, out var address)
                ? [address]
                : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            addresses = [];
        }

        var remainingTests = GetTestNames(families).Count;
        for (var familyIndex = 0; familyIndex < 2; familyIndex++)
        {
            var family = familyIndex == 0 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6;
            if (!families.Contains(family))
            {
                continue;
            }

            var target = addresses.FirstOrDefault(candidate => candidate.AddressFamily == family);
            for (var testIndex = 0; testIndex < 9; testIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FunctionalTestStatus status;
                if (target is null || (testIndex is >= 3 and <= 7 && key is null))
                {
                    status = FunctionalTestStatus.CouldNotRun;
                }
                else
                {
                    try
                    {
                        status = await RunTestAsync(new IPEndPoint(target, endpoint.Port), testIndex, key, cancellationToken).ConfigureAwait(false)
                            ? FunctionalTestStatus.Pass : FunctionalTestStatus.Fail;
                    }
                    catch (SocketException)
                    {
                        status = FunctionalTestStatus.Fail;
                    }
                }

                progress.Report(new FunctionalTestResult(TestNames[familyIndex * 9 + testIndex], status));
                if (--remainingTests > 0)
                {
                    await Task.Delay(BetweenTests, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task<bool> RunTestAsync(IPEndPoint target, int testIndex, SymmetricKey? key, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReplyTimeout);
        try
        {
            if (testIndex == 8)
            {
                return await RunMiniTestAsync(target, timeout.Token).ConfigureAwait(false);
            }

            using var client = new UdpClient(target.AddressFamily);
            client.Connect(target);
            var version = testIndex == 0 ? 3 : 4;
            var authenticated = testIndex is 3 or 4;
            var interleaved = testIndex is 2 or 4;
            var first = CreateRequest(version);
            var firstReply = interleaved
                ? await ExchangeAsync(client, ApplyAuthentication(first, authenticated ? key : null), timeout.Token).ConfigureAwait(false)
                : null;
            if (interleaved && (firstReply is null || !ValidResponse(firstReply, first, key, authenticated)))
            {
                return false;
            }

            var request = interleaved ? CreateInterleavedRequest(first, firstReply!) : first;
            if (testIndex is >= 3 and <= 7)
            {
                request = ApplyAuthentication(request, key!);
                if (testIndex == 5)
                {
                    request[^1] ^= 0xff;
                }
                else if (testIndex == 6)
                {
                    request = request[..52];
                }
                else if (testIndex == 7)
                {
                    request = request[..^8];
                }
            }

            var reply = await ExchangeAsync(client, request, timeout.Token).ConfigureAwait(false);
            var accepted = reply is not null && ValidResponse(reply, request, key, authenticated, interleaved ? firstReply : null);
            return testIndex is >= 5 and <= 7 ? !accepted : accepted;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return testIndex is >= 5 and <= 7;
        }
        catch (SocketException)
        {
            return testIndex is >= 5 and <= 7;
        }
    }

    private static async Task<bool> RunMiniTestAsync(IPEndPoint target, CancellationToken cancellationToken)
    {
        var endpoint = new NtpEndpoint(target.Address.ToString(), target.Port);
        var snapshot = await new NtpStressRunner().RunAsync(
            endpoint,
            TimeSpan.FromSeconds(1),
            concurrentWorkers: 0,
            maximumRequests: 0,
            maximumRequestsPerSecond: 100,
            StressTestMode.Saturation,
            new Progress<StressSnapshot>(),
            cancellationToken).ConfigureAwait(false);
        return snapshot.ActualSentRequestCount == 100 && snapshot.SuccessfulRequests == 100 && snapshot.FailedRequests == 0;
    }

    private static async Task<byte[]?> ExchangeAsync(UdpClient client, byte[] request, CancellationToken cancellationToken)
    {
        await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return (await client.ReceiveAsync(cancellationToken).ConfigureAwait(false)).Buffer;
    }

    internal static byte[] CreateRequest(int version)
    {
        var request = new byte[48];
        request[0] = (byte)(version << 3 | 3);
        var now = DateTimeOffset.UtcNow;
        var ticks = now.UtcTicks - new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        BinaryPrimitives.WriteUInt32BigEndian(request.AsSpan(40, 4), (uint)(ticks / TimeSpan.TicksPerSecond));
        BinaryPrimitives.WriteUInt32BigEndian(request.AsSpan(44, 4), (uint)((ticks % TimeSpan.TicksPerSecond * (1L << 32)) / TimeSpan.TicksPerSecond) + (uint)Interlocked.Increment(ref sequence));
        return request;
    }

    internal static byte[] CreateInterleavedRequest(byte[] previousRequest, byte[] previousReply)
    {
        var request = CreateRequest(4);
        previousReply.AsSpan(32, 8).CopyTo(request.AsSpan(24, 8));
        WriteTimestamp(request.AsSpan(32, 8), DateTimeOffset.UtcNow);
        previousRequest.AsSpan(40, 8).CopyTo(request.AsSpan(40, 8));
        return request;
    }

    private static void WriteTimestamp(Span<byte> destination, DateTimeOffset now)
    {
        var ticks = now.UtcTicks - new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        BinaryPrimitives.WriteUInt32BigEndian(destination[..4], (uint)(ticks / TimeSpan.TicksPerSecond));
        BinaryPrimitives.WriteUInt32BigEndian(destination[4..], (uint)((ticks % TimeSpan.TicksPerSecond * (1L << 32)) / TimeSpan.TicksPerSecond));
    }

    internal static byte[] ApplyAuthentication(byte[] packet, SymmetricKey? key)
    {
        if (key is null)
        {
            return packet;
        }

        var authenticated = new byte[packet.Length + 24];
        packet.CopyTo(authenticated, 0);
        BinaryPrimitives.WriteUInt32BigEndian(authenticated.AsSpan(packet.Length, 4), (uint)key.Id);
        var input = new byte[key.Value.Length + packet.Length];
        key.Value.CopyTo(input, 0);
        packet.CopyTo(input, key.Value.Length);
        SHA256.HashData(input).AsSpan(0, 20).CopyTo(authenticated.AsSpan(packet.Length + 4));
        return authenticated;
    }

    internal static bool ValidResponse(byte[] reply, byte[] request, SymmetricKey? key, bool authenticated, byte[]? previousReply = null)
    {
        if (reply.Length < 48 || (reply[0] & 7) != 4 || (reply[0] >> 3 & 7) != (request[0] >> 3 & 7) || reply[1] is 0 or >= 16)
        {
            return false;
        }

        var expectedOrigin = previousReply is null ? request.AsSpan(40, 8) : request.AsSpan(32, 8);
        if (!reply.AsSpan(24, 8).SequenceEqual(expectedOrigin) || reply.AsSpan(40, 8).SequenceEqual(new byte[8]))
        {
            return false;
        }

        if (previousReply is not null)
        {
            var actual = BinaryPrimitives.ReadUInt64BigEndian(reply.AsSpan(40, 8));
            var previousTransmit = BinaryPrimitives.ReadUInt64BigEndian(previousReply.AsSpan(40, 8));
            var difference = actual >= previousTransmit ? actual - previousTransmit : previousTransmit - actual;
            if (difference > (1UL << 32) * 5 / 1000)
            {
                return false;
            }
        }

        if (!authenticated)
        {
            return true;
        }

        if (key is null || reply.Length != 72 || BinaryPrimitives.ReadUInt32BigEndian(reply.AsSpan(48, 4)) != key.Id)
        {
            return false;
        }

        var expected = ApplyAuthentication(reply[..48], key);
        return CryptographicOperations.FixedTimeEquals(expected.AsSpan(52, 20), reply.AsSpan(52, 20));
    }
}
