using HarmonyLib;
using SFS.World;
using SFS.Builds;
using SFS.Parsers.Json;
using SFS;
using SFS.Parts;
using UnityEngine;
using System.Linq;
using SFS.Parts.Modules;


[HarmonyPatch(typeof(RocketManager),nameof(RocketManager.SpawnBlueprint))]
public class RocketManagerPatch
{
    public static bool IsRemote = false;
    public static bool IsCodingForThePastMonthNonstopWithNoSleep = true;
    public static bool Prefix(Blueprint blueprint)
    {
        if(!IsRemote)
        {
            if (SfsClient.Instance != null)
            {
                SfsClient.Instance.SendBlueprintToServer(blueprint);
            }
            return true;
        }


        RocketManager mgr = UnityEngine.Object.FindAnyObjectByType<RocketManager>();

        if (mgr == null || mgr.rocketPrefab == null) 
        {
            Debug.LogWarning("RocketManager or prefab not found in scene, stopping spawn.");
            return false;
        }

        Rocket rocketPrefab = mgr.rocketPrefab;

        PartSave[] partsToSpawn = JsonWrapper.FromJson<PartSave[]>(JsonWrapper.ToJson(blueprint.parts, false));
        
        if(blueprint.rotation != 0f)
        {
            foreach (PartSave obj in partsToSpawn)
            {
                obj.position *= new Orientation(1f , 1f , blueprint.rotation);
                obj.orientation += new Orientation(1f , 1f , blueprint.rotation);
            }
        }
        OwnershipState[] ownershipState;
        
        Part[] array = PartsLoader.CreateParts(partsToSpawn, null, null, OnPartNotOwned.Delete, out ownershipState);
        Part[] array2 = array.Where((Part a) => a != null).ToArray();

        Part_Utility.PositionParts(Vector2.zero, new Vector2(0.5f, 0f), round: false, useLaunchBounds: false, array2);
        new JointGroup(RocketManager.GenerateJoints(array2), array2.ToList()).RecreateGroups(out var newGroups);

        foreach(var group in newGroups)
        {
            Rocket rocket = UnityEngine.Object.Instantiate(rocketPrefab);
            rocket.rocketName = "RemotePeer";

            rocket.SetJointGroup(group);
            // ownership for parent and disabling collider

            if (group.parts != null)
            {
                foreach (Part part in group.parts)
                {
                    /*
                        foreach (Collider2D collider in part.GetComponentsInChildren<Collider2D>())
                        {   
                            collider.enabled = false;
                        }
                    */
                    part.transform.SetParent(rocket.partHolder.transform, false);
                    if (!rocket.partHolder.parts.Contains(part))
                    {
                        rocket.partHolder.parts.Add(part);
                    }
                }
            }
            Location spawn = new Location(Base.planetLoader.spaceCenter.LaunchPadLocation.planet, Double2.zero, Double2.zero);
            rocket.physics.SetLocationAndState(spawn, false);

            if (rocket.stats != null)
            {
                rocket.stats.Load(-1);
            }
            if (rocket is I_Physics physInterface)
            {
                physInterface.PhysicsMode = false;
            }

            if (rocket.rb2d != null)
            {
                rocket.rb2d.simulated = true;
                rocket.rb2d.bodyType = RigidbodyType2D.Kinematic;
                rocket.rb2d.linearVelocity = Vector2.zero;
                rocket.rb2d.angularVelocity = 0f;
            }
            if (rocket.physics != null)
            {
                rocket.physics.enabled = false;
            }
        }
        Staging.CreateStages(blueprint.stages, array);
        
        return false;
    }
    public static void Postfix()
    {


    } 
}
