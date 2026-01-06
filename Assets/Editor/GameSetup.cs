using UnityEngine;
using UnityEditor;

public class GameSetup : MonoBehaviour
{
    [MenuItem("Moon Deception/Setup Phase 1")]
    public static void SetupPhase1()
    {
        // Create Player with CharacterController
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0, 1, 0);
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0, 1, 0);

        // Attach PlayerMovement and PlayerShooting scripts
        player.AddComponent(System.Type.GetType("PlayerMovement, Assembly-CSharp"));
        player.AddComponent(System.Type.GetType("PlayerShooting, Assembly-CSharp"));

        // Create Camera as child of Player
        GameObject cameraObj = new GameObject("Main Camera");
        cameraObj.transform.SetParent(player.transform);
        cameraObj.transform.localPosition = new Vector3(0, 1.6f, 0);
        cameraObj.transform.localRotation = Quaternion.identity;
        cameraObj.AddComponent<Camera>();
        cameraObj.AddComponent<AudioListener>();
        cameraObj.tag = "MainCamera";

        // Create Ground (Plane)
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10, 1, 10);

        // Create Directional Light
        GameObject light = new GameObject("Directional Light");
        Light lightComp = light.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Select the player
        Selection.activeGameObject = player;

        Debug.Log("Moon Deception Phase 1 setup complete! Press Play to test.");
    }
}
