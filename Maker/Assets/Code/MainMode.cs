using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lean.Touch;

public class MainMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;
    [SerializeField] GameObject body;
    [SerializeField] GameObject cam;
    [SerializeField] GameObject Tools;
    // Start is called before the first frame update
    void OnEnable()
    {
        Touch.SetActive(true);
        LeanMultiUpdate lmu = (LeanMultiUpdate)body.GetComponent("LeanMultiUpdate");
        LeanPinchCamera lpc = (LeanPinchCamera)cam.GetComponent("LeanPinchCamera");
        LeanDragCamera ldc = (LeanDragCamera)cam.GetComponent("LeanDragCamera");
        Debug.Log("Jo1");
        lmu.enabled = false;
        ldc.enabled = true;
        lpc.enabled = true;
        for (int i = Tools.transform.childCount - 1; i >= 1; i--)
        {
            Tools.transform.GetChild(i).gameObject.SetActive(false);
            //Debug.Log(Tools.transform.GetChild(i).gameObject);

        }

        UIController.ShowUI("Main");    
    }

    
}

