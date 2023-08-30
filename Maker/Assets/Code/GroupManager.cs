using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public class GroupManager : MonoBehaviour
{
    [SerializeField] public PartManager partmanager;
    [SerializeField] public GameObject prefab;

    public void build()
    {
        bool tempListening = partmanager.Listening;
        partmanager.Listening = false;
        foreach (PartManager.GroupData groupdata in partmanager.groups) 
        {
            Group group = createPersistentGroup(groupdata);
            group.gameObject.transform.GetComponentInChildren<InputField>().text = groupdata.name;
        }
        partmanager.Listening = tempListening;
    }


    void Start()
    {
        this.createNewNonpersistentGroup();
    }

    public void createNewNonpersistentGroup()
    {
        createGroup();
    }
    public Group createPersistentGroup(PartManager.GroupData groupdata)
    {
        Group group = createGroup();
        group.id = groupdata.id;
        group.groupdata = groupdata;
        group.persistent = true;
        return group;
    }
    public Group createGroup()
    {
        GameObject group = Instantiate(prefab);
        group.transform.SetParent(this.transform, false);
        group.transform.localScale = prefab.transform.localScale;
        Group groupcomponent = group.GetComponent<Group>();
        groupcomponent.groupparent = this.gameObject;
        return groupcomponent;
    }

}
