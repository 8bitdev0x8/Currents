using UnityEngine;

/// <summary>Controls the world-space fluid surface, its Karman wake, and the interactive sphere.</summary>
[RequireComponent(typeof(Camera))]
public class FluidCurrentDemo : MonoBehaviour
{
    [Header("Surface palette")]
    public Color deepColor = new Color(0.004f, 0.003f, 0.008f);
    public Color purpleColor = new Color(0.16f, 0.055f, 0.21f);
    public Color lineColor = new Color(0.86f, 0.72f, 0.94f);
    public Color redCurrent = new Color(1f, 0f, 0f);
    public Color goldCurrent = new Color(1f, 0.52f, 0f);

    [Header("Flow model")]
    [Min(0.1f)] public float flowVelocity = 1f;
    [Range(0.12f, 0.30f)] public float strouhalNumber = 0.245f;
    [Range(0.001f, 0.15f)] public float kinematicViscosity = 0.028f;
    [Min(1f)] public float lineFrequency = 4.49f;
    [Range(0f, 2f)] public float wakeStrength = 2f;
    [Range(0.5f, 5f)] public float wakeViolence = 3f;
    [Range(0.005f, 0.2f)] public float lineWidth = 0.045f;
    [Range(0.02f, 0.4f)] public float redLineWidth = 0.16f;
    [Range(0f, 0.2f)] public float blackLineWidth = 0.04f;
    public Vector2 flowDirection = new Vector2(0.22f, -0.41f);

    [Header("Fixed accent current")]
    public Vector2 currentOriginXZ = new Vector2(0.22f, 8f);

    private Camera targetCamera;
    private Renderer surfaceRenderer;
    private Material runtimeSurfaceMaterial;
    private FluidBallInteraction ball;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = deepColor;
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
        runtimeSurfaceMaterial.SetVector("_CurrentOrigin", new Vector4(currentOriginXZ.x, currentOriginXZ.y, 0f, 0f));
        runtimeSurfaceMaterial.SetVector("_BallPosition", ball.transform.position);
        runtimeSurfaceMaterial.SetFloat("_BallMotion", ball.MovementIntensity);
        runtimeSurfaceMaterial.SetFloat("_BallRadius", ball.Radius);
        runtimeSurfaceMaterial.SetFloat("_FlowVelocity", flowVelocity);
        runtimeSurfaceMaterial.SetFloat("_Strouhal", strouhalNumber);
        runtimeSurfaceMaterial.SetFloat("_Viscosity", kinematicViscosity);
        runtimeSurfaceMaterial.SetFloat("_LineFrequency", lineFrequency);
        runtimeSurfaceMaterial.SetFloat("_LineWidth", lineWidth);
        runtimeSurfaceMaterial.SetFloat("_RedLineWidth", redLineWidth);
        runtimeSurfaceMaterial.SetFloat("_BlackLineWidth", blackLineWidth);
        runtimeSurfaceMaterial.SetFloat("_WakeStrength", wakeStrength);
        runtimeSurfaceMaterial.SetFloat("_WakeViolence", wakeViolence);
        runtimeSurfaceMaterial.SetFloat("_ElapsedTime", Time.time);
    }
}
