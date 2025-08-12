using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CW.Common;
using UnityEngine.UI;

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
            Debug.Log("old value tool" + toolNameDisplay.GetComponent<Text>().text);
            toolNameDisplay.GetComponent<Text>().text = myButton.GetComponentInChildren<InputField>().text;
            Debug.Log("new value tool" + toolNameDisplay.GetComponent<Text>().text);
            Debug.Log("really set: " +myButton.GetComponentInChildren<InputField>().text);

        }
    }

    private void OnDisable()
    {
        if (toolNameDisplays == null)
        {
            toolNameDisplays = GameObject.FindGameObjectsWithTag("CurrentTool");
        }

        Debug.Log("removing tool");

        foreach (GameObject toolNameDisplay in toolNameDisplays)
        {
            Debug.Log("old value tool" + toolNameDisplay.GetComponent<Text>().text);
            toolNameDisplay.GetComponent<Text>().text = "-";

        }
    }
}
