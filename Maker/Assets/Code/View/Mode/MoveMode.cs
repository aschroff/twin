using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lean.Touch;

public class MoveMode : MonoBehaviour
{
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject body;
    [SerializeField] GameObject cam;
    [SerializeField] GameObject Tools;
    [SerializeField] GameObject Touch;
    void Start()
    {
        
    }

    private void OnEnable()
    {
        Touch.SetActive(true);
        LeanMultiUpdate lmu = (LeanMultiUpdate)body.GetComponent("LeanMultiUpdate");
        LeanPinchCamera lpc = (LeanPinchCamera)cam.GetComponent("LeanPinchCamera");
        LeanDragCamera ldc = (LeanDragCamera)cam.GetComponent("LeanDragCamera");
        Debug.Log("Jo");
        Debug.Log(lmu);
        Debug.Log(cam);
        Debug.Log(ldc);
        Debug.Log(lpc);
        lmu.enabled = true;
        ldc.enabled = false;
        lpc.enabled = false;
        for (int i = Tools.transform.childCount - 1; i >= 0; i--)
        {
            Tools.transform.GetChild(i).gameObject.SetActive(false);
        }

        UIController.ShowUI("Move");


    }

    void OnDisable()
    {

    }


}
