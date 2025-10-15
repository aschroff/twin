using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AnswerManager : MonoBehaviour
{
    [SerializeField] public Text text;
    [SerializeField] public Text source;

    public void showDetail()
    {
        text.text = source.text;
        InteractionController.EnableMode("Answer");
    }
    
}
