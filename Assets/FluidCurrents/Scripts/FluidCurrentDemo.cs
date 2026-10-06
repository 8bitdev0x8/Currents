using UnityEngine;

/// <summary>Controls the world-space fluid surface, its Karman wake, and the interactive sphere.</summary>
[RequireComponent(typeof(Camera))]
public class FluidCurrentDemo : MonoBehaviour
{
    [Header("Surface palette")]
    public Color deepColor = new Color(0.008f, 0.006f, 0.014f);
    public Color purpleColor = new Color(0.18f, 0.075f, 0.24f);
    public Color lineColor = new Color(0.82f, 0.69f, 0.91f);
    public Color redCurrent = new Color(0.96f, 0.035f, 0.075f);
    public Color goldCurrent = new Color(1f, 0.44f, 0.035f);

    [Header("Flow model")]
    [Min(0.1f)] public float flowVelocity = 2.6f;
    [Range(0.12f, 0.30f)] public float strouhalNumber = 0.20f;
    [Range(0.001f, 0.15f)] public float kinematicViscosity = 0.025f;
    [Min(1f)] public float lineFrequency = 11f;
    [Range(0f, 2f)] public float wakeStrength = 1f;
    public Vector2 flowDirection = new Vector2(0.88f, -0.47f);

    private Camera targetCamera;
    private Renderer surfaceRenderer;
    private Material runtimeSurfaceMaterial;
    private FluidBallInteraction ball;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = new Color(0.008f, 0.006f, 0.014f);
        FindSceneObjects();
        if (surfaceRenderer != null) runtimeSurfaceMaterial = surfaceRenderer.material;
    }

    private void OnDestroy()
    {
        if (runtimeSurfaceMaterial != null && Application.isPlaying)
            Destroy(runtimeSurfaceMaterial);
    }

    private void FindSceneObjects()
    {
        GameObject surface = GameObject.Find("Fluid Surface");
        surfaceRenderer = surface != null ? surface.GetComponent<Renderer>() : null;
        ball = FindObjectOfType<FluidBallInteraction>();
    }

    private void Update()
    {
        if (surfaceRenderer == null || ball == null) FindSceneObjects();
        if (runtimeSurfaceMaterial == null || ball == null) return;

        Vector2 direction = flowDirection.sqrMagnitude > 0.0001f ? flowDirection.normalized : Vector2.right;
        runtimeSurfaceMaterial.SetColor("_Deep", deepColor);
        runtimeSurfaceMaterial.SetColor("_Purple", purpleColor);
        runtimeSurfaceMaterial.SetColor("_Line", lineColor);
        runtimeSurfaceMaterial.SetColor("_RedCurrent", redCurrent);
        runtimeSurfaceMaterial.SetColor("_GoldCurrent", goldCurrent);
        runtimeSurfaceMaterial.SetVector("_FlowDirection", new Vector4(direction.x, direction.y, 0f, 0f));
        runtimeSurfaceMaterial.SetVector("_BallPosition", ball.transform.position);
        runtimeSurfaceMaterial.SetFloat("_BallRadius", ball.Radius);
        runtimeSurfaceMaterial.SetFloat("_FlowVelocity", flowVelocity);
        runtimeSurfaceMaterial.SetFloat("_Strouhal", strouhalNumber);
        runtimeSurfaceMaterial.SetFloat("_Viscosity", kinematicViscosity);
        runtimeSurfaceMaterial.SetFloat("_LineFrequency", lineFrequency);
        runtimeSurfaceMaterial.SetFloat("_WakeStrength", wakeStrength);
        runtimeSurfaceMaterial.SetFloat("_ElapsedTime", Time.time);
    }
}
