using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;


public class EditMarkerMode : MonoBehaviour
{
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject Touch;


    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("EditMarker");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}