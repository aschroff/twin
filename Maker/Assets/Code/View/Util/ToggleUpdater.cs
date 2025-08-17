using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleUpdater : MonoBehaviour
{
    
    [SerializeField] GameObject reference;
    // Start is called before the first frame update
    void OnEnable()
    { 
        gameObject.GetComponent<Toggle>().isOn = reference.activeSelf;
    }

}
