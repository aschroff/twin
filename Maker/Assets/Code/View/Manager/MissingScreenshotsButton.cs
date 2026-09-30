using Code;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// The "create missing images" button under the part list on the group detail panel.
/// </summary>
/// <remarks>
/// <para>A part only gets its screenshot while an AI summary runs, so parts painted since the
/// last summary - or on a twin that never had one - show the placeholder instead of a picture,
/// here and in the part detail. This button fills those gaps without asking for a summary.</para>
///
/// <para><b>Why a button and not something automatic.</b> The run takes the screen: it hides the
/// whole canvas (<see cref="Recorder.Prepare"/>), moves the camera to each part's stored view and
/// isolates that part on the shared body, for about a second per part. That is not a flicker one
/// can engineer away - the isolation changes what the user is looking at - so it happens when the
/// user asks for it and not behind their back.</para>
///
/// <para><b>The run itself is <see cref="PartsScreenshotProcess"/>, unchanged.</b> It already
/// skips every part whose file is on disk, so "create the missing ones" is simply what it does
/// when it is started again. This component only counts them first - so the label can say what a
/// press will cost, and so a press with nothing missing does not blank the screen for nothing -
/// and puts the panel back in step with the files afterwards.</para>
///
/// <para>The process is looked up rather than serialized: it is a scene object while this
/// component lives in the prefab, so a <c>[SerializeField]</c> would have to be an override on
/// the instance in <c>Maker Main.unity</c>, and every save of that scene writes ~200 lines of
/// driven RectTransform noise (APP_DOCUMENTATION §9).</para>
/// </remarks>
public class MissingScreenshotsButton : MonoBehaviour
{
    private const string TableName = "TwinLocalTables";
    private const string LabelKey = "GROUP_DETAILS_CREATE_IMAGES";

    /// <summary>The alpha every other button on this panel is drawn with.</summary>
    private const float AlphaEnabled = 0.5f;
    private const float AlphaDisabled = 0.2f;

    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private CanvasGroup canvasGroup;

    private PartsScreenshotProcess process;
    private int missing;
    private bool running;

    /// <summary>How many parts the next press would shoot.</summary>
    public int Missing { get { return missing; } }

    /// <summary>True from the press until the run has put the panel back.</summary>
    public bool Running { get { return running; } }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
    }

    private void OnDestroy()
    {
        if (process != null)
        {
            process.ExecuteCompleted -= HandleCompleted;
        }
    }

    /// <summary>
    /// Counts the parts without a screenshot again and redraws the button.
    /// </summary>
    /// <remarks>Called whenever the panel comes up, which covers every way the number can have
    /// changed while it was away - a part painted, a part deleted, another twin selected.</remarks>
    public void Refresh()
    {
        PartsScreenshotProcess screenshots = ScreenshotProcess();
        missing = screenshots != null ? screenshots.CountMissingScreenshots() : 0;
        ApplyState();
    }

    /// <summary>
    /// Shoots the parts that have no screenshot yet. Wired to the button's own click.
    /// </summary>
    /// <remarks>The panel is hidden by the run and comes back with it, so a second press cannot
    /// arrive while one is going; <see cref="running"/> guards the case anyway, because the event
    /// this subscribes to would otherwise be subscribed to twice.</remarks>
    public void CreateMissing()
    {
        if (running || missing <= 0)
        {
            return;
        }

        PartsScreenshotProcess screenshots = ScreenshotProcess();
        if (screenshots == null)
        {
            return;
        }

        running = true;
        ApplyState();

        screenshots.ExecuteCompleted += HandleCompleted;
        screenshots.ExecuteSync();
    }

    private void HandleCompleted()
    {
        if (process != null)
        {
            process.ExecuteCompleted -= HandleCompleted;
        }

        running = false;
        Refresh();
    }

    private void HandleLocaleChanged(Locale locale)
    {
        ApplyState();
    }

    private void ApplyState()
    {
        bool usable = missing > 0 && !running;

        if (button != null)
        {
            button.interactable = usable;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = usable ? AlphaEnabled : AlphaDisabled;
        }

        if (label != null)
        {
            label.text = Localise(LabelKey) + " (" + missing + ")";
        }
    }

    /// <summary>The scene's screenshot process, found once and kept.</summary>
    private PartsScreenshotProcess ScreenshotProcess()
    {
        if (process == null)
        {
            process = Object.FindFirstObjectByType<PartsScreenshotProcess>();

            if (process == null)
            {
                Debug.LogWarning("[" + nameof(MissingScreenshotsButton)
                                 + "] No PartsScreenshotProcess in the scene - nothing to press.");
            }
        }

        return process;
    }

    /// <summary>
    /// Look a key up in the current locale.
    /// </summary>
    /// <remarks>Not <see cref="StringLocalizer"/>, which writes a console line on every
    /// successful lookup - see Assets/Code/Localization/README.md §3.</remarks>
    private static string Localise(string key)
    {
        var table = LocalizationSettings.StringDatabase?.GetTable(TableName);
        var entry = table?.GetEntry(key);

        if (entry != null) return entry.GetLocalizedString();

        Debug.LogWarning($"[{nameof(MissingScreenshotsButton)}] No entry '{key}' in {TableName}.");
        return key;
    }
}
