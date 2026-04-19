using UnityEditor;
using UnityEngine;

public static class CopyPathInformation
{
    [MenuItem("GameObject/Copy Path", false, 0)]
    private static void CopySelectedGameObjectPath()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Copy Path", "No GameObject selected.", "OK");
            return;
        }

        var path = GetHierarchyPath(go.transform);
        EditorGUIUtility.systemCopyBuffer = path;
        Debug.Log("Copied GameObject path to clipboard: " + path);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

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
