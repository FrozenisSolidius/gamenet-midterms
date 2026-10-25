using Unity.Netcode;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    private void OnGUI()
    {
        
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));
            
            if (GUILayout.Button("Start Host (Player 1)", GUILayout.Height(50)))
            {
                NetworkManager.Singleton.StartHost();
            }
            if (GUILayout.Button("Start Client (Player 2)", GUILayout.Height(50)))
            {
                NetworkManager.Singleton.StartClient();
            }
            if (GUILayout.Button("Start Server Only", GUILayout.Height(50)))
            {
                NetworkManager.Singleton.StartServer();
            }

            GUILayout.EndArea();
        }
        else
        {
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            string mode = NetworkManager.Singleton.IsHost ? "Host" : (NetworkManager.Singleton.IsServer ? "Server" : "Client");
            GUILayout.Label($"<b>Status: Connected as {mode}</b>");
            GUILayout.EndArea();
        }
    }
}