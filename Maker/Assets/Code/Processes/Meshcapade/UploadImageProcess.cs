using System.Threading.Tasks;
using Code.Processes.Meshcapade;

namespace Code.Processes
{
    public class UploadImageProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "", ProcessResult previousResult = null)
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(previousResult);
            var apiPath = meshcapadeResult.apiPath;
            var authToken = meshcapadeResult.authToken;
            var newAfiAvatarId = meshcapadeResult.newAfiAvatarId;

            var response = PostAsync($"{apiPath}/avatars/{newAfiAvatarId}/images", authToken).Result;
            response.EnsureSuccessStatusCode();

            return meshcapadeResult;
        }
    }
}