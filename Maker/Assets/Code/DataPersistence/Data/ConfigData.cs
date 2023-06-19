using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConfigData
{
    public long lastUpdated;
    public int sampleInteger;
    public Vector3 sampleVector;
    public SerializableDictionary<string, bool> sampleDictonary;
    public AttributesData sampleAttributesData;

    public ConfigData() 
    {
        this.sampleInteger = 0;
        sampleVector = Vector3.zero;
        sampleDictonary = new SerializableDictionary<string, bool>();
        sampleAttributesData = new AttributesData();
    }
}
