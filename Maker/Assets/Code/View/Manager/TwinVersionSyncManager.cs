using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Code.Net.Auth;
using Code.Net.Twins;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// The sync screen for one twin: which of its versions are on the server, which exist only in this
/// app, and a button that puts the ticked ones up there.
///
/// Built like the other list panels - a manager on the Panel inside the ScrollRect, a row prefab
/// instantiated per entry, rows cleared and rebuilt rather than updated in place.
///
/// Two rules come from the server rather than from taste. A version that is already uploaded
/// cannot be ticked, because a second upload of the same twin name and version is refused
/// permanently. And nothing is offered at all while nobody is signed in: the button stays there
/// but disabled, with a line saying where to sign in, because a button that vanishes explains
/// nothing.
/// </summary>
public class TwinVersionSyncManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DataPersistenceManager dataManager;

    [Header("List")]
    [Tooltip("The Panel the rows are instantiated into. Leave empty to use this object.")]
    [SerializeField] private Transform rowContainer;
    [SerializeField] private GameObject rowPrefab;

    [Header("Controls")]
    [SerializeField] private Button uploadButton;
    [SerializeField] private Text statusText;

    [Tooltip("Optional: shows which twin is being synced.")]
    [SerializeField] private Text titleText;

    private const string TableName = "TwinLocalTables";

    // One mechanism for every string here, including the ones without arguments: several of these
    // need Arguments, and LocalizedString is what the project already uses for those - see
    // DateFormatter and Assets/Code/Localization/README.md.
    private const string KeyLocalOnly = "SYNC_STATE_LOCAL_ONLY";
    private const string KeyServerOnly = "SYNC_STATE_SERVER_ONLY";
    private const string KeyUploaded = "SYNC_STATE_UPLOADED";
    private const string KeyUploading = "SYNC_STATE_UPLOADING";
    private const string KeyFailed = "SYNC_STATE_FAILED";
    private const string KeySignInRequired = "SYNC_SIGN_IN_REQUIRED";
    private const string KeyLoading = "SYNC_LOADING";
    private const string KeySelectHint = "SYNC_SELECT_HINT";
    private const string KeyServerUnreachable = "SYNC_SERVER_UNREACHABLE";
    private const string KeyUploadDone = "SYNC_UPLOAD_DONE";
    private const string KeyUploadFailedSome = "SYNC_UPLOAD_FAILED_SOME";
    private const string KeyTooLarge = "SYNC_ARCHIVE_TOO_LARGE";
    private const string KeyTitle = "SYNC_TITLE";

    private readonly List<TwinVersionRow> rows = new List<TwinVersionRow>();
    private readonly TwinVersionsClient client = new TwinVersionsClient();

    /// <summary>True while a refresh or an upload is running; keeps a second one from starting.</summary>
    private bool busy;

    private string twinName;

    /// <summary>
    /// The data layer.
    /// </summary>
    /// <remarks>
    /// The serialized field stays empty in the prefab, because a prefab asset cannot hold a
    /// reference to a scene object - so this falls back to the singleton. Everything here goes
    /// through this property rather than the field: an upload once failed with a null reference
    /// because one call site out of three used the field directly.
    /// </remarks>
    private DataPersistenceManager Data =>
        dataManager != null ? dataManager : DataPersistenceManager.instance;

    private void OnEnable()
    {
        TwinAuth.SignedIn += OnAuthChanged;
        TwinAuth.SignedOut += OnAuthChanged;
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        Refresh();
    }

    private void OnDisable()
    {
        TwinAuth.SignedIn -= OnAuthChanged;
        TwinAuth.SignedOut -= OnAuthChanged;
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    /// <summary>Put this on the close button's OnClick list.</summary>
    public void Close()
    {
        InteractionController.EnableMode("Version");
    }

    /// <summary>Rebuild the list. Also on the refresh button, if there is one.</summary>
    public void Refresh() => _ = RefreshAsync();

    /// <summary>Put this on the upload button's OnClick list.</summary>
    public void UploadSelected() => _ = UploadSelectedAsync();

    private void OnAuthChanged() => Refresh();

    private void OnLocaleChanged(Locale locale) => Refresh();

    private async Task RefreshAsync()
    {
        if (busy) return;

        busy = true;
        try
        {
            twinName = CurrentTwinName();
            if (titleText != null) titleText.text = Localise(KeyTitle, twinName);

            Dictionary<string, ConfigData> local = LocalVersions(twinName);
            Dictionary<string, TwinVersionInfo> remote = null;
            string status;

            if (!TwinAuth.IsSignedIn)
            {
                // The local list is still worth showing - it is the honest answer to "what do I
                // have?", and it makes the disabled button understandable.
                status = Localise(KeySignInRequired);
            }
            else
            {
                SetStatus(Localise(KeyLoading));
                try
                {
                    List<TwinVersionInfo> found = await client.ListAllAsync(twinName);
                    remote = found.GroupBy(v => v.VersionName).ToDictionary(g => g.Key, g => g.First());
                    status = Localise(KeySelectHint);
                }
                catch (TwinApiException ex) when (ex.IsNetworkFailure)
                {
                    Debug.Log($"[{nameof(TwinVersionSyncManager)}] {ex.Message}");
                    status = Localise(KeyServerUnreachable);
                }
                catch (TwinApiException ex)
                {
                    Debug.LogError($"[{nameof(TwinVersionSyncManager)}] Listing failed: HTTP {ex.StatusCode} {ex.ErrorCode} - {ex.Message}");
                    status = Localise(KeyServerUnreachable);
                }
            }

            Build(local, remote);
            SetStatus(status);
        }
        finally
        {
            busy = false;
            UpdateUploadButton();
        }
    }

    /// <summary>
    /// One row per version, newest first, merged on the version name - which is the one thing the
    /// local profile id and the server row have in common.
    /// </summary>
    private void Build(Dictionary<string, ConfigData> local, Dictionary<string, TwinVersionInfo> remote)
    {
        Clear();

        Transform parent = rowContainer != null ? rowContainer : transform;
        if (rowPrefab == null)
        {
            Debug.LogError($"[{nameof(TwinVersionSyncManager)}] No row prefab assigned.", this);
            return;
        }

        IEnumerable<string> versions = local.Keys
            .Concat(remote != null ? remote.Keys : Enumerable.Empty<string>())
            .Distinct()
            .OrderByDescending(v => v, StringComparer.Ordinal);

        foreach (string version in versions)
        {
            GameObject instance = Instantiate(rowPrefab, parent, false);
            instance.transform.localScale = rowPrefab.transform.localScale;

            var row = instance.GetComponent<TwinVersionRow>();
            if (row == null)
            {
                Debug.LogError($"[{nameof(TwinVersionSyncManager)}] The row prefab carries no {nameof(TwinVersionRow)}.");
                Destroy(instance);
                continue;
            }

            row.Fill(twinName, version, Localise(KeyLocalOnly));

            TwinVersionInfo uploaded = null;
            if (remote != null && remote.TryGetValue(version, out uploaded))
            {
                bool onlyOnServer = !local.ContainsKey(version);
                row.SetState(TwinVersionRow.State.Uploaded, UploadedText(uploaded, onlyOnServer));
            }

            row.WhenSelectionChanges(_ => UpdateUploadButton());
            rows.Add(row);
        }
    }

    private async Task UploadSelectedAsync()
    {
        if (busy || !TwinAuth.IsSignedIn) return;

        List<TwinVersionRow> selected = rows.Where(r => r != null && r.Selected).ToList();
        if (selected.Count == 0) return;

        busy = true;
        UpdateUploadButton();

        int done = 0;
        string lastError = null;

        try
        {
            // One at a time on purpose: each archive is tens of megabytes, and three at once on a
            // tablet's connection is slower than three in a row as well as harder to report on.
            foreach (TwinVersionRow row in selected)
            {
                row.SetState(TwinVersionRow.State.Uploading, Localise(KeyUploading));
                SetStatus(Localise(KeyUploading));

                string error = await UploadOneAsync(row);
                if (error == null) done++;
                else lastError = error;
            }
        }
        finally
        {
            busy = false;
            UpdateUploadButton();
        }

        SetStatus(lastError == null
            ? Localise(KeyUploadDone, done)
            : Localise(KeyUploadFailedSome, done, selected.Count) + " " + lastError);
    }

    /// <summary>
    /// Upload one row's version. Returns null on success, or a line to show on failure.
    /// </summary>
    private async Task<string> UploadOneAsync(TwinVersionRow row)
    {
        string zipPath = null;
        try
        {
            if (Data == null)
            {
                Debug.LogError($"[{nameof(TwinVersionSyncManager)}] No DataPersistenceManager in the scene.");
                row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
                return Localise(KeyFailed);
            }

            zipPath = Data.ExportZipForVersion(row.ProfileId);
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
                return Localise(KeyFailed);
            }

            TwinVersionInfo uploaded = await client.UploadAsync(twinName, row.VersionName, File.ReadAllBytes(zipPath));
            row.SetState(TwinVersionRow.State.Uploaded, UploadedText(uploaded, false));
            return null;
        }
        catch (TwinApiException ex) when (ex.IsAlreadyExists)
        {
            // Someone else put it there between the listing and now. The row is right to show as
            // uploaded; this is not a failure the person has to do anything about.
            row.SetState(TwinVersionRow.State.Uploaded, Localise(KeyServerOnly));
            return null;
        }
        catch (TwinApiException ex) when (ex.IsArchiveTooLarge)
        {
            row.SetState(TwinVersionRow.State.Failed, Localise(KeyTooLarge));
            return Localise(KeyTooLarge);
        }
        catch (TwinApiException ex)
        {
            Debug.LogError($"[{nameof(TwinVersionSyncManager)}] Upload of {row.ProfileId} failed: HTTP {ex.StatusCode} {ex.ErrorCode} - {ex.Message}");
            row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
            return Localise(KeyFailed);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{nameof(TwinVersionSyncManager)}] Packing {row.ProfileId} failed: {ex.Message}");
            row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
            return Localise(KeyFailed);
        }
        finally
        {
            // The archive was only ever a carrier. Leaving it behind fills a tablet with copies
            // of every twin the user ever uploaded.
            TryDelete(zipPath);
        }
    }

    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException ex)
        {
            Debug.Log($"[{nameof(TwinVersionSyncManager)}] Could not remove the temporary archive: {ex.Message}");
        }
    }

    private string UploadedText(TwinVersionInfo info, bool onlyOnServer)
    {
        if (info == null) return Localise(KeyServerOnly);

        // Local time, not UTC: the person reading it is standing in a room, not in a data centre.
        string text = Localise(KeyUploaded, info.UploadedByEmail, info.UploadedAtUtc.ToLocalTime().DateTime);
        return onlyOnServer ? text + " " + Localise(KeyServerOnly) : text;
    }

    private void UpdateUploadButton()
    {
        if (uploadButton == null) return;

        uploadButton.interactable = !busy && TwinAuth.IsSignedIn && rows.Any(r => r != null && r.Selected);
    }

    private void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }

    private void Clear()
    {
        Transform parent = rowContainer != null ? rowContainer : transform;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }

        rows.Clear();
    }

    /// <summary>
    /// The twin whose versions this screen is about: the one currently open.
    /// </summary>
    /// <remarks>
    /// Taken from the selected profile id rather than from
    /// <see cref="InteractionController.Twin"/>, because the profile id is what the data layer
    /// itself uses and cannot be out of step with the twin that is actually loaded.
    /// </remarks>
    private string CurrentTwinName()
    {
        string profileId = Data != null ? Data.selectedProfileId : null;

        if (string.IsNullOrEmpty(profileId)) return string.Empty;

        int dot = profileId.LastIndexOf('.');
        return dot > 0 ? profileId.Substring(0, dot) : profileId;
    }

    private Dictionary<string, ConfigData> LocalVersions(string name)
    {
        if (Data == null || string.IsNullOrEmpty(name)) return new Dictionary<string, ConfigData>();

        return Data.GetAllVersionsGameData(name) ?? new Dictionary<string, ConfigData>();
    }

    private static string Localise(string key, params object[] arguments)
    {
        var localized = new LocalizedString(TableName, key);
        if (arguments != null && arguments.Length > 0) localized.Arguments = arguments;

        try
        {
            return localized.GetLocalizedString();
        }
        catch (Exception ex)
        {
            // A missing key must not take the screen down with it.
            Debug.LogWarning($"[{nameof(TwinVersionSyncManager)}] Could not resolve '{key}': {ex.Message}");
            return key;
        }
    }
}
