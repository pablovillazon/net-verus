using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Verus.Companies.Infrastructure;

/// <summary>
/// Server-Side Request Forgery protection for content inspection. The check runs inside the socket connect
/// callback, i.e. on the address that is actually connected to, for the first request AND every redirect.
/// That closes both the "redirect to http://169.254.169.254" and the DNS-rebinding holes that a pre-flight
/// "is this URL OK?" check would leave open.
/// </summary>
internal static class SsrfGuard
{
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        var allowed = addresses.Where(IsPublic).ToArray();

        if (allowed.Length == 0)
        {
            throw new HttpRequestException($"Host '{host}' does not resolve to a public IP address; request blocked.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0
                     || b[0] == 10
                     || (b[0] == 100 && b[1] is >= 64 and <= 127)   // carrier-grade NAT
                     || (b[0] == 169 && b[1] == 254)                // link-local / cloud metadata
                     || (b[0] == 172 && b[1] is >= 16 and <= 31)
                     || (b[0] == 192 && b[1] == 168)
                     || b[0] >= 224);                               // multicast + reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal
                     || address.IsIPv6SiteLocal
                     || address.IsIPv6Multicast
                     || (b[0] & 0xFE) == 0xFC);                     // unique local fc00::/7
        }

        return false;
    }
}
