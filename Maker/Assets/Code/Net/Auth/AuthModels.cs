using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Code.Net.Auth
{
    /// <summary>
    /// The wire types for <c>/v1/auth</c>.
    /// </summary>
    /// <remarks>
    /// Hand-written against the backend's OpenAPI schema. The long-term plan is
    /// to generate these with NSwag from the published <c>openapi.json</c>
    /// (docs/STACK.md section 2) — until that pipeline exists, these three small
    /// types are cheaper than the pipeline, and the property names below are the
    /// only thing that has to stay in step with the server.
    ///
    /// Every JSON name is spelled out with <see cref="JsonPropertyAttribute"/>
    /// rather than relying on a naming strategy: the server uses snake_case and
    /// C# uses PascalCase, and an implicit convention is one refactor away from
    /// silently sending the wrong field name.
    /// </remarks>
    internal sealed class LoginRequest
    {
        [JsonProperty("email")] public string Email { get; set; }

        [JsonProperty("password")] public string Password { get; set; }
    }

    internal sealed class RefreshRequest
    {
        [JsonProperty("refresh_token")] public string RefreshToken { get; set; }
    }

    internal sealed class LogoutRequest
    {
        [JsonProperty("refresh_token")] public string RefreshToken { get; set; }
    }

    /// <summary>
    /// What <c>login</c> and <c>refresh</c> both return.
    /// </summary>
    public sealed class TokenResponse
    {
        /// <summary>Signed JWT. Send as <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
        [JsonProperty("access_token")] public string AccessToken { get; set; }

        /// <summary>
        /// Single-use. Spending it returns a new one; the old one is dead the
        /// moment the new one is issued.
        /// </summary>
        [JsonProperty("refresh_token")] public string RefreshToken { get; set; }

        /// <summary>Always <c>bearer</c>.</summary>
        [JsonProperty("token_type")] public string TokenType { get; set; } = "bearer";

        /// <summary>Lifetime of the access token in seconds. Currently 900.</summary>
        [JsonProperty("expires_in")] public int ExpiresIn { get; set; }

        /// <summary>Space-separated and sorted, so it is stable to compare.</summary>
        [JsonProperty("scope")] public string Scope { get; set; }
    }

    /// <summary>
    /// The error envelope every failing endpoint returns.
    /// </summary>
    public sealed class ApiErrorResponse
    {
        [JsonProperty("error")] public ApiErrorDetail Error { get; set; }
    }

    public sealed class ApiErrorDetail
    {
        /// <summary>
        /// A stable machine-readable code — see <see cref="TwinAuthErrorCodes"/>.
        /// Branch on this, never on <see cref="Message"/>.
        /// </summary>
        [JsonProperty("code")] public string Code { get; set; }

        /// <summary>
        /// English, and written for a developer reading a log. Do **not** put this
        /// in front of a user: it is not localised and its wording is not a
        /// contract. Map <see cref="Code"/> to your own localised string.
        /// </summary>
        [JsonProperty("message")] public string Message { get; set; }

        [JsonProperty("details")] public Dictionary<string, object> Details { get; set; }
    }

    /// <summary>
    /// The <c>code</c> values these endpoints can return.
    /// </summary>
    public static class TwinAuthErrorCodes
    {
        /// <summary>
        /// Wrong password, unknown address, or a deactivated account.
        /// </summary>
        /// <remarks>
        /// The server answers identically for all three, and takes the same time
        /// doing it, so the login form cannot be used to find out which addresses
        /// have accounts. Do not try to be more specific in the UI than the server
        /// is — there is nothing to be specific with.
        /// </remarks>
        public const string InvalidCredentials = "INVALID_CREDENTIALS";

        /// <summary>
        /// The refresh token was rejected: unknown, already spent, or revoked.
        /// The only correct response is to send the person back to the login screen.
        /// </summary>
        public const string InvalidToken = "INVALID_TOKEN";

        /// <summary>The token was valid but has expired.</summary>
        public const string TokenExpired = "TOKEN_EXPIRED";

        /// <summary>The request body did not match the schema — a client bug.</summary>
        public const string ValidationFailed = "VALIDATION_FAILED";
    }

    /// <summary>
    /// Thrown for every failed call. Inspect <see cref="ErrorCode"/> to decide
    /// what to do; <see cref="StatusCode"/> is 0 when the request never reached
    /// the server at all (no network, wrong host, timeout).
    /// </summary>
    public class TwinAuthException : Exception
    {
        public long StatusCode { get; }

        /// <summary>
        /// The server's <c>code</c>, or <c>null</c> when the failure was at the
        /// transport level and there is no envelope to read.
        /// </summary>
        public string ErrorCode { get; }

        public TwinAuthException(long statusCode, string errorCode, string message, Exception inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// True when the address or password was rejected. The one failure that
        /// is the person's to fix rather than yours.
        /// </summary>
        public bool IsInvalidCredentials => ErrorCode == TwinAuthErrorCodes.InvalidCredentials;

        /// <summary>
        /// True when the session is gone and the only way forward is a fresh login.
        /// </summary>
        public bool IsSessionEnded =>
            ErrorCode == TwinAuthErrorCodes.InvalidToken || ErrorCode == TwinAuthErrorCodes.TokenExpired;

        /// <summary>
        /// True when the request never reached the server. Retrying may work;
        /// the credentials are not implicated.
        /// </summary>
        public bool IsNetworkFailure => StatusCode == 0;
    }
}
