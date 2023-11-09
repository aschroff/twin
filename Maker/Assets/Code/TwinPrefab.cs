using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class TwinPrefab : MonoBehaviour
{
    public string prefabName;


    public bool IsInstanceOfPrefabWithName(string name)
    {
            return prefabName == name;
    }
        
    
}
