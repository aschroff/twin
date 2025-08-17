using System.Collections;
using System.Collections.Generic;
using System.IO;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

namespace Code
{
    public class PartDescriptionProcess: Process
    {
        private PartManager.PartData part;

        public override ProcessResult Execute(string variant = "")
        {
            Debug.Log("Process Status: PartDescriptionProcess Execute");
            StartCoroutine(execute(variant));
            return new ProcessResult();
        }
        
        public (string before, string after) SplitStringByDoubleHash(string input)
        {
            if (string.IsNullOrEmpty(input) || !input.Contains("##"))
            {
                return (input, string.Empty);
            }

            string[] parts = input.Split(new[] { "##" }, 2, System.StringSplitOptions.None);
            return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
        }
        
        private IEnumerator execute(string variantRaw)
        {
            Debug.Log("Process Status: PartDescriptionProcess execute");
            string variant;
            string idPart;
            (variant, idPart) = SplitStringByDoubleHash(variantRaw);
            PartManager partManager = getPartManager();
            part = partManager.getPart(idPart);
            describePart(part, variant);
            yield return new WaitForEndOfFrame();
        }
        public string get_path()
        {   
            DataPersistenceManager dataManager = getDataManager();
            PartManager partManager = getPartManager();
            PartManager.GroupData group = partManager.getGroup(part);
            string name = dataManager.selectedProfileId + " - " + group.name + " - part " + part.id;
            string folder = dataManager.selectedProfileId;
            
            return Path.Combine(Application.persistentDataPath,folder,
                "screenshot_" + name + ".png");
		
        }

        private void describePart(PartManager.PartData part, string variant)
        {
            AI.AI ai = getAI();
            part.pathScreenshot = get_path();
            if (File.Exists(part.pathScreenshot))
            {
                Debug.Log("Before Call AI");
                ai.DescribePart(part, variant); 
            }
            else 
            {
                Debug.Log("No Screenshot");
                LeanPulse notification = getNotification();
                foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
                {
                    text.text = "No Screenshot";
                }

                notification.Pulse();
            }
            
        }
        
        
        
    }
}