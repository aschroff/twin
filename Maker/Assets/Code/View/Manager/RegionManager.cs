using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fills its panel with one read-only entry per body region from the bundled
/// template catalog (see PartTemplateService / FEATURE_TEXT_TO_PART.md).
/// Clicking an entry's icon paints that region onto the current twin.
/// </summary>
public class RegionManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        Delete();
        Create();
    }

    private void Create()
    {
        foreach (PartTemplateService.TemplateTwinInfo twin in PartTemplateService.GetTemplateCatalog().twins)
        {
            foreach (string region in twin.regions)
            {
                CreateRegionEntry(twin.twinName, region);
            }
        }
    }

    private void Delete()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform childTransform = transform.GetChild(i);
            Destroy(childTransform.gameObject);
        }
    }

    private void CreateRegionEntry(string twinName, string region)
    {
        GameObject entry = Instantiate(prefab);
        entry.transform.SetParent(this.transform, false);
        entry.transform.localScale = prefab.transform.localScale;
        Transform action = entry.transform.Find("Action");
        action.Find("Twin").GetComponent<Text>().text = twinName;
        action.Find("Region").GetComponent<Text>().text = region;
        Button buttonPaint = entry.transform.Find("Icon").GetComponentInChildren<Button>();
        buttonPaint.onClick.AddListener(() => { Paint(twinName, region); });
    }

    /// <summary>
    /// Paints the selected region onto the current twin, using the tool the user currently
    /// has selected (falls back to the first marker tool when the active tool cannot carry
    /// region templates, e.g. a sticker).
    /// </summary>
    public void Paint(string twinName, string region)
    {
        PartTemplateService.PaintTemplateGroupWithCurrentTool(twinName, region);
    }
}
