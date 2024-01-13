using System.Collections;
using System.Collections.Generic;
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
        Debug.Log("HandleEdit");
        GroupManager groupmanager = groupparent.GetComponentInChildren<GroupManager>();
        PartManager partmanager = groupmanager.partmanager;
        if (this.persistent == false) {
            this.persistent = true;
            this.GenerateGuid();
            groupdata = partmanager.StartNewGroup(this);            
            groupdata.id = this.id;
            groupmanager.createNewNonpersistentGroup();
        }
        foreach (Outline child in groupparent.GetComponentsInChildren<Outline>())
        {
            child.enabled = false;
        }
        foreach (Outline child in this.transform.GetComponentsInChildren<Outline>())
        {
            child.enabled = true;
        }
        partmanager.currentGroup = groupdata;
        partmanager.currentGroup.name = this.transform.GetComponentInChildren<InputField>().text;
        Debug.Log("HandledEdit");
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

}
