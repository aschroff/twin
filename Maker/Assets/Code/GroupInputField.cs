using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class GroupInputField : MonoBehaviour, ISelectHandler
{

    public void OnSelect(BaseEventData eventData)
    {
        Debug.Log("Input field selected");
        Group group = this.transform.parent.gameObject.GetComponent<Group>();
        group.HandleEdit();
    }
}
