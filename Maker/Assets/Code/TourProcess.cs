using System.Collections;
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
                yield return null;
                Recorder recorder = getRecorder();
                DataPersistenceManager dataManager = getDataManager();
                recorder.name = dataManager.selectedProfileId + " - " + view.name;
                recorder.folder = dataManager.selectedProfileId;
                recorder.Screenshot();
            }
            viewManager.select(currentView);
            
        }
        
    }
}