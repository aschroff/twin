using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lean.Gui;
using UnityEngine;

namespace Code
{
    public abstract class QuickHelpProcess: Process
    {
        
        protected abstract void CallAI(AI.AI ai);
        
        public override ProcessResult Execute()
        {
           StartCoroutine(execute());
            return new ProcessResult();
        }
        
        
        private IEnumerator execute()
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
            CallAI(ai);
        }



    }
}