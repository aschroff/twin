using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CW.Common;
using UnityEngine.UI;
using TMPro;

public class ToolTracker : MonoBehaviour
{
    public GameObject myButton;

    private GameObject[] toolNameDisplays = null;


    private void OnEnable()
    {
        if (toolNameDisplays == null)
        {
            toolNameDisplays = GameObject.FindGameObjectsWithTag("CurrentTool");
        }
        CwDemoButton[] objectsWithToolButton = GameObject.FindObjectsOfType<CwDemoButton>();

        
        foreach (CwDemoButton button in objectsWithToolButton)
        {
       
            if (button.IsolateTarget == this.transform)
            {
                myButton = button.gameObject;
            }
        }

        Debug.Log("setting tool");

        foreach (GameObject toolNameDisplay in toolNameDisplays)
        {
            Debug.Log("old value tool" + toolNameDisplay.GetComponent<TextMeshProUGUI>().text);
            toolNameDisplay.GetComponent<TextMeshProUGUI>().text = myButton.GetComponentInChildren<InputField>().text;
            Debug.Log("new value tool" + toolNameDisplay.GetComponent<TextMeshProUGUI>().text);
            Debug.Log("really set: " +myButton.GetComponentInChildren<InputField>().text);

        }
    }

    private void OnDisable()
    {
        if (toolNameDisplays != null)
        {
            Debug.Log("removing tool");

            foreach (GameObject toolNameDisplay in toolNameDisplays)
            {
                //in case the gameobject has been destroyed, e.g. app is stopped in edit mode
                if (toolNameDisplay != null)
                {
                    Debug.Log("old value tool" + toolNameDisplay.GetComponent<Text>().text);
                    toolNameDisplay.GetComponent<Text>().text = "-"; //TODO change to TMPUGUI
                }

            }
        }
    }
}
