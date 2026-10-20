#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CardBattle;

/// <summary>Multiplayer parts of the scene builder: network components on prefabs, the NetworkPlayer prefab, the Menu scene.</summary>
public static partial class CardBattleSetup
{
    /// <summary>
    /// NetworkObject + NetworkEntity on every unit / building / tower prefab. Units also get a NetworkTransform
    /// (server-authoritative position, interpolated on clients). Buildings and towers never move, so they need none.
    /// </summary>
    static void AddNetworkComponents(GameObject root, bool moves)
    {
        root.AddComponent<NetworkObject>();
        if (moves)
        {
            NetworkTransform nt = root.AddComponent<NetworkTransform>();
            nt.PositionThreshold = 0.03f; // ignore sub-3cm jitter from the soft collision: far fewer messages
        }
        root.AddComponent<NetworkEntity>();
    }

    // Battle scene: the in-scene network object that connects MatchManager to the network.
    static void AddNetworkSceneObjects()
    {
        GameObject match = new GameObject("NetworkMatch");
        match.AddComponent<NetworkObject>();
        match.AddComponent<NetworkMatchManager>();
        new GameObject("ConnectionStatus").AddComponent<ConnectionStatusUI>();
    }

    static GameObject BuildNetworkPlayerPrefab()
    {
        GameObject root = new GameObject("NetworkPlayer");
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkPlayer>();
        return Save(root, "NetworkPlayer");
    }

    static void BuildMenuScene(string battlePath)
    {
        GameObject playerPrefab = BuildNetworkPlayerPrefab();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.09f, 0.12f, 0.2f);
        camGo.AddComponent<AudioListener>();

        // These two survive scene changes (DontDestroyOnLoad) and create the NetworkManager + UnityTransport at runtime.
        GameObject bootGo = new GameObject("NetworkBootstrap");
        NetworkBootstrap boot = bootGo.AddComponent<NetworkBootstrap>();
        SetObj(boot, "playerPrefab", playerPrefab);
        new GameObject("NetworkGameManager").AddComponent<NetworkGameManager>();

        new GameObject("MenuUI").AddComponent<NetworkConnectionUI>();

        string menuPath = Root + "/Scenes/Menu.unity";
        EditorSceneManager.SaveScene(scene, menuPath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(menuPath, true),   // index 0: opens first
            new EditorBuildSettingsScene(battlePath, true)
        };
        Debug.Log("Card Battle multiplayer: Menu scene built (" + menuPath + "). Open it and press Play to host or join.");
    }
}
#endif
