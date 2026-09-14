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
/// The download screen for one twin: which of its versions are on the server and not on this
/// device, and a button that fetches the ticked ones into the app.
///
/// The mirror image of <see cref="TwinVersionSyncManager"/>, down to the shape of the list - same
/// merge on the version name, same row prefab, same rule that a version the destination already
/// has is shown ticked and dead rather than hidden. What differs is the direction, and therefore
/// which single state can be ticked: there, a version this device has and the server does not;
/// here, one the server has and this device does not.
///
/// **Why this is a second manager rather than a flag on the first.** The two screens share their
/// list and share nothing else. Upload packs an archive, hands it to the server and is finished;
/// download fetches one, unpacks it through the app's ordinary import and thereby creates a twin
/// that was not there before. Folding both into one class would have meant two of every branch it
/// contains for the sake of the twenty lines that merge the list.
/// </summary>
public class TwinVersionDownloadManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DataPersistenceManager dataManager;

    [Header("List")]
    [Tooltip("The Panel the rows are instantiated into. Leave empty to use this object.")]
    [SerializeField] private Transform rowContainer;
    [SerializeField] private GameObject rowPrefab;

    [Header("Controls")]
    [SerializeField] private Button downloadButton;
    [SerializeField] private Text statusText;

    [Tooltip("Optional: shows which twin is being downloaded.")]
    [SerializeField] private Text titleText;

    private const string TableName = "TwinLocalTables";

    private const string KeyTitle = "SYNC_TITLE";
    private const string KeyLocalOnly = "SYNC_STATE_LOCAL_ONLY";
    private const string KeyUploaded = "SYNC_STATE_UPLOADED";
    private const string KeyOnDevice = "SYNC_STATE_ON_DEVICE";
    private const string KeyDownloading = "SYNC_STATE_DOWNLOADING";
    private const string KeyFailed = "SYNC_STATE_DOWNLOAD_FAILED";
    private const string KeySignInRequired = "SYNC_SIGN_IN_DOWNLOAD";
    private const string KeyLoading = "SYNC_LOADING";
    private const string KeySelectHint = "SYNC_DOWNLOAD_SELECT_HINT";
    private const string KeyServerUnreachable = "SYNC_SERVER_UNREACHABLE";
    private const string KeyDownloadDone = "SYNC_DOWNLOAD_DONE";
    private const string KeyDownloadFailedSome = "SYNC_DOWNLOAD_FAILED_SOME";
    private const string KeyUnavailable = "SYNC_DOWNLOAD_UNAVAILABLE";

    private readonly List<TwinVersionRow> rows = new List<TwinVersionRow>();
    private readonly TwinVersionsClient client = new TwinVersionsClient();

    /// <summary>
    /// Which server entry each row stands for. Kept here rather than on the row, because the id is
    /// the only thing a download needs that a row has no other use for - the server addresses an
    /// archive by id, not by the name and version the row shows.
    /// </summary>
    private readonly Dictionary<TwinVersionRow, TwinVersionInfo> remoteOf =
        new Dictionary<TwinVersionRow, TwinVersionInfo>();

    /// <summary>True while a refresh or a download is running; keeps a second one from starting.</summary>
    private bool busy;

    private string twinName;

    /// <summary>
    /// The data layer. Same reasoning as on the upload screen: a prefab asset cannot hold a
    /// reference to a scene object, so the serialized field stays empty and this falls back to the
    /// singleton. Every call site goes through the property.
    /// </summary>
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

    /// <summary>Rebuild the list.</summary>
    public void Refresh() => _ = RefreshAsync();

    /// <summary>Put this on the download button's OnClick list.</summary>
    public void DownloadSelected() => _ = DownloadSelectedAsync();

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
                // The local list is still worth showing, for the same reason as on the upload
                // screen: it is the honest answer to "what do I have?", and it makes the disabled
                // button understandable.
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
                    Debug.Log($"[{nameof(TwinVersionDownloadManager)}] {ex.Message}");
                    status = Localise(KeyServerUnreachable);
                }
                catch (TwinApiException ex)
                {
                    Debug.LogError($"[{nameof(TwinVersionDownloadManager)}] Listing failed: HTTP {ex.StatusCode} {ex.ErrorCode} - {ex.Message}");
                    status = Localise(KeyServerUnreachable);
                }
            }

            Build(local, remote);
            SetStatus(status);
        }
        finally
        {
            busy = false;
            UpdateDownloadButton();
        }
    }

    /// <summary>
    /// One row per version, newest first, merged on the version name.
    /// </summary>
    /// <remarks>
    /// Three outcomes, and only the first is an offer:
    /// <list type="bullet">
    /// <item>on the server and not here - downloadable;</item>
    /// <item>on the server and here as well - shown, ticked and dead, because fetching it again
    /// would land beside it as a <c>V01</c> rather than replace it;</item>
    /// <item>only here - shown for reference, so the list answers "what do I have?" and not only
    /// "what could I fetch?". Without it a twin with one local version and nothing on the server
    /// would open an empty screen that looks broken.</item>
    /// </list>
    /// </remarks>
    private void Build(Dictionary<string, ConfigData> local, Dictionary<string, TwinVersionInfo> remote)
    {
        Clear();

        Transform parent = rowContainer != null ? rowContainer : transform;
        if (rowPrefab == null)
        {
            Debug.LogError($"[{nameof(TwinVersionDownloadManager)}] No row prefab assigned.", this);
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
                Debug.LogError($"[{nameof(TwinVersionDownloadManager)}] The row prefab carries no {nameof(TwinVersionRow)}.");
                Destroy(instance);
                continue;
            }

            row.SetRole(TwinVersionRow.Role.Download);
            row.Fill(twinName, version, Localise(KeyLocalOnly));

            bool here = local.ContainsKey(version);
            TwinVersionInfo uploaded = null;
            bool there = remote != null && remote.TryGetValue(version, out uploaded);

            if (there)
            {
                remoteOf[row] = uploaded;
                row.SetState(
                    here ? TwinVersionRow.State.Downloaded : TwinVersionRow.State.ServerOnly,
                    RemoteText(uploaded, here));
            }
            else
            {
                // Fill already put it in LocalOnly; saying so again is what ticks and dims the box.
                row.SetState(TwinVersionRow.State.LocalOnly, Localise(KeyLocalOnly));
            }

            row.WhenSelectionChanges(_ => UpdateDownloadButton());
            rows.Add(row);
        }
    }

    private async Task DownloadSelectedAsync()
    {
        if (busy || !TwinAuth.IsSignedIn) return;

        List<TwinVersionRow> selected = rows.Where(r => r != null && r.Selected).ToList();
        if (selected.Count == 0) return;

        busy = true;
        UpdateDownloadButton();

        int done = 0;
        string lastError = null;

        try
        {
            // One at a time, for the same reason the upload is: each archive is tens of megabytes,
            // and three at once on a tablet's connection is slower as well as harder to report on.
            foreach (TwinVersionRow row in selected)
            {
                row.SetState(TwinVersionRow.State.Downloading, Localise(KeyDownloading));
                SetStatus(Localise(KeyDownloading));

                string error = await DownloadOneAsync(row);
                if (error == null) done++;
                else lastError = error;
            }
        }
        finally
        {
            busy = false;
            UpdateDownloadButton();
        }

        SetStatus(lastError == null
            ? Localise(KeyDownloadDone, done)
            : Localise(KeyDownloadFailedSome, done, selected.Count) + " " + lastError);
    }

    /// <summary>
    /// Fetch one row's version and import it. Returns null on success, or a line to show on failure.
    /// </summary>
    /// <remarks>
    /// The archive goes through a file rather than straight into the import, because the import is
    /// <c>DataPersistenceManager.ImportConfig(path)</c> - the same one the file picker uses, which
    /// is the point. A download that unpacked archives its own way would be a second import to keep
    /// in step with the first, and the version-suffix rule alone is more than enough reason not to
    /// have two of those.
    /// </remarks>
    private async Task<string> DownloadOneAsync(TwinVersionRow row)
    {
        string zipPath = null;
        try
        {
            if (Data == null)
            {
                Debug.LogError($"[{nameof(TwinVersionDownloadManager)}] No DataPersistenceManager in the scene.");
                row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
                return Localise(KeyFailed);
            }

            if (!remoteOf.TryGetValue(row, out TwinVersionInfo info) || info == null || string.IsNullOrEmpty(info.Id))
            {
                // Only a server row can be ticked, so this is a bug rather than a state a person
                // can reach.
                Debug.LogError($"[{nameof(TwinVersionDownloadManager)}] {row.ProfileId} was selectable without a server entry.");
                row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
                return Localise(KeyFailed);
            }

            byte[] archive = await client.DownloadArchiveAsync(info.Id);

            zipPath = Path.Combine(Application.temporaryCachePath, $"{info.TwinName}.{info.VersionName}.zip");
            File.WriteAllBytes(zipPath, archive);

            string imported = Data.ImportConfig(zipPath);
            if (string.IsNullOrEmpty(imported))
            {
                // The bytes were fine - the digest was checked before they got here - so this is
                // the unpacking or the twin store, and it has already logged why.
                row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
                return Localise(KeyFailed);
            }

            row.SetState(TwinVersionRow.State.Downloaded, RemoteText(info, true));
            return null;
        }
        catch (TwinApiException ex) when (ex.IsArchiveNotStored || ex.IsServiceUnavailable)
        {
            // Two different reasons - the archive was deleted, or the server cannot reach its
            // storage - with the same sentence, because neither is anything the person can act on
            // beyond trying later.
            Debug.LogError($"[{nameof(TwinVersionDownloadManager)}] {row.ProfileId} is not available: HTTP {ex.StatusCode} {ex.ErrorCode} - {ex.Message}");
            row.SetState(TwinVersionRow.State.Failed, Localise(KeyUnavailable));
            return Localise(KeyUnavailable);
        }
        catch (TwinApiException ex)
        {
            Debug.LogError($"[{nameof(TwinVersionDownloadManager)}] Download of {row.ProfileId} failed: HTTP {ex.StatusCode} {ex.ErrorCode} - {ex.Message}");
            row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
            return Localise(KeyFailed);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{nameof(TwinVersionDownloadManager)}] Importing {row.ProfileId} failed: {ex.Message}");
            row.SetState(TwinVersionRow.State.Failed, Localise(KeyFailed));
            return Localise(KeyFailed);
        }
        finally
        {
            // The archive was only ever a carrier - the import has copied what it needs out of it.
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
            Debug.Log($"[{nameof(TwinVersionDownloadManager)}] Could not remove the temporary archive: {ex.Message}");
        }
    }

    /// <summary>Who put this version on the server and when, plus whether it is also here.</summary>
    private string RemoteText(TwinVersionInfo info, bool alsoHere)
    {
        if (info == null) return Localise(KeyOnDevice);

        // Local time, not UTC: the person reading it is standing in a room, not in a data centre.
        string text = Localise(KeyUploaded, info.UploadedByEmail, info.UploadedAtUtc.ToLocalTime().DateTime);
        return alsoHere ? text + " " + Localise(KeyOnDevice) : text;
    }

    private void UpdateDownloadButton()
    {
        if (downloadButton == null) return;

        downloadButton.interactable = !busy && TwinAuth.IsSignedIn && rows.Any(r => r != null && r.Selected);
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
        remoteOf.Clear();
    }

    /// <summary>
    /// The twin whose versions this screen is about: the one currently open. Taken from the
    /// selected profile id, as on the upload screen, because that is what the data layer uses and
    /// it cannot be out of step with the twin that is actually loaded.
    /// </summary>
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
            Debug.LogWarning($"[{nameof(TwinVersionDownloadManager)}] Could not resolve '{key}': {ex.Message}");
            return key;
        }
    }
}
