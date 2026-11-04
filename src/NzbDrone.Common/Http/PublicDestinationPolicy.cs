using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NzbDrone.Common.Http
{
    public static class PublicDestinationPolicy
    {
        public static async Task ValidateAsync(Uri uri, bool trustedProxy = false)
        {
            if (uri == null || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new WebException("Release download destination must use HTTP or HTTPS", WebExceptionStatus.ProtocolError);
            }

            if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            {
                throw new WebException("Release download destination is not public", WebExceptionStatus.ProtocolError);
            }

            // A trusted proxy may own DNS for names that the application host
            // cannot resolve. Literal addresses are still checked locally.
            if (trustedProxy && !IPAddress.TryParse(uri.Host, out _))
            {
                return;
            }

            IPAddress[] addresses;

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                addresses = IPAddress.TryParse(uri.Host, out var address)
                    ? new[] { address }
                    : await Dns.GetHostAddressesAsync(uri.DnsSafeHost, timeout.Token);
            }
            catch (Exception ex) when (ex is SocketException or OperationCanceledException)
            {
                throw new WebException("Release download destination could not be resolved", ex, WebExceptionStatus.NameResolutionFailure, null);
            }

            if (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a)))
            {
                throw new WebException("Release download destination is not public", WebExceptionStatus.ProtocolError);
            }
        }

        public static bool IsPublicAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            var bytes = address.GetAddressBytes();

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                return bytes[0] != 0 && bytes[0] != 10 && bytes[0] != 127 &&
                       !(bytes[0] == 169 && bytes[1] == 254) &&
                       !(bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) &&
                       !(bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) &&
                       !(bytes[0] == 192 && (bytes[1] == 0 || bytes[1] == 168)) &&
                       !(bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19 || bytes[1] == 51)) &&
                       !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) &&
                       bytes[0] < 224;
            }

            // Only globally routed unicast IPv6 is permitted. Exclude the
            // documentation prefix even though it sits within 2000::/3.
            return address.AddressFamily == AddressFamily.InterNetworkV6 &&
                   (bytes[0] & 0xE0) == 0x20 &&
                   !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
        }
    }
}
