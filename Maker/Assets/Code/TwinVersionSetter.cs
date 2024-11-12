using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class TwinVersionSetter : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
        InputField versionInput = this.GetComponent<InputField>();
        versionInput.text = DateTime.Now.ToString("MM/dd/yyyy");

        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
