using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewListMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    private void OnEnable()
    {

        UIController.ShowUI("ViewList");
        Touch.SetActive(false);

    }
}
