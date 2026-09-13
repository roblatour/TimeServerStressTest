using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TimeServerStressTest;

internal static class NetworkAddressScope
{
    public static async Task<bool> ResolvesToExternalAddressAsync(string host, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            return false;
        }

        return addresses.Any(address => !IsInternal(address));
    }

    private static bool IsInternal(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }

        return address.AddressFamily == AddressFamily.InterNetwork &&
            (IsLinkLocalIpv4(address) || GetLocalIpv4Networks().Any(network => IsInNetwork(address, network.Address, network.Mask)));
    }

    private static bool IsLinkLocalIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    private static IEnumerable<(IPAddress Address, IPAddress Mask)> GetLocalIpv4Networks()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface => networkInterface.OperationalStatus == OperationalStatus.Up)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Where(unicastAddress => unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork && unicastAddress.IPv4Mask is not null)
            .Select(unicastAddress => (unicastAddress.Address, unicastAddress.IPv4Mask!));
    }

    private static bool IsInNetwork(IPAddress address, IPAddress networkAddress, IPAddress mask)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = networkAddress.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        return addressBytes.Zip(networkBytes, maskBytes).All(bytes => (bytes.First & bytes.Third) == (bytes.Second & bytes.Third));
    }
}
