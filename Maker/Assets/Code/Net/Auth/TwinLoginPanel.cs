using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Code.Net.Auth
{
    /// <summary>
    /// The sign-in row on the settings page.
    /// </summary>
    /// <remarks>
    /// A view over <see cref="TwinAuth"/> and nothing more. It does not own the
    /// session, does not keep a token and holds no state of its own beyond what
    /// is on screen — so it can be enabled, disabled and destroyed with the
    /// settings page without any of that touching the signed-in state.
    ///
    /// That separation is the point. The settings page is toggled by the mode
    /// system, and its <c>Start</c> does not run until someone first opens it; a
    /// session living here would not exist for any data call made before then.
    /// </remarks>
    public class TwinLoginPanel : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputField emailField;
        [SerializeField] private InputField passwordField;

        [Header("Buttons")]
        [SerializeField] private Button signInButton;
        [SerializeField] private Button signOutButton;

        [Header("Feedback")]
        [SerializeField] private Text statusText;

        /// <summary>
        /// Remembers the address between visits, so only the password has to be
        /// typed again. Never the password: this is an unencrypted plist on iOS
        /// and the registry on Windows.
        /// </summary>
        private const string LastEmailKey = "twin.auth.last_email";

        /// <summary>The one string table collection this app has.</summary>
        private const string TableName = "TwinLocalTables";

        // Keys rather than sentences, because the app ships in five locales — two
        // languages in three medical registers. Which of them is showing is not
        // this class's business; it only says *which* message.
        private const string KeyEnterCredentials = "LOGIN_ENTER_CREDENTIALS";
        private const string KeySigningIn = "LOGIN_SIGNING_IN";
        private const string KeySigningOut = "LOGIN_SIGNING_OUT";
        private const string KeySignedIn = "LOGIN_SIGNED_IN";
        private const string KeySignedOut = "LOGIN_SIGNED_OUT";
        private const string KeyInvalidCredentials = "LOGIN_INVALID_CREDENTIALS";
        private const string KeyServerUnreachable = "LOGIN_SERVER_UNREACHABLE";
        private const string KeyUnexpectedError = "LOGIN_UNEXPECTED_ERROR";
        private const string KeyNotConfigured = "LOGIN_NOT_CONFIGURED";

        /// <summary>
        /// The message currently on screen, kept as its key so it can be
        /// re-resolved when the language changes underneath it.
        /// </summary>
        private string _messageKey;

        private void Awake()
        {
            if (passwordField != null)
            {
                // Set in code as well as in the inspector, so a prefab edit cannot
                // quietly turn the password field back into a visible one.
                passwordField.contentType = InputField.ContentType.Password;
                passwordField.ForceLabelUpdate();
            }

            if (emailField != null)
            {
                emailField.text = PlayerPrefs.GetString(LastEmailKey, string.Empty);
            }
        }

        private void OnEnable()
        {
            // Static events outlive this object, hence the matching OnDisable.
            TwinAuth.SignedIn += ShowSignedIn;
            TwinAuth.SignedOut += ShowSignedOut;

            // The settings page is where the language is switched, so a status
            // line sitting here in the old language is not hypothetical.
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

            ShowCurrentState();
        }

        private void OnDisable()
        {
            TwinAuth.SignedIn -= ShowSignedIn;
            TwinAuth.SignedOut -= ShowSignedOut;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale locale) => ApplyMessage();

        /// <summary>
        /// Put this on the sign-in Button's <b>OnClick</b> list.
        /// </summary>
        /// <remarks>
        /// <para>A <c>void</c> with no arguments, because that is all Unity's
        /// inspector can call — an <c>async Task</c> method does not appear in the
        /// list at all. The work is started and deliberately not awaited: a click
        /// handler has nobody to return to.</para>
        ///
        /// <para>Nothing is thrown out of here. Every failure that a person can do
        /// something about is turned into a line of status text by
        /// <see cref="SignInAsync"/>; an exception escaping an <c>async void</c>
        /// would be swallowed by Unity and shown to nobody.</para>
        /// </remarks>
        public void SignIn() => _ = SignInAsync();

        /// <summary>Put this on the sign-out Button's <b>OnClick</b> list.</summary>
        public void SignOut() => _ = SignOutAsync();

        private async Task SignInAsync()
        {
            var email = emailField != null ? emailField.text?.Trim() : null;
            var password = passwordField != null ? passwordField.text : null;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                SetBusy(false, KeyEnterCredentials);
                return;
            }

            SetBusy(true, KeySigningIn);

            try
            {
                await TwinAuth.SignInAsync(email, password);

                PlayerPrefs.SetString(LastEmailKey, email);
                PlayerPrefs.Save();
                ShowSignedIn();
            }
            catch (TwinAuthException ex) when (ex.IsInvalidCredentials)
            {
                // One message for a wrong password and for an address that does
                // not exist, because the server answers identically for both on
                // purpose. Being more specific here would turn this form into a
                // way to find out who has an account.
                SetBusy(false, KeyInvalidCredentials);
                ClearPassword();
            }
            catch (TwinAuthException ex) when (ex.IsNetworkFailure)
            {
                // The person gets a sentence they can act on; the console gets the
                // reason. A wrong port, https where the server speaks http, a proxy
                // in the way and a server that is simply off all produce the same
                // sentence on screen — and, without this line, the same silence.
                Debug.Log($"[{nameof(TwinLoginPanel)}] {ex.Message}");
                SetBusy(false, KeyServerUnreachable);
            }
            catch (TwinAuthException ex)
            {
                // A 500, a proxy in the way, a half-finished deployment. The
                // detail goes to the console; the person gets something they can
                // act on.
                Debug.LogError($"[{nameof(TwinLoginPanel)}] Sign-in failed: HTTP {ex.StatusCode} {ex.ErrorCode} — {ex.Message}");
                SetBusy(false, KeyUnexpectedError);
            }
            catch (System.InvalidOperationException ex)
            {
                // No TwinApiConfig in Resources — a setup problem, not a user one.
                Debug.LogError($"[{nameof(TwinLoginPanel)}] {ex.Message}");
                SetBusy(false, KeyNotConfigured);
            }
        }

        private async Task SignOutAsync()
        {
            SetBusy(true, KeySigningOut);
            await TwinAuth.SignOutAsync();
            ShowSignedOut();
        }

        private void ShowCurrentState()
        {
            if (TwinAuth.IsSignedIn) ShowSignedIn();
            else SetBusy(false, KeySignedOut);
        }

        private void ShowSignedIn()
        {
            ClearPassword();

            // The user id is a UUID and there is no endpoint that turns it into a
            // name yet, so it stays out of the message.
            SetBusy(false, KeySignedIn);
            SetSignedInVisuals(true);
        }

        private void ShowSignedOut()
        {
            ClearPassword();
            SetBusy(false, KeySignedOut);
            SetSignedInVisuals(false);
        }

        private void SetSignedInVisuals(bool signedIn)
        {
            if (signOutButton != null) signOutButton.gameObject.SetActive(signedIn);
            if (signInButton != null) signInButton.gameObject.SetActive(!signedIn);
            if (passwordField != null) passwordField.gameObject.SetActive(!signedIn);
        }

        private void ClearPassword()
        {
            if (passwordField != null) passwordField.text = string.Empty;
        }

        /// <summary>
        /// Disable the button while a request is in flight. Not cosmetic: the
        /// server hashes the password with argon2 and takes a few hundred
        /// milliseconds by design, which is long enough for a second click.
        /// </summary>
        private void SetBusy(bool busy, string messageKey)
        {
            if (signInButton != null) signInButton.interactable = !busy;
            if (signOutButton != null) signOutButton.interactable = !busy;

            _messageKey = messageKey;
            ApplyMessage();
        }

        private void ApplyMessage()
        {
            if (statusText == null || _messageKey == null) return;

            statusText.text = Localise(_messageKey);
        }

        /// <summary>
        /// Look a key up in the current locale.
        /// </summary>
        /// <remarks>
        /// Not <see cref="StringLocalizer"/>, which writes a line to the console on
        /// every successful lookup — that would narrate every status change. A
        /// missing key is worth one warning and then the key itself, which is more
        /// useful on screen than an empty line.
        /// </remarks>
        private static string Localise(string key)
        {
            var table = LocalizationSettings.StringDatabase?.GetTable(TableName);
            var entry = table?.GetEntry(key);

            if (entry != null) return entry.GetLocalizedString();

            Debug.LogWarning($"[{nameof(TwinLoginPanel)}] No entry '{key}' in {TableName}.");
            return key;
        }
    }
}
