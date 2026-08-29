using HarmonyLib;
using ModLoader;
using ModLoader.Helpers;
using UnityEngine;

namespace RTMSFS
{
    public class Main : Mod
    {
        public override string ModNameID => "MultiplayerMod";

        public override string DisplayName => "Real Time Multiplayer Mod";

        public override string Author => "Ntsan";

        public override string MinimumGameVersionNecessary => "1.5.9.8";

        public override string ModVersion => "v0.0.1 PRE ALPHA";
        
        public override string Description => "This mod introduces a multiplayer system to the game, allowing players to connect and play together in real-time.";

        static Harmony patcher;

        public override void Load()
        {
            MultiplayerButton.Load();

        
            GameObject netObject = new GameObject("SFSNetwork");
            netObject.AddComponent<SfsClient>();
            Object.DontDestroyOnLoad(netObject);
        }
        public override void Early_Load()
        {

            patcher = new Harmony("com.multiplayermod.rtmsfs");
            patcher.PatchAll();
        }
        
        public void Unload()
        {
            try { MultiplayerButton.Unload(); } catch { }

            GameObject netObject = GameObject.Find("SFSNetwork");
            if (netObject != null) Object.Destroy(netObject);

            patcher = null;
        }
    }
}