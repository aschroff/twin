using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SaveMode : MonoBehaviour
{
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject Touch;

    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("Save");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}
