using UnityEngine;

/// <summary>
/// The mode that shows the version download screen. The twin of
/// <see cref="TwinVersionSyncMode"/>, and the same shape as the other modes: it exists so the
/// InteractionController has a GameObject to switch to, and it names the panel the UIController
/// should show.
/// </summary>
public class TwinVersionDownloadMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;

    private void OnEnable()
    {
        UIController.ShowUI("TwinVersionDownload");

        // Nothing on this screen is aimed at the model behind it, and a stray tap that rotated
        // the twin while a list is open would be a surprise.
        if (Touch != null) Touch.SetActive(false);
    }
}
