using System;
using System.Security.Cryptography;
using System.Text;

namespace Radarr.Http.Authentication
{
    public static class CalendarFeedCredential
    {
        private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("plattarr-calendar-feed-v1");

        public static string Derive(string apiKey)
        {
            return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(apiKey), Purpose));
        }

        public static bool Matches(string candidate, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }

            var expected = Derive(apiKey);
            return candidate.Length == expected.Length &&
                   CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(expected));
        }
    }
}
