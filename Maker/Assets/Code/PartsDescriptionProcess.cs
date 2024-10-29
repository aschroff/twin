using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code
{
    public class PartsDescriptionProcess: Process
    {

        public override ProcessResult Execute(string variant = "")
        {
            StartCoroutine(execute());
            return new ProcessResult();
        }
        
        
        private IEnumerator execute()
        {
            Debug.Log("Start Part Description");
            PartsProcess partProcess = this.transform.parent.gameObject.GetComponentInChildren<PartsProcess>();
            partProcess.Handle();
            yield return new WaitForEndOfFrame();

        }
        
    }
}