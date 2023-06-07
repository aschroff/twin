using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class EditMode : MonoBehaviour
{
    public GameObject twin;
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject Touch;

    void Start()
    {
        
    }

    private void OnEnable()
    {
        UIController.ShowUI("Edit");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}
