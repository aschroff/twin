using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;


public class PartMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("Part");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}