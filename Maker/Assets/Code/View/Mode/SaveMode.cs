using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SaveMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;
    [SerializeField] GameObject inputField;
    [SerializeField] GameObject Tools;

    private void OnEnable()
    {
        
        //inputField.GetComponent<TwinNameValidator>().ValidateInput("TwinName");

        UIController.ShowUI("Save");
        Touch.SetActive(false);

        for (int i = Tools.transform.childCount - 1; i >= 0; i--)
        {
            Tools.transform.GetChild(i).gameObject.SetActive(false);
        }

    }



}
