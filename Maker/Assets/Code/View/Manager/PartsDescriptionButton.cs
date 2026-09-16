using System.Linq;
using Code;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// The two "describe the parts" buttons under the part list on the group detail panel.
/// </summary>
/// <remarks>
/// <para>Same shape as <see cref="MissingScreenshotsButton"/> next to it, and for the same reason:
/// the label says how many parts a press would work on, so nobody taps a button that would do
/// nothing, and the button is dead while its run is going.</para>
///
/// <para><b>Two instances, one script.</b> <see cref="forced"/> picks which of the two
/// <see cref="Code.PartsDescriptionProcess"/> objects in the scene it drives - the one that fills
/// the gaps, or the one that describes everything again. Nothing else differs.</para>
///
/// <para><b>Both count only parts that have a screenshot.</b> The model is asked about the picture
/// of a part, so a part without one cannot be described however often it is asked for. Those parts
/// belong to the images button above; press that first and this count goes up by itself. That is
/// also why pressing this does not quietly start a screenshot run: that run hides the whole canvas
/// for about a second per part, and it happens when the user asks for it - see
/// <see cref="MissingScreenshotsButton"/>.</para>
///
/// <para>The twin, not the group: the count and the run cover every part of the open twin, exactly
/// as the images button above already does.</para>
/// </remarks>
public class PartsDescriptionButton : MonoBehaviour
{
    private const string TableName = "TwinLocalTables";

    /// <summary>Label of the button that only fills the gaps.</summary>
    public const string LabelKeyMissing = "GROUP_DETAILS_DESCRIBE_MISSING";

    /// <summary>Label of the button that describes everything again.</summary>
    public const string LabelKeyAll = "GROUP_DETAILS_DESCRIBE_ALL";

    /// <summary>The prompt row the descriptions are asked with - the one the Help menu uses too.</summary>
    private const string Variant = "Part Description";

    /// <summary>The alpha every other button on this panel is drawn with.</summary>
    private const float AlphaEnabled = 0.5f;
    private const float AlphaDisabled = 0.2f;

    [Tooltip("Off: describe parts that have no description yet. On: describe every part again.")]
    [SerializeField] private bool forced;

    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private CanvasGroup canvasGroup;

    private Code.PartsDescriptionProcess process;
    private int describable;
    private bool running;

    /// <summary>How many parts the next press would describe.</summary>
    public int Describable { get { return describable; } }

    /// <summary>True from the press until the run has come back.</summary>
    public bool Running { get { return running; } }

    /// <summary>Which of the two processes this button drives.</summary>
    public bool Forced { get { return forced; } }

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
    /// Counts the parts again and redraws the button.
    /// </summary>
    /// <remarks>Called whenever the panel comes up, which covers every way the number can have
    /// changed while it was away - a part painted, a description typed, images created.</remarks>
    public void Refresh()
    {
        Code.PartsDescriptionProcess descriptions = DescriptionProcess();
        describable = descriptions != null ? descriptions.CountDescribable() : 0;
        ApplyState();
    }

    /// <summary>Describes the parts. Wired to the button's own click.</summary>
    public void Describe()
    {
        if (running || describable <= 0 || AnyRunning())
        {
            return;
        }

        Code.PartsDescriptionProcess descriptions = DescriptionProcess();
        if (descriptions == null)
        {
            return;
        }

        running = true;
        ApplyState();

        descriptions.ExecuteCompleted += HandleCompleted;
        descriptions.ExecuteSync(Variant);

        // the other button drives its own process and would happily start a second run on top of
        // this one - describing the same parts twice. Redrawing both takes it out of reach.
        RefreshAll();
    }

    /// <summary>Whether a description run is going at all, whichever button started it.</summary>
    private static bool AnyRunning()
    {
        return FindObjectsByType<Code.PartsDescriptionProcess>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Any(process => process.Running);
    }

    private static void RefreshAll()
    {
        foreach (PartsDescriptionButton button in
                 FindObjectsByType<PartsDescriptionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            button.Refresh();
        }
    }

    private void HandleCompleted()
    {
        if (process != null)
        {
            process.ExecuteCompleted -= HandleCompleted;
        }

        running = false;

        // both buttons, not only this one: describing parts changes what the other one would do
        RefreshAll();
    }

    private void HandleLocaleChanged(Locale locale)
    {
        ApplyState();
    }

    private void ApplyState()
    {
        bool usable = describable > 0 && !running && !AnyRunning();

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
            label.text = Localise(forced ? LabelKeyAll : LabelKeyMissing) + " (" + describable + ")";
        }
    }

    /// <summary>The scene's process for this button's variant, found once and kept.</summary>
    private Code.PartsDescriptionProcess DescriptionProcess()
    {
        if (process == null)
        {
            process = FindObjectsByType<Code.PartsDescriptionProcess>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.hardRedo == forced);

            if (process == null)
            {
                Debug.LogWarning("[" + nameof(PartsDescriptionButton) + "] No PartsDescriptionProcess with"
                                 + " hardRedo == " + forced + " in the scene - nothing to press.");
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

        Debug.LogWarning($"[{nameof(PartsDescriptionButton)}] No entry '{key}' in {TableName}.");
        return key;
    }
}
