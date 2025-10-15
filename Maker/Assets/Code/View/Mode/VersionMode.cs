using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class VersionMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    private void OnEnable()
    {
        
        UIController.ShowUI("Version");
        Touch.SetActive(false);

    }



}
