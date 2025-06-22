using UnityEngine;

namespace Code.Processes.Meshcapade
{
    
    public class MeshcapadeProcessResult : ProcessResult
    {
        public string apiPath { get; set; }
        public string authToken { get; set; }
        public string newAfiAvatarId { get; set; }
        public string presignedPutUrl { get; set; }
        public string fileContent { get; set; }
        public string avatarName { get; set; }
        public string height { get; set; }
        public string weight { get; set; }
        public string gender { get; set; }
        public string format { get; set; }
        public string pose { get; set; }
    }
    
    public class InitialitzeMeshcapadeImageProcess : ProcessSync
    {
        [SerializeField] public string apiPath;
        [SerializeField] public string authToken;
        [SerializeField] public string format;
        [SerializeField] public string pose;
        
        public override ProcessResult ExecuteSync(string variant = "", ProcessResult previousResult = null)
        {
            MeshcapadeProcessResult meshcapadeResult= new MeshcapadeProcessResult();
            meshcapadeResult.avatarName = getDataManager().selectedProfileId;
            meshcapadeResult.apiPath = apiPath;
            meshcapadeResult.authToken = authToken;
            meshcapadeResult.format = format;
            meshcapadeResult.pose = pose;
            
            return new MeshcapadeProcessResult { authToken = "123", apiPath = "abc"};
        }
        public override ProcessResult Execute(string variant = "")
        {
            return ExecuteSync(variant);
        } 
        
    }
}