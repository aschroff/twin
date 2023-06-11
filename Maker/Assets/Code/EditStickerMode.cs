using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;


public class EditStickerMode : MonoBehaviour
{
    public GameObject twin;
    [SerializeField] MainMode selectImage;
    [SerializeField] GameObject Touch;


    void Start()
    {

    }

    private void OnEnable()
    {
        UIController.ShowUI("EditSticker");
        Touch.SetActive(false);

    }

    void OnDisable()
    {

    }


}
