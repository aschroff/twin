using System;
using System.Collections.Generic;
using System.Linq;
using PaintCore;
using PaintIn3D;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Text→Part feature (see FEATURE_TEXT_TO_PART.md in this folder): paints a pre-painted
/// body-region template onto the currently loaded twin.
///
/// Templates are the bundled area twins under Resources/templates/&lt;Area&gt;.twin/ConfigTwin,
/// where each group is one body region (region catalog: Assets/Resources/BODY_REGIONS.md).
///
/// Painting a region works like painting with a tool by hand: the new part is added to the
/// twin's currently active group. Groups are the user's categories (Injuries, Pain,
/// Treatment, …); the region key is carried on the part (PartData.description).
/// </summary>
public static class PartTemplateService
{
    /// <summary>The bundled region-template twins (excludes sample twins like LipEdema).
    /// Extend when a new area twin is promoted — see TemplateLibrary/README.md.</summary>
    public static readonly string[] TemplateTwins =
        { "Torso.twin", "Arms.twin", "Legs.twin", "Feet.twin", "Head.twin", "Hands.twin" };

    /// <summary>Catalog of all template twins and their body regions — serializable, so it can
    /// be handed to an LLM as JSON (structured-output region enum) or drive a UI picker.</summary>
    [Serializable]
    public class TemplateCatalog
    {
        public List<TemplateTwinInfo> twins = new List<TemplateTwinInfo>();
    }

    [Serializable]
    public class TemplateTwinInfo
    {
        public string twinName;      // e.g. "Arms.twin" — pass to PaintRegion
        public List<string> regions; // region keys, e.g. "shoulder_front_left" (see BODY_REGIONS.md)
    }

    /// <summary>Shape-compatible subset of PartManager's serialized JSON (commandDetails).
    /// Reuses PartManager's nested types so [SerializeReference] command data deserializes.</summary>
    [Serializable]
    private class TemplateTwin
    {
        public List<PartManager.GroupData> groups = new List<PartManager.GroupData>();
    }

    /// <summary>
    /// Paints the body region <paramref name="regionName"/> from the bundled template twin
    /// <paramref name="twinName"/> (e.g. "Arms.twin") onto the current twin, adding the new
    /// part(s) to the currently active group — exactly like painting with a tool by hand.
    /// Returns the created part(s).
    /// </summary>
    public static List<PartManager.PartData> PaintRegion(string twinName, string regionName)
    {
        return PaintRegion(twinName, regionName, toolName: null);
    }

    /// <param name="toolName">Name of a marker/filler tool GameObject under the app's Tools
    /// container (e.g. "Yellow", "Cyan Filling"). Its color and meaning are applied to the
    /// stamped part. Null keeps the tool the template was painted with.</param>
    public static List<PartManager.PartData> PaintRegion(string twinName, string regionName, string toolName)
    {
        var partManager = UnityEngine.Object.FindObjectOfType<PartManager>();
        return PaintRegion(twinName, regionName, toolName, partManager);
    }

    public static List<PartManager.PartData> PaintRegion(string twinName, string regionName, string toolName, PartManager partManager)
    {
        if (partManager == null)
            throw new ArgumentNullException(nameof(partManager), "PartManager not found — is the app scene loaded?");

        var paintableTexture = UnityEngine.Object.FindObjectOfType<CwPaintableTexture>();
        if (paintableTexture == null)
            throw new InvalidOperationException("No CwPaintableTexture in scene — cannot bind template commands.");

        // validate the inputs first, then the app state — so callers get the precise error
        var templateGroup = LoadTemplateGroup(twinName, regionName);
        var tool = toolName != null ? ResolveTool(toolName) : null;
        PartManager.GroupData targetGroup = ResolveTargetGroup(partManager);
        var newParts = ClonePartsInto(templateGroup, targetGroup, paintableTexture, tool);

        // replay the cloned commands onto the body texture
        var oldListening = partManager.Listening;
        partManager.Listening = false;
        foreach (var part in newParts)
            partManager.RefreshPart(part);
        partManager.Listening = oldListening;

        return newParts;
    }

