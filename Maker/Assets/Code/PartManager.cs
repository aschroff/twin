using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PaintIn3D;


public class PartManager : P3dCommandSerialization
{

	/// <summary>Aggregates single commands by the same tool created in a single sequence</summary>
	[SerializeField] protected List<PartData> parts = new List<PartData>();
	[SerializeField] public bool startNewPart = true;
	[SerializeField] public GameObject listTools;
	private GameObject activeTool;
	private GameObject lastActiveTool;
	private P3dPaintableTexture lastTexture; 

	[System.Serializable]
	public class PartData
		{
			public List<CommandData> partCommands = new List<CommandData>();
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


	public void addCommand(CommandData commandData)
		{
			if (startNewPart == true) {
				PartData newPart = new PartData();
				newPart.partCommands.Add(commandData);
				parts.Add(newPart);
				startNewPart = false;
				setActiveTool();
				lastActiveTool = activeTool;
				lastTexture = commandData.PaintableTexture;
				return;
			}
			if (commandData.PaintableTexture != lastTexture)
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
				addCommand(commandDatas[^1]);
			}
		}
	}

}
