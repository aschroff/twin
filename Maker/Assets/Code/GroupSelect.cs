using System.Collections;
using System.Collections.Generic;
using Code;
using Code.Processes;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

public class GroupSelect : MonoBehaviour
{

    [SerializeField] public PartManager.GroupData groupdata = null;
    [SerializeField] public GameObject groupparent;

    public void HandleToggle()
    {
        PartListManager partListManager = groupparent.transform.GetComponent<GroupListSelectionManager>().partListManager;
        partListManager.rebuild();
        
    }
   
}


