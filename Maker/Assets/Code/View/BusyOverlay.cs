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

    private static BusyOverlay instance;
    private Coroutine fade;
    private static bool working;

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
            overlay.Appear(message);
        }
    }

    public static void Hide()
    {
        if (instance != null)
        {
            instance.Disappear();
        }
    }

    private IEnumerator BlockingRoutine(string message, Action work, Action onDone)
    {
        Appear(message);

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

    private void Appear(string message)
    {
        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
        }

        if (label != null)
        {
            label.text = message;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
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
        }
    }
}
