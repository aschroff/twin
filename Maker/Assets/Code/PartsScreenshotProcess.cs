using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    public class PartsScreenshotProcess : Process
    {
        private ViewManager viewManager;
        private Recorder recorder;
        private DataPersistenceManager dataManager;
        private PartManager partManager;
        private PartManager.GroupData nextGroup;
        private PartManager.PartData nextPart;
        private bool isProcessingPart = false;

        private IEnumerator ProcessParts()
        {
            Debug.Log("Start Parts Screenshots");
            viewManager = getViewmanager();
            SceneManagement.View currentView = viewManager.shootView();
            recorder = getRecorder();
            dataManager = getDataManager();
            List<GameObject> listActive = recorder.Prepare();
            partManager = getPartManager();
            nextGroup = null;
            nextPart = null;

            foreach (PartManager.GroupData group in partManager.groups)
            {
                Debug.Log("---Set group");
                foreach (PartManager.PartData part in group.groupParts)
                {
                    Debug.Log("---Set part");
                    nextGroup = group;
                    nextPart = part;
                    yield return StartCoroutine(WaitForIdle());
                }
            }

            partManager.ClearRefreshAll();
            recorder.Reset(listActive);
            Debug.Log("---End Screenshots Parts, # of active groups is: " + listActive.Count);
            recorder.name = dataManager.selectedProfileId;
            recorder.Post(getNotification());
            viewManager.select(currentView);
        }

        private IEnumerator WaitForIdle()
        {
            while (isProcessingPart || nextPart != null)
            {
                yield return null;
            }
        }

        public override ProcessResult Execute(string variant = "")
        {
            StartCoroutine(ProcessParts());
            return new ProcessResult();
        }

        private void Update()
        {
            if (nextPart != null && !isProcessingPart)
            {
                StartCoroutine(StartPart());
            }
        }

        private IEnumerator StartPart()
        {
            isProcessingPart = true;
            Debug.Log("---Start Part");
            yield return StartCoroutine(execute(nextPart, nextGroup));
            Debug.Log("---End Part A");
            isProcessingPart = false;
            nextPart = null;
            nextGroup = null;
            Debug.Log("---End Part B");
        }

        private IEnumerator execute(PartManager.PartData part, PartManager.GroupData group)
        {
            Debug.Log("Start execute");
            partManager.EnforceNewPart();
            partManager.ClearRefreshPart(part);
            viewManager.select(part.view);
            recorder.name = dataManager.selectedProfileId + " - " + group.name + " - part " + part.id;
            recorder.folder = dataManager.selectedProfileId;
            Debug.Log("---Start WaitForEndOfFrame");
            yield return new WaitForEndOfFrame();
            Debug.Log("---End WaitForEndOfFrame");
            recorder.Do();
            Debug.Log("---Start yield return null");
            yield return null;
        }
    }
}