using System;
using System.IO;
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
        [SerializeField] private string apiPath;
        [SerializeField] private string authToken;
        [SerializeField] private string format;
        [SerializeField] private string pose;
        [SerializeField] public string imagePath;
        
        public override ProcessResult ExecuteSync(string variant = "", ProcessResult previousResult = null)
        {
            Body.Body body = getBody();
            MeshcapadeProcessResult meshcapadeResult= new MeshcapadeProcessResult();
            meshcapadeResult.avatarName = getDataManager().selectedProfileId;
            meshcapadeResult.apiPath = apiPath;
            meshcapadeResult.authToken = authToken;
            meshcapadeResult.format = format;
            meshcapadeResult.pose = pose;
            meshcapadeResult.gender = body.gender;
            meshcapadeResult.height = body.height.ToString();
            meshcapadeResult.weight = body.weight.ToString();
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);
            meshcapadeResult.fileContent = base64Image;
            
            return meshcapadeResult;
        }
        public override ProcessResult Execute(string variant = "")
        {
            return ExecuteSync(variant);
        } 
        
    }
}