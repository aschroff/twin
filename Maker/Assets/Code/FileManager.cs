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
        AssignToggleGroup(configData);
        configData.transform.SetParent(this.transform, false);
        configData.transform.localScale = prefab.transform.localScale;
        Text text = configData.transform.Find("Name").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        text.text = entry.Key;
        Text currentDate = configData.transform.Find("Date").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        currentDate.text = DateTime.Now.ToString("ddd, dd'.'MM'.'yy H:mm");
        Button buttonDelete = configData.transform.Find("Delete").GetComponentInChildren<Button>();
        Toggle toggleSelect = configData.transform.Find("SingleToggle").GetComponentInChildren<Toggle>();
        Button buttonDetail = configData.transform.Find("DetailsMode").GetComponentInChildren<Button>();
        if (dataManager.selectedProfileId == entry.Key)
        {
            buttonDelete.interactable = false;
            //toggleSelect.isOn = false;
            //Debug.Log(entry.Key + "is now disabled!");

        }
        else
        {
            buttonDelete.onClick.AddListener(() => { Remove(entry.Key); });
            toggleSelect.onValueChanged.AddListener((isOn) => { if (isOn) Select(entry.Key); });
            //toggleSelect.isOn = true;
            buttonDetail.onClick.AddListener(() => { Detail(entry.Key); });
            //Debug.Log(entry.Key + "is now enabeld!");
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

    private void AssignToggleGroup(GameObject PrefabInstance)
    {
        ToggleGroup toggleGroup = this.gameObject.GetComponent<ToggleGroup>();
        Toggle singleToggle = PrefabInstance.GetComponentInChildren<Toggle>();
        singleToggle.group = toggleGroup;
    }

    private void Detail(string profile)
    {
        InteractionController.EnableMode("Version");
    }
}