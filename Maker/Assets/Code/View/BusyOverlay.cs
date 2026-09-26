using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tells the user that the app is working, and keeps them from tapping into it meanwhile.
/// </summary>
/// <remarks>
/// <para>Several things in this app take seconds: switching a twin takes about two, imports,
/// exports and the AI reports take longer. Until now none of them said anything, so there was no
/// way to tell a slow operation from a hung one — and an impatient second tap started the whole
/// thing again, because the touch was simply delivered on the next frame.</para>
///
/// <para><b>Two kinds of slow, and they need different handling.</b> Work that blocks the main
/// thread renders no frames at all while it runs: nothing can animate, and a panel shown in the
/// same frame the work starts is never drawn. <see cref="RunBlocking"/> therefore puts the panel
/// up, lets one frame render, and only then does the work. Work that leaves the main thread free —
/// a download, an await — can use <see cref="Show"/> and <see cref="Hide"/>, where the panel
/// behaves normally.</para>
///
/// <para>The panel deliberately does not slide in the way the notification toast does. A slide
/// needs frames, and before a blocking operation there are none; it would freeze halfway or never
/// appear. It leaves with a fade instead, which is safe because the work is done by then.</para>
///
/// <para>A missing prefab must never stop the app from working: the overlay then logs a warning
/// and the operation runs without it.</para>
/// </remarks>
public class BusyOverlay : MonoBehaviour
{
    /// <summary>In <c>Assets/Resources/</c>, so no scene or prefab instance has to be touched to
    /// have it available - see APP_DOCUMENTATION §9 on why that matters.</summary>
    public const string PrefabName = "BusyOverlay";

    [SerializeField] private Text label;
    [SerializeField] private CanvasGroup canvasGroup;

    /// <summary>The box the message sits in. Hidden while the logo is up - the two occupy the
    /// same place on screen.</summary>
    [SerializeField] private GameObject box;

    /// <summary>The app's logo, shown instead of a message while the model is being asked.</summary>
    [SerializeField] private RawImage logo;

    /// <summary>
    /// Drawings of the logo that are played while the model is being asked - the dog wagging its
    /// tail.
    /// </summary>
    /// <remarks>
    /// <para>Played there and back again (1, 2, 3, 4, 3, 2, ...), so four drawings from one
    /// extreme of the wag to the other give a six step cycle with no jump back to the start.
    /// Everything but the tail has to be identical between them or the dog twitches.</para>
    ///
    /// <para>Empty is fine: the logo then simply stands still, which is what it did before there
    /// were any frames.</para>
    /// </remarks>
    [SerializeField] private Texture2D[] logoFrames;

    /// <summary>How long one drawing is shown. A tenth of a second reads as a happy wag.</summary>
    [SerializeField] private float logoFrameSeconds = 0.1f;

    private Coroutine wag;

    private static BusyOverlay instance;
    private Coroutine fade;
    private static bool working;
    private static int thinking;

    /// <summary>True from the moment blocking work is asked for until it has finished - not
    /// including the fade that follows. Anything that has to wait for the work itself, a test
    /// above all, should wait on this rather than on the panel being visible.</summary>
    public static bool Working { get { return working; } }

    /// <summary>True while the user is being told the app is busy.</summary>
    public static bool Visible
    {
        get { return instance != null && instance.canvasGroup != null && instance.canvasGroup.alpha > 0f; }
    }

    /// <summary>Whether taps are being swallowed. While the main thread is blocked a touch is
    /// only delivered on the next frame, so without this an impatient second tap would start the
    /// whole operation again the moment the first one finished.</summary>
    public static bool BlocksInput
    {
        get { return instance != null && instance.canvasGroup != null && instance.canvasGroup.blocksRaycasts; }
    }

    /// <summary>What the panel currently says. Empty when it is not up.</summary>
    public static string Message
    {
        get { return Visible && instance.label != null ? instance.label.text : string.Empty; }
    }

    /// <summary>
    /// Runs work that blocks the main thread, with the panel up while it does.
    /// </summary>
    /// <remarks>The frame between showing and working is the whole point: without it the panel
    /// would only be drawn after the work had already finished, and the user would see the freeze
    /// and no explanation.</remarks>
    /// <param name="onDone">Runs after the work, on the same frame. Callers that used to do
    /// something straight after the call need this - without it their code would run while the
    /// work is still a frame away.</param>
    public static void RunBlocking(string message, Action work, Action onDone = null)
    {
        if (work == null) return;

        BusyOverlay overlay = Instance();
        if (overlay == null)
        {
            working = true;
            try { work(); } finally { working = false; }
            if (onDone != null) onDone();
            return;
        }

        working = true;
        overlay.StartCoroutine(overlay.BlockingRoutine(message, work, onDone));
    }

    /// <summary>Puts the panel up for work that leaves the main thread free. Pair with
    /// <see cref="Hide"/> - including on the failure path.</summary>
    public static void Show(string message)
    {
        BusyOverlay overlay = Instance();
        if (overlay != null)
        {
            overlay.Appear(message, withLogo: false, blockInput: true);
        }
    }

    public static void Hide()
    {
        if (instance != null)
        {
            instance.Disappear();
        }
    }

    /// <summary>True while at least one question to the model is outstanding.</summary>
    public static bool Thinking { get { return thinking > 0; } }

    /// <summary>
    /// The logo goes up: a question is on its way to the model.
    /// </summary>
    /// <remarks>
    /// <para><b>Counted, not toggled.</b> Describing a list of parts asks one question after
    /// another, and the logo has to stay up across the whole run rather than blink once per part.
    /// Every <see cref="BeginThinking"/> is matched by an <see cref="EndThinking"/> in a
    /// <c>finally</c>, so a failed request takes its own count with it.</para>
    ///
    /// <para><b>Taps are not swallowed.</b> Unlike the loading panel this does not block input:
    /// an answer can take half a minute, and the twin is worth looking at meanwhile - the document
    /// flow says so in as many words. The logo says the app is working, it does not hold it
    /// hostage.</para>
    /// </remarks>
    public static void BeginThinking()
    {
        thinking++;

        BusyOverlay overlay = Instance();
        if (overlay != null)
        {
            overlay.Appear(message: null, withLogo: true, blockInput: false);
        }
    }

    /// <summary>One question has been answered, or has failed. The logo goes when none is left.</summary>
    public static void EndThinking()
    {
        if (thinking > 0)
        {
            thinking--;
        }

        if (thinking == 0 && instance != null)
        {
            instance.Disappear();
        }
    }

    private IEnumerator BlockingRoutine(string message, Action work, Action onDone)
    {
        Appear(message, withLogo: false, blockInput: true);

        // two frames: one to lay the panel out, one to draw it. One is usually enough, but a
        // layout rebuild can push the draw into the next frame, and then the user sees nothing.
        yield return null;
        yield return null;

        try
        {
            work();
        }
        finally
        {
            working = false;
            Disappear();
        }

        if (onDone != null)
        {
            onDone();
        }
    }

    /// <param name="withLogo">Show the logo instead of the message box - the two share the place
    /// in the middle of the screen, so only one of them is ever up.</param>
    /// <param name="blockInput">Swallow taps while this is showing. Right for work that freezes
    /// the main thread, wrong for a request the user can sit out.</param>
    private void Appear(string message, bool withLogo, bool blockInput)
    {
        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
        }

        if (label != null && message != null)
        {
            label.text = message;
        }

        if (box != null)
        {
            box.SetActive(!withLogo);
        }

        if (logo != null)
        {
            logo.gameObject.SetActive(withLogo);
        }

        if (withLogo)
        {
            StartWagging();
        }
        else
        {
            StopWagging();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = blockInput;
        }

        transform.SetAsLastSibling();
    }

    private void Disappear()
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        if (isActiveAndEnabled == true)
        {
            fade = StartCoroutine(FadeOut());
        }
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private IEnumerator FadeOut()
    {
        const float seconds = 0.25f;
        float elapsed = 0f;

        while (elapsed < seconds && canvasGroup != null)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - elapsed / seconds);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        fade = null;

        // back to the shape the loading panel expects to find
        StopWagging();
        if (logo != null) logo.gameObject.SetActive(false);
        if (box != null) box.SetActive(true);
    }

    private void StartWagging()
    {
        if (wag != null || logo == null || logoFrames == null || logoFrames.Length < 2)
        {
            return;
        }

        if (isActiveAndEnabled)
        {
            wag = StartCoroutine(Wag());
        }
    }

    private void StopWagging()
    {
        if (wag != null)
        {
            StopCoroutine(wag);
            wag = null;
        }

        // back to the first drawing, so the next question starts the wag from the same place
        if (logo != null && logoFrames != null && logoFrames.Length > 0 && logoFrames[0] != null)
        {
            logo.texture = logoFrames[0];
        }
    }

    /// <summary>
    /// Plays the drawings there and back again for as long as the app is asking.
    /// </summary>
    /// <remarks>Unscaled time, like the fade: a report is being fetched, and nothing here should
    /// depend on whether the game clock happens to be running.</remarks>
    private IEnumerator Wag()
    {
        int frame = 0;
        int step = 1;

        while (true)
        {
            if (logoFrames[frame] != null)
            {
                logo.texture = logoFrames[frame];
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, logoFrameSeconds));

            // turn around at either end rather than jumping back to the first drawing
            if (frame + step < 0 || frame + step >= logoFrames.Length)
            {
                step = -step;
            }
            frame += step;
        }
    }

    private static BusyOverlay Instance()
    {
        if (instance != null)
        {
            return instance;
        }

        var prefab = Resources.Load<GameObject>(PrefabName);
        if (prefab == null)
        {
            Debug.LogWarning("No '" + PrefabName + "' prefab in Resources - the app will work, "
                             + "but slow operations will not say so.");
            return null;
        }

        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("No Canvas to put the busy overlay on.");
            return null;
        }

        GameObject host = Instantiate(prefab, canvas.transform, false);
        host.name = PrefabName;
        instance = host.GetComponent<BusyOverlay>();

        // The object stays active for its whole life. An inactive GameObject cannot run a
        // coroutine, and the blocking routine is a coroutine - deactivating it between uses left
        // the overlay unable to start and the app permanently convinced it was busy.
        if (instance != null && instance.canvasGroup != null)
        {
            instance.canvasGroup.alpha = 0f;
            instance.canvasGroup.blocksRaycasts = false;
        }
        return instance;
    }

    /// <summary>The main canvas, preferred by name so the overlay does not end up on a sub-canvas
    /// that something else hides.</summary>
    private static Canvas FindCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas candidate in canvases)
        {
            if (candidate.name == "Canvas" && candidate.transform.parent == null)
            {
                return candidate;
            }
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            // a scene change while work was running must not leave the app thinking it is busy
            working = false;
            thinking = 0;
        }
    }
}
