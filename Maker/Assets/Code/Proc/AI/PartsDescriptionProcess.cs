using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    public class PartsDescriptionProcess: ProcessSync
    {
        private ProcessManager processManager;
        private PartDescriptionProcess partDescriptionProcess;
        [SerializeField] public bool hardRedo = false;
        private ProcessManager getProcessManager()
        {
            return this.gameObject.transform.parent.gameObject.GetComponent<ProcessManager>();
        }

        public override ProcessResult Execute(string variant = "")
        {
            processManager = getProcessManager();
            partDescriptionProcess = processManager.gameObject.GetComponentInChildren<PartDescriptionProcess>();
            execute(variant);
            return new ProcessResult();
        }
        
        
        public override ProcessResult ExecuteSync(string variant = "")
        {
            Debug.Log("Process status: Start PartsDescriptionProcess");
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
            foreach (PartManager.GroupData group in partManager.groups)
            {
                int partCounter = 0;
                foreach (PartManager.PartData part in group.groupParts)
                {
                    StartCoroutine(describePart(part, variant));
                }

            }
            yield return new WaitForEndOfFrame();
            OnExecuteCompleted();
        }
        private void execute(string variant)
        {
            Debug.Log("Process status: PartsDescriptionProcess execute");
            PartManager partManager = getPartManager();
            AI.AI ai = getAI();
            foreach (PartManager.GroupData group in partManager.groups)
            {
                int partCounter = 0;
                foreach (PartManager.PartData part in group.groupParts)
                {
                    if ((part.description == "") || (hardRedo == true))
                    {
                        partCounter++;
                        part.description = "Part Number " + partCounter + " :\n";
                        part.description += part.meaning + "\n";
                    }
                    else
                    {
                        part.description = "No description";
                    }
                    StartCoroutine(describePart(part, variant));
                }

            }

        }
        
        private IEnumerator describePart(PartManager.PartData part, string variant)
        {   
            Debug.Log("Process status: describePart" + part.id);
            partDescriptionProcess.Handle(variant + "##" + part.id);
            yield return new WaitForEndOfFrame();
        }
        
        
        
    }
}