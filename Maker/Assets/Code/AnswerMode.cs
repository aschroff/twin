using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnswerMode : MonoBehaviour
{
    [SerializeField] GameObject Touch;


    private void OnEnable()
    {

        UIController.ShowUI("Answer");
        Touch.SetActive(false);

    }
}
