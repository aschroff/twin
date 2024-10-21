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
            Recorder recorder = getRecorder();
            DataPersistenceManager dataManager = getDataManager();
            List<GameObject> listActive = recorder.Prepare();
            foreach (SceneManagement.View view in getStandardViewManager().views)
            {
                viewManager.select(view);
                recorder.name = dataManager.selectedProfileId + " - " + view.name;
                recorder.folder = dataManager.selectedProfileId;
                yield return new WaitForEndOfFrame();
                recorder.Do();
            }
            recorder.Reset(listActive);
            recorder.name = dataManager.selectedProfileId + " - <view name>";
            recorder.Post(getNotification());
            viewManager.select(currentView);
            
        }
        
    }
}