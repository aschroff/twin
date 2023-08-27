using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConfigData
{
    public long lastUpdated;
    public int sampleInteger;
    public Vector3 sampleVector;
    public SerializableDictionary<string, string> itemTexts;
    public List<string> groupList;
    public SerializableDictionary<string, string> partList;
    public SerializableDictionary<string, string> commandList;
    public AttributesData sampleAttributesData;

    public ConfigData() 
    {
        this.sampleInteger = 0;
        sampleVector = Vector3.zero;
        itemTexts = new SerializableDictionary<string, string>();
        groupList = new List<string>();
        partList = new SerializableDictionary<string, string>();
        commandList = new SerializableDictionary<string, string>();
        sampleAttributesData = new AttributesData();
    }
}
