using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PaintIn3D;


public class PartManager : P3dCommandSerialization, IDataPersistence
{

	/// <summary>Aggregates single commands by the same tool created in a single sequence</summary>
	[SerializeField] protected List<PartData> parts = new List<PartData>();
	[SerializeField] public List<GroupData> groups;
	[SerializeField] public bool startNewPart = true;
	[SerializeField] public bool startNewGroup = true;
	[SerializeField] public GroupData currentGroup;
	[SerializeField] public GameObject listTools;
	[SerializeField] public GameObject groupmanagerGameobject;
	private GameObject activeTool;
	private GameObject lastActiveTool;
	private P3dPaintableTexture lastTexture;


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
			parts.Add(newPart);
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
		parts[^1].partCommands.Add(commandData);


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
		currentGroup.groupParts.Add(newPart);
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
			groups.Add(nextGroup);
			currentGroup = nextGroup;
			return nextGroup;
		}
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
		parts.Add(newPart);
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
		var json = JsonUtility.ToJson(this);
		data.commandDetails = json;
	}
	public void LoadData(ConfigData data)
	{
		var json = data.commandDetails;
		JsonUtility.FromJsonOverwrite(json, this);
		GroupManager groupmanager = groupmanagerGameobject.GetComponent<GroupManager>();
		groupmanager.build();

    }
}