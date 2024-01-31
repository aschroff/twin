using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PaintIn3D;
using System.Linq;
using System;

public class PartManager : P3dCommandSerialization, IDataPersistence
{

	/// <summary>Aggregates single commands by the same tool created in a single sequence</summary>
	//[SerializeField] protected List<PartData> parts = new List<PartData>();
	[SerializeField] public List<GroupData> groups;
	[SerializeField] public bool startNewPart = true;
	[SerializeField] public bool startNewGroup = true;
	[SerializeField] public GroupData currentGroup;
	[SerializeField] public PartData currentPart;
	[SerializeField] public GameObject listTools;
	[SerializeField] public GameObject groupmanagerGameobject;
	private GameObject activeTool;
	private GameObject lastActiveTool;
	private P3dPaintableTexture lastTexture;
	//[SerializeField] public bool temp_skiploading = false;

	public GameObject relatedGameObject()
	{
		return this.gameObject;
	}
	

	[System.Serializable]
	
	
	public class CommandDataTwin
	{
		public string id;
		public CommandData data;
	}

	[System.Serializable]
	public class PartData
	{
		public List<CommandDataTwin> partCommands = new List<CommandDataTwin>();
		public string id;
	}

	[System.Serializable]
	public class GroupData
	{
		public List<PartData> groupParts = new List<PartData>();
		public Group group;
		public string id;
		public string name;
		public bool visible;
	}

	private void setActiveTool()
	{
		foreach (Transform child in listTools.transform)
		{
			if (child.gameObject.activeSelf == true)
			{
				activeTool = child.gameObject;
			}
		}
	}


	public void addCommand(CommandDataTwin commandData)
	{
		if (startNewPart == true)
		{
			PartData newPart = new PartData();
			newPart.id = System.Guid.NewGuid().ToString();
			newPart.partCommands.Add(commandData);
			//parts.Add(newPart);
			currentPart = newPart;
			HandleNewPart(newPart);
			startNewPart = false;
			setActiveTool();
			lastActiveTool = activeTool;
			lastTexture = commandData.data.PaintableTexture;
			return;
		}
		if (commandData.data.PaintableTexture != lastTexture)
		{
			startNewPart = true;
			addCommand(commandData);
			return;
		}
		setActiveTool();
		if (lastActiveTool != activeTool)
		{
			startNewPart = true;
			addCommand(commandData);
			return;
		}
		//parts[^1].partCommands.Add(commandData);
		currentPart.partCommands.Add(commandData);

	}


	protected virtual void OnEnable()
	{
		P3dPaintableTexture.OnAddCommandGlobal += HandleAddCommandGlobal;
	}

	protected virtual void OnDisable()
	{
		P3dPaintableTexture.OnAddCommandGlobal -= HandleAddCommandGlobal;
	}

	public int count_parts()
	{
		return commandDatas.Count;
	}

	private void HandleAddCommandGlobal(P3dPaintableTexture paintableTexture, P3dCommand command)
	{
		if (checkRedundancy(command))
		{
			return;
		}
		base.HandleAddCommandGlobal(paintableTexture, command);
		if (base.listening == true)
		{
			// Ignore preview paint commands
			if (command.Preview == false)
			{
				CommandDataTwin newCommand = new CommandDataTwin();
				newCommand.data = commandDatas[^1];
				newCommand.id = System.Guid.NewGuid().ToString();
				addCommand(newCommand);
			}
		}
	}

	private void HandleNewPart(PartData newPart)
	{
		if (currentGroup != null)
        {
			currentGroup.groupParts.Add(newPart);
		}
		
	}

	public GroupData StartNewGroup(Group group)
	{
		if (groups == null)
		{
			groups = new List<GroupData>();
			GroupData firstGroup = new GroupData();
			firstGroup.group = group;
			groups.Add(firstGroup);
			currentGroup = firstGroup;
			return firstGroup;
		}
		else
		{
			GroupData nextGroup = new GroupData();
			nextGroup.group = group;
			nextGroup.visible = true;
			groups.Add(nextGroup);
			currentGroup = nextGroup;
			return nextGroup;
		}
		startNewPart = true;
	}

	public GroupData addGroup(Group group)
	{
		GroupData nextGroup = new GroupData();
		nextGroup.group = group;
		groups.Add(nextGroup);
		currentGroup = nextGroup;
		return nextGroup;

	}
	public PartData addPart(GroupData groupdata, string id)
	{
		PartData newPart = new PartData();
		newPart.id = id;
		//parts.Add(newPart);
		groupdata.groupParts.Add(newPart);
		return newPart;

	}
	public CommandDataTwin addCommand(PartData partdata, string id)
	{
		CommandDataTwin newCommand = new CommandDataTwin();
		newCommand.id = id;
		partdata.partCommands.Add(newCommand);
		return newCommand;

	}

