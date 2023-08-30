using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Group : Item
{

    [SerializeField] public PartManager.GroupData groupdata = null;
    [SerializeField] public GameObject groupparent;


    // Start is called before the first frame update
    public void HandleEdit()
    {
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

    }


}
