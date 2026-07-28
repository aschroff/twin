using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class GroupDisplay : MonoBehaviour 
{
    [SerializeField] private PartManager partmanager; 
    private void Update()
    {
        TextMeshProUGUI currentGroupText = this.gameObject.GetComponent<TextMeshProUGUI>();
        if (partmanager.currentGroup != null && partmanager.currentGroup.group != null )
        {
            currentGroupText.text = partmanager.currentGroup.name;  
        }
        else
        {
            currentGroupText.text = "-";
        }
        
    }

}
