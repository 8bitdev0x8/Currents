using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FluidCurrentsSceneSetup
{
    private const string ScenePath = "Assets/Scenes/FluidCurrentsDemo.unity";

    static FluidCurrentsSceneSetup()
    {
        EditorApplication.delayCall += CreateSceneIfMissing;
    }

    private static void CreateSceneIfMissing()
    {
        if (!File.Exists(ScenePath))
        {
            CreateScene();
            return;
        }

        string activePath = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(activePath))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (EditorSceneManager.GetActiveScene().path == ScenePath)
        {
            EnsureBallExists();
            AddSceneToBuildSettings();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }

    [MenuItem("Fluid Currents/Create Demo Scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 60f;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<FluidCurrentDemo>();

        EnsureBallExists();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        Debug.Log("Created Fluid Currents demo scene at " + ScenePath);
    }

    private static void EnsureBallExists()
    {
        if (Object.FindObjectOfType<FluidBallInteraction>() != null) return;
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("The demo scene needs a Main Camera before the ball can be created.");
            return;
        }

        const string materialPath = "Assets/FluidCurrents/Materials/SilverBall.mat";
        Directory.CreateDirectory("Assets/FluidCurrents/Materials");
        Material ballMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (ballMaterial == null)
        {
            Shader ballShader = Shader.Find("FluidCurrents/Silver Ball");
            if (ballShader == null)
            {
                Debug.LogError("FluidCurrents/Silver Ball shader is missing. Let Unity finish importing the project, then create the scene again.");
                return;
            }
            ballMaterial = new Material(ballShader) { name = "Silver Ball" };
            AssetDatabase.CreateAsset(ballMaterial, materialPath);
            AssetDatabase.SaveAssets();
        }

        float visibleHeight = 2f * 7f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float ballDiameter = visibleHeight * 0.28f;
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Interactive Silver Ball";
        ball.transform.position = camera.ViewportToWorldPoint(new Vector3(0.55f, 0.38f, 7f));
        ball.transform.localScale = Vector3.one * ballDiameter;
        ball.GetComponent<Renderer>().sharedMaterial = ballMaterial;
        ball.AddComponent<FluidBallInteraction>();
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
