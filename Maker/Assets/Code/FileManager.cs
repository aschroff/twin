using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;
using System.Linq;
using Lean.Transition;
using UnityEngine.UI;
using System;

public class FileManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public DataPersistenceManager dataManager;
    [SerializeField] public GameObject inputFieldName;
    private Dictionary<string, string> profiles = new Dictionary<string, string>();

    // Start is called before the first frame update
    void Start()
    {
        Create();
    }
    

    public void Refresh()
    {
        Delete();
        Create();
    }

    private void Create()
    {
        foreach (KeyValuePair<string, ConfigData> entry in dataManager.GetAllProfilesGameData())
        {
            createConfigEntry(entry);
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

    public ConfigData createConfigEntry(KeyValuePair<string, ConfigData> entry)
    {
        GameObject configData = Instantiate(prefab);
        configData.transform.SetParent(this.transform, false);
        configData.transform.localScale = prefab.transform.localScale;
        Text text = configData.transform.Find("Name").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        text.text = entry.Key;
        Text currentDate = configData.transform.Find("Date").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        currentDate.text = DateTime.Now.ToString("dddd, dd MMM yyyy H:m");  
        Button buttonDelete = configData.transform.Find("Delete").GetComponentInChildren<Button>();
        Button buttonSelect = configData.transform.Find("Select").GetComponentInChildren<Button>();
        if (dataManager.selectedProfileId == entry.Key)
        {            
            buttonDelete.interactable = false ;
            buttonSelect.interactable = false ;
        }
        else
        {
            buttonDelete.onClick.AddListener(() => { Remove(entry.Key); });
            buttonSelect.onClick.AddListener(() => { Select(entry.Key); });
        }

        return entry.Value;
    }

    private void Select(string profile)
    {
        dataManager.ChangeSelectedProfileId(profile);
        Refresh();
       }

    private void Remove(string profile)
    {
        dataManager.DeleteProfileData(profile);
        Refresh();
        inputFieldName.GetComponent<TwinNameValidator>().ValidateInput();
    }
}