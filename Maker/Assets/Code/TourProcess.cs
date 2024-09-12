using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    public class TourProcess: Process
    {

        public override ProcessResult Execute()
        {
            StartCoroutine(execute());
            return new ProcessResult();
        }
        
        
        private IEnumerator execute()
        {
            Debug.Log("Start Screenshots");
            ViewManager viewManager = getViewmanager();
            SceneManagement.View currentView = viewManager.shootView();
            foreach (SceneManagement.View view in getStandardViewManager().views)
            {
                viewManager.select(view);
                Recorder recorder = getRecorder();
                DataPersistenceManager dataManager = getDataManager();
                recorder.name = dataManager.selectedProfileId + " - " + view.name;
                recorder.folder = dataManager.selectedProfileId;
                List<GameObject> listActive = recorder.Prepare();
                yield return new WaitForEndOfFrame();
                recorder.Do();
                recorder.Post(listActive, getNotification());
            }
            viewManager.select(currentView);
            
        }
        
    }
}