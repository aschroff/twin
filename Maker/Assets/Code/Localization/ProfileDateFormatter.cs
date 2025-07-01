using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Localization;
using UnityEngine.UI; 
using System; 

public class ProfileDateFormatter : MonoBehaviour
{
    [SerializeField] private LocalizedString localizedDateString;
    LocalizedString formattedLocalizedString;

    private void Start()
    {
        Text textComponent = gameObject.GetComponent<Text>();
        string storedDate = textComponent.text.ToString();
        if (DateTime.TryParse(storedDate, out DateTime dateTime))
        {
            formattedLocalizedString = DateFormatter.formatDate(dateTime, localizedDateString);
            textComponent.text = formattedLocalizedString.GetLocalizedString();
        }
        else
        {
            Debug.Log("Invalid date format: " + storedDate);
        }
    }

    /*private void Start()
    {
        Text textComponent = this.gameObject.GetComponent<Text>();
        string storedDate = textComponent.text.ToString();
        if (DateTime.TryParse(storedDate, out DateTime dateTime))
        {
            formattedLocalizedString = DateFormatter.formatDate(dateTime, localizedDateString);
            //UpdateDate gets called, when event StringChanged is triggered
            //parameter value of UpdateString is formatted date-string
            formattedLocalizedString.StringChanged += UpdateDate;
            //Triggers display update
            formattedLocalizedString.RefreshString();
        }
        else
        {
            Debug.Log("Invalid date format: " + storedDate);
        }


    }

    //prevents UpdateDate of trying to get text component when game object is disabled 
    private void OnDisable()
    {
        formattedLocalizedString.StringChanged -= UpdateDate;
    }

    private void UpdateDate(string value)
    {
        Text textComponent = this.gameObject.GetComponent<Text>();
        //Update UI with formatted date
        textComponent.text = value;

    }*/


}
