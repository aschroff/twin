using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PaintIn3D;

public class SetFontSize : MonoBehaviour
{

    [SerializeField] private GameObject folderTexttools;
    [SerializeField] private float multiplicator;

    public bool IsInstanceOfPrefabWithName(GameObject obj, string name)
    {
        TwinPrefab twinPrefab = obj.GetComponent<TwinPrefab>();
        if (twinPrefab != null)
        {
            return twinPrefab.IsInstanceOfPrefabWithName(name);
        }
        return false;
    }

    public void  SetFontSizeOFText(System.Int32 index)
    {


        for (int j = 0; j < folderTexttools.transform.childCount; j++)
        {

            GameObject child = folderTexttools.transform.GetChild(j).gameObject;
            TwinPrefab twinPrefab = child.GetComponent<TwinPrefab>();
            if (IsInstanceOfPrefabWithName(child, "Tool Paint Dynamic Decal"))
            {
                P3dPaintDecal textPrefab = child.GetComponent<P3dPaintDecal>();
                textPrefab.Radius = (index + 1) * multiplicator;
                Debug.Log("Set font");

            }

        } 
    }

    
}
