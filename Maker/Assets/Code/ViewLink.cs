using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewLink : MonoBehaviour
{
    public ViewManager.View link;
    public ViewManager manager;
    public void HandleDelete()
    {
        manager.delete(link);
    }
    
    public void HandleSelect()
    {
        manager.select(link);
    }
    
    public void HandleEdit(string name)
    {
        link.name = name;
    }
}
