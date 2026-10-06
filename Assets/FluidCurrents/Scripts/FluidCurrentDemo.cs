using UnityEngine;

/// <summary>Controls a 2D incompressible-flow simulation and its streamline rendering.</summary>
[RequireComponent(typeof(Camera))]
public class FluidCurrentDemo : MonoBehaviour
{
    [Header("Surface palette")]
    public Color deepColor = new Color(0.004f, 0.003f, 0.008f);
    public Color purpleColor = new Color(0.16f, 0.055f, 0.21f);
    public Color lineColor = new Color(0.86f, 0.72f, 0.94f);
    public Color redCurrent = new Color(1f, 0f, 0f);
    public Color goldCurrent = new Color(1f, 0.52f, 0f);

    [Header("Fluid properties")]
    [Min(0.1f)] public float flowVelocity = 1f;
    [Range(0.001f, 0.15f)] public float kinematicViscosity = 0.028f;
    [Header("Wake and visualization")]
    [Min(1f)] public float lineFrequency = 4.49f;
    [Range(0f, 2f)] public float wakeStrength = 2f;
    [Range(0.5f, 5f)] public float wakeViolence = 3f;
    [Range(0.005f, 0.2f)] public float lineWidth = 0.045f;
    [Range(0.02f, 0.4f)] public float redLineWidth = 0.16f;
    [Range(0f, 0.2f)] public float blackLineWidth = 0.04f;
    [Range(1f, 40f)] public float edgeFadeDistance = 12f;
    [Range(8f, 60f)] public float rainbowFadeDistance = 36f;
    [Range(4f, 40f)] public float rainbowCycleLength = 14f;
    [Range(15f, 60f)] public float simulationRate = 30f;
    [Header("Flow recovery")]
    [Range(0.005f, 2f)] public float normalFlowRecovery = 0.04f;
    public Vector2 flowDirection = new Vector2(0.22f, -0.41f);

    [Header("Fixed accent current")]
    public Vector2 currentOriginXZ = new Vector2(0.22f, 8f);

    private Camera targetCamera;
    private Renderer surfaceRenderer;
    private Material runtimeSurfaceMaterial;
    private FluidBallInteraction ball;
    private FluidSimulation2D simulation;
    private bool settingFlowDirection;
    private Vector3 flowDirectionDragStart;
    private float displayedFps;
    private GUIStyle fpsStyle;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = deepColor;
        FindSceneObjects();
        EnsureSimulation();
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
        EnsureSimulation();
    }

    private void EnsureSimulation()
    {
        if (surfaceRenderer == null) return;
        if (simulation == null) simulation = GetComponent<FluidSimulation2D>();
        if (simulation == null) simulation = gameObject.AddComponent<FluidSimulation2D>();
        simulation.Initialize(surfaceRenderer);
    }

    private void Update()
    {
        UpdateFps();
        if (surfaceRenderer == null || ball == null) FindSceneObjects();
        if (runtimeSurfaceMaterial == null || ball == null) return;
        HandleFlowDirectionInput();

        Vector2 direction = flowDirection.sqrMagnitude > 0.0001f ? flowDirection.normalized : Vector2.right;
        runtimeSurfaceMaterial.SetColor("_Deep", deepColor);
        runtimeSurfaceMaterial.SetColor("_Purple", purpleColor);
        runtimeSurfaceMaterial.SetColor("_Line", lineColor);
        runtimeSurfaceMaterial.SetColor("_RedCurrent", redCurrent);
        runtimeSurfaceMaterial.SetColor("_GoldCurrent", goldCurrent);
        runtimeSurfaceMaterial.SetVector("_FlowDirection", new Vector4(direction.x, direction.y, 0f, 0f));
        runtimeSurfaceMaterial.SetVector("_CurrentOrigin", new Vector4(currentOriginXZ.x, currentOriginXZ.y, 0f, 0f));
        runtimeSurfaceMaterial.SetFloat("_BallRadius", ball.Radius);
        runtimeSurfaceMaterial.SetFloat("_FlowVelocity", flowVelocity);
        runtimeSurfaceMaterial.SetFloat("_LineFrequency", lineFrequency);
        runtimeSurfaceMaterial.SetFloat("_LineWidth", lineWidth);
        runtimeSurfaceMaterial.SetFloat("_RedLineWidth", redLineWidth);
        runtimeSurfaceMaterial.SetFloat("_BlackLineWidth", blackLineWidth);
        runtimeSurfaceMaterial.SetFloat("_EdgeFadeDistance", edgeFadeDistance);
        runtimeSurfaceMaterial.SetFloat("_RainbowFadeDistance", rainbowFadeDistance);
        runtimeSurfaceMaterial.SetFloat("_RainbowCycleLength", rainbowCycleLength);
        runtimeSurfaceMaterial.SetFloat("_ElapsedTime", Time.time);
    }

    private void UpdateFps()
    {
        float delta = Time.unscaledDeltaTime;
        if (delta <= 0f) return;
        float instantFps = 1f / delta;
        float blend = 1f - Mathf.Exp(-delta * 3f);
        displayedFps = displayedFps <= 0f ? instantFps : Mathf.Lerp(displayedFps, instantFps, blend);
    }

    private void OnGUI()
    {
        if (fpsStyle == null)
        {
            fpsStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 45f))
            };
            fpsStyle.normal.textColor = new Color(1f, 0.82f, 0.42f);
        }

        GUI.Label(new Rect(Screen.width - 150f, 12f, 136f, 30f), $"{displayedFps:0} FPS", fpsStyle);
    }

    private void HandleFlowDirectionInput()
    {
        if (surfaceRenderer == null || targetCamera == null) return;
        if (Input.GetMouseButtonDown(0))
        {
            Ray pickRay = targetCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(pickRay, out RaycastHit hit) && hit.collider.gameObject == ball.gameObject)
            {
                settingFlowDirection = false;
                return;
            }

            if (TryGetSurfacePoint(Input.mousePosition, out Vector3 point))
            {
                flowDirectionDragStart = point;
                settingFlowDirection = true;
            }
        }

        if (Input.GetMouseButtonUp(0)) settingFlowDirection = false;
        if (!settingFlowDirection || !Input.GetMouseButton(0)) return;
        if (!TryGetSurfacePoint(Input.mousePosition, out Vector3 currentPoint)) return;

        Vector2 drag = new Vector2(currentPoint.x - flowDirectionDragStart.x,
                                   currentPoint.z - flowDirectionDragStart.z);
        if (drag.sqrMagnitude > 0.04f)
            flowDirection = drag.normalized;
    }

    private bool TryGetSurfacePoint(Vector3 screenPoint, out Vector3 point)
    {
        Ray ray = targetCamera.ScreenPointToRay(screenPoint);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, surfaceRenderer.transform.position.y, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }
        point = Vector3.zero;
        return false;
    }

    private void LateUpdate()
    {
        if (surfaceRenderer == null || ball == null || runtimeSurfaceMaterial == null) return;
        EnsureSimulation();
        simulation.Advance(Time.deltaTime, ball, flowDirection, flowVelocity, kinematicViscosity,
            wakeStrength, wakeViolence, currentOriginXZ, simulationRate, normalFlowRecovery);
        Bounds bounds = surfaceRenderer.bounds;
        runtimeSurfaceMaterial.SetVector("_FieldBounds",
            new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
        runtimeSurfaceMaterial.SetVector("_BallPosition", ball.transform.position);
        runtimeSurfaceMaterial.SetTexture("_FlowField", simulation.FieldTexture);
    }
}
