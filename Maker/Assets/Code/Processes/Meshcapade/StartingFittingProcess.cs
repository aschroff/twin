using System.Threading.Tasks;
using Code.Processes.Meshcapade;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Processes
{
    public class StartFittingProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "")
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(processManager().processResult);
            var apiPath = meshcapadeResult.apiPath;
            var authToken = meshcapadeResult.authToken;
            var newAfiAvatarId = meshcapadeResult.newAfiAvatarId;

            var payload = new
            {
                avatarname = meshcapadeResult.avatarName,
                height = meshcapadeResult.height,
                weight = meshcapadeResult.weight,
                gender = meshcapadeResult.gender
            };

            Debug.Log("Starting fitting process...");
            var response = PostAsync($"{apiPath}/avatars/{newAfiAvatarId}/fit-to-images", authToken, JsonUtility.ToJson(payload)).Result;

            if (response.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Fitting process started successfully.");
            }
            else
            {
                Debug.LogError($"Error starting fitting process: {response.error}");
                meshcapadeResult.ErrorCode = "FITTING_ERROR";
            }

            return meshcapadeResult;
        }
    }
}