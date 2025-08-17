using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GroupDetailMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    private void OnEnable()
    {

        UIController.ShowUI("GroupDetail");
        Touch.SetActive(false);

    }
}
