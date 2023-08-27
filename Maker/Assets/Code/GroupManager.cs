using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GroupManager : MonoBehaviour, IDataPersistence
{
    [SerializeField] public PartManager partmanager;
    [SerializeField] public GameObject prefab;

    public void LoadData(ConfigData data)
    {
        bool tempListening = partmanager.Listening;
        partmanager.Listening = false;
        List<string> groupList = data.groupList;
        SerializableDictionary<string, string> partList = data.partList;
        SerializableDictionary<string, string> commandList = data.commandList;
        foreach (string idGroup in groupList) 
        {
            Group group = createPersistentGroup(idGroup);

            foreach (string idPart in partList.Keys)
            {
                if (partList.ContainsKey(idPart) && partList[idPart] == idGroup)
                {
                    PartManager.PartData partdata = partmanager.addPart(group.groupdata, idPart);
                    foreach (string idCommand in commandList.Keys)
                    {
                        if(commandList.ContainsKey(idCommand) && commandList[idCommand] == idPart)
                        {
                            partmanager.addCommand(partdata, idCommand);
                        }
                    }
                }
                
            }

            
        }
        partmanager.Listening = tempListening;
    }

    public void SaveData(ConfigData data)
    {
       foreach (PartManager.GroupData group in partmanager.groups)
        {
            if (group.group.persistent == true) {
                data.groupList.Add(group.group.id);
                foreach (PartManager.PartData part in group.groupParts)
                {
                    data.partList.Add(part.id, group.group.id);
                    foreach (PartManager.CommandDataTwin command in part.partCommands)
                    {
                        data.commandList.Add(command.id, part.id);
                    }
                }
            }
        } 
    }

    void Start()
    {
        this.createNewNonpersistentGroup();
    }

    public void createNewNonpersistentGroup()
    {
        createGroup();
    }
    public Group createPersistentGroup(string id)
    {
        Group group = createGroup();
        group.id = id;
        PartManager.GroupData groupdata = partmanager.addGroup(group);
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
