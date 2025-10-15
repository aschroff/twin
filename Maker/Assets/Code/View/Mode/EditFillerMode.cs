using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;


public class EditFillerMode : MonoBehaviour
{
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject Touch;


    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("EditFiller");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}