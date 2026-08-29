using SFS;
using SFS.Builds;
using SFS.World;
using SFS.WorldBase;

namespace RTMSFS
{
    public class RocketHelper
    {
        public bool CanSpawnRocket()
        {
            return PlayerController.main != null && UnityEngine.Object.FindAnyObjectByType<RocketManager>() != null;
        }
        public void SpawnRemoteRocket(Blueprint blueprint)
        {
            try
            {
                global::RocketManagerPatch.IsRemote = true;
                RocketManager.SpawnBlueprint(blueprint);
            }
            finally
            {
                global::RocketManagerPatch.IsRemote = false;
            }
        }
    }
}