    /// <summary>The group the new part belongs to: the active group, as with normal painting.
    /// Falls back to the twin's first group when nothing is selected yet (same helper the app
    /// uses); throws when the twin has no group at all — the user must create one first.</summary>
    private static PartManager.GroupData ResolveTargetGroup(PartManager partManager)
    {
        PartManager.GroupData group = partManager.currentGroup;
        if (group == null)
        {
            group = partManager.trySetCurrentGroupIfEmpty();
        }
        if (group == null)
        {
            throw new InvalidOperationException(
                "No group available in the current twin — create or select a group before painting a region.");
        }
        return group;
    }

    /// <summary>
    /// Paints a region with the tool the user currently has selected in the app. Region
    /// templates are sphere-painted, so only marker/filler tools can carry them — if the
    /// active tool is a sticker/text tool (or nothing is active), the first marker tool
    /// found in the Tools container is used instead.
    /// </summary>
    public static List<PartManager.PartData> PaintRegionWithCurrentTool(string twinName, string regionName)
    {
        return PaintRegion(twinName, regionName, ResolveCurrentOrDefaultToolName());
    }

    /// <summary>Name of the active marker/filler tool, or of the first marker tool as fallback.</summary>
    public static string ResolveCurrentOrDefaultToolName()
    {
        GameObject active = FindActiveTool();
        if (active != null && IsRegionCapableTool(active))
        {
            return active.name;
        }
        GameObject marker = FindFirstMarkerTool();
        if (marker == null)
        {
            throw new InvalidOperationException("No marker tool found in the Tools container.");
        }
        return marker.name;
    }

    /// <summary>Region templates consist of CwCommandSphere data — only tools that paint
    /// spheres (markers, fillers) can be used for them; stickers/text paint decals.</summary>
    private static bool IsRegionCapableTool(GameObject tool)
    {
        return tool.GetComponent<CwPaintSphere>() != null;
    }

