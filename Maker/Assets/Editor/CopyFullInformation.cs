using System.Text;
using UnityEditor;
using UnityEngine;

public static class CopyFullInformation
{
    [MenuItem("GameObject/Copy Full Information", false, 0)]
    private static void CopySelectedGameObjectInfo()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Copy Full Information", "No GameObject selected.", "OK");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Scene: " + go.scene.name);
        builder.AppendLine("Path: " + GetHierarchyPath(go.transform));
        builder.AppendLine("Components:");

        var components = go.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component == null)
            {
                builder.AppendLine("- Missing (Script)");
                continue;
            }

            var type = component.GetType();
            builder.AppendLine("- " + type.FullName);
        }

        EditorGUIUtility.systemCopyBuffer = builder.ToString();
        Debug.Log("Copied GameObject information to clipboard:\n" + builder);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        var path = transform.name;
        var parent = transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
