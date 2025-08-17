using System.Threading.Tasks;
using Code.Processes.Meshcapade;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Processes
{
    public class DownloadAvatarProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "")
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(processManager().processResult);
            var apiPath = meshcapadeResult.apiPath;
            var authToken = meshcapadeResult.authToken;
            var newAfiAvatarId = meshcapadeResult.newAfiAvatarId;

            var payload = new
            {
                format = meshcapadeResult.format,
                pose = meshcapadeResult.pose
            };

            Debug.Log("Downloading avatar...");
            var response = PostAsync($"{apiPath}/avatars/{newAfiAvatarId}/export", authToken, JsonUtility.ToJson(payload)).Result;

            if (response.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Avatar downloaded successfully.");
                meshcapadeResult.avatarData = response.downloadHandler.text;
            }
            else
            {
                Debug.LogError($"Error downloading avatar: {response.error}");
                meshcapadeResult.ErrorCode = "DOWNLOAD_ERROR";
            }

            return meshcapadeResult;
        }
    }
}