    private static GameObject FindActiveTool()
    {
        Transform container = FindToolsContainer();
        foreach (Transform child in container)
        {
            if (child.gameObject.activeSelf)
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private static GameObject FindFirstMarkerTool()
    {
        Transform container = FindToolsContainer();
        foreach (Transform child in container)
        {
            // markers paint spheres and are not fills
            if (child.GetComponent<CwPaintSphere>() != null && child.GetComponent<CwHitScreenFill>() == null)
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private static Transform FindToolsContainer()
    {
        GameObject[] containers = GameObject.FindGameObjectsWithTag("Tools");
        if (containers.Length == 0)
        {
            throw new InvalidOperationException("Tools container not found in scene.");
        }
        return containers[0].transform;
    }

    /// <summary>Region names available in a bundled template twin (for prompts/UI).</summary>
    public static List<string> GetTemplateGroupNames(string twinName)
    {
        return LoadTemplateTwin(twinName).groups.Select(g => g.name).ToList();
    }

    /// <summary>All template twins with their body regions, read from the bundled assets
    /// (ground truth — no separate manifest to maintain). Twins listed in
    /// <see cref="TemplateTwins"/> that fail to load are skipped with a warning.</summary>
    public static TemplateCatalog GetTemplateCatalog()
    {
        var catalog = new TemplateCatalog();
        foreach (var twinName in TemplateTwins)
        {
            try
            {
                catalog.twins.Add(new TemplateTwinInfo
                {
                    twinName = twinName,
                    regions = GetTemplateGroupNames(twinName),
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartTemplateService] Skipping template twin '{twinName}': {e.Message}");
            }
        }
        return catalog;
    }

    /// <summary>The catalog as JSON — ready to embed into an LLM prompt or a structured-output
    /// schema ("pick twinName + region from this catalog").</summary>
    public static string GetTemplateCatalogJson()
    {
        return JsonUtility.ToJson(GetTemplateCatalog());
    }

    private static PartManager.GroupData LoadTemplateGroup(string twinName, string groupName)
    {
        var template = LoadTemplateTwin(twinName);
        var group = template.groups.FirstOrDefault(g => g.name == groupName);
        if (group == null)
            throw new ArgumentException(
                $"Group '{groupName}' not found in template '{twinName}'. Available: {string.Join(", ", template.groups.Select(g => g.name))}");
        return group;
    }

    private static TemplateTwin LoadTemplateTwin(string twinName)
    {
        var asset = Resources.Load<TextAsset>($"templates/{twinName}/ConfigTwin");
        if (asset == null)
            throw new ArgumentException($"Template twin '{twinName}' not found under Resources/templates/.");

        var config = JsonUtility.FromJson<ConfigData>(asset.text);
        if (string.IsNullOrEmpty(config?.commandDetails))
            throw new InvalidOperationException($"Template twin '{twinName}' contains no command data.");

        var template = JsonUtility.FromJson<TemplateTwin>(config.commandDetails);
        if (template?.groups == null || template.groups.Count == 0)
            throw new InvalidOperationException($"Template twin '{twinName}' contains no groups.");
        return template;
    }

    private static List<PartManager.PartData> ClonePartsInto(PartManager.GroupData templateGroup,
        PartManager.GroupData targetGroup, CwPaintableTexture paintableTexture, ToolInfo tool)
    {
        var newParts = new List<PartManager.PartData>();
        foreach (var templatePart in templateGroup.groupParts)
        {
            // the deserialized template objects are private to this call, so they can be
            // adopted directly — only ids and the texture binding must be renewed
            var newPart = templatePart;
            newPart.id = Guid.NewGuid().ToString();
            newPart.group = targetGroup;
            if (tool != null)
            {
                newPart.nameTool = tool.name;
                newPart.colorTool = tool.color;
                newPart.typeTool = tool.type;
                newPart.meaning = tool.meaning;
            }
            foreach (var command in newPart.partCommands)
            {
                command.id = Guid.NewGuid().ToString();
                command.data.PaintableTexture = paintableTexture; // re-bind to the live texture
                if (tool != null && command.data.LocalCommand is CwCommandSphere sphere)
                {
                    sphere.Color = tool.color; // region templates are sphere-painted (markers/fillers)
                }
            }
            targetGroup.groupParts.Add(newPart);
            newParts.Add(newPart);
        }
        return newParts;
    }

    // ---------------- Tool resolution (Tools container GameObjects) ----------------

    /// <summary>A tool as the app defines it: a GameObject under the Tools container whose
    /// color and meaning are fixed properties of that tool.</summary>
    private class ToolInfo
    {
        public string name;
        public Color color;
        public PartManager.Tool type;
        public string meaning;
    }

    private static ToolInfo ResolveTool(string toolName)
    {
        var containers = GameObject.FindGameObjectsWithTag("Tools");
        if (containers.Length == 0)
            throw new InvalidOperationException("Tools container not found in scene.");

        Transform toolTransform = null;
        foreach (var container in containers)
        {
            toolTransform = container.transform.Find(toolName);
            if (toolTransform != null) break;
        }
        if (toolTransform == null)
            throw new ArgumentException($"Tool '{toolName}' not found under the Tools container.");

        var paintSphere = toolTransform.GetComponent<CwPaintSphere>();
        if (paintSphere == null)
            throw new ArgumentException(
                $"Tool '{toolName}' is not a marker/filler tool — sticker/text tools cannot stamp region templates.");

        return new ToolInfo
        {
            name = toolName,
            color = paintSphere.Color,
            type = DeriveToolType(toolTransform.gameObject),
            meaning = ExtractMeaning(toolTransform.gameObject, toolName),
        };
    }

    /// <summary>Marker/filler classification, mirroring PartManager.DeriveType (which is private).</summary>
    private static PartManager.Tool DeriveToolType(GameObject tool)
    {
        if (tool.GetComponent<CwHitScreenFill>() != null)
            return PartManager.Tool.Filler;
        var hit = tool.GetComponent<CwHitScreen>();
        if (hit != null && hit.Frequency == CwHitScreen.FrequencyType.PixelInterval && hit.Connector.ConnectHits == false)
            return PartManager.Tool.MarkerDotted;
        return PartManager.Tool.MarkerLine;
    }

    /// <summary>The tool's user-facing meaning from its UI button (mirrors PartManager.GetText);
    /// falls back to the tool name when the button text isn't available.</summary>
    private static string ExtractMeaning(GameObject tool, string fallback)
    {
        var tracker = tool.GetComponent<ToolTracker>();
        if (tracker != null && tracker.myButton != null)
        {
            foreach (var text in tracker.myButton.gameObject.GetComponentsInChildren<Text>(true))
            {
                if (text.text != "Placeholder" && !string.IsNullOrEmpty(text.text)
                    && text.transform.parent != null && text.transform.parent.name == "InputField")
                {
                    return text.text;
                }
            }
        }
        return fallback;
    }
}
