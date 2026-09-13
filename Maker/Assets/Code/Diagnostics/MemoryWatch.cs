using UnityEngine;

namespace Diagnostics
{
    /// <summary>
    /// Feeds <see cref="MemoryProbe"/> once a frame.
    /// </summary>
    /// <remarks>
    /// <para>It puts itself into the scene rather than being placed there, so that measuring costs
    /// no change to <c>Maker Main.unity</c> — a scene edit is the expensive, risky kind of change,
    /// and this is a diagnostic that we expect to remove again.</para>
    /// </remarks>
    public class MemoryWatch : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("MemoryWatch");
            host.AddComponent<MemoryWatch>();
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
        }

        /// <summary>Long enough after the start for the scene and the first twin to be up, so the
        /// baseline describes a working app rather than one that is still loading.</summary>
        private const float BaselineAfterSeconds = 6f;

        private void Update()
        {
            MemoryProbe.Observe();

            if (MemoryProbe.HasBaseline == false && Time.realtimeSinceStartup >= BaselineAfterSeconds)
            {
                MemoryProbe.CaptureBaseline();
            }
        }
    }
}
