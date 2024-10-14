using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lean.Gui;
using UnityEngine;

namespace Code
{
    public abstract class QuickHelpProcess: Process
    {
        
        protected abstract void CallAI(AI ai);
        
        public override ProcessResult Execute()
        {
           execute();
            return new ProcessResult();
        }
        
        
        private void execute()
        {
            Recorder recorder = getRecorder();
            DataPersistenceManager dataManager = getDataManager();
            recorder.name = dataManager.selectedProfileId;
            recorder.folder = dataManager.selectedProfileId;
            LeanPulse notification = getNotification();
            recorder.Screenshot(notification);
            AI ai = getAI();
            ai.path = recorder.get_path();
            CallAI(ai);
        }



    }
}