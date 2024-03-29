using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;

public class TwinNameValidator : MonoBehaviour
{
    private InputField myInputField;
    private Regex regex;

    private void Start()
    {
        regex = new Regex("[^a-zA-Z0-9]"); // change this to the characters you want to allow
        myInputField = this.GetComponent<InputField>();
        myInputField.onValueChanged.AddListener(ValidateInput);
    }

    private void ValidateInput(string input)
    {
        if (regex.IsMatch(input))
        {
            myInputField.text = regex.Replace(input, ""); // replace disallowed characters
        }
    }
}
