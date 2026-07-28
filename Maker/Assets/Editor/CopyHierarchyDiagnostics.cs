using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class CopyHierarchyDiagnostics
{
    [MenuItem("GameObject/Copy Hierarchy Diagnostics", false, 0)]
    private static void CopyDiagnostics()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Copy Hierarchy Diagnostics", "No GameObject selected.", "OK");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Scene: " + go.scene.name);
        builder.AppendLine("Root path: " + GetHierarchyPath(go.transform));
        builder.AppendLine();
        AppendGameObjectInfo(builder, go.transform, 0);

        EditorGUIUtility.systemCopyBuffer = builder.ToString();
        Debug.Log("Copied hierarchy diagnostics to clipboard:\n" + builder);
    }

    private static void AppendGameObjectInfo(StringBuilder builder, Transform t, int depth)
    {
        var indent = new string(' ', depth * 2);
        var go = t.gameObject;

        builder.AppendLine($"{indent}[{GetHierarchyPath(t)}]");
        builder.AppendLine($"{indent}  activeSelf={go.activeSelf}  activeInHierarchy={go.activeInHierarchy}");

        foreach (var component in go.GetComponents<Component>())
        {
            if (component == null)
            {
                builder.AppendLine($"{indent}  - Missing (Script)");
                continue;
            }

            var typeName = component.GetType().FullName;

            if (component is Button btn)
            {
                builder.AppendLine($"{indent}  - {typeName}");
                builder.AppendLine($"{indent}      interactable={btn.interactable}");
                var count = btn.onClick.GetPersistentEventCount();
                builder.AppendLine($"{indent}      onClick listeners={count} (serialized)");
                for (var i = 0; i < count; i++)
                {
                    var target = btn.onClick.GetPersistentTarget(i);
                    var method = btn.onClick.GetPersistentMethodName(i);
                    var targetName = target != null ? target.GetType().Name + " (" + ((Object)target).name + ")" : "null";
                    var arg = GetPersistentStringArgument(btn.onClick, i);
                    var argStr = arg != null ? $" arg=\"{arg}\"" : "";
                    builder.AppendLine($"{indent}        [{i}] {targetName} -> {method}{argStr}");
                }
            }
            else if (component is Behaviour behaviour)
            {
                builder.AppendLine($"{indent}  - {typeName}  enabled={behaviour.enabled}");
            }
            else
            {
                builder.AppendLine($"{indent}  - {typeName}");
            }
        }

        for (var i = 0; i < t.childCount; i++)
            AppendGameObjectInfo(builder, t.GetChild(i), depth + 1);
    }

    private static string GetPersistentStringArgument(UnityEventBase evt, int index)
    {
        try
        {
            var persistentCallsField = typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.NonPublic | BindingFlags.Instance);
            var group = persistentCallsField?.GetValue(evt);
            var callsField = group?.GetType().GetField("m_Calls", BindingFlags.NonPublic | BindingFlags.Instance);
            var calls = callsField?.GetValue(group) as IList;
            if (calls == null || index >= calls.Count) return null;
            var call = calls[index];
            var argsField = call.GetType().GetField("m_Arguments", BindingFlags.NonPublic | BindingFlags.Instance);
            var args = argsField?.GetValue(call);
            var strField = args?.GetType().GetField("m_StringArgument", BindingFlags.NonPublic | BindingFlags.Instance);
            return strField?.GetValue(args) as string;
        }
        catch
        {
            return null;
        }
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
