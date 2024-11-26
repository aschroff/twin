using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Localization;
using UnityEngine.UI; 
using System; 

public class NewBehaviourScript : MonoBehaviour
{
    [SerializeField] private LocalizedString localizedDateString;
    [SerializeField] private Text textComp;

    private void Start()
    {
        string storedDate = textComp.text.ToString();
        if (DateTime.TryParse(storedDate, out DateTime dateTime))
        {

            localizedDateString.Arguments = new object[] { dateTime };
            //UpdateDate gets called, when event StringChanged is triggered
            //parameter value of UpdateString is formatted date-string
            localizedDateString.StringChanged += UpdateDate;
            //Triggers display update
            localizedDateString.RefreshString();
        }
        else
        {
            Debug.Log("Invalid date format: " + storedDate);
        }

    }

    //prevents UpdateDate of trying to get text component when game object is disabled 
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
