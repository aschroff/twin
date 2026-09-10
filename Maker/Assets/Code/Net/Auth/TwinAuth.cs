using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Net.Auth
{
    /// <summary>
    /// The one signed-in session the whole app shares.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this exists.</b> <see cref="TwinSession"/> is an ordinary
    /// object, so whoever constructs it owns it. If the login screen constructed
    /// its own — as <see cref="LoginPanelExample"/> does — the session would live
    /// and die with that panel, and code elsewhere that needs a token would have
    /// no way to reach it. The settings panel is a <i>view</i> onto the session;
    /// it must not be the thing that holds it.</para>
    ///
    /// <para><b>Call sites use the static members and nothing else:</b></para>
    /// <code>
    /// using var request = UnityWebRequest.Get(url);
    /// await TwinAuth.AuthorizeAsync(request);
    /// </code>
    ///
    /// <para><b>No ordering to get right.</b> The session is created on first use
    /// and the stored token is restored exactly once, so a data call that runs
    /// before the login panel has ever been opened still works — it awaits the
    /// same restore rather than racing it. That also survives an editor domain
    /// reload, which wipes the statics: the next call rebuilds from the stored
    /// refresh token.</para>
    ///
    /// <para>Put one of these in the scene so the restore starts at launch rather
    /// than at the first request. The app never loads a second scene, so there is
    /// nothing to survive and no <c>DontDestroyOnLoad</c> — same as
    /// <c>DataPersistenceManager</c>.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class TwinAuth : MonoBehaviour
    {
        [Tooltip("Leave empty to load Resources/TwinApiConfig.")]
        [SerializeField] private TwinApiConfig apiConfig;

        private static TwinAuth _instance;
        private static TwinApiConfig _config;
        private static TwinSession _session;

        /// <summary>
        /// The restore that runs once per app run. Held as a task, not a bool, so
        /// that callers arriving while it is still in flight await it instead of
        /// starting a second one.
        /// </summary>
        private static Task<bool> _restore;

        /// <summary>Raised on a successful sign-in or restore.</summary>
        /// <remarks>
        /// Static, so a panel can subscribe whenever it is first enabled without
        /// caring whether the session already existed. Unsubscribe in
        /// <c>OnDestroy</c> — a static event outlives the objects listening to it.
        /// </remarks>
        public static event Action SignedIn;

        /// <summary>Raised when the session ends, by logout or by the server refusing it.</summary>
        public static event Action SignedOut;

        /// <summary>
        /// The shared session, created on first use.
        /// </summary>
        public static TwinSession Session
        {
            get
            {
                if (_session == null)
                {
                    _session = new TwinSession(new TwinAuthClient(Config));
                    _session.SignedIn += RaiseSignedIn;
                    _session.SignedOut += RaiseSignedOut;
                }

                return _session;
            }
        }

        /// <summary>
        /// True when a refresh token is held. Says nothing about whether the
        /// server still honours it — only a request can establish that.
        /// </summary>
        public static bool IsSignedIn => _session != null && _session.IsSignedIn;

        private static TwinApiConfig Config => _config != null ? _config : (_config = TwinApiConfig.LoadDefault());

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{nameof(TwinAuth)}] More than one in the scene. Destroying the newest one.", this);
                Destroy(this);
                return;
            }

            _instance = this;

            if (apiConfig != null)
            {
                _config = apiConfig;
            }
        }

        private async void Start()
        {
            // Fire and forget on purpose: nothing at launch is waiting on the
            // result, and anything that needs a token awaits the same task.
            try
            {
                await RestoreAsync();
            }
            catch (TwinAuthException ex) when (ex.IsNetworkFailure)
            {
                // Not a sign-out. The session may be perfectly good and the
                // network merely absent; the next request will try again.
                Debug.Log($"[{nameof(TwinAuth)}] Could not reach the server to restore the session: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                // No TwinApiConfig in Resources. Worth saying plainly once here,
                // rather than as an opaque failure at the first request.
                Debug.LogError($"[{nameof(TwinAuth)}] {ex.Message}", this);
            }
            catch (Exception ex)
            {
                // A backstop, because this is async void: anything not caught here
                // is rethrown on the synchronization context and Unity reports it as
                // an unhandled exception at scene load. Restoring a session is a
                // best-effort attempt at launch - it must never be able to break
                // starting the app.
                Debug.LogWarning($"[{nameof(TwinAuth)}] Restoring the session failed, starting signed out: {ex}", this);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Restore the session saved by a previous run — at most once per run.
        /// </summary>
        /// <returns>
        /// True when a stored token was accepted. False when there was none, or
        /// the server refused it; both are ordinary and neither throws.
        /// </returns>
        /// <exception cref="TwinAuthException">
        /// <c>IsNetworkFailure</c> when the server could not be reached. The
        /// session is kept, and a later call retries.
        /// </exception>
        public static Task<bool> RestoreAsync()
        {
            // A faulted task is not cached: a restore that failed because the
            // server was unreachable must be retried later, not remembered as a
            // permanent "no".
            if (_restore is { IsFaulted: true } or { IsCanceled: true })
            {
                _restore = null;
            }

            return _restore ??= Session.TryRestoreAsync();
        }

        /// <summary>
        /// Sign in and remember the session across app restarts.
        /// </summary>
        /// <exception cref="TwinAuthException">
        /// <c>IsInvalidCredentials</c> for a rejected pair, <c>IsNetworkFailure</c>
        /// when the server was unreachable.
        /// </exception>
        public static async Task SignInAsync(string email, string password)
        {
            await Session.SignInAsync(email, password);

            // There is now a live session, so the startup restore has nothing left
            // to do. Marking it done keeps a later call from spending the refresh
            // token a second time — which the server treats as theft.
            _restore = Task.FromResult(true);
        }

        /// <summary>Sign out this device. Other devices keep their sessions.</summary>
        public static async Task SignOutAsync()
        {
            _restore = Task.FromResult(false);
            await Session.SignOutAsync();
        }

        /// <summary>
        /// A valid access token, waiting for the startup restore if it is still
        /// running and renewing the token if it is close to expiring.
        /// </summary>
        /// <exception cref="InvalidOperationException">When nobody is signed in.</exception>
        public static async Task<string> GetAccessTokenAsync()
        {
            try
            {
                await RestoreAsync();
            }
            catch (TwinAuthException ex) when (ex.IsNetworkFailure)
            {
                // Fall through: the token in hand may still be usable, and the
                // request about to be made will produce the better error anyway.
                Debug.Log($"[{nameof(TwinAuth)}] Session restore could not reach the server: {ex.Message}");
            }

            return await Session.GetAccessTokenAsync();
        }

        /// <summary>
        /// Put the bearer token on a request. This is how every authenticated
        /// call in the app should be made.
        /// </summary>
        /// <remarks>
        /// Ask for the token per request rather than holding the string: it lasts
        /// fifteen minutes, and this is the call that quietly renews it.
        /// <code>
        /// using var request = UnityWebRequest.Get($"{TwinAuth.BaseUrl}/v1/twins");
        /// await TwinAuth.AuthorizeAsync(request);
        /// await request.SendWebRequest();
        /// </code>
        /// </remarks>
        public static async Task<UnityWebRequest> AuthorizeAsync(UnityWebRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.SetRequestHeader("Authorization", $"Bearer {await GetAccessTokenAsync()}");
            return request;
        }

        /// <summary>The API origin, for building request URLs.</summary>
        public static string BaseUrl => Config.BaseUrl;

        /// <summary>
        /// Replace the session — for tests, which must not talk to the real
        /// server or touch the real PlayerPrefs token.
        /// </summary>
        /// <remarks>
        /// Pass <c>null</c> to tear the shared state down again, so one test
        /// cannot leave a session behind for the next one.
        /// </remarks>
        public static void OverrideForTests(TwinApiConfig config, TwinSession session)
        {
            if (_session != null)
            {
                _session.SignedIn -= RaiseSignedIn;
                _session.SignedOut -= RaiseSignedOut;
            }

            _config = config;
            _session = session;
            _restore = null;

            if (_session != null)
            {
                _session.SignedIn += RaiseSignedIn;
                _session.SignedOut += RaiseSignedOut;
            }
        }

        private static void RaiseSignedIn() => SignedIn?.Invoke();

        private static void RaiseSignedOut() => SignedOut?.Invoke();
    }
}
