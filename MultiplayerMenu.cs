using SFS.UI;
using SFS.UI.ModGUI;
using UnityEngine;

namespace RTMSFS
{
    public class MultiplayerMenu
    {
        private static GameObject menuHolder;
        private static Window window;
        public static string RoomText = "Room 1";
        public static string PortText = "5090";

        public static bool IsOpen => menuHolder != null;

        public static void Open()
        {
            if (IsOpen) return;

            menuHolder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "MultiplayerHolder");
            window = Builder.CreateWindow(
                menuHolder.transform, 
                Builder.GetRandomID(), 
                400, 350,
                Screen.width / 2 - 200, Screen.height / 2 + 175, 
                true, true, 0.9f, 
                "Multiplayer Setup"
            );
            window.CreateLayoutGroup(Type.Vertical);

            Builder.CreateLabel(window, 380, 30, text: "Server Port:");
            
            Builder.CreateTextInput(window, 380, 50, text: PortText, onChange: (text) => {
                PortText = text;
            });

            Builder.CreateLabel(window, 380, 30, text: "Room Number:");

            Builder.CreateTextInput(window, 380, 50, text: RoomText, onChange: (text) => {
                RoomText = text;
            });

            Builder.CreateSpace(window, 0, 20);
            
            Builder.CreateButton(window, 200, 50, 0, 0, Connect, "Connect");
        }

        public static void Close()
        {
            if (IsOpen)
            {
                GameObject.Destroy(menuHolder);
                menuHolder = null;
            }
        }

        private static void Connect()
        {
            MsgDrawer.main.Log($"Connected to room : {RoomText}, port: {PortText}");
        }
    }
}
