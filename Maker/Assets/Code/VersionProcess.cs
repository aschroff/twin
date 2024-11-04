using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

namespace Code
{
    public class VersionProcess: Process
    {
        
        public override ProcessResult Execute(string variant = "")
        {
            StartCoroutine(execute());
            return new ProcessResult();
        }
        
        
        private IEnumerator execute()
        {
            PartManager partManager = getPartManager();
            LeanPulse notification = getNotification();
            if (!partManager.AllPartsDescribed())
            {
                foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
                {
                    text.text = "Complete parts process first!";
                }
                notification.Pulse();
            }
            else
            {
                AI.AI ai = getAI();
                ai.DescribeVersion(partManager);
            }
            yield return new WaitForEndOfFrame();
        }
        
       
        
        
        
    }
}