using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Lean.Gui;
using UnityEngine.UI;

public class GroupManager : MonoBehaviour, ItemFile, IDataPersistence
{
    private string childTagSelector = "GroupSelector"; 
    private string childTagDelete = "GroupDelete"; 
    [SerializeField] public PartManager partmanager;
    [SerializeField] public GameObject prefab;
    [SerializeField] public LeanPulse notification;
    public void build()
    {
        PartManager.GroupData saveCurrentGroup = partmanager.currentGroup;
        bool tempListening = partmanager.Listening;
        partmanager.Listening = false;
        foreach (PartManager.GroupData groupdata in partmanager.groups) 
        {
            Group group = createPersistentGroup(groupdata);
            group.gameObject.transform.GetComponentInChildren<InputField>().text = groupdata.name;
        }
        //this.Refresh();
        partmanager.Listening = tempListening;
        saveCurrentGroup.group.HandleEdit();
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
    public Group createPersistentGroup(PartManager.GroupData groupdata)
    {
        Group group = createGroup(true);
        group.id = groupdata.id;
        group.groupdata = groupdata;
        group.groupdata.group = group;
        group.persistent = true;
        //TODO persistent visible flag
        return group;
    }
    public Group createGroup(bool buttons)
    {
        GameObject group = Instantiate(prefab);
        group.transform.SetParent(this.transform, false);
        group.transform.localScale = prefab.transform.localScale;
        setButtons(group, buttons);
        
        Group groupcomponent = group.GetComponent<Group>();
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
        
        
        Text currentGroupText = null; 
        foreach (GameObject currentGroupTextGameObject in GameObject.FindGameObjectsWithTag("CurrentGroup"))
        {
            currentGroupText = currentGroupTextGameObject.GetComponent<Text>();

            if(partmanager.currentGroup == null) //no group is currently selected
            {
                currentGroupText.text = "<no group>";
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

    public void Show(bool visible)
    {   
        RectTransform recttransform = this.gameObject.transform.parent.GetComponent<RectTransform>();
        float width = recttransform.rect.width;
        if (visible & recttransform.anchoredPosition.x  >= 0)
        {
            
        }
        else if (!visible  & recttransform.anchoredPosition.x  >= 0)
        {
            recttransform.anchoredPosition += new Vector2(-width, 0);
        }  
        else if (!visible & recttransform.anchoredPosition.x  < 0)
        {
            
        } 
        else if (visible  & recttransform.anchoredPosition.x  < 0)
        {
            recttransform.anchoredPosition += new Vector2(width, 0);
        }  
    }

    public void toggleShow()
    {
        RectTransform recttransform = this.gameObject.transform.parent.GetComponent<RectTransform>();
        bool newVisible = (recttransform.anchoredPosition.x < 0);
        Show(newVisible);
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
