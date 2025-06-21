using System.Threading.Tasks;
using Code.Processes.Meshcapade;
using UnityEngine;

namespace Code.Processes
{
    public class StartFittingProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "", ProcessResult previousResult = null)
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(previousResult);
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

            var response = PostAsync($"{apiPath}/avatars/{newAfiAvatarId}/fit-to-images", authToken, JsonUtility.ToJson(payload)).Result;
            response.EnsureSuccessStatusCode();

            return meshcapadeResult;
        }
    }
}