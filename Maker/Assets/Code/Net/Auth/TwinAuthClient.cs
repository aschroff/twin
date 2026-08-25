using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Net.Auth
{
    /// <summary>
    /// The three <c>/v1/auth</c> endpoints, and nothing else.
    /// </summary>
    /// <remarks>
    /// Deliberately stateless: it makes calls and returns what came back. It does
    /// not remember a session, does not refresh on your behalf, and does not know
    /// when a token expires. <see cref="TwinSession"/> is the piece that does all
    /// of that, and it is the one you normally use.
    ///
    /// Kept separate because the two have very different reasons to change. This
    /// class changes when the server's contract changes; the session changes when
    /// the app's idea of "signed in" changes.
    ///
    /// A plain class, not a MonoBehaviour — it has no frame-by-frame work and
    /// nothing to draw, so tying it to a GameObject's lifetime would only make it
    /// harder to test.
    /// </remarks>
    public class TwinAuthClient
    {
        private const string LoginPath = "/v1/auth/login";
        private const string RefreshPath = "/v1/auth/refresh";
        private const string LogoutPath = "/v1/auth/logout";

        private readonly TwinApiConfig _config;

        public TwinAuthClient(TwinApiConfig config)
        {
            _config = config != null
                ? config
                : throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(_config.BaseUrl))
            {
                throw new ArgumentException("TwinApiConfig.BaseUrl is empty.", nameof(config));
            }
        }

        /// <summary>Load the config from Resources and use that.</summary>
        public TwinAuthClient() : this(TwinApiConfig.LoadDefault())
        {
        }

        /// <summary>
        /// Exchange an email address and password for a token pair.
        /// </summary>
        /// <remarks>
        /// Slow on purpose — the server hashes the password with argon2, which
        /// takes a few hundred milliseconds by design. Show a spinner and disable
        /// the button; do not treat the delay as a fault.
        /// </remarks>
        /// <exception cref="TwinAuthException">
        /// <c>IsInvalidCredentials</c> when the pair was rejected,
        /// <c>IsNetworkFailure</c> when the server was unreachable.
        /// </exception>
        public Task<TokenResponse> LoginAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email must not be empty.", nameof(email));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password must not be empty.", nameof(password));
            }

            var body = new LoginRequest { Email = email.Trim(), Password = password };
            return PostAsync<TokenResponse>(LoginPath, body, "login");
        }

        /// <summary>
        /// Trade a refresh token for a fresh pair.
        /// </summary>
        /// <remarks>
        /// ⚠️ The presented token is consumed. Store the returned one and discard
        /// the old one immediately, in that order — if the app crashes between the
        /// two, the person has to sign in again, which is the safe direction to
        /// fail in.
        ///
        /// Presenting a token that was already spent is treated as theft: the
        /// server revokes *every* token this user holds, on every device. That is
        /// why two concurrent refreshes must never be allowed, and why
        /// <see cref="TwinSession"/> serialises them.
        /// </remarks>
        public Task<TokenResponse> RefreshAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException("Refresh token must not be empty.", nameof(refreshToken));
            }

            var body = new RefreshRequest { RefreshToken = refreshToken };
            return PostAsync<TokenResponse>(RefreshPath, body, "refresh");
        }

        /// <summary>
        /// Revoke one refresh token — this device only.
        /// </summary>
        /// <remarks>
        /// Succeeds whether or not the token existed, so it is safe to call twice
        /// and cannot be used to probe whether a value is real. Other devices keep
        /// their sessions.
        /// </remarks>
        public async Task LogoutAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return;
            }

            var body = new LogoutRequest { RefreshToken = refreshToken };
            await PostAsync<object>(LogoutPath, body, "logout", expectsBody: false);
        }

        private async Task<T> PostAsync<T>(string path, object body, string operation, bool expectsBody = true)
            where T : class
        {
            var url = _config.BaseUrl + path;
            var json = JsonConvert.SerializeObject(body);

            // `new UnityWebRequest` rather than UnityWebRequest.Post(url, json):
            // that overload has a long history of form-encoding the body instead
            // of sending it raw, and the difference only shows up as a 422 from
            // the server. Building it explicitly leaves nothing to the overload.
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _config.TimeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            if (_config.VerboseLogging)
            {
                // The URL and the operation, never the body — it holds the password.
                Debug.Log($"[TwinAuth] POST {url} ({operation})");
            }

            var tcs = new TaskCompletionSource<T>();

            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    var status = request.responseCode;
                    var responseText = request.downloadHandler?.text;

                    if (_config.VerboseLogging)
                    {
                        Debug.Log($"[TwinAuth] {operation} -> HTTP {status}");
                    }

                    // Transport failure: DNS, refused connection, timeout. There is
                    // no envelope to parse, and status is 0.
                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        tcs.SetException(new TwinAuthException(
                            0, null,
                            $"Could not reach the Twin API at {_config.BaseUrl} ({operation}): {request.error}. " +
                            "Check that the backend is running and that TwinApiConfig.BaseUrl is correct."));
                        return;
                    }

                    if (status >= 400)
                    {
                        tcs.SetException(BuildApiException(status, responseText, operation));
                        return;
                    }

                    if (!expectsBody)
                    {
                        tcs.SetResult(null);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(responseText))
                    {
                        tcs.SetException(new TwinAuthException(
                            status, null, $"The server returned HTTP {status} with an empty body ({operation})."));
                        return;
                    }

                    var parsed = JsonConvert.DeserializeObject<T>(responseText);
                    if (parsed == null)
                    {
                        tcs.SetException(new TwinAuthException(
                            status, null, $"Could not read the {operation} response as {typeof(T).Name}."));
                        return;
                    }

                    tcs.SetResult(parsed);
                }
                catch (Exception ex)
                {
                    // The response text is deliberately absent from this message:
                    // on login and refresh it contains tokens.
                    tcs.SetException(new TwinAuthException(
                        request.responseCode, null, $"Unexpected failure handling the {operation} response.", ex));
                }
            };

            return await tcs.Task;
        }

        /// <summary>
        /// Turn an error response into an exception, preferring the server's own
        /// code over anything inferred from the status.
        /// </summary>
        private static TwinAuthException BuildApiException(long status, string responseText, string operation)
        {
            string code = null;
            string message = null;

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    var envelope = JsonConvert.DeserializeObject<ApiErrorResponse>(responseText);
                    code = envelope?.Error?.Code;
                    message = envelope?.Error?.Message;
                }
                catch (JsonException)
                {
                    // Not our envelope. Happens when something in front of the API
                    // answers instead — an ingress 502, a captive portal, a proxy.
                    // Fall through to the generic message below.
                }
            }

            if (code == null && status == 401)
            {
                // Defensive: every 401 from this API carries an envelope today, so
                // this only fires if something else produced it.
                code = TwinAuthErrorCodes.InvalidCredentials;
            }

            var text = message ?? $"The {operation} request failed with HTTP {status}.";
            return new TwinAuthException(status, code, text);
        }
    }
}
