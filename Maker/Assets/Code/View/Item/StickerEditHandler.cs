using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PaintIn3D;

//component to find active sticker and apply flip/turn/resize 
public class StickerEditHandler : MonoBehaviour
{
    [SerializeField] private GameObject folderTools;

    public bool IsInstanceOfPrefabWithName(GameObject obj, string name)
    {
        TwinPrefab twinPrefab = obj.GetComponent<TwinPrefab>(); //Fehler twinPrefab = null
        if (twinPrefab != null)
        {
            return twinPrefab.IsInstanceOfPrefabWithName(name);
        }
        return false;
    }

    public GameObject GetActiveSticker()
    {

        for (int j = 0; j < folderTools.transform.childCount; j++)
        {

            GameObject child = folderTools.transform.GetChild(j).gameObject;
            TwinPrefab twinPrefab = child.GetComponent<TwinPrefab>();
            if (IsInstanceOfPrefabWithName(child, "Tool Sticker") && child.activeInHierarchy)
            {
                Debug.Log("Found active Sticker");
                return child;

            }

        }
        Debug.Log("No active Sticker found");  
        return null; 
    }

    public void FlipStickerHorizontal()
    {
        GameObject activeSticker = this.GetActiveSticker();
        if(activeSticker != null)
        {

            CwPaintDecal stickerPrefab = activeSticker.GetComponent<CwPaintDecal>();
            stickerPrefab.FlipHorizontal();
            Debug.Log("Sticker flipped horizontally"); 

        }
    }


    public void FlipStickerVertical()
    {
        GameObject activeSticker = this.GetActiveSticker();
        if (activeSticker != null)
        {

            CwPaintDecal stickerPrefab = activeSticker.GetComponent<CwPaintDecal>();
            stickerPrefab.FlipVertical();
            Debug.Log("Sticker flipped vertically");

        }
    }


    public void TurnStickerToTheLeft()
    {
        GameObject activeSticker = this.GetActiveSticker();
        if (activeSticker != null)
        {

            CwPaintDecal stickerPrefab = activeSticker.GetComponent<CwPaintDecal>();
            stickerPrefab.IncrementAngle(45);
            Debug.Log("Sticker turned to the left");

        }
    }

    public void TurnStickerToTheRight()
    {
        GameObject activeSticker = this.GetActiveSticker();
        if (activeSticker != null)
        {

            CwPaintDecal stickerPrefab = activeSticker.GetComponent<CwPaintDecal>();
            stickerPrefab.IncrementAngle(-45);
            Debug.Log("Sticker turned to the right");

        }
    }

    public void ExpandSticker()
    {
        GameObject activeSticker = this.GetActiveSticker();
        if (activeSticker != null)
        {

            CwPaintDecal stickerPrefab = activeSticker.GetComponent<CwPaintDecal>();
            stickerPrefab.MultiplyScale(1.5F);
            Debug.Log("Sticker expanded");

        }
    }

    public void ShrinkSticker()
    {
        GameObject activeSticker = this.GetActiveSticker();
        if (activeSticker != null)
        {

            CwPaintDecal stickerPrefab = activeSticker.GetComponent<CwPaintDecal>();
            stickerPrefab.MultiplyScale(0.6667F);
            Debug.Log("Sticker shrunk");

        }
    }

}
