using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Code
{
    /// <summary>
    /// Asks the model to describe the parts of the open twin, one after the other.
    /// </summary>
    /// <remarks>
    /// <para><b>Two of these exist in the scene</b> and they differ only in <see cref="hardRedo"/>:
    /// the plain one fills the gaps, the forced one describes everything again. That is the whole
    /// difference between the two entries the Help menu offers.</para>
    ///
    /// <para><b>Nothing here writes to <c>part.description</c>.</b> It used to stamp a placeholder
    /// into every part before asking - and when the ask then could not happen, because the part had
    /// no screenshot, that placeholder was what stayed. Only the model's own answer lands in that
    /// field now (<c>AI.OnInjuryAnalyzed</c>).</para>
    ///
    /// <para><b>One at a time.</b> This used to start one request per part in the same frame and
    /// report itself finished a frame later. Nothing could show progress, the version report that
    /// follows was built from answers that had not arrived, and twenty parts meant twenty
    /// simultaneous calls. Now the run ends when the work does, which is what lets a button stay
    /// disabled while it goes.</para>
    /// </remarks>
    public class PartsDescriptionProcess: ProcessSync
    {
        /// <summary>Describe parts that already have a description again, overwriting them.</summary>
        [SerializeField] public bool hardRedo = false;

        private const string TableName = "TwinLocalTables";

        /// <summary>Key of the line shown when a run is through.</summary>
        public const string SummaryKey = "PARTS_DESCRIBED_SUMMARY";

        private bool running;

        /// <summary>True from the start of a run until it has finished.</summary>
        public bool Running { get { return running; } }

        public override ProcessResult Execute(string variant = "")
        {
            Begin(variant);
            return new ProcessResult();
        }

        public override ProcessResult ExecuteSync(string variant = "")
        {
            Begin(variant);
            return new ProcessResult();
        }

        private void Begin(string variant)
        {
            if (running)
            {
                Debug.Log("PartsDescriptionProcess: a run is already going.");
                return;
            }
            StartCoroutine(Run(variant));
        }

        /// <summary>
        /// The parts a run would visit, each with the group it sits in.
        /// </summary>
        /// <remarks>A part that already says something is left alone unless this is the forced
        /// one: that text is the doctor's, or a finding a document brought in, and asking the
        /// model again would replace it.</remarks>
        public List<(PartManager.GroupData group, PartManager.PartData part)> Candidates()
        {
            var candidates = new List<(PartManager.GroupData, PartManager.PartData)>();
            PartManager partManager = getPartManager();
            if (partManager == null || partManager.groups == null)
            {
                return candidates;
            }

            foreach (PartManager.GroupData group in partManager.groups)
            {
                if (group == null || group.groupParts == null) continue;
                foreach (PartManager.PartData part in group.groupParts)
                {
                    if (part == null) continue;
                    if (!hardRedo && !string.IsNullOrWhiteSpace(part.description)) continue;
                    candidates.Add((group, part));
                }
            }
            return candidates;
        }

        /// <summary>
        /// How many parts a press would actually describe - the number a button shows.
        /// </summary>
        /// <remarks>A part without a screenshot is not one of them: the model is asked about the
        /// picture, so there is nothing to ask. Those parts are what the images button is for, and
        /// the run says how many it met.</remarks>
        public int CountDescribable()
        {
            PartDescriptionProcess one = SingleProcess();
            if (one == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var candidate in Candidates())
            {
                if (one.HasScreenshot(candidate.group, candidate.part)) count++;
            }
            return count;
        }

        private IEnumerator Run(string variant)
        {
            PartDescriptionProcess one = SingleProcess();
            int described = 0;
            int withoutScreenshot = 0;
            int failed = 0;

            if (one == null)
            {
                Debug.LogError("PartsDescriptionProcess: no PartDescriptionProcess under the ProcessManager.");
            }
            else
            {
                running = true;
                try
                {
                    foreach (var candidate in Candidates())
                    {
                        yield return one.DescribeAndWait(candidate.part, variant, outcome =>
                        {
                            if (outcome == DescribeOutcome.Described) described++;
                            else if (outcome == DescribeOutcome.NoScreenshot) withoutScreenshot++;
                            else failed++;
                        });
                    }
                }
                finally
                {
                    // whatever ends the run - the last part, or the scene going away - the process
                    // must not stay marked busy, or its buttons never come back
                    running = false;
                }

                Report(described, withoutScreenshot, failed);
            }

            OnExecuteCompleted();
        }

        private PartDescriptionProcess SingleProcess()
        {
            ProcessManager manager = getProcessManager();
            return manager != null
                ? manager.gameObject.GetComponentInChildren<PartDescriptionProcess>()
                : null;
        }

        private ProcessManager getProcessManager()
        {
            return this.gameObject.transform.parent.gameObject.GetComponent<ProcessManager>();
        }

        /*
         * One line when the run is through, instead of the "No Screenshot" toast the old run showed
         * per part - each of which overwrote the one before it, so a run over twelve parts said
         * nothing a person could act on.
         */
        private void Report(int described, int withoutScreenshot, int failed)
        {
            LeanPulse notification = getNotification();
            if (notification == null)
            {
                return;
            }

            string message = string.Format(Localise(SummaryKey), described, withoutScreenshot, failed);
            foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
            {
                text.text = message;
            }
            notification.Pulse();
        }

        /// <summary>Not StringLocalizer, which writes a console line on every successful lookup -
        /// see Assets/Code/Localization/README.md §3.</summary>
        private static string Localise(string key)
        {
            var table = LocalizationSettings.StringDatabase != null
                ? LocalizationSettings.StringDatabase.GetTable(TableName)
                : null;
            var entry = table != null ? table.GetEntry(key) : null;
            if (entry != null) return entry.GetLocalizedString();

            Debug.LogWarning("[PartsDescriptionProcess] No entry '" + key + "' in " + TableName + ".");
            return "{0} described, {1} without image, {2} failed";
        }
    }
}
