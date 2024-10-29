using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    public class PartsProcess: Process
    {

        public override ProcessResult Execute(string variant = "")
        {
            StartCoroutine(execute());
            return new ProcessResult();
        }
        
        
        private IEnumerator execute()
        {
            Debug.Log("Start Screenshots Parts");
            ViewManager viewManager = getViewmanager();
            SceneManagement.View currentView = viewManager.shootView();
            Recorder recorder = getRecorder();
            DataPersistenceManager dataManager = getDataManager();
            List<GameObject> listActive = recorder.Prepare();
            PartManager partManager = getPartManager();
            foreach (PartManager.GroupData group in partManager.groups)
            {
                int partCounter = 0;
                foreach (PartManager.PartData part in group.groupParts)
                {
                    partManager.EnforceNewPart();
                    partManager.ClearRefreshPart(part);
                    viewManager.select(part.view);
                    recorder.name = dataManager.selectedProfileId + " - " + group.name + " - part " + part.id;
                    recorder.folder = dataManager.selectedProfileId;
                    yield return new WaitForEndOfFrame();
                    recorder.Do();
                }
            }

            partManager.ClearRefreshAll();
            recorder.Reset(listActive);
            Debug.Log("End Screenshots Parts, # of active groups ist: " + listActive.Count);
            recorder.name = dataManager.selectedProfileId;
            recorder.Post(getNotification());
            viewManager.select(currentView);
            
        }
        
    }
}