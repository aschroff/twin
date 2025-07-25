using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Group : Item
{

    [SerializeField] public PartManager.GroupData groupdata = null;
    [SerializeField] public GameObject groupparent;

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    public void HandleEdit()
    {
        GroupManager groupmanager = groupparent.GetComponentInChildren<GroupManager>();
        PartManager partmanager = groupmanager.partmanager;
        foreach (LeanToggle child in groupparent.GetComponentsInChildren<LeanToggle>())
        {
            child.On = false;
        }
        foreach (LeanToggle child in this.transform.GetComponentsInChildren<LeanToggle>())
        {
            child.On = true;
        }
        partmanager.currentGroup = groupdata;
        partmanager.currentGroup.name = this.transform.GetComponentInChildren<Text>().text;
        UpdateCurrent(partmanager.currentGroup.name);
    }

    
    public void HandleClick()
    {
        Debug.Log("HandleClick");
        GroupManager groupmanager = groupparent.GetComponentInChildren<GroupManager>();
        bool boolOn = this.gameObject.GetComponentInChildren<Toggle>().isOn;
        groupdata.visible = boolOn;
        PartManager partmanager = groupmanager.partmanager;
        groupmanager.Refresh();
        Debug.Log("HandledClick");

    }
    public void HandleDelete()
    {
        Debug.Log("HandleDelete");
        GroupManager groupmanager = groupparent.GetComponentInChildren<GroupManager>();
        groupmanager.DeleteGroup(groupdata, this.gameObject);
        Debug.Log("HandledDelete");
    }

    private void UpdateCurrent(string current)
    {
        foreach (GameObject currentGroupTextGameObject in GameObject.FindGameObjectsWithTag("CurrentGroup"))
        {
            TextMeshProUGUI currentGroupText = currentGroupTextGameObject.GetComponent<TextMeshProUGUI>();
            currentGroupText.text = current;
        }

        
    }
}


