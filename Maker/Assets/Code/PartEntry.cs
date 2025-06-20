using System.Collections;
using System.Collections.Generic;
using Code;
using Code.Processes;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

public class PartEntry : Item
{

    [SerializeField] public PartManager.PartData partdata = null;
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
    
    public void delete()
    {
        if (partdata != null)
        {
            PartManager partManager = this.gameObject.transform.parent.gameObject.GetComponent<PartListManager>().partmanager;
            ViewManager viewManager = this.gameObject.transform.parent.gameObject.GetComponent<PartListManager>().viewmanager;
            viewManager.select(partdata.view);
            partManager.deletePart(partdata);
            partManager.Erase();
            partManager.Refresh();
            InteractionController.EnableMode("Main");
        }
        
        
        
    }
    
    

   
}


