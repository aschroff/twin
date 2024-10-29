using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PaintIn3D;
using System.Linq;
using System;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class PartManager : P3dCommandSerialization, IDataPersistence, ItemFile
{
	[SerializeField] public ViewManager viewManager;
	[SerializeField] public List<GroupData> groups;
	[SerializeField] public bool startNewPart = true;
	[SerializeField] public bool startNewGroup = true;
	[SerializeField] private GroupData CurrentGroup;
	[SerializeField] public Text currentText;
	
	public GroupData currentGroup 
	{
		get { return CurrentGroup; }
		set { SetCurrentGroup(value); }
	}
	[SerializeField] public PartData currentPart;
	private GameObject listTools;
	private GameObject groupmanagerGameobject;
	private GameObject activeTool;
	private GameObject lastActiveTool;
	private P3dPaintableTexture lastTexture;
	private P3dCommandSphere lastSphereCommand;
	//[SerializeField] public bool temp_skiploading = false;
	public enum Tool
	{
		MarkerLine,
		MarkerDotted,
		Filler,
		Sticker,
		Text,
		Delete,
		Unknown
	}
	
	private void SetCurrentGroup(GroupData value)
	{
		if (CurrentGroup != null)
		{
			CurrentGroup.selected = false;
		}
		
		CurrentGroup = value;
		value.selected = true;

	}
	
	private void Start()
	{
		listTools = GameObject.FindGameObjectsWithTag("Tools")[0];
		InteractionController.OnModeChange += HandleModeChange;
	}
	
	private void HandleModeChange(GameObject modeOld, GameObject modeNew)
	{
		List<string> viewChangingModes = new List<string> { "MainMode", "Shape", "MoveMode" };
		if (modeOld.name.Contains("Edit") && viewChangingModes.Contains(modeNew.name))
		{
			Debug.Log(modeOld.name + "-> " + modeNew.name);
			startNewPart = true;
		}
	}

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
		public SceneManagement.View view;
		public Tool typeTool;
		public string nameTool;
		public Color colorTool;
		public string guidTool;
		public string meaning;
		public string textTool;
		public string description;
	}

	[System.Serializable]
	public class GroupData
	{
		public List<PartData> groupParts = new List<PartData>();
		public Group group;
		public string id;
		public string name;
		public bool visible;
		public bool selected;
	}

	private void setActiveTool()
	{
		activeTool = getActiveTool();
	}
	
	private GameObject getActiveTool()
	{
		foreach (Transform child in listTools.transform)
		{
			if (child.gameObject.activeSelf == true)
			{
				return child.gameObject;
			}
		}

		return null;
	}


	public void addCommand(CommandDataTwin commandData)
	{
		if (startNewPart)
		{
			StoreCurrentPartInformation();
			PartData newPart = new PartData();
			newPart.id = System.Guid.NewGuid().ToString();
			newPart.partCommands.Add(commandData);
			HandleNewPart(newPart);
			setActiveTool();
			lastActiveTool = activeTool;
			lastTexture = commandData.data.PaintableTexture;
			lastSphereCommand = null;
			return;
		}
		if (commandData.data.PaintableTexture != lastTexture)
		{
			startNewPart = true;
			addCommand(commandData);
			return;
		}
		if (activeTool != getActiveTool())
		{
			startNewPart = true;
			lastSphereCommand = null;
			addCommand(commandData);
			return;
		}
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
		currentPart = newPart;
		if (CurrentGroup != null)
        {
			CurrentGroup.groupParts.Add(newPart);
		}
		startNewPart = false;
		
	}

	public GroupData StartNewGroup(Group group)
	{
		if (groups == null)
		{
			groups = new List<GroupData>();
			GroupData firstGroup = new GroupData();
			firstGroup.group = group;
			groups.Add(firstGroup);
			CurrentGroup = firstGroup;
			return firstGroup;
		}
		else
		{
			GroupData nextGroup = new GroupData();
			nextGroup.group = group;
			nextGroup.visible = true;
			groups.Add(nextGroup);
			CurrentGroup = nextGroup;
			return nextGroup;
		}
		startNewPart = true;
	}

	public GroupData addGroup(Group group)
	{
		GroupData nextGroup = new GroupData();
		nextGroup.group = group;
		groups.Add(nextGroup);
		CurrentGroup = nextGroup;
		return nextGroup;

	}
	public PartData addPart(GroupData groupdata, string id)
	{
		PartData newPart = new PartData();
		newPart.id = id;
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
		StoreCurrentPartInformation();
		startNewGroup = true;
		startNewPart = true;
		var json = JsonUtility.ToJson(this);
		data.commandDetails = json;
	}
	public void LoadData(ConfigData data)
	{
		try
		{
			//if (temp_skiploading == false)
			//{		
			ViewManager viewManagerTemp = viewManager;
			var json = data.commandDetails;
			if (json == "")
			{
				ClearAll();
			}
			else
			{
				JsonUtility.FromJsonOverwrite(json, this);
			}
			viewManager = viewManagerTemp;
			//}
		}
		catch (Exception e)
		{
			Erase();
		}

		foreach (GroupData group in groups)
		{
			if (group.selected == true)
			{
				currentGroup = group;
			}
		}
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
	public P3dCommand RefreshPart(PartData partData)
	{
		var oldListening = listening;
		P3dCommand last = null;
		listening = false;
		foreach (CommandDataTwin commandData in partData.partCommands)
		{
			last = this.Apply(commandData);
		}
		listening = oldListening;
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
		if (CurrentGroup == groupData)
        {
			CurrentGroup = null;
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

	public GroupData trySetCurrentGroupIfEmpty()
	{
		if (currentGroup == null)
		{
			if (groups.Count > 0)
			{
				currentGroup = groups[0];
				return groups[0];
			}
		}
		return null;
	}
	public void deletePart(PartData partData)
	{
		Debug.Log("Removing part: " + partData.id);
		if (currentPart == partData)
		{
			currentPart = null;
		}
		else if ((currentPart != null) && (CurrentGroup != null ))
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
	
	private bool ArePositionsEqual(Vector3 pos1, Vector3 pos2, float precision = 0.000000001f)
	{
		return Mathf.Abs(pos1.x - pos2.x) < precision &&
		       Mathf.Abs(pos1.y - pos2.y) < precision &&
		       Mathf.Abs(pos1.z - pos2.z) < precision;
	}
	
	[ContextMenu("Reset")]
	public void ClearAll()
	{
		base.Clear();
		groups.Clear();
		CurrentGroup = null;
		currentPart = null;
	}
	[ContextMenu("Clear and refresh all")]
	public void ClearRefreshAll()
	{
		Erase();
		Refresh();
	}
	
	public void ClearRefreshPart(PartData partData)
	{
		Erase();
		RefreshPart(partData);
	}


	public  void handleChange(string profile)
	{
		
	}
	public  void handleCopyChange(string profile)
	{

	}
	public  void handleDelete(string profile)
	{
        
	}

	private GameObject GetGroupmanager()
	{
		if (groupmanagerGameobject == null)
		{
			groupmanagerGameobject = GameObject.FindGameObjectsWithTag("Groupmanager")[0];
		}
		return groupmanagerGameobject;
	}

	private void StoreCurrentPartInformation()
	{
		if (currentPart != null && viewManager != null)
		{
			Debug.Log("Store current part view");
			currentPart.view = viewManager.shootView();
		}
		else
		{
			Debug.Log("Cannot store current part view because current part or viewmanager is null");
		}

		if (activeTool != null)
		{
			GameObject selected = activeTool.GetComponent<ToolTracker>().myButton.gameObject;
			currentPart.typeTool = DeriveType(activeTool);
			if (currentPart.typeTool == Tool.MarkerLine || currentPart.typeTool == Tool.MarkerDotted || currentPart.typeTool == Tool.Filler)
			{
				SimplifyPart();
				currentPart.meaning = GetText(selected);
				currentPart.colorTool = activeTool.GetComponent<P3dPaintSphere>().Color;
				currentPart.nameTool = activeTool.name;
			}
			else if (currentPart.typeTool == Tool.Sticker)
			{
				currentPart.meaning = GetText(selected);
				ToolTracker toolTracker = activeTool.GetComponent<ToolTracker>();
				currentPart.guidTool = toolTracker.myButton.GetComponent<Item>().id;
				currentPart.nameTool = activeTool.name;
			}
			else if (currentPart.typeTool == Tool.Text)
			{
				currentPart.meaning = GetText(selected);
				currentPart.textTool = currentText.text;
				currentPart.colorTool = activeTool.GetComponent<P3dPaintDecal>().Color;
				currentPart.nameTool = activeTool.name;
			}

		}
		else
		{
			Debug.Log("Cannot store current part tool because active tool is null");
		}
	}

	private Tool DeriveType(GameObject tool)
	{	
		P3dPaintSphere sphere = tool.GetComponent<P3dPaintSphere>();
		bool hasSphere = (sphere != null);
		bool isDelete = false;
		if (hasSphere)
		{
			isDelete = (sphere.BlendMode == P3dBlendMode.REPLACE_ORIGINAL);
		}
		P3dPaintDecal dynamic = tool.GetComponent<P3dPaintDecal>();
		bool hasDynamic = (dynamic != null);
		bool hasTexture = false;
		bool hasShape = false;
		if (hasDynamic)
		{
			Texture texture = tool.GetComponent<P3dPaintDecal>().Texture;
			hasTexture = (texture != null);
			Texture shape = tool.GetComponent<P3dPaintDecal>().Shape;
			hasShape = (shape != null);
		}
		P3dHitScreen hit = tool.GetComponent<P3dHitScreen>();
		bool hasHit = (hit != null);
		bool isDotted = false;
		if (hasHit)
		{
			isDotted = (hit.Frequency == P3dHitScreen.FrequencyType.PixelInterval) && 
			           (hit.Connector.ConnectHits == false);
		}
		P3dHitScreenFill hitFill = tool.GetComponent<P3dHitScreenFill>();
		bool hasFill = (hitFill != null);


		if (hasSphere && !isDotted && !hasFill && !isDelete)
		{
			return Tool.MarkerLine;
		}
		if (hasSphere && isDotted)
		{ 
			return Tool.MarkerDotted;
		}
		if (hasSphere && hasFill)
		{ 
			return Tool.Filler;
		}
		if (hasDynamic && hasTexture)
		{
			return Tool.Sticker;
		}
		if (hasDynamic && hasShape)
		{
			return Tool.Text;
		}
		if (isDelete)
		{
			return Tool.Delete;
		}

		return Tool.Unknown;
	}
	
	public void EnforceNewPart()
	{
		StoreCurrentPartInformation();
		Debug.Log("Enforce new part");
		startNewPart = true;
	}
	
	private string GetText(GameObject gameObject)
	{
		foreach (Text text in gameObject.GetComponentsInChildren<Text>())
		{
			if (text.enabled && text.text != "Placeholder" && text.gameObject.transform.parent.name == "InputField")
			{   
				return text.text;
			}
		}
		return "unknown meaning";
	}

	private void SimplifyPart()
	{
		List<CommandDataTwin> commandsDelete  = new List<CommandDataTwin>();
		P3dCommandSphere lastCommandSphere = null;
		foreach (CommandDataTwin commandData in currentPart.partCommands)
		{
			P3dCommandSphere commandSphere = (P3dCommandSphere)commandData.data.LocalCommand;
			if (lastCommandSphere != null)
			{
				if (ArePositionsEqual(commandSphere.Position, lastCommandSphere.Position))
				{
					commandsDelete.Add(commandData);
				}
			}
			lastCommandSphere = commandSphere;
		}
		foreach (CommandDataTwin commandData in commandsDelete)
		{
			currentPart.partCommands.Remove(commandData);
			deleteCommand(commandData);
		}
	}
	
	public PartData getPart(string id)
	{
		foreach (GroupData group in groups)
		{
			foreach (PartData part in group.groupParts)
			{
				if (part.id == id)
				{
					return part;
				}
			}
		}
		return null;
	}
	
	public GroupData getGroup(PartData part)
	{
		foreach (GroupData group in groups)
		{
			foreach (PartData partSearch in group.groupParts)
			{
				if (part.id == partSearch.id)
				{
					return group;
				}
			}
		}
		return null;
	}
	
	
}