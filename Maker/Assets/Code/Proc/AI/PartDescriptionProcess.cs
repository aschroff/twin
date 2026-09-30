using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

namespace Code
{
    /// <summary>What describing one part did. The batch has to tell these apart to report on
    /// itself; a part without a screenshot is not a failure, there is simply nothing to look at.</summary>
    public enum DescribeOutcome
    {
        Described,
        NoScreenshot,
        Failed
    }

    public class PartDescriptionProcess: Process
    {
        public override ProcessResult Execute(string variant = "")
        {
            Debug.Log("Process Status: PartDescriptionProcess Execute");
            StartCoroutine(execute(variant));
            return new ProcessResult();
        }
        
        public (string before, string after) SplitStringByDoubleHash(string input)
        {
            if (string.IsNullOrEmpty(input) || !input.Contains("##"))
            {
                return (input, string.Empty);
            }

            string[] parts = input.Split(new[] { "##" }, 2, System.StringSplitOptions.None);
            return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
        }
        
        private IEnumerator execute(string variantRaw)
        {
            Debug.Log("Process Status: PartDescriptionProcess execute");
            string variant;
            string idPart;
            (variant, idPart) = SplitStringByDoubleHash(variantRaw);
            PartManager partManager = getPartManager();
            PartManager.PartData part = partManager.getPart(idPart);
            if (part == null)
            {
                Debug.LogError("PartDescriptionProcess: there is no part with id " + idPart);
                yield break;
            }

            // one part on its own, asked for by hand: it is worth saying why nothing happened
            yield return DescribeAndWait(part, variant, outcome =>
            {
                if (outcome == DescribeOutcome.NoScreenshot)
                {
                    Report("No Screenshot");
                }
            });
        }

        /// <summary>
        /// Describes one part and comes back only once the model has answered.
        /// </summary>
        /// <remarks>
        /// <para>The waiting is the point. Before this, describing a list of parts started one
        /// request per part in the same frame and reported itself finished a frame later - so the
        /// version report that follows was built from descriptions that had not arrived yet, and
        /// nothing could show progress or say when it was done.</para>
        ///
        /// <para>Nothing is written to <c>part.description</c> here or below: only the model's own
        /// answer ever lands in it (<c>AI.OnInjuryAnalyzed</c>). A placeholder or an error message
        /// in that field overwrites text a doctor typed, and there is no undo for it.</para>
        /// </remarks>
        public IEnumerator DescribeAndWait(PartManager.PartData part, string variant,
            Action<DescribeOutcome> onDone)
        {
            if (part == null)
            {
                if (onDone != null) onDone(DescribeOutcome.Failed);
                yield break;
            }

            part.pathScreenshot = get_path(part);
            if (!File.Exists(part.pathScreenshot))
            {
                Debug.Log("No screenshot for part " + part.id + " - not described.");
                if (onDone != null) onDone(DescribeOutcome.NoScreenshot);
                yield break;
            }

            string failure = null;
            yield return getAI().DescribePartCoroutine(part, variant, error => failure = error);

            if (onDone != null)
            {
                onDone(failure == null ? DescribeOutcome.Described : DescribeOutcome.Failed);
            }
        }

        /// <summary>Whether this part has the screenshot the model needs to look at.</summary>
        public bool HasScreenshot(PartManager.GroupData group, PartManager.PartData part)
        {
            return File.Exists(ScreenshotPath(group, part));
        }

        public string get_path(PartManager.PartData part)
        {   
            PartManager partManager = getPartManager();
            return ScreenshotPath(partManager.getGroup(part), part);
        }

        /// <summary>
        /// Where the screenshot of a part lives.
        /// </summary>
        /// <remarks>Takes the group rather than looking it up, so a caller walking the groups does
        /// not pay a search per part - and so the count a button shows is built from exactly the
        /// path the run then uses.</remarks>
        public string ScreenshotPath(PartManager.GroupData group, PartManager.PartData part)
        {
            DataPersistenceManager dataManager = getDataManager();
            string groupName = group != null ? group.name : "";
            string name = dataManager.selectedProfileId + " - " + groupName + " - part " + part.id;
            string folder = dataManager.selectedProfileId;

            return Path.Combine(DataPaths.PersistentDataPath, folder,
                "screenshot_" + name + ".png");
        }

        private void Report(string message)
        {
            LeanPulse notification = getNotification();
            if (notification == null)
            {
                return;
            }
            foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
            {
                text.text = message;
            }
            notification.Pulse();
        }
    }
}
