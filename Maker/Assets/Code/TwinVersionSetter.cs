using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Localization;

public class TwinVersionSetter : MonoBehaviour
{
    [SerializeField] private LocalizedString dateFormat;
    [SerializeField] private InputField inputFieldText;
    private LocalizedString formattedLocalizedString;
    // Start is called before the first frame update
    void Start()
    {

        DateTime currentDate = DateTime.Now; // getting current Time to put into version input field

        // format the date to current localization settings
        formattedLocalizedString = DateFormatter.formatDate(currentDate, dateFormat);

        //UpdateDate gets called, when event StringChanged is triggered
        //parameter value of UpdateString is formatted date-string
        formattedLocalizedString.StringChanged += UpdateDate;
        //Triggers display update
        formattedLocalizedString.RefreshString();
    }

    private void UpdateDate(string value)
    {
        //InputField version = this.GetComponent<InputField>();
        //Update UI with formatted date
        inputFieldText.text = value;
    }

    private void OnDisable()
    {
        formattedLocalizedString.StringChanged -= UpdateDate;
    }
}
