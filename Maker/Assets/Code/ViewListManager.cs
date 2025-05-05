using UnityEngine;
using Lean.Gui;
using UnityEngine.UI;

public class ViewListManager : MonoBehaviour, ItemFile
{
    [SerializeField] private ViewManager viewmanager;
    [SerializeField] private GameObject prefab;
    [SerializeField] private LeanPulse notification;
    public void handleChange(string profile)
    {
        throw new System.NotImplementedException();
    }

    public void handleCopyChange(string profile)
    {
        throw new System.NotImplementedException();
    }

    public void handleDelete(string profile)
    {
        throw new System.NotImplementedException();
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //Copy from Group
}
