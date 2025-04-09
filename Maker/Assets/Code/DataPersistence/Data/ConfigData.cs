using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConfigData
{
    public long lastUpdated;
    public SerializableDictionary<string, string> itemTexts;
    public List<string> groupList;
    public SerializableDictionary<string, string> partList;
    public SerializableDictionary<string, string> commandList;
    public AttributesData sampleAttributesData;
    public string commandDetails;
    public string views;
    public float yaw;
    public float pitch;
    public Vector3 positionCamera;
    public float sizeCamera;
    public string name;
    public string version;
    public string updated;
    public int languageID;
    public string prompt;

    public ConfigData(string modelName, string modelVersion = "")
    {
        itemTexts = new SerializableDictionary<string, string>();
        groupList = new List<string>();
        partList = new SerializableDictionary<string, string>();
        commandList = new SerializableDictionary<string, string>();
        sampleAttributesData = new AttributesData();
        commandDetails = "";
        views = "";
        yaw = 0.0f;
        pitch = 0.0f;
        positionCamera = new Vector3(x: 0.0f, y: 0.5f, z: 1.0f);
        sizeCamera = 2.0f;
        version = modelVersion;
        name = modelName;
        updated = new DateTime(1969, 4,25, 10,10,00).ToString();
        languageID = 0;
        prompt = "";
    }
}
