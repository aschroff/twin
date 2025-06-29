using System.Threading.Tasks;
using Code.Processes.Meshcapade;

namespace Code.Processes
{
    public class InitiateAvatarCreationProcess : WebApiProcess
    {
        public override ProcessResult ExecuteSync(string variant = "", ProcessResult previousResult = null)
        {
            MeshcapadeProcessResult meshcapadeResult = TryCastToMeshcapadeProcessResult(previousResult);
            var apiPath = meshcapadeResult.apiPath;
            var authToken = meshcapadeResult.authToken;

            var response = PostAsync($"{apiPath}/avatars/create/from-images", authToken).Result;
            response.EnsureSuccessStatusCode();

            meshcapadeResult.newAfiAvatarId = response.Content.ReadAsStringAsync().Result;
            return meshcapadeResult;
        }
        
        
    }
}