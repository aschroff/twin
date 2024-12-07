using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    private void OnEnable()
    {

        UIController.ShowUI("Settings");
        Touch.SetActive(false);

    }
}
