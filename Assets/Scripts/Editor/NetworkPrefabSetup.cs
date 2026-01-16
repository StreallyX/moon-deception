#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// Editor tool to set up player prefabs for networking.
/// Use Window > Moon Deception > Setup Network Prefabs
/// </summary>
public class NetworkPrefabSetup : EditorWindow
{
    private GameObject astronautPrefab;
    private GameObject alienPrefab;

    [MenuItem("Window/Moon Deception/Setup Network Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<NetworkPrefabSetup>("Network Prefab Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Network Prefab Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "This tool configures your player prefabs for multiplayer.\n" +
            "It will add NetworkObject, NetworkTransform, and NetworkedPlayer components.\n" +
            "It will also DISABLE components that shouldn't be active on spawn.",
            MessageType.Info);

        GUILayout.Space(10);

        GUILayout.Label("Drag your player prefabs here:", EditorStyles.label);
        GUILayout.Space(5);

        astronautPrefab = (GameObject)EditorGUILayout.ObjectField("Astronaut Prefab", astronautPrefab, typeof(GameObject), false);
        alienPrefab = (GameObject)EditorGUILayout.ObjectField("Alien Prefab", alienPrefab, typeof(GameObject), false);

        GUILayout.Space(20);

        if (GUILayout.Button("Setup Astronaut Prefab", GUILayout.Height(30)))
        {
            if (astronautPrefab != null)
            {
                SetupPrefab(astronautPrefab, true);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please assign the Astronaut prefab first!", "OK");
            }
        }

        if (GUILayout.Button("Setup Alien Prefab", GUILayout.Height(30)))
        {
            if (alienPrefab != null)
            {
                SetupPrefab(alienPrefab, false);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please assign the Alien prefab first!", "OK");
            }
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Setup Both Prefabs", GUILayout.Height(40)))
        {
            bool success = true;

            if (astronautPrefab != null)
            {
                SetupPrefab(astronautPrefab, true);
            }
            else
            {
                success = false;
            }

            if (alienPrefab != null)
            {
                SetupPrefab(alienPrefab, false);
            }
            else
            {
                success = false;
            }

            if (success)
            {
                EditorUtility.DisplayDialog("Success", "Both prefabs have been set up for networking!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Warning", "Please assign both prefabs!", "OK");
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("What this tool does:", EditorStyles.boldLabel);
        GUILayout.Label("1. Adds NetworkObject (required for networking)");
        GUILayout.Label("2. Adds NetworkTransform (syncs position/rotation)");
        GUILayout.Label("3. Adds NetworkedPlayer (controls ownership)");
        GUILayout.Label("4. DISABLES movement/control scripts by default");
        GUILayout.Label("5. NetworkedPlayer will enable the right scripts at runtime");
    }

    void SetupPrefab(GameObject prefab, bool isAstronaut)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(prefabPath))
        {
            EditorUtility.DisplayDialog("Error", "This is not a prefab asset!", "OK");
            return;
        }

        // Open prefab for editing
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            string prefabType = isAstronaut ? "Astronaut" : "Alien";
            Debug.Log($"[NetworkPrefabSetup] Setting up {prefabType} prefab: {prefab.name}");

            // === Add NetworkObject if missing ===
            if (prefabRoot.GetComponent<NetworkObject>() == null)
            {
                prefabRoot.AddComponent<NetworkObject>();
                Debug.Log($"  Added NetworkObject");
            }

            // === Add OwnerNetworkTransform if missing (Owner Authoritative!) ===
            // Using our custom OwnerNetworkTransform instead of NetworkTransform
            // This allows the OWNER (client) to control their own position
            var ownerNetTransform = prefabRoot.GetComponent<OwnerNetworkTransform>();
            if (ownerNetTransform == null)
            {
                // Remove old NetworkTransform if exists (but not our custom one)
                var oldTransform = prefabRoot.GetComponent<NetworkTransform>();
                if (oldTransform != null && !(oldTransform is OwnerNetworkTransform))
                {
                    DestroyImmediate(oldTransform);
                    Debug.Log($"  Removed old NetworkTransform (was server authoritative)");
                }

                ownerNetTransform = prefabRoot.AddComponent<OwnerNetworkTransform>();
                Debug.Log($"  Added OwnerNetworkTransform (Owner Authoritative - client can move!)");
            }
            // Configure OwnerNetworkTransform
            ownerNetTransform.SyncPositionX = true;
            ownerNetTransform.SyncPositionY = true;
            ownerNetTransform.SyncPositionZ = true;
            ownerNetTransform.SyncRotAngleY = true;

            // === Add NetworkedPlayer if missing ===
            NetworkedPlayer networkedPlayer = prefabRoot.GetComponent<NetworkedPlayer>();
            if (networkedPlayer == null)
            {
                networkedPlayer = prefabRoot.AddComponent<NetworkedPlayer>();
                Debug.Log($"  Added NetworkedPlayer");
            }

            // Configure NetworkedPlayer
            networkedPlayer.isAstronaut = isAstronaut;

            // Find and assign components
            networkedPlayer.playerMovement = prefabRoot.GetComponent<PlayerMovement>();
            networkedPlayer.playerShooting = prefabRoot.GetComponent<PlayerShooting>();
            networkedPlayer.stressSystem = prefabRoot.GetComponent<StressSystem>();
            networkedPlayer.alienController = prefabRoot.GetComponent<AlienController>();
            networkedPlayer.hungerSystem = prefabRoot.GetComponent<HungerSystem>();
            networkedPlayer.alienHealth = prefabRoot.GetComponent<AlienHealth>();
            networkedPlayer.alienAbilities = prefabRoot.GetComponent<AlienAbilities>();
            networkedPlayer.playerCamera = prefabRoot.GetComponentInChildren<Camera>(true);
            networkedPlayer.audioListener = prefabRoot.GetComponentInChildren<AudioListener>(true);

            // === CRITICAL: Disable movement/control scripts by default ===
            // NetworkedPlayer will enable the correct ones at runtime based on ownership

            // Disable Astronaut components
            PlayerMovement pm = prefabRoot.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.enabled = false;
                Debug.Log($"  Disabled PlayerMovement");
            }

            PlayerShooting ps = prefabRoot.GetComponent<PlayerShooting>();
            if (ps != null)
            {
                ps.enabled = false;
                Debug.Log($"  Disabled PlayerShooting");
            }

            StressSystem ss = prefabRoot.GetComponent<StressSystem>();
            if (ss != null)
            {
                ss.enabled = false;
                Debug.Log($"  Disabled StressSystem");
            }

            // Disable Alien components
            AlienController ac = prefabRoot.GetComponent<AlienController>();
            if (ac != null)
            {
                ac.enabled = false;
                Debug.Log($"  Disabled AlienController");
            }

            HungerSystem hs = prefabRoot.GetComponent<HungerSystem>();
            if (hs != null)
            {
                hs.enabled = false;
                Debug.Log($"  Disabled HungerSystem");
            }

            AlienAbilities aa = prefabRoot.GetComponent<AlienAbilities>();
            if (aa != null)
            {
                aa.enabled = false;
                Debug.Log($"  Disabled AlienAbilities");
            }

            // Disable camera by default (will be enabled for local player)
            Camera cam = prefabRoot.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.gameObject.SetActive(false);
                Debug.Log($"  Disabled Camera GameObject");
            }

            // Disable AudioListener by default
            AudioListener al = prefabRoot.GetComponentInChildren<AudioListener>(true);
            if (al != null)
            {
                al.enabled = false;
                Debug.Log($"  Disabled AudioListener");
            }

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            Debug.Log($"[NetworkPrefabSetup] Saved {prefab.name}");

            EditorUtility.DisplayDialog("Success",
                $"{prefab.name} has been set up for networking!\n\n" +
                "Components are DISABLED by default.\n" +
                "NetworkedPlayer will enable the correct ones\n" +
                "when the player spawns based on ownership.",
                "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
#endif
