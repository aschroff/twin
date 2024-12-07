using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

namespace Code
{
    public class VersionProcess: ProcessSync
    {
        
        public override ProcessResult Execute(string variant = "")
        {
            StartCoroutine(execute());
            return new ProcessResult();
        }
        
        public override ProcessResult ExecuteSync(string variant = "")
        {
            Debug.Log("Process status: Start VersionProcess");
            StartCoroutine(ExecuteCoroutine());
            Debug.Log("Process status: End VersionProcess");
            return new ProcessResult();
        }

        private IEnumerator ExecuteCoroutine()
        {
            PartManager partManager = getPartManager();
            LeanPulse notification = getNotification();
            float timeout = 10f;
            float elapsedTime = 0f;

            while (!partManager.AllPartsDescribed() && elapsedTime < timeout)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            AI.AI ai = getAI();
            ai.DescribeVersion(partManager);
            
            yield return new WaitForEndOfFrame();
            OnExecuteCompleted();
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