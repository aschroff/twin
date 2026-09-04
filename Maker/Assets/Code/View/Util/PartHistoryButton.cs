using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// The Undo and Redo buttons of the editing header. One click takes back (or brings back) the
/// last part painted in this session - see PartManager.Undo / Redo.
///
/// Replaces PaintIn3D's CwButtonUndoAll / CwButtonRedoAll, which undid texture states: that mode
/// kept a full copy of the body texture per stroke and got the app killed on iPads with little
/// memory. Like those components, this one reacts to the click itself and dims the button through
/// its CanvasGroup while there is nothing to do, so the prefab needs no further wiring.
/// </summary>
public class PartHistoryButton : MonoBehaviour, IPointerClickHandler
{
    public enum HistoryAction
    {
        Undo,
        Redo
    }

    [SerializeField] private HistoryAction action = HistoryAction.Undo;

    private const float AlphaAvailable = 1.0f;
    private const float AlphaUnavailable = 0.5f;

    private PartManager partManager;
    private CanvasGroup canvasGroup;

    /// <summary>Undo or Redo - which of the two this button does.</summary>
    public HistoryAction Action
    {
        get { return action; }
        set { action = value; }
    }

    /// <summary>Whether a click would do anything right now.</summary>
    public bool Available
    {
        get
        {
            PartManager manager = Manager;
            if (manager == null)
            {
                return false;
            }
            return action == HistoryAction.Undo ? manager.CanUndo : manager.CanRedo;
        }
    }

    private PartManager Manager
    {
        get
        {
            if (partManager == null)
            {
                partManager = FindObjectOfType<PartManager>();
            }
            return partManager;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Trigger();
    }

    /// <summary>Does what a click does. Returns whether a part was undone or redone.</summary>
    public bool Trigger()
    {
        PartManager manager = Manager;
        if (manager == null)
        {
            return false;
        }
        return action == HistoryAction.Undo ? manager.Undo() : manager.Redo();
    }

    private void Update()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Available ? AlphaAvailable : AlphaUnavailable;
        }
    }
}
