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
        currentGroupText.text = partmanager.currentGroup.name;
    }

}
