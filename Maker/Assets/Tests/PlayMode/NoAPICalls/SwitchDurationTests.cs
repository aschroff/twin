using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace NoAPICalls
{
    /// <summary>
    /// How long a twin switch blocks, measured rather than remembered.
    /// </summary>
    /// <remarks>Explicit: it asserts nothing, because a duration on a desktop says little about a
    /// device. It exists to answer "is this still slow enough to need feedback on screen", and to
    /// answer it again after anything that claims to make loading faster.</remarks>
    [Category(Processes.Technical)]
    [Explicit("Diagnostic, not a regression test.")]
    public class SwitchDurationTests : TwinPaintTestBase
    {
        [UnityTest]
        public IEnumerator HowLongDoesATwinSwitchTake()
        {
            yield return ResetApp();

            var report = new System.Text.StringBuilder("Twin switch duration\n");
            string[] twins = { "LipEdema", "Torso", "LipEdema", "Legs", "LipEdema" };

            foreach (string twin in twins)
            {
                // the click itself is cheap; the frame that carries the work is what blocks
                var watch = Stopwatch.StartNew();
                yield return SelectTwin(twin);
                watch.Stop();
                report.AppendLine("  -> " + twin.PadRight(10)
                                  + (watch.ElapsedMilliseconds / 1000.0).ToString("0.00") + " s");
            }

            Debug.Log("[switch duration]\n" + report);
        }
    }
}
