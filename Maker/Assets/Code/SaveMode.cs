using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SaveMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;
    [SerializeField] GameObject inputField;


    private void OnEnable()
    {
        
        //inputField.GetComponent<TwinNameValidator>().ValidateInput("TwinName");

        UIController.ShowUI("Save");
        Touch.SetActive(false);

    }



}
