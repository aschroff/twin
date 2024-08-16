using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;


public class MenuMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("Menu");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}