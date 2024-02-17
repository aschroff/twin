using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class GroupDisplay : MonoBehaviour 
{
    [SerializeField] private PartManager partmanager; 
    private void OnEnable()
    {
        Text currentGroupText = this.gameObject.GetComponent<Text>();
        if (partmanager.currentGroup != null)
        {
            currentGroupText.text = partmanager.currentGroup.name;  
        }
        else
        {
            currentGroupText.text = "<no group>";
        }
        
    }

}
