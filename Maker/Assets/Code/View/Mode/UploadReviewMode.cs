using UnityEngine;

/*
 * Shows what an uploaded document is about to become, before it reaches the twin.
 * The panel is filled by DocumentReviewManager.
 */
public class UploadReviewMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;

    private void OnEnable()
    {
        UIController.ShowUI("UploadReview");
        Touch.SetActive(false);
    }
}
