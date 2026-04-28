using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using CW.Common;
using Lean.Gui;
using PaintIn3D;
using UnityEngine.UI;

namespace Code
{
    public class PartDetailManager: MonoBehaviour
    {
        [SerializeField] public DataPersistenceManager dataManager;
        void OnEnable()
        {
            Display();
        }

    
        // Start is called before the first frame update
        void Start()
        {
            Display();
        }

        void Display()
        {
            PartManager.PartData partdata = InteractionController.Partdata;
            PartManager.GroupData groupdata = InteractionController.Groupdata;
            InputField input_field = this.transform.GetComponentInChildren<InputField>();
            if (input_field != null)
            {
                input_field.text = partdata.description;
            }
            string name = dataManager.selectedProfileId + " - " + partdata.group.name + " - part " + partdata.id;
            string folder = dataManager.selectedProfileId;
            string fullPath = Path.Combine(DataPaths.PersistentDataPath,folder,
                "screenshot_" + name + ".png");
            if (File.Exists(fullPath))
            {
                byte[] imageBytes = File.ReadAllBytes(fullPath);
                Texture2D loadedTexture = new Texture2D(1024, 1024);
                loadedTexture.LoadImage(imageBytes);
                GameObject icon = transform.Find("Icon").gameObject;
                transform.Find("Placeholder").gameObject.SetActive(false);
                Image iconImage = icon.GetComponent<Image>();
                int width = loadedTexture.width;
                int height = loadedTexture.height;

                Sprite sprite = Sprite.Create(loadedTexture, new Rect(0, 0, width, height),
                    new Vector2(0.5f, 0.5f), 100.0f);
                iconImage.sprite = sprite;
            }
            else
            {
                transform.Find("Icon").gameObject.SetActive(false);
            }
            
            
        }
    }
}