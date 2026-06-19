using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Category5.UI;
using Category5.Core;
using System.Collections;

[InitializeOnLoad]
public class PlayModeTestRunner
{
    static PlayModeTestRunner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.update += RunTest;
        }
    }

    private static int frameCount = 0;
    private static void RunTest()
    {
        frameCount++;
        if (frameCount < 10) return; // Wait for initial spawn
        EditorApplication.update -= RunTest;

        var bootstrap = new GameObject("TestBootstrap").AddComponent<MonoBehaviour>();
        bootstrap.StartCoroutine(TestSequence());
    }

    private static IEnumerator TestSequence()
    {
        Debug.Log("[Test] Starting Hosting test sequence.");
        
        // 1. Check if we are in Homebase and have a player
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Homebase")
        {
            Debug.Log("[Test] Loading Homebase scene...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Homebase");
            yield return new WaitForSeconds(1f);
        }

        var offlinePlayer = GameObject.Find("LocalPlayer_Offline");
        Debug.Log("[Test] Offline player: " + (offlinePlayer != null ? "Found" : "Missing"));

        // 2. Open Network Terminal and click Host
        var terminal = Object.FindFirstObjectByType<Category5.Interactions.NetworkTerminal>();
        if (terminal == null)
        {
            SessionState.SetString("PlayModeTest.Result", "FAIL: NetworkTerminal not found");
            EditorApplication.isPlaying = false;
            yield break;
        }

        Debug.Log("[Test] Interacting with terminal...");
        terminal.Interact(offlinePlayer != null ? offlinePlayer : GameObject.FindWithTag("Player"));
        yield return new WaitForSeconds(0.5f);

        var menu = Object.FindFirstObjectByType<NetworkMenu>();
        if (menu == null)
        {
            SessionState.SetString("PlayModeTest.Result", "FAIL: NetworkMenu not found after interaction");
            EditorApplication.isPlaying = false;
            yield break;
        }

        Debug.Log("[Test] Clicking Host button...");
        menu.OnHostClicked();

        // 3. Wait for hosting to start
        float timeout = 10f;
        while (timeout > 0 && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer))
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            SessionState.SetString("PlayModeTest.Result", "FAIL: Server failed to start within timeout");
            EditorApplication.isPlaying = false;
            yield break;
        }

        Debug.Log("[Test] Server started. Waiting for player spawn...");
        yield return new WaitForSeconds(2f);

        // 4. Verify player state
        var networkedPlayer = Object.FindFirstObjectByType<Category5.Player.PlayerController>();
        bool isNetworked = networkedPlayer != null && networkedPlayer.GetComponent<NetworkObject>() != null && networkedPlayer.GetComponent<NetworkObject>().IsSpawned;
        
        Debug.Log("[Test] Networked Player: " + (networkedPlayer != null ? networkedPlayer.name : "NULL"));
        Debug.Log("[Test] Is Spawned: " + isNetworked);

        if (networkedPlayer != null && isNetworked)
        {
            SessionState.SetString("PlayModeTest.Result", "SUCCESS: Player spawned and networked");
        }
        else
        {
            SessionState.SetString("PlayModeTest.Result", "FAIL: No networked player spawned");
        }

        EditorApplication.isPlaying = false;
    }
}
