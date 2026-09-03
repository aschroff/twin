using UnityEngine;

/// <summary>
/// The mode that shows the version sync screen. Same shape as the other modes: it exists so the
/// InteractionController has a GameObject to switch to, and it names the panel the UIController
/// should show.
/// </summary>
public class TwinVersionSyncMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;

    private void OnEnable()
    {
        UIController.ShowUI("TwinVersionSync");

        // Nothing on this screen is aimed at the model behind it, and a stray tap that rotated
        // the twin while a list is open would be a surprise.
        if (Touch != null) Touch.SetActive(false);
    }
}
