using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Lean.Gui;
using UnityEngine.UI;

public class GroupListSelectionManager : MonoBehaviour, ItemFile
{
    private string childTagSelector = "GroupSelector"; 
    private string childTagDelete = "GroupDelete"; 
    [SerializeField] public PartManager partmanager;
    [SerializeField] public GroupManager groupmanager;
    [SerializeField] public GameObject prefab;
    [SerializeField] public LeanPulse notification;
    [SerializeField] public PartListManager partListManager;
    public void build()
    {
        partmanager.Listening = false;
        foreach (PartManager.GroupData groupdata in partmanager.groups) 
        {
            GroupSelect group = createPersistentGroup(groupdata);
            Transform detail_text = group.gameObject.transform.Find("Text").transform.Find("Text");
            if (detail_text != null)
            {
                detail_text.GetComponent<Text>().text = groupdata.name;
            }
        }
        
    }

    public List<GroupSelect> getGroups()
    {
        List<GroupSelect> groups = new List<GroupSelect>();
        Debug.Log("Start Groups: " + groups.Count);
        foreach (GroupSelect groupSelect in this.transform.GetComponentsInChildren<GroupSelect>())
        {
            if (groupSelect.gameObject.transform.GetComponentInChildren<Toggle>().isOn)
            {
                groups.Add(groupSelect);
            }
                

        }
        Debug.Log("End Groups: " + groups.Count);
        return groups;
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
        StartCoroutine(WaitForNextFrame());
    }
    
    private IEnumerator WaitForNextFrame()
    {
        yield return null; // Wait for the next frame
        partListManager.rebuild();
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
    
    public GroupSelect createPersistentGroup(PartManager.GroupData groupdata)
    {
        GroupSelect group = createGroup(true);
        group.groupdata = groupdata;
        return group;
    }
    public GroupSelect createGroup(bool buttons)
    {
        GameObject group = Instantiate(prefab);
        group.transform.SetParent(this.transform, false);
        group.transform.localScale = prefab.transform.localScale;
        setButtons(group, buttons);
        
        GroupSelect groupcomponent = group.GetComponent<GroupSelect>();
        groupcomponent.groupparent = this.gameObject;
        return groupcomponent;
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

