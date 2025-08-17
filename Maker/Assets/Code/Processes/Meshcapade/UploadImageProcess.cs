using System.Threading.Tasks;
using Code.Processes.Meshcapade;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Processes
{
    public class UploadImageProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "")
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(processManager().processResult);
            var apiPath = meshcapadeResult.apiPath;
            var authToken = meshcapadeResult.authToken;
            var newAfiAvatarId = meshcapadeResult.newAfiAvatarId;

            Debug.Log("Uploading image...");
            var response = PostAsync($"{apiPath}/avatars/{newAfiAvatarId}/images", authToken).Result;

            if (response.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Image uploaded successfully.");
            }
            else
            {
                Debug.LogError($"Error uploading image: {response.error}");
                meshcapadeResult.ErrorCode = "UPLOAD_ERROR";
            }

            return meshcapadeResult;
        }
    }
}