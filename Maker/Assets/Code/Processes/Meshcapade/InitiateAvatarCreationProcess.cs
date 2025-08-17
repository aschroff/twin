using System.Threading.Tasks;
using Code.Processes.Meshcapade;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Processes
{
    public class InitiateAvatarCreationProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "")
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(processManager().processResult);
            var apiPath = meshcapadeResult.apiPath;
            var authToken = meshcapadeResult.authToken;

            Debug.Log("Initiating avatar creation...");
            var response = PostAsync($"{apiPath}/avatars/create/from-images", authToken).Result;

            if (response.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Avatar creation initiated successfully.");
                meshcapadeResult.newAfiAvatarId = response.downloadHandler.text;
            }
            else
            {
                Debug.LogError($"Error initiating avatar creation: {response.error}");
                meshcapadeResult.ErrorCode = "CREATION_ERROR";
            }

            return meshcapadeResult;
        }
        
        
    }
}