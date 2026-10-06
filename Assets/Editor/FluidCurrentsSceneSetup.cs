using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FluidCurrentsSceneSetup
{
    private const string ScenePath = "Assets/Scenes/FluidCurrentsDemo.unity";
    private const string SurfaceMaterialPath = "Assets/FluidCurrents/Materials/FluidSurface.mat";
    private const string BallMaterialPath = "Assets/FluidCurrents/Materials/SilverBall.mat";

    static FluidCurrentsSceneSetup()
    {
        EditorApplication.delayCall += OpenOrUpgradeDemoScene;
    }

    private static void OpenOrUpgradeDemoScene()
    {
        if (!File.Exists(ScenePath))
        {
            CreateScene();
            return;
        }

        if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (EditorSceneManager.GetActiveScene().path != ScenePath) return;

        if (GameObject.Find("Fluid Surface") == null || Object.FindObjectOfType<FluidBallInteraction>() == null)
            CreateScene();
        else
            AddSceneToBuildSettings();
    }

    [MenuItem("Fluid Currents/Rebuild 3D Demo Scene")]
    public static void CreateScene()
    {
        Shader surfaceShader = Shader.Find("FluidCurrents/World Surface");
        Shader ballShader = Shader.Find("FluidCurrents/Silver Ball");
        if (surfaceShader == null || ballShader == null)
        {
            Debug.LogError("Fluid Currents shaders are still importing. Wait for Unity to finish, then choose Fluid Currents > Rebuild 3D Demo Scene.");
            return;
        }

        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/FluidCurrents/Materials");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.008f, 0.006f, 0.014f);
        camera.fieldOfView = 47f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 180f;
        Vector3 cameraTarget = new Vector3(0f, 0f, 8f);
        cameraObject.transform.position = new Vector3(0f, 10f, -17f);
        cameraObject.transform.rotation = Quaternion.LookRotation(cameraTarget - cameraObject.transform.position, Vector3.up);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<FluidCurrentDemo>();

        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
        surface.name = "Fluid Surface";
        surface.transform.position = new Vector3(0f, -0.03f, 10f);
        surface.transform.localScale = new Vector3(24f, 1f, 24f);
        Collider surfaceCollider = surface.GetComponent<Collider>();
        if (surfaceCollider != null) Object.DestroyImmediate(surfaceCollider);
        surface.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(
            SurfaceMaterialPath,
            surfaceShader,
            material =>
            {
                material.SetColor("_Deep", new Color(0.008f, 0.006f, 0.014f));
                material.SetColor("_Purple", new Color(0.18f, 0.075f, 0.24f));
                material.SetColor("_Line", new Color(0.82f, 0.69f, 0.91f));
                material.SetColor("_RedCurrent", new Color(0.96f, 0.035f, 0.075f));
                material.SetColor("_GoldCurrent", new Color(1f, 0.44f, 0.035f));
            });

        const float ballRadius = 1.5f;
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Interactive Silver Ball";
        ball.transform.position = new Vector3(0f, ballRadius * 0.5f, 8f);
        ball.transform.localScale = Vector3.one * (ballRadius * 2f);
        ball.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(
            BallMaterialPath,
            ballShader,
            material => { });
        ball.AddComponent<FluidBallInteraction>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("Created the 3D fluid-current demo with an interactive sphere and a Strouhal-based wake.");
    }

    private static Material GetOrCreateMaterial(string path, Shader shader, System.Action<Material> setup)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        setup(material);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void AddSceneToBuildSettings()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
