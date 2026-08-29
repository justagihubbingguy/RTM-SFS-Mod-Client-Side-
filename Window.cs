
using ModLoader.Helpers;
using SFS.UI;
using SFS.UI.ModGUI;
using UnityEngine;

namespace RTMSFS
{
    public class MultiplayerButton
    {
        private static GameObject windowHolder;

        public static void ShowGUI()
        {
            if (windowHolder != null)
            {
                return;
            }

            windowHolder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "MultiplayerHolder");
            
            RectTransform rect = windowHolder.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(20, 20);

            Builder.CreateButton(
                windowHolder.transform, 
                345, 65, 
                1210, 765, 
                ButtonMethod,
                "Multiplayer"
            );
        }

        public static void Load()
        {
            SceneHelper.OnHomeSceneLoaded += ShowGUI;
        }

        public static void Unload()
        {
            SceneHelper.OnHomeSceneLoaded -= ShowGUI;
            if (windowHolder != null)
            {
                GameObject.Destroy(windowHolder);
                windowHolder = null;
            }
        }

        private static void ButtonMethod()
        {
           if (MultiplayerMenu.IsOpen)
            {
                MultiplayerMenu.Close();
            }
            else
            {
                MultiplayerMenu.Open();
            }
        }
    }
}