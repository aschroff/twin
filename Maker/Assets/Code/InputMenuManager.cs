using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/*[Serializable]
/// <summary>
/// Function definition for a button click event.
/// </summary>
public class ButtonClickedEvent : UnityEvent {}*/

public class InputMenuManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    //[SerializeField] public InputMenuDictionary  menu;
    [SerializeField] public List<String> promptNames;

    

    void OnEnable()
    {
        Refresh();
    }

    
    // Start is called before the first frame update
    void Start()
    {
        Create();
    }
    

    public void Refresh()
    {
        Create();
    }

    private void Create()
    {
        Delete();
        foreach (String prompt in promptNames)
        {
            createMenuEntry(prompt);
        }
    }
    
    private void Delete()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform childTransform = transform.GetChild(i);
            Destroy(childTransform.gameObject);
        }
    }

    public void  createMenuEntry(String prompt)
    {
        GameObject actionData = Instantiate(prefab);
        actionData.transform.SetParent(this.transform, false);
        actionData.transform.localScale = prefab.transform.localScale;
        Text text = actionData.transform.Find("Label").gameObject.GetComponentInChildren<Text>();
        text.text = prompt;

    }
}