	public void SaveData(ConfigData data)
	{
		currentPart = null;
		currentGroup = null;
		startNewGroup = true;
		startNewPart = true;
		var json = JsonUtility.ToJson(this);
		data.commandDetails = json;
	}
	public void LoadData(ConfigData data)
	{
		//InteractionController.EnableMode("EditSticker");
		try
		{
			//if (temp_skiploading == false)
			//{				
				var json = data.commandDetails;
				JsonUtility.FromJsonOverwrite(json, this);
			//}
		}
		catch (Exception e)
        {
			Erase();
		}
		GroupManager groupmanager = groupmanagerGameobject.GetComponent<GroupManager>();
		groupmanager.build();
		
	}
	private P3dCommand Apply(CommandDataTwin commandData)
	{

		// Make sure it's still valid
		if (commandData.data.PaintableTexture != null)
		{
			// Convert the command to world space
			var command = commandData.data.LocalCommand.SpawnCopyWorld(commandData.data.PaintableTexture.transform);

			// Apply it to its paintable texture
			commandData.data.PaintableTexture.AddCommand(command);

			// Pool
			//command.Pool();

			Debug.Log("Switch on command" + commandData.id);
			return command;
		}
		return null;
	}
	public P3dCommand Apply(GroupData groupData)
	{
		var oldListening = listening;
		P3dCommand last = null;
		listening = false;
		foreach (PartData partData in groupData.groupParts)
        {
			foreach (CommandDataTwin commandData in partData.partCommands)
            {
				last = this.Apply(commandData);
			}
        }		
		listening = oldListening;
		//last.Pool();
		return last;
	}
	[ContextMenu("Erase texture")]
	public void Erase()
	{
		Debug.Log("Start erase");
		// Ignore added commands while this method is running
		var oldListening = listening;

		listening = false;

		// Loop through all paintable textures, and reset them to their original state
		foreach (var paintableTexture in P3dPaintableTexture.Instances)
		{
			paintableTexture.Clear();
		}

		listening = oldListening;
		Debug.Log("Erased");
	}
  
	[ContextMenu("Re-paint texture")]
	public void Refresh()
	{
		Debug.Log("Start refresh with +groups: " + groups.Count.ToString());
		// Ignore added commands while this method is running
		var oldListening = listening;
		P3dCommand last = null;
		listening = false;

		// Loop through all paintable textures, and reset them to their original state
		foreach (GroupData group_data in groups)
		{
			Debug.Log("Refreshing group: " + group_data.id);
			if (group_data.visible == true)
            {
				last = this.Apply(group_data);
			}
			
		}
		if (last != null) {
			//last.Pool();
			Debug.Log("Pooled");
		}
		listening = oldListening;
		Debug.Log("Refreshed");
	}
	public void deleteGroup(GroupData groupData)
	{
		Debug.Log("Removing group: " + groupData.id);
		string id = groupData.id;
		if (currentGroup == groupData)
        {
			currentGroup = null;
        }
		foreach (PartData part in groupData.groupParts)
        {
			deletePart(part);
        }
		groupData.groupParts.Clear();
		groupData.group.groupdata = null;
		groupData.group = null;
		groups.Remove(groupData);
		Debug.Log("Removed group: " + id);
	}
	public void deletePart(PartData partData)
	{
		Debug.Log("Removing part: " + partData.id);
		if (currentPart == partData)
		{
			currentPart = null;
		}
		else if ((currentPart != null) && (currentGroup != null ))
		{
			//Debug.Log("Current part: " + currentPart.id.ToString());
			Debug.Log("To be deleted part: " + partData.id.ToString());
		}
		else
        {
			Debug.Log("current with null");
        }
		string id = partData.id;
		foreach (CommandDataTwin commandData in partData.partCommands)
		{
			deleteCommand(commandData);
		}
		partData.partCommands.Clear();
		Debug.Log("Removed part: " + id)    ;
	}
	public void deleteCommand(CommandDataTwin commandData)
	{
		Debug.Log("Removing command: " + commandData.id);
		string id = commandData.id;
		commandDatas.Remove(commandData.data);
		Debug.Log("Removed command: " + id);
	}

	public bool checkRedundancy(P3dCommand command)
	{
		if (command is P3dCommandSphere)
		{
			P3dCommandSphere commandSphere = (P3dCommandSphere)command;

			foreach (CommandData commandExist in commandDatas)
			{
				if (commandExist.LocalCommand is P3dCommandSphere)
				{
					P3dCommandSphere commandSphereExists = (P3dCommandSphere)commandExist.LocalCommand;
					if (commandSphereExists.Position.Equals(commandSphere.Position))
					{
						return true;
					}
				}

			}
		}
		return false;
	}

	[ContextMenu("Reset")]
	public void ClearAll()
	{
		base.Clear();
		groups.Clear();
		currentGroup = null;
		currentPart = null;
	}
	[ContextMenu("Clear and refresh all")]
	public void ClearRefreshAll()
	{
		Erase();
		Refresh();
	}


}