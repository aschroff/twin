using UnityEngine;

/*
 * Offers the ways a document can reach the twin - a photo or a file. The panel is the action list
 * the Menu mode uses, so the two entries are configured on its MenuManager.
 */
public class UploadMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;

    private void OnEnable()
    {
        UIController.ShowUI("Upload");
        Touch.SetActive(false);
    }
}
