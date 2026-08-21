using UnityEngine;
using UnityEngine.UI;

/*
 * The screen that shows what an uploaded document turned into, before anything is written to the
 * twin. As long as the analysis is not built yet it shows what was picked and the prompt that
 * would be sent with it - the prompt is what needs reviewing at this point, and it is worth
 * reading on the device, where a real twin with its own groups and tool meanings is behind it.
 *
 * The list of proposed groups, tool meanings and paintings, each to be confirmed by the user,
 * goes onto this screen next. See Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md.
 */
public class DocumentReviewManager : MonoBehaviour
{
    [SerializeField] private Text text;

    public void Show(string body)
    {
        if (text == null)
        {
            Debug.LogError("[DocumentReviewManager] No text field wired - nothing to show.");
            return;
        }

        text.text = body;
        InteractionController.EnableMode("UploadReview");
    }

    /// <summary>What the screen shows at the moment.</summary>
    public string GetShownText()
    {
        return text != null ? text.text : "";
    }
}
