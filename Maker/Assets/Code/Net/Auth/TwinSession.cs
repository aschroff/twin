using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Code.Net.Auth
{
    /// <summary>
    /// The signed-in state of the app: who is signed in, and a valid access token
    /// whenever one is needed.
    /// </summary>
    /// <remarks>
    /// This is the type screens talk to. <see cref="TwinAuthClient"/> sits
    /// underneath and makes the HTTP calls.
    ///
    /// What it takes care of, so callers do not have to:
    /// <list type="bullet">
    /// <item>Refreshing the access token before it expires.</item>
    /// <item>Making sure only one refresh is ever in flight — see below.</item>
    /// <item>Raising <see cref="SignedOut"/> when the session ends for any reason.</item>
    /// </list>
    ///
    /// <para><b>Why one refresh at a time matters.</b> Refresh tokens are
    /// single-use, and presenting a spent one is treated by the server as a stolen
    /// token: it revokes every token this user holds, on every device. Two screens
    /// refreshing at the same moment would do exactly that to your own user. The
    /// second caller therefore awaits the first one's result instead of starting
    /// its own.</para>
    ///
    /// <para><b>Not thread-safe, and does not need to be.</b> Everything here runs
    /// on Unity's main thread, because <c>UnityWebRequest</c> does. The lock below
    /// guards against re-entrancy from overlapping async calls, not from threads.</para>
    /// </remarks>
    public class TwinSession
    {
        /// <summary>
        /// How long before expiry we treat the access token as already stale.
        /// </summary>
        /// <remarks>
        /// A token that expires while a request is in flight fails that request,
        /// so we refresh early rather than exactly on time. Sixty seconds also
        /// absorbs a clock that is a little out of step with the server's.
        /// </remarks>
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

        private readonly TwinAuthClient _client;
        private readonly ITokenStore _store;
        private readonly SemaphoreSlim _refreshGate = new(1, 1);

        private string _accessToken;
        private DateTime _accessTokenExpiresAtUtc;
        private string _refreshToken;

        /// <summary>Raised when the session ends — by logout, or because the server rejected it.</summary>
        public event Action SignedOut;

        /// <summary>Raised on a successful sign-in or restore.</summary>
        public event Action SignedIn;

        public TwinSession(TwinAuthClient client, ITokenStore store = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _store = store ?? new PlayerPrefsTokenStore();
        }

        /// <summary>Convenience: config from Resources, default token store.</summary>
        public TwinSession() : this(new TwinAuthClient())
        {
        }

        /// <summary>
        /// True when a refresh token is held. Note this does not mean the server
        /// still honours it — only <see cref="GetAccessTokenAsync"/> can tell you that.
        /// </summary>
        public bool IsSignedIn => !string.IsNullOrEmpty(_refreshToken);

        /// <summary>
        /// The signed-in user's id (the JWT <c>sub</c> claim), or <c>null</c>.
        /// </summary>
        public string UserId { get; private set; }

        /// <summary>
        /// The user's organisation id (the JWT <c>org</c> claim), or <c>null</c>.
        /// </summary>
        /// <remarks>
        /// A UUID, not a name. The API exposes no endpoint that turns it into one
        /// yet, so do not try to show it to a person.
        /// </remarks>
        public string OrganisationId { get; private set; }

        /// <summary>
        /// Sign in and remember the session.
        /// </summary>
        /// <exception cref="TwinAuthException">
        /// Check <c>IsInvalidCredentials</c> to tell "wrong password" from
        /// "server unreachable" (<c>IsNetworkFailure</c>).
        /// </exception>
        public async Task SignInAsync(string email, string password)
        {
            var tokens = await _client.LoginAsync(email, password);
            Adopt(tokens);
            SignedIn?.Invoke();
        }

        /// <summary>
        /// Restore a session saved by a previous run.
        /// </summary>
        /// <returns>
        /// True when a stored token was accepted and the session is live. False
        /// when there was nothing stored, or the server refused it — either way,
        /// show the login screen. It does not throw for a refused token, because
        /// "no session" is an ordinary outcome at startup rather than an error.
        /// </returns>
        /// <remarks>
        /// A network failure at startup is different and *does* throw: the session
        /// may well be fine, and silently returning false would sign the person
        /// out over a lost Wi-Fi connection.
        /// </remarks>
        public async Task<bool> TryRestoreAsync()
        {
            var stored = _store.Load();
            if (string.IsNullOrEmpty(stored))
            {
                return false;
            }

            _refreshToken = stored;

            try
            {
                await RefreshAsync();
                SignedIn?.Invoke();
                return true;
            }
            catch (TwinAuthException ex) when (ex.IsSessionEnded)
            {
                ClearLocalSession();
                return false;
            }
        }

        /// <summary>
        /// A valid access token, refreshing first if the current one is near expiry.
        /// </summary>
        /// <remarks>
        /// Call this immediately before each request rather than caching the
        /// string — that is what makes the refresh invisible to callers.
        ///
        /// <code>
        /// request.SetRequestHeader("Authorization", $"Bearer {await session.GetAccessTokenAsync()}");
        /// </code>
        /// </remarks>
        /// <exception cref="InvalidOperationException">When not signed in.</exception>
        /// <exception cref="TwinAuthException">
        /// When the session has ended (<c>IsSessionEnded</c>) — send the person to
        /// the login screen. <see cref="SignedOut"/> has already been raised.
        /// </exception>
        public async Task<string> GetAccessTokenAsync()
        {
            if (!IsSignedIn)
            {
                throw new InvalidOperationException(
                    "Not signed in. Call SignInAsync or TryRestoreAsync first.");
            }

            if (!string.IsNullOrEmpty(_accessToken) &&
                DateTime.UtcNow < _accessTokenExpiresAtUtc - RefreshMargin)
            {
                return _accessToken;
            }

            await RefreshAsync();
            return _accessToken;
        }

        /// <summary>
        /// Sign out this device and forget the stored token.
        /// </summary>
        /// <remarks>
        /// The local session is cleared whether or not the server call succeeds.
        /// A logout that fails because the network is down must still log the
        /// person out of the app — the token expires on its own soon enough.
        /// </remarks>
        public async Task SignOutAsync()
        {
            var token = _refreshToken;
            ClearLocalSession();

            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            try
            {
                await _client.LogoutAsync(token);
            }
            catch (TwinAuthException ex)
            {
                Debug.LogWarning($"[TwinAuth] Sign-out could not reach the server: {ex.Message}");
            }
        }

        /// <summary>
        /// Spend the refresh token for a new pair, letting a concurrent caller
        /// share the result rather than start a second refresh.
        /// </summary>
        private async Task RefreshAsync()
        {
            await _refreshGate.WaitAsync();
            try
            {
                // A caller that queued behind another refresh may find the token
                // already fresh. Re-check inside the gate before spending one.
                if (!string.IsNullOrEmpty(_accessToken) &&
                    DateTime.UtcNow < _accessTokenExpiresAtUtc - RefreshMargin)
                {
                    return;
                }

                try
                {
                    var tokens = await _client.RefreshAsync(_refreshToken);
                    Adopt(tokens);
                }
                catch (TwinAuthException ex) when (ex.IsSessionEnded)
                {
                    // The server will not honour this session again. Drop it here
                    // rather than leaving a token that fails every future call.
                    ClearLocalSession();
                    throw;
                }
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        /// <summary>
        /// Take a new token pair. Persisting the refresh token happens before the
        /// old one is forgotten, so a crash mid-way costs a login rather than
        /// leaving an unusable token behind.
        /// </summary>
        private void Adopt(TokenResponse tokens)
        {
            _accessToken = tokens.AccessToken;
            _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn);
            _refreshToken = tokens.RefreshToken;
            _store.Save(tokens.RefreshToken);

            var claims = JwtClaims.ReadUnverified(tokens.AccessToken);
            UserId = claims?.Subject;
            OrganisationId = claims?.Organisation;
        }

        private void ClearLocalSession()
        {
            var wasSignedIn = IsSignedIn;

            _accessToken = null;
            _refreshToken = null;
            _accessTokenExpiresAtUtc = DateTime.MinValue;
            UserId = null;
            OrganisationId = null;
            _store.Clear();

            if (wasSignedIn)
            {
                SignedOut?.Invoke();
            }
        }
    }
}
