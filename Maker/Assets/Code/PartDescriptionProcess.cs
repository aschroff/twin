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
            StartCoroutine(execute(variant));
            return new ProcessResult();
        }
        
        
        private IEnumerator execute(string idPart)
        {
            Debug.Log("Start Part Description");
            PartManager partManager = getPartManager();
            part = partManager.getPart(idPart);
            describePart(part);
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

        private void describePart(PartManager.PartData part)
        {
            AI.AI ai = getAI();
            part.pathScreenshot = get_path();
            if (File.Exists(part.pathScreenshot))
            {
                Debug.Log("Before Call AI");
                ai.DescribePart(part); 
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