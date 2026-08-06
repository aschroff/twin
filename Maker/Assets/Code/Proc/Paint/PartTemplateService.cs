using System;
using System.Collections.Generic;
using System.Linq;
using PaintCore;
using PaintIn3D;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Text→Part feature (see FEATURE_TEXT_TO_PART.md in this folder): stamps a pre-painted
/// body-region template onto the currently loaded twin.
///
/// Templates are the bundled area twins under Resources/templates/&lt;Area&gt;.twin/ConfigTwin
/// (generated via TemplateLibraryGenerator, one group per region — see
/// Assets/Resources/BODY_REGIONS.md for the region catalog).
///
/// The cloned commands are re-bound to the live CwPaintableMeshTexture at insertion time,
/// because serialized PaintableTexture references are session-local instanceIDs and never
/// survive into another session.
/// Each stamped region goes into its OWN new group (named like the region): this keeps the
/// save file small (parts-per-group grows it exponentially via the PartData.group cycle,
/// see FEATURE_TEXT_TO_PART.md finding #1) and gives per-region visibility toggling for free.
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
        public string twinName;      // e.g. "Arms.twin" — pass to PaintTemplateGroup
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
    /// Paints all parts of the template group <paramref name="groupName"/> from the bundled
    /// template twin <paramref name="twinName"/> (e.g. "Arms.twin") onto the current twin.
    /// Creates a new group named like the template group and returns it.
    /// </summary>
    public static PartManager.GroupData PaintTemplateGroup(string twinName, string groupName)
    {
        return PaintTemplateGroup(twinName, groupName, toolName: null);
    }

    public static PartManager.GroupData PaintTemplateGroup(string twinName, string groupName, PartManager partManager)
    {
        return PaintTemplateGroup(twinName, groupName, null, partManager);
    }

    /// <param name="toolName">Name of a marker/filler tool GameObject under the app's Tools
    /// container (e.g. "Yellow", "Cyan Filling"). Its color and meaning are applied to the
    /// stamped part. Null keeps the template's own (Red) tool — the default.</param>
    public static PartManager.GroupData PaintTemplateGroup(string twinName, string groupName, string toolName)
    {
        var partManager = UnityEngine.Object.FindObjectOfType<PartManager>();
        return PaintTemplateGroup(twinName, groupName, toolName, partManager);
    }

    public static PartManager.GroupData PaintTemplateGroup(string twinName, string groupName, string toolName, PartManager partManager)
    {
        if (partManager == null)
            throw new ArgumentNullException(nameof(partManager), "PartManager not found — is the app scene loaded?");

        var paintableTexture = UnityEngine.Object.FindObjectOfType<CwPaintableTexture>();
        if (paintableTexture == null)
            throw new InvalidOperationException("No CwPaintableTexture in scene — cannot bind template commands.");

        var tool = toolName != null ? ResolveTool(toolName) : null;
        var templateGroup = LoadTemplateGroup(twinName, groupName);
        var newGroup = CloneGroup(templateGroup, paintableTexture, tool);

        if (partManager.groups == null)
            partManager.groups = new List<PartManager.GroupData>();
        partManager.groups.Add(newGroup);

        // replay the cloned commands onto the body texture
        var oldListening = partManager.Listening;
        partManager.Listening = false;
        foreach (var part in newGroup.groupParts)
            partManager.RefreshPart(part);
        partManager.Listening = oldListening;

        AddGroupToOverlay(newGroup);
        return newGroup;
    }

    /// <summary>
    /// Paints a region with the tool the user currently has selected in the app. Region
    /// templates are sphere-painted, so only marker/filler tools can carry them — if the
    /// active tool is a sticker/text tool (or nothing is active), the first marker tool
    /// found in the Tools container is used instead.
    /// </summary>
    public static PartManager.GroupData PaintTemplateGroupWithCurrentTool(string twinName, string groupName)
    {
        return PaintTemplateGroup(twinName, groupName, ResolveCurrentOrDefaultToolName());
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

    private static PartManager.GroupData CloneGroup(PartManager.GroupData templateGroup, CwPaintableTexture paintableTexture, ToolInfo tool)
    {
        var newGroup = new PartManager.GroupData
        {
            id = Guid.NewGuid().ToString(),
            name = templateGroup.name,
            visible = true,
            selected = false,
        };

        foreach (var templatePart in templateGroup.groupParts)
        {
            // the deserialized template objects are fresh instances owned by nobody else,
            // so they can be adopted directly — only ids and texture bindings must be renewed
            var newPart = templatePart;
            newPart.id = Guid.NewGuid().ToString();
            newPart.group = newGroup;
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
            newGroup.groupParts.Add(newPart);
        }
        return newGroup;
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

    /// <summary>Adds the UI entry for the new group to the group overlay — mirroring what
    /// GroupManager.build() does per group. Deliberately NOT GroupManager.rebuild(): that
    /// tears down the whole overlay and re-runs HandleEdit on the current group, which plays
    /// the group-selection sound and resets selection/scroll position.</summary>
    private static void AddGroupToOverlay(PartManager.GroupData groupData)
    {
        var groupManager = UnityEngine.Object.FindObjectOfType<GroupManager>(true);
        if (groupManager == null)
        {
            return;
        }
        Group group = groupManager.createPersistentGroup(groupData);
        group.gameObject.transform.GetComponentInChildren<Text>().text = groupData.name;
    }
}
