using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.Localization.Settings;

public class TwinVersionSetter : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        LocalizeStringEvent localizeStringEvent = this.transform.GetComponentInChildren<LocalizeStringEvent>();
        TableReference localizationTableReference = localizeStringEvent.StringReference.TableReference;
        TableEntryReference localizationTableEntryReference = localizeStringEvent.StringReference.TableEntryReference;

        string currentDateFormat = LocalizationSettings.StringDatabase.
            GetLocalizedString(localizationTableReference, localizationTableEntryReference);

        //string currentDateFormat = localizeStringEvent.
        //LocalizationSettings.StringDatabase.GetLocalizedString(tableReference, entryReference);
        //if (localizeStringEvent.StringReference) {
        //} else if () {

        //} else if () {
        //}
        InputField versionInput = this.GetComponent<InputField>();
        versionInput.text = currentDateFormat.Replace(".","-");
        Debug.Log(versionInput.text);
        //versionInput.text = DateTime.Now.ToString(currentDateFormat
        //    .Replace(".","-")
        //    .Substring(currentDateFormat.IndexOf(": ")));


    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
