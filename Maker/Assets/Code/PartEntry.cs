using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

public class PartEntry : Item
{

    [SerializeField] public PartManager.PartData partdata = null;
    [SerializeField] public GameObject parent;
    [SerializeField] public Texture2D loadedTexture;
    
    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }
    
    public void showDetails()
    {
        InteractionController.Partdata = partdata;
        InteractionController.EnableMode("Part");
    }
    
    

   
}


