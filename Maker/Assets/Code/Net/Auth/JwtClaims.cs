using System;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Code.Net.Auth
{
    /// <summary>
    /// Reads the payload of an access token — <b>without verifying its signature</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>Read this before using it for anything but display.</b></para>
    ///
    /// A JWT is three base64url segments: header, payload, signature. Only the
    /// third proves the first two were not tampered with, and verifying it needs
    /// the server's signing key — which the client does not have and must never
    /// have, because a client that can verify a token can also mint one.
    ///
    /// So everything this class returns is <em>claimed</em>, not proven. Anyone
    /// who can edit the app's memory can make it say whatever they like.
    ///
    /// <list type="bullet">
    /// <item><b>Fine:</b> showing which user is signed in, tagging a log line,
    /// deciding which screen to open first.</item>
    /// <item><b>Not fine:</b> deciding whether someone may do something. Every
    /// authorisation decision belongs on the server, which verifies the signature
    /// on every request. A client-side check is a convenience for the honest user
    /// and no obstacle at all to a dishonest one.</item>
    /// </list>
    /// </remarks>
    public sealed class JwtClaims
    {
        /// <summary>The <c>sub</c> claim — the user's id.</summary>
        public string Subject { get; private set; }

        /// <summary>The <c>org</c> claim — the user's organisation id.</summary>
        public string Organisation { get; private set; }

        /// <summary>The <c>scope</c> claim — space-separated and sorted.</summary>
        public string Scope { get; private set; }

        /// <summary>The <c>exp</c> claim as UTC, or <c>null</c> when absent.</summary>
        public DateTime? ExpiresAtUtc { get; private set; }

        /// <summary>
        /// Parse the payload. Returns <c>null</c> for anything that is not a
        /// readable JWT rather than throwing — a malformed token is a reason to
        /// have no claims, not a reason to bring down the caller.
        /// </summary>
        public static JwtClaims ReadUnverified(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            try
            {
                var parts = accessToken.Split('.');
                if (parts.Length != 3)
                {
                    return null;
                }

                var payload = JObject.Parse(Encoding.UTF8.GetString(DecodeBase64Url(parts[1])));

                var claims = new JwtClaims
                {
                    Subject = (string)payload["sub"],
                    Organisation = (string)payload["org"],
                    Scope = (string)payload["scope"],
                };

                var exp = payload["exp"];
                if (exp != null && exp.Type == JTokenType.Integer)
                {
                    claims.ExpiresAtUtc = DateTimeOffset.FromUnixTimeSeconds((long)exp).UtcDateTime;
                }

                return claims;
            }
            catch (Exception ex)
            {
                // Never logs the token itself, which is a bearer credential.
                Debug.LogWarning($"[TwinAuth] Could not read the access token payload: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// base64url → bytes. JWTs use the URL-safe alphabet and drop the padding,
        /// so <c>Convert.FromBase64String</c> rejects them as-is.
        /// </summary>
        private static byte[] DecodeBase64Url(string value)
        {
            var s = value.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }

            return Convert.FromBase64String(s);
        }
    }
}
