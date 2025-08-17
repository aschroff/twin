using UnityEngine;

namespace Code.Processes.Meshcapade
{

    
    public class FinalitzeMeshcapadeImageProcess : ProcessSync
    {
        public override ProcessResult ExecuteSync(string variant = "")
        {
            return new ProcessResult();
        }
        public override ProcessResult Execute(string variant = "")
        {
            return ExecuteSync(variant);
        } 
        
    }
}