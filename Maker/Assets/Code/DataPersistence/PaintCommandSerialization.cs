using System.Collections.Generic;
using PaintCore;
using UnityEngine;

/// <summary>
/// Records the paint commands added to any CwPaintableTexture in the scene, so they can be
/// stored with a twin and replayed later. Base class of PartManager.
///
/// Adopted from the PaintIn3D example script CwCommandSerialization so the plugin can be
/// updated without patching it. The serialized field names are kept, so existing saves and
/// scene data load unchanged.
/// </summary>
public class PaintCommandSerialization : MonoBehaviour
{
    [System.Serializable]
    public struct CommandData
    {
        /// <summary>Target of the command. Not serialized: a Unity object reference is stored
        /// as a session-local instanceID and would resolve to null in any later session.
        /// PartManager binds it after loading.</summary>
        [System.NonSerialized]
        public CwPaintableTexture PaintableTexture;

        [SerializeReference]
        public CwCommand LocalCommand;
    }

    /// <summary>Should this component record added commands?</summary>
    public bool Listening { set { listening = value; } get { return listening; } }
    [SerializeField] private bool listening = true;

    /// <summary>All recorded commands, in local space of their paintable texture.</summary>
    [SerializeField] protected List<CommandData> commandDatas = new List<CommandData>();

    /// <summary>Pools and drops all recorded commands.</summary>
    [ContextMenu("Clear")]
    public void Clear()
    {
        foreach (CommandData commandData in commandDatas)
        {
            commandData.LocalCommand.Pool();
        }

        commandDatas.Clear();
    }

    protected virtual void OnEnable()
    {
        CwPaintableTexture.OnAddCommandGlobal += HandleAddCommandGlobal;
    }

    protected virtual void OnDisable()
    {
        CwPaintableTexture.OnAddCommandGlobal -= HandleAddCommandGlobal;
    }

    protected virtual void HandleAddCommandGlobal(CwPaintableTexture paintableTexture, CwCommand command)
    {
        if (listening == false || command.Preview == true)
        {
            return;
        }

        CommandData commandData = new CommandData();
        commandData.PaintableTexture = paintableTexture;
        commandData.LocalCommand = command.SpawnCopyLocal(paintableTexture.transform);
        commandDatas.Add(commandData);
    }
}
