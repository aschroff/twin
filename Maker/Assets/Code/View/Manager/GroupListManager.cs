using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Lean.Gui;
using UnityEngine.UI;
using TMPro;

public class GroupListManager : MonoBehaviour, ItemFile
{
    private string childTagSelector = "GroupSelector"; 
    private string childTagDelete = "GroupDelete"; 
    [SerializeField] public PartManager partmanager;
    [SerializeField] public GroupManager groupmanager;
    [SerializeField] public GameObject prefab;
    [SerializeField] public LeanPulse notification;
    public void build()
    {
        PartManager.GroupData saveCurrentGroup = partmanager.currentGroup;
        bool tempListening = partmanager.Listening;
        partmanager.Listening = false;
        foreach (PartManager.GroupData groupdata in partmanager.groups) 
        {
            GroupEdit group = createPersistentGroup(groupdata);
            InputField input_field = group.gameObject.transform.GetComponentInChildren<InputField>();
            if (input_field != null)
            {
                input_field.text = groupdata.name;
            }
            Transform detail_text = group.gameObject.transform.Find("Details");
            if (detail_text != null)
            {
                detail_text.GetComponent<Text>().text = groupdata.groupParts.Count + " parts";
            }
        }
        //this.Refresh();
        partmanager.Listening = tempListening;
        if (saveCurrentGroup != null && saveCurrentGroup.group != null)
        {
            saveCurrentGroup.group.HandleEdit();
        }
        
    }
    
    
    public void clear()
    {
        bool tempListening = partmanager.Listening;
        for (int j = 0; j < this.transform.childCount; j++) {
            GameObject child = this.transform.GetChild(j).gameObject;
            Destroy(child);
        }
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
        this.rebuild();
    }
    
    void OnEnable()
    {
        this.rebuild();
    }

    public void setButtons(GameObject group, bool value)
    {
        Transform[] allChildren = group.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if ((child.gameObject.tag == childTagDelete) || (child.gameObject.tag == childTagSelector))
            {
                child.gameObject.SetActive(value);
            }
        }
    }

    public void createNewNonpersistentGroup()
    {
        createGroup(false);
    }
    public GroupEdit createPersistentGroup(PartManager.GroupData groupdata)
    {
        GroupEdit group = createGroup(true);
        group.id = groupdata.id;
        group.groupdata = groupdata;
        group.persistent = true;
        //TODO persistent visible flag
        return group;
    }
    public GroupEdit createGroup(bool buttons)
    {
        GameObject group = Instantiate(prefab);
        group.transform.SetParent(this.transform, false);
        group.transform.localScale = prefab.transform.localScale;
        setButtons(group, buttons);
        
        GroupEdit groupcomponent = group.GetComponent<GroupEdit>();
        groupcomponent.groupparent = this.gameObject;
        return groupcomponent;
    }
    public void DeleteGroup(PartManager.GroupData groupdata, GameObject gameobjectGroup)
    {
        groupdata.group.persistent = false;
        partmanager.deleteGroup(groupdata);
        if (partmanager.currentGroup == null)
        {
            if (partmanager.trySetCurrentGroupIfEmpty() == null)
            {
                foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
                {
                    text.text = "No group selectable because no group is available";
                }
            }
            else
            {
                foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
                {
                    text.text = "The group " + partmanager.currentGroup.name + " is now selected";
                }
                partmanager.currentGroup.group.HandleEdit();
            }
            notification.Pulse();
        }


        TextMeshProUGUI currentGroupText = null; 
        foreach (GameObject currentGroupTextGameObject in GameObject.FindGameObjectsWithTag("CurrentGroup"))
        {
            currentGroupText = currentGroupTextGameObject.GetComponent<TextMeshProUGUI>();

            if(partmanager.currentGroup == null) //no group is currently selected
            {
                currentGroupText.text = "-";
            }
            else //if group is selected, set current group name in overview 
            {
                currentGroupText.text = partmanager.currentGroup.name;
            }
        }
        
        
        //gameobjectGroup.SetActive(false);
        Destroy(gameobjectGroup);
        this.Refresh();
    }
    public void Refresh()
    {
        partmanager.Erase();
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
