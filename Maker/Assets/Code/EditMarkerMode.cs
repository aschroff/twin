using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class EditMarkerMode : MonoBehaviour
{
    public GameObject twin;
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