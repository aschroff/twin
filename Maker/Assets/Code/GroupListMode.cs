using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroupListMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    private void OnEnable()
    {

        UIController.ShowUI("GroupList");
        Touch.SetActive(false);

    }
}
