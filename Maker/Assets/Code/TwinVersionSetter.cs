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
    void Start()
    {

        DateTime currentDate = DateTime.Now; // getting current Time to put into version input field

        // format the date to current localization settings
        formattedLocalizedString = DateFormatter.formatDate(currentDate, dateFormat);

        // Subscribe to the StringChanged event
        formattedLocalizedString.StringChanged += UpdateDate;
        // Triggers display update
        formattedLocalizedString.RefreshString();
    }

    // Event handler method
    private void UpdateDate(string value)
    {
        // Update UI with formatted date
        inputFieldText.text = value;
        this.lastFormattedDate = value;
    }

    private void OnDisable()
    {
        formattedLocalizedString.StringChanged -= UpdateDate;
    }

    public string GetLastFormattedDate()
    {
        return this.lastFormattedDate;
    }
}
