using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditDeleteMode : MonoBehaviour
{
[SerializeField] MainMode selectImage;
[SerializeField] GameObject Touch;


void Start()
{

}

private void OnEnable()
{
    UIController.ShowUI("EditDelete");
    Touch.SetActive(false);

}

void OnDisable()
{

}


}