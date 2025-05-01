using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    public class PartsDescriptionProcess: ProcessSync
    {
        private ProcessManager processManager;
        private PartDescriptionProcess partDescriptionProcess;
        private ProcessManager getProcessManager()
        {
            return this.gameObject.transform.parent.gameObject.GetComponent<ProcessManager>();
        }

        public override ProcessResult Execute(string variant = "")
        {
            processManager = getProcessManager();
            partDescriptionProcess = processManager.gameObject.GetComponentInChildren<PartDescriptionProcess>();
            execute();
            return new ProcessResult();
        }
        
        
        public override ProcessResult ExecuteSync(string variant = "")
        {
            Debug.Log("Process status: Start PartsDescriptionProcess ExecuteSync");
            processManager = getProcessManager();
            partDescriptionProcess = processManager.gameObject.GetComponentInChildren<PartDescriptionProcess>();
            StartCoroutine(ExecuteCoroutine(variant));
            Debug.Log("Process status: End PartsDescriptionProcess");
            return new ProcessResult();
        }

        private IEnumerator ExecuteCoroutine(string variant)
        {
            Debug.Log("Process status: PartsDescriptionProcess ExecuteCoroutine");
            PartManager partManager = getPartManager();
            AI.AI ai = getAI();
            ai.characterDescription.text = "Description of the medical findings: \n";
            foreach (PartManager.GroupData group in partManager.groups)
            {
                int partCounter = 0;
                foreach (PartManager.PartData part in group.groupParts)
                {
                    StartCoroutine(describePart(part));
                }

            }
            yield return new WaitForEndOfFrame();
            OnExecuteCompleted();
        }
        private void execute()
        {
            Debug.Log("Process status: PartsDescriptionProcess execute");
            PartManager partManager = getPartManager();
            AI.AI ai = getAI();
            ai.characterDescription.text = "Description of the medical findings: \n";
            foreach (PartManager.GroupData group in partManager.groups)
            {
                int partCounter = 0;
                foreach (PartManager.PartData part in group.groupParts)
                {
                    StartCoroutine(describePart(part));
                }

            }

        }
        
        private IEnumerator describePart(PartManager.PartData part)
        {   
            Debug.Log("Process status: describePart" + part.id);
            partDescriptionProcess.Handle(part.id);
            yield return new WaitForEndOfFrame();
        }
        
        
        
    }
}