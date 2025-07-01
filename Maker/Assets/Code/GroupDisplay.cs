using UnityEngine;
using UnityEngine.UI;
public class GroupDisplay : MonoBehaviour 
{
    [SerializeField] private PartManager partmanager; 
    private void Update()
    {
        Text currentGroupText = this.gameObject.GetComponent<Text>();
        if (partmanager.currentGroup != null && partmanager.currentGroup.group != null )
        {
            currentGroupText.text = partmanager.currentGroup.name;  
        }
        else
        {
            currentGroupText.text = "<no group>";
        }
        
    }

}
