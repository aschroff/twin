using System.Collections;
using Lean.Gui;
using UnityEngine;

namespace Code.Processes
{
    public abstract class QuickHelpProcess: Process
    {
        
        protected abstract void CallAI(AI.AI ai, string variant = "");
        
        public override ProcessResult Execute(string variant = "")
        {
           StartCoroutine(execute(variant));
            return new ProcessResult();
        }
        
        
        private IEnumerator execute(string variant = "")
        {
            Recorder recorder = getRecorder();
            DataPersistenceManager dataManager = getDataManager();
            recorder.name = dataManager.selectedProfileId;
            recorder.folder = dataManager.selectedProfileId;
            LeanPulse notification = getNotification();
            recorder.Screenshot(notification);
            yield return new WaitForEndOfFrame();
            AI.AI ai = getAI();
            ai.path = recorder.get_path();
            Debug.Log("Before Call AI");
            CallAI(ai, variant);
        }



    }
}