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


}
