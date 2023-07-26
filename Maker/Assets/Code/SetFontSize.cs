using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SetFontSize : MonoBehaviour
{

    [SerializeField] private GameObject gameObjectText;
    [SerializeField] private int multiplicator;


    
    public void  SetFontSizeOFText(System.Int32 index)
    {
        gameObjectText.GetComponent<Text>().fontSize = index*multiplicator; 
    }

    
}
