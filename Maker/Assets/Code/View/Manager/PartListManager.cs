using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using CW.Common;
using Lean.Gui;
using PaintIn3D;
using UnityEngine.UI;

public class PartListManager : MonoBehaviour
{

    [SerializeField] public PartManager partmanager;
    [SerializeField] public ViewManager viewmanager;
    [SerializeField] public GameObject prefab;
    [SerializeField] public GroupListSelectionManager selectionManager;
    [SerializeField] public DataPersistenceManager dataManager;
    [SerializeField] public Process process;
    public void build()
    {
        foreach (GroupSelect group in selectionManager.getGroups())
        {
            foreach (PartManager.PartData partData in group.groupdata.groupParts)
            {
                GameObject part = Instantiate(prefab); 
                part.transform.SetParent(this.transform, false);
                part.transform.localScale = prefab.transform.localScale;

                PartEntry partEntry = part.GetComponent<PartEntry>();
                partEntry.partdata = partData;
                InputField input_field = part.gameObject.transform.GetComponentInChildren<InputField>();
                if (input_field != null)
                {
                    input_field.text = partData.description;
                }
                string name = dataManager.selectedProfileId + " - " + group.groupdata.name + " - part " + partData.id;
                string folder = dataManager.selectedProfileId;
                string fullPath = Path.Combine(DataPaths.PersistentDataPath,folder,
                        "screenshot_" + name + ".png");
                if (File.Exists(fullPath))
                {
                    byte[] imageBytes = File.ReadAllBytes(fullPath);
                    partEntry.loadedTexture = new Texture2D(1024, 1024);
                    partEntry.loadedTexture.LoadImage(imageBytes);
                    GameObject icon = part.transform.Find("Icon").gameObject;
                    part.transform.Find("Placeholder").gameObject.SetActive(false);
                    Image iconImage = icon.GetComponent<Image>();
                    int width = partEntry.loadedTexture.width;
                    int height = partEntry.loadedTexture.height;

                    Sprite sprite = Sprite.Create(partEntry.loadedTexture, new Rect(0, 0, width, height),
                        new Vector2(0.5f, 0.5f), 100.0f);
                    iconImage.sprite = sprite;
                }
                else
                {
                    part.transform.Find("Icon").gameObject.SetActive(false);
                }
                /*Button change_button = part.transform.Find("Change").gameObject.GetComponent<Button>();
                change_button.onClick.AddListener(() =>
                {
                    process.Handle();
                });*/
                
            }

        }
         
    }
    
    
    public void clear()
    {
        for (int j = 0; j < this.transform.childCount; j++) {
            GameObject child = this.transform.GetChild(j).gameObject;
            Destroy(child);
        }
    }

    public void rebuild()
    {
        clear();
        build();
    }
    
    void Start()
    {
    }
    
    void OnEnable()
    {
    }
    

    public  void handleChange(string profile)
    {
        
    }
    public  void handleCopyChange(string profile)
    {

    }
    public  void handleDelete(string profile)
    {
        
    }
    
    public void LoadData(ConfigData data)
    {
     
    }

    public void SaveData(ConfigData data)
    {
      
    }
    
    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

}

