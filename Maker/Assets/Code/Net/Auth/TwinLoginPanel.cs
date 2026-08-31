using System.Threading.Tasks;
using UnityEngine;
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

            ShowCurrentState();
        }

        private void OnDisable()
        {
            TwinAuth.SignedIn -= ShowSignedIn;
            TwinAuth.SignedOut -= ShowSignedOut;
        }

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
                SetBusy(false, "Enter your email address and password.");
                return;
            }

            SetBusy(true, "Signing in…");

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
                SetBusy(false, "Email address or password is incorrect.");
                ClearPassword();
            }
            catch (TwinAuthException ex) when (ex.IsNetworkFailure)
            {
                SetBusy(false, "Could not reach the server. Check your connection and try again.");
            }
            catch (TwinAuthException ex)
            {
                // A 500, a proxy in the way, a half-finished deployment. The
                // detail goes to the console; the person gets something they can
                // act on.
                Debug.LogError($"[{nameof(TwinLoginPanel)}] Sign-in failed: HTTP {ex.StatusCode} {ex.ErrorCode} — {ex.Message}");
                SetBusy(false, "Something went wrong. Please try again.");
            }
            catch (System.InvalidOperationException ex)
            {
                // No TwinApiConfig in Resources — a setup problem, not a user one.
                Debug.LogError($"[{nameof(TwinLoginPanel)}] {ex.Message}");
                SetBusy(false, "The app is not configured to reach the server.");
            }
        }

        private async Task SignOutAsync()
        {
            SetBusy(true, "Signing out…");
            await TwinAuth.SignOutAsync();
            ShowSignedOut();
        }

        private void ShowCurrentState()
        {
            if (TwinAuth.IsSignedIn) ShowSignedIn();
            else SetBusy(false, "Not signed in.");
        }

        private void ShowSignedIn()
        {
            ClearPassword();

            // The user id is a UUID and there is no endpoint that turns it into a
            // name yet, so it stays out of the message.
            SetBusy(false, "Signed in.");
            SetSignedInVisuals(true);
        }

        private void ShowSignedOut()
        {
            ClearPassword();
            SetBusy(false, "Not signed in.");
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
        private void SetBusy(bool busy, string message)
        {
            if (signInButton != null) signInButton.interactable = !busy;
            if (signOutButton != null) signOutButton.interactable = !busy;
            if (statusText != null) statusText.text = message;
        }
    }
}
