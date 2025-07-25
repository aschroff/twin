using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GroupEdit : Item
{

    [SerializeField] public PartManager.GroupData groupdata = null;
    [SerializeField] public GameObject groupparent;

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    public void showDetails()
    {
        InteractionController.EnableMode("GroupDetail");
    }

    public void HandleEdit()
    {
        GroupListManager groupmanager = groupparent.GetComponentInChildren<GroupListManager>();
        PartManager partmanager = groupmanager.partmanager;
        if (this.persistent == false) {
            this.persistent = true;
            groupmanager.setButtons(this.gameObject, true);
            this.GenerateGuid();
            groupdata = partmanager.StartNewGroup(null);            
            groupdata.id = this.id;
            groupmanager.createNewNonpersistentGroup();
        }
        partmanager.currentGroup = groupdata;
        partmanager.currentGroup.name = this.transform.GetComponentInChildren<Text>().text;
        UpdateCurrent(partmanager.currentGroup.name);
        groupmanager.groupmanager.rebuild();
    }

    
    public void HandleDelete()
    {
        Debug.Log("HandleDelete");
        GroupListManager groupmanager = groupparent.GetComponentInChildren<GroupListManager>();
        groupmanager.DeleteGroup(groupdata, this.gameObject);
        groupmanager.groupmanager.rebuild();
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


