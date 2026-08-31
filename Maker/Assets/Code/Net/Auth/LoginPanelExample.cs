using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Net.Auth
{
    /// <summary>
    /// A working login screen, meant to be read and then replaced.
    /// </summary>
    /// <remarks>
    /// It is here as the shortest complete answer to "how do I wire this up?" —
    /// every call the library expects, in the order it expects them, with the
    /// error handling that actually matters. It is not a component to build a
    /// product on: there is no layout, no localisation, and no design.
    ///
    /// To try it: put this on a GameObject, drag in two TMP_InputFields, a Button
    /// and a TMP_Text, and press Play. With the backend running natively, sign in
    /// as <c>anna@example.com</c> / <c>correct-horse-battery-staple</c>.
    /// </remarks>
    public class LoginPanelExample : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TMP_InputField emailField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private Button signInButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Config")]
        [Tooltip("Leave empty to load from Resources/TwinApiConfig.")]
        [SerializeField] private TwinApiConfig apiConfig;

        private TwinSession _session;

        private async void Start()
        {
            var config = apiConfig != null ? apiConfig : TwinApiConfig.LoadDefault();
            _session = new TwinSession(new TwinAuthClient(config));

            // Anything that ends the session — an expired refresh token, a
            // revocation from another device — arrives here, whenever it happens.
            // Subscribing once at startup is what makes that reliable; handling it
            // at each call site would mean remembering to, every time.
            _session.SignedOut += OnSignedOut;

            signInButton.onClick.AddListener(() => _ = SignInAsync());

            await TryRestoreSessionAsync();
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.SignedOut -= OnSignedOut;
            }
        }

        /// <summary>
        /// On launch, try to carry on where the last run left off.
        /// </summary>
        private async Task TryRestoreSessionAsync()
        {
            SetBusy(true, "Checking your session…");
            try
            {
                if (await _session.TryRestoreAsync())
                {
                    ShowSignedIn();
                    return;
                }

                SetBusy(false, "Please sign in.");
            }
            catch (TwinAuthException ex) when (ex.IsNetworkFailure)
            {
                // Deliberately NOT a sign-out. The session may be perfectly valid
                // and the network merely absent; dropping it here would log people
                // out every time they open the app on a train.
                SetBusy(false, "Could not reach the server. Check your connection and try again.");
            }
        }

        private async Task SignInAsync()
        {
            var email = emailField.text?.Trim();
            var password = passwordField.text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetBusy(false, "Enter your email address and password.");
                return;
            }

            SetBusy(true, "Signing in…");

            try
            {
                await _session.SignInAsync(email, password);
                ShowSignedIn();
            }
            catch (TwinAuthException ex) when (ex.IsInvalidCredentials)
            {
                // One message for a wrong password AND an unknown address, because
                // the server gives us one answer for both on purpose — telling them
                // apart would turn this form into a way to discover who has an
                // account. Do not try to be more helpful here than the server is.
                SetBusy(false, "Email address or password is incorrect.");
                passwordField.text = string.Empty;
            }
            catch (TwinAuthException ex) when (ex.IsNetworkFailure)
            {
                // A different message, because it is a different problem and the
                // person can act on it: wait, check the connection, try again.
                SetBusy(false, "Could not reach the server. Check your connection and try again.");
            }
            catch (TwinAuthException ex)
            {
                // Anything else: a 500, a proxy in the way, a broken deployment.
                // The detail goes to the console, not to the person.
                Debug.LogError($"[TwinAuth] Unexpected sign-in failure: HTTP {ex.StatusCode} {ex.ErrorCode} — {ex.Message}");
                SetBusy(false, "Something went wrong. Please try again.");
            }
        }

        private void ShowSignedIn()
        {
            // OrganisationId is a UUID and has no display name yet — shown here
            // only because this is a wiring example.
            SetBusy(false, $"Signed in. User {_session.UserId}, organisation {_session.OrganisationId}.");
            passwordField.text = string.Empty;
        }

        private void OnSignedOut()
        {
            SetBusy(false, "Your session ended. Please sign in again.");
        }

        /// <summary>
        /// Disable the button while a request is in flight. Not cosmetic: login
        /// takes a few hundred milliseconds by design, which is long enough for
        /// an impatient second click.
        /// </summary>
        private void SetBusy(bool busy, string message)
        {
            if (signInButton != null)
            {
                signInButton.interactable = !busy;
            }

            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        /// <summary>
        /// How every other request in the app should get its token.
        /// </summary>
        /// <remarks>
        /// Ask the session each time rather than holding the string: the access
        /// token lasts 15 minutes, and this is the call that quietly renews it.
        ///
        /// <code>
        /// using var request = UnityWebRequest.Get($"{config.BaseUrl}/v1/twins");
        /// request.SetRequestHeader("Authorization", $"Bearer {await session.GetAccessTokenAsync()}");
        /// </code>
        /// </remarks>
        public Task<string> GetTokenForRequestAsync() => _session.GetAccessTokenAsync();
    }
}
