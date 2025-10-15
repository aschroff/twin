using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;


public class EditTextMode : MonoBehaviour
{
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject Touch;


    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("EditText");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}
