using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CW.Common;
using UnityEngine.UI;

public class ToolTracker : MonoBehaviour
{
    GameObject myButton;

    private static GameObject[] toolNameDisplays = null;
    // Start is called before the first frame update
    void Start()
    {
        if (toolNameDisplays == null)
        {
            toolNameDisplays = GameObject.FindGameObjectsWithTag("CurrentTool");
        }
        CwDemoButton[] objectsWithToolButton = GameObject.FindObjectsOfType<CwDemoButton>();

        // Iterate through all GameObjects with a Rigidbody component
        foreach (CwDemoButton button in objectsWithToolButton)
        {
            // Check if the mass of the Rigidbody is 10
            if (button.IsolateTarget == this.transform)
            {
                myButton = button.gameObject;
            }
        }
    }

    private void OnEnable()
    {
        foreach (GameObject toolNameDisplay in toolNameDisplays)
        {
            toolNameDisplay.GetComponent<Text>().text = myButton.GetComponentInChildren<InputField>().text;
        }
    }
}
