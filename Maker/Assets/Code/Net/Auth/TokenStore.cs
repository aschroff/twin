using UnityEngine;

namespace Code.Net.Auth
{
    /// <summary>
    /// Where the refresh token survives between runs.
    /// </summary>
    /// <remarks>
    /// An interface because the right answer differs per platform, and because
    /// the current answer is provisional (see <see cref="PlayerPrefsTokenStore"/>).
    /// Swapping the implementation must not touch <see cref="TwinSession"/>.
    /// </remarks>
    public interface ITokenStore
    {
        /// <summary>The stored refresh token, or <c>null</c> when there is none.</summary>
        string Load();

        void Save(string refreshToken);

        void Clear();
    }

    /// <summary>
    /// Stores the refresh token in <see cref="PlayerPrefs"/>.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Provisional — not suitable for a production build.</b> PlayerPrefs is
    /// a plist on iOS and the registry on Windows: unencrypted, and readable by
    /// anything with access to the device's file system. A stolen refresh token is
    /// a session, and it stays valid for 30 days.
    ///
    /// The intended replacement is the platform keychain — Keychain Services on
    /// iOS, Keystore on Android — which needs a native plugin, which is why it is
    /// not here yet. That work is one implementation of this interface; nothing
    /// else changes.
    ///
    /// Two things make the current state tolerable in the meantime: the token is
    /// revocable server-side, and it rotates on every use, so a copied token stops
    /// working as soon as the real client refreshes.
    ///
    /// The access token is deliberately <em>not</em> stored anywhere. It lives in
    /// memory only, dies with the process, and is cheap to obtain again.
    /// </remarks>
    public class PlayerPrefsTokenStore : ITokenStore
    {
        private const string Key = "twin.auth.refresh_token";

        public string Load()
        {
            var value = PlayerPrefs.GetString(Key, null);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        public void Save(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
            {
                Clear();
                return;
            }

            PlayerPrefs.SetString(Key, refreshToken);
            // Written immediately: without Save(), the value is only flushed when
            // the app quits normally, and a crash or a force-quit on iOS loses it.
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Keeps the token in memory only. For tests, and for "do not remember me".
    /// </summary>
    public class InMemoryTokenStore : ITokenStore
    {
        private string _token;

        public string Load() => _token;

        public void Save(string refreshToken) => _token = refreshToken;

        public void Clear() => _token = null;
    }
}
