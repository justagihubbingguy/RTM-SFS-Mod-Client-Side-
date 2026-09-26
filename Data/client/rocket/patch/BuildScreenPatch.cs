using HarmonyLib;
using SFS.Builds;
using SFS.Parts;
using System.Collections.Generic;


[HarmonyPatch(typeof(BuildManager), "Launch")]
public static class BuildScreenPatch
{
    public static void Postfix()
    {
        if (SfsClient.Instance == null) return;

        if (BuildManager.main != null && 
            BuildManager.main.buildGrid != null && 
            BuildManager.main.buildGrid.activeGrid != null && 
            BuildManager.main.buildGrid.activeGrid.partsHolder != null)
        {
            List<Part> currentParts = BuildManager.main.buildGrid.activeGrid.partsHolder.parts;
            if (currentParts != null && currentParts.Count > 0)
            {
                Blueprint currentBlueprint = new Blueprint
                {
                    parts = PartSave.CreateSaves(currentParts.ToArray())
                };
                UnityEngine.Debug.Log("Blueprint serialized. Sending Blueprint.");
                
                SfsClient.Instance.SendBlueprintToServer(currentBlueprint);
            }

        }

    }

}