using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public class GroupManager : MonoBehaviour, IDataPersistence
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
        //this.Refresh();
        partmanager.Listening = tempListening;
    }
    
    
    public void clear()
    {
        bool tempListening = partmanager.Listening;
        for (int j = 0; j < this.transform.childCount; j++) {
            GameObject child = this.transform.GetChild(j).gameObject;
            Destroy(child);
        }
        Debug.Log("not found");
        partmanager.Listening = tempListening;
    }

    public void rebuild()
    {
        clear();
        build();
        this.createNewNonpersistentGroup();
    }
    
    void Start()
    {
        //this.createNewNonpersistentGroup();
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
        group.groupdata.group = group;
        group.persistent = true;
        //TODO persistent visible flag
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
    public void DeleteGroup(PartManager.GroupData groupdata, GameObject gameobjectGroup)
    {
        groupdata.group.persistent = false;
        partmanager.deleteGroup(groupdata);
        //gameobjectGroup.SetActive(false);
        Destroy(gameobjectGroup);
        this.Refresh();
    }
    public void Refresh()
    {
        partmanager.Erase();
        /*foreach (Toggle child in this.GetComponentsInChildren<Toggle>())
        {
            Group group_for_child = child.transform.parent.GetComponent<Group>();
            if (group_for_child.persistent == true && child.isOn == true && group_for_child.groupdata != null)
            {
                //partmanager.Apply(group_for_child.groupdata);
                group_for_child.groupdata.visible = true;

            }
            else
            {
                group_for_child.groupdata.visible = false;
            }

        }*/
        partmanager.Refresh();
    }
    
    public  void handleChange(string profile)
    {
        rebuild();
    }
    public  void handleCopyChange(string profile)
    {
        rebuild();
    }
    public  void handleDelete(string profile)
    {
        
    }
    
    public void LoadData(ConfigData data)
    {
     
    }

    public void SaveData(ConfigData data)
    {
      
    }
    
    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

}
