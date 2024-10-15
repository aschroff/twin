using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Localization;
using TMPro;
using System;

public class NewBehaviourScript : MonoBehaviour
{
    [SerializeField] private LocalizedString localizedDateString;
    [SerializeField] private TextMeshProUGUI textComp;

    //string that stores date from configFile; 
    private string storedDate = "8/27/2024 3:27:26 PM";

    private void Start()
    {
        if (DateTime.TryParse(storedDate, out DateTime dateTime))
        {

            localizedDateString.Arguments = new object[] { dateTime };
            localizedDateString.StringChanged += UpdateDate;
            localizedDateString.Arguments[0] = dateTime;
            //Triggers display update
            localizedDateString.RefreshString();
        }
        else
        {
            Debug.Log("Invalid date format: " + storedDate);
        }


    }

    private void OnDisable()
    {
        localizedDateString.StringChanged -= UpdateDate;
    }

    private void UpdateDate(string value)
    {
        //Update UI with formatted date
        textComp.text = value;

    }


}
