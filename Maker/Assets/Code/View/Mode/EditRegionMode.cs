using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;


public class EditRegionMode : MonoBehaviour
{
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject Touch;


    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("EditRegion");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}