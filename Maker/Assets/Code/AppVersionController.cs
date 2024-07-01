using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AppVersionController : MonoBehaviour
{
    
    // Start is called before the first frame update
    void Start()
    {
        Text versionDisplay = this.gameObject.transform.GetComponent<Text>();
        versionDisplay.text = Application.version;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
