using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;


public class HelpMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("Help");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}