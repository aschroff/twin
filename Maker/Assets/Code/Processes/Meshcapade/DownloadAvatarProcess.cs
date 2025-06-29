using System.Threading.Tasks;
using Code.Processes.Meshcapade;
using UnityEngine;

namespace Code.Processes
{
    public class DownloadAvatarProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "", ProcessResult previousResult = null)
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(previousResult);
            var apiPath = meshcapadeResult.apiPath;
            var authToken = meshcapadeResult.authToken;
            var newAfiAvatarId = meshcapadeResult.newAfiAvatarId;

            var payload = new
            {
                format = meshcapadeResult.format,
                pose = meshcapadeResult.pose
            };

            var response = PostAsync($"{apiPath}/avatars/{newAfiAvatarId}/export", authToken, JsonUtility.ToJson(payload)).Result;
            response.EnsureSuccessStatusCode();

            return meshcapadeResult;
        }
    }
}