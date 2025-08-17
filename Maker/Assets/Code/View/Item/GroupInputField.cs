using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class GroupInputField : MonoBehaviour, ISelectHandler
{

    public void OnSelect(BaseEventData eventData)
    {
        Debug.Log("Input field selected");
        GroupEdit group = this.transform.parent.gameObject.GetComponent<GroupEdit>();
        group.HandleEdit();
    }
}
