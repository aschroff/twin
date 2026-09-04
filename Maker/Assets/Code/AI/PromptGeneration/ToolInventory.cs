using System.Collections.Generic;
using CW.Common;
using PaintIn3D;
using UnityEngine;
using UnityEngine.UI;

namespace Code.AI.PromptGeneration
{
    /// <summary>One marker or filler tool, as a prompt needs to see it.</summary>
    public class ToolInfo
    {
        /// <summary>Name of the tool GameObject - the name PartTemplateService.PaintRegion takes.</summary>
        public string name;

        public PartManager.Tool kind;
        public Color color;

        /// <summary>What the user wrote into the tool's row. Empty while the tool is still free.</summary>
        public string meaning = "";

        public bool inUse => !string.IsNullOrWhiteSpace(meaning);

        /// <summary>What this tool draws, in words the LLM can act on.</summary>
        public string kindText
        {
            get
            {
                switch (kind)
                {
                    case PartManager.Tool.MarkerLine: return "a line";
                    case PartManager.Tool.MarkerDotted: return "a dotted line";
                    case PartManager.Tool.Filler: return "a surface";
                    default: return "unknown";
                }
            }
        }
    }

    /// <summary>
    /// The marker and filler tools of the app together with the meaning the user gave them.
    ///
    /// A tool row lives under the EditMarker / EditFiller panels (the <see cref="Tools"/>
    /// subclasses) and carries three things: a <c>CwDemoButton</c> pointing at the actual tool
    /// GameObject, an <c>Item</c> whose InputField holds the meaning (persisted per twin in
    /// ConfigData.itemTexts), and a <see cref="Marker"/> for the existing report prompts.
    ///
    /// Read here from the InputField rather than from the row's Text labels: the labels are only
    /// synced once their panel has been open, so a panel the user never visited still shows the
    /// text the prefab shipped with.
    /// </summary>
    public static class ToolInventory
    {
        /// <summary>Every marker and filler tool of the app, in panel order.</summary>
        public static List<ToolInfo> All()
        {
            var tools = new List<ToolInfo>();
            foreach (Tools panel in Object.FindObjectsOfType<Tools>(true))
            {
                foreach (Transform row in panel.transform)
                {
                    ToolInfo info = Read(row);
                    if (info != null)
                    {
                        tools.Add(info);
                    }
                }
            }
            return tools;
        }

        /// <summary>Tools the user has given a meaning - they keep it.</summary>
        public static List<ToolInfo> InUse()
        {
            return All().FindAll(tool => tool.inUse);
        }

        /// <summary>Tools without a meaning - these are the ones a new finding may claim.</summary>
        public static List<ToolInfo> Free()
        {
            return All().FindAll(tool => !tool.inUse);
        }

        private static ToolInfo Read(Transform row)
        {
            CwDemoButton button = row.GetComponent<CwDemoButton>();
            if (button == null || button.IsolateTarget == null)
            {
                return null;
            }

            GameObject tool = button.IsolateTarget.gameObject;
            CwPaintSphere sphere = tool.GetComponent<CwPaintSphere>();
            if (sphere == null)
            {
                // only markers and fillers paint spheres, and only those can carry a region
                return null;
            }

            InputField meaning = row.GetComponentInChildren<InputField>(true);
            return new ToolInfo
            {
                name = tool.name,
                kind = PartManager.DeriveType(tool),
                color = sphere.Color,
                meaning = meaning != null ? meaning.text.Trim() : ""
            };
        }
    }
}
