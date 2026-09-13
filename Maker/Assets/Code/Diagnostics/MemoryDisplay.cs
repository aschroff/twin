using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Diagnostics
{
    /// <summary>
    /// Puts the figures from <see cref="MemoryProbe"/> on the settings page.
    /// </summary>
    /// <remarks>
    /// <para>This builds its panel at runtime instead of living in <c>Maker Main.unity</c>. Saving
    /// that scene from the editor writes about two hundred lines of driven RectTransform values
    /// into it (APP_DOCUMENTATION §9), and a diagnostic we intend to remove again is not worth
    /// that diff. Removing this later means deleting two files and one line in
    /// <c>SettingsManager</c>.</para>
    ///
    /// <para>No localized strings: everything here is generated at runtime from numbers, so there
    /// is nothing to put in the five locale tables.</para>
    /// </remarks>
    public class MemoryDisplay : MonoBehaviour
    {
        private const string HostName = "MemoryDisplay (diagnostic)";

        private TextMeshProUGUI label;

        /// <summary>
        /// Called when the settings page opens. Creates the panel the first time and refreshes it
        /// every time, so the numbers are the ones from the moment it was opened.
        /// </summary>
        public static void Refresh(Transform parent)
        {
            if (parent == null) return;

            Transform existing = parent.Find(HostName);
            MemoryDisplay display = existing != null
                ? existing.GetComponent<MemoryDisplay>()
                : Build(parent);

            if (display != null)
            {
                display.Render();
            }
        }

        private static MemoryDisplay Build(Transform parent)
        {
            var host = new GameObject(HostName, typeof(RectTransform), typeof(Image), typeof(MemoryDisplay));
            host.transform.SetParent(parent, false);

            var rect = host.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot     = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(620f, -40f);
            rect.anchoredPosition = new Vector2(-20f, 0f);

            host.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
            host.GetComponent<Image>().raycastTarget = false;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(host.transform, false);

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 16f);
            textRect.offsetMax = new Vector2(-16f, -16f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize      = 20f;
            text.color         = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.alignment     = TextAlignmentOptions.TopLeft;

            var display = host.GetComponent<MemoryDisplay>();
            display.label = text;
            return display;
        }

        private void Render()
        {
            MemoryProbe.Sample now   = MemoryProbe.Take();
            MemoryProbe.Sample peak  = MemoryProbe.Peak;
            MemoryProbe.Sample first = MemoryProbe.First;

            var sb = new StringBuilder();
            sb.AppendLine("MEMORY — diagnostic build     (iOS kills this app near 3000 MB)");
            sb.AppendLine("peak " + MemoryProbe.Mb(peak.Total) + "    now " + MemoryProbe.Mb(now.Total)
                          + "    grown " + MemoryProbe.Signed(now.Total - first.Total)
                          + "    switches " + MemoryProbe.SwitchCount);
            sb.AppendLine("  unity " + MemoryProbe.Mb(now.UnityReserved)
                          + " / mono " + MemoryProbe.Mb(now.MonoReserved)
                          + " / graphics " + MemoryProbe.Mb(now.GraphicsDriver)
                          + "   (peaks " + MemoryProbe.Mb(peak.UnityReserved)
                          + " / " + MemoryProbe.Mb(peak.MonoReserved)
                          + " / " + MemoryProbe.Mb(peak.GraphicsDriver) + ")");
            sb.AppendLine();

            AppendGrowth(sb);
            AppendBiggest(sb);
            AppendSwitches(sb);

            label.text = sb.ToString();
        }

        /// <summary>
        /// What has piled up since the app started. Restarting the app makes the crash go away, so
        /// everything listed here is, by definition, surplus — this is the list to shorten.
        /// </summary>
        private static void AppendGrowth(StringBuilder sb)
        {
            sb.AppendLine("WHAT GREW SINCE THE APP STARTED");

            if (MemoryProbe.HasBaseline == false)
            {
                sb.AppendLine("  (baseline not taken yet — wait a few seconds and reopen)");
                sb.AppendLine();
                return;
            }

            var growth = MemoryProbe.GrowthSinceBaseline(MemoryProbe.Composition());
            bool any = false;

            foreach (MemoryProbe.TypeTotal entry in growth)
            {
                if (entry.Bytes < 1024 * 1024 && entry.Bytes > -1024 * 1024) continue;
                sb.AppendLine("  " + MemoryProbe.Signed(entry.Bytes).PadLeft(9) + "   "
                              + entry.Type.PadRight(18)
                              + (entry.Count >= 0 ? "+" : "") + entry.Count + " objects");
                any = true;
            }

            if (any == false) sb.AppendLine("  nothing grew by more than a megabyte.");
            sb.AppendLine();
        }

        private static void AppendBiggest(StringBuilder sb)
        {
            sb.AppendLine("BIGGEST OBJECTS ALIVE NOW");
            foreach (string line in MemoryProbe.Biggest(12, 8L * 1024 * 1024))
            {
                sb.AppendLine("  " + line);
            }
            sb.AppendLine();
        }

        private static void AppendSwitches(StringBuilder sb)
        {
            var switches = MemoryProbe.Switches;
            sb.AppendLine("TWIN SWITCHES: " + MemoryProbe.SwitchCount);

            if (switches.Count == 0)
            {
                sb.AppendLine("  none yet — switch a twin, then come back here.");
                return;
            }

            // the most recent one in full, because that is where the attribution is
            MemoryProbe.SwitchRecord last = switches[switches.Count - 1];
            sb.AppendLine();
            sb.AppendLine("last switch  #" + last.Index + "  " + last.Twin
                          + "   " + last.Seconds.ToString("0.0") + " s");

            long previous = last.Before;
            foreach (MemoryProbe.Mark mark in last.Marks)
            {
                sb.AppendLine("  " + mark.Label.PadRight(26)
                              + MemoryProbe.Signed(mark.Total - previous).PadLeft(8)
                              + "   = " + MemoryProbe.Mb(mark.Total).PadLeft(9));
                previous = mark.Total;
            }
            sb.AppendLine("  " + "peak during switch".PadRight(26) + "        "
                          + "   = " + MemoryProbe.Mb(last.Peak).PadLeft(9));

            // and the trend, so growth is visible without writing numbers down
            sb.AppendLine();
            sb.AppendLine("per switch (total / peak):");
            foreach (MemoryProbe.SwitchRecord record in switches)
            {
                sb.AppendLine("  #" + record.Index.ToString().PadRight(4)
                              + MemoryProbe.Signed(record.Total).PadLeft(8)
                              + "   peak " + MemoryProbe.Mb(record.Peak).PadLeft(9)
                              + "   " + record.Seconds.ToString("0.0") + " s");
            }
        }
    }
}
