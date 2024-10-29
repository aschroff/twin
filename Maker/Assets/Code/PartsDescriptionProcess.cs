using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    public class PartsDescriptionProcess: Process
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
            StartCoroutine(execute());
            return new ProcessResult();
        }
        
        
        private IEnumerator execute()
        {
            Debug.Log("Start Part Description");
            PartsProcess partProcess = this.transform.parent.gameObject.GetComponentInChildren<PartsProcess>();
            partProcess.Handle();
            yield return new WaitForEndOfFrame();
            PartManager partManager = getPartManager();
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
            partDescriptionProcess.Handle(part.id);
            yield return new WaitForEndOfFrame();
        }
        
        
        
    }
}