using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    /// <summary>
    /// Handles the process of taking screenshots for parts in a scene.
    /// Manages the execution flow, including preparing data, iterating through parts,
    /// and capturing screenshots using a recorder.
    /// </summary>
    public class PartsScreenshotProcess : ProcessSync
    {
        private ViewManager viewManager;
        private Recorder recorder;
        private DataPersistenceManager dataManager;
        private PartManager partManager;
        private PartManager.GroupData nextGroup;
        private PartManager.PartData nextPart;
        private bool isProcessingPart = false;

        public override ProcessResult Execute(string variant = "")
        {
            StartCoroutine(ProcessParts());
            return new ProcessResult();
        }
        
        private IEnumerator ProcessParts()
        {
            Debug.Log("ProcessParts: Start Parts Screenshots");
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

        /// <summary>
        /// Executes the coroutine responsible for capturing screenshots of all parts in the scene.
        /// This method handles the following tasks:
        /// - Iterates through all groups and their respective parts.
        /// - Waits for each part to finish processing before moving to the next. Update()-function starts process for next part when process of part before is completed
        /// - Resets and cleans up after processing is complete.
        /// - Posts the captured data and restores the original view.
        /// </summary>
        public override ProcessResult ExecuteSync(string variant = "")
        {
            Debug.Log("Process status: Start PartsScreenshotProcess");
            StartCoroutine(ExecuteCoroutine());
            Debug.Log("Process status: End PartsScreenshotProcess");
            return new ProcessResult();
        }
        
        private IEnumerator ExecuteCoroutine()
        {
            Debug.Log("ExecuteCoroutine: Start Parts Screenshots");
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
                    // waits until Update() gets called and starts coroutine to take screenshot of this part
                }
            }

            partManager.ClearRefreshAll();
            recorder.Reset(listActive);

            recorder.name = dataManager.selectedProfileId;
            recorder.Post(getNotification());
            viewManager.select(currentView);
            yield return new WaitForEndOfFrame();
            OnExecuteCompleted();
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
            viewManager.select(part.view);
            yield return new WaitForSeconds(0.5f);
            
            partManager.ClearRefreshPart(part);
            yield return new WaitForSeconds(0.5f);
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