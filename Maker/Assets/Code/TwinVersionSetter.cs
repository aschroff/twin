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
    private string lastFormattedDate;
    // Start is called before the first frame update
    void OnEnable()
    {

        DateTime currentDate = DateTime.Now; // getting current Time to put into version input field

        // format the date to current localization settings
        formattedLocalizedString = DateFormatter.formatDate(currentDate, dateFormat);
        string formattedString = formattedLocalizedString.GetLocalizedString();
        inputFieldText.text = formattedString;
        lastFormattedDate = formattedString;
    }

    public string GetLastFormattedDate()
    {
        return this.lastFormattedDate;
    }
}
