using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ShapeMode : MonoBehaviour
{
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject Touch;

    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("Shape");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}
