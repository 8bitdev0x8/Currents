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
        runtimeSurfaceMaterial.SetFloat("_ElapsedTime", Time.time);
    }

    private void HandleFlowDirectionInput()
    {
        if (surfaceRenderer == null || targetCamera == null) return;
        if (Input.GetMouseButtonDown(1))
        {
            if (TryGetSurfacePoint(Input.mousePosition, out Vector3 point))
            {
                flowDirectionDragStart = point;
                settingFlowDirection = true;
            }
        }

        if (Input.GetMouseButtonUp(1)) settingFlowDirection = false;
        if (!settingFlowDirection || !Input.GetMouseButton(1)) return;
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
            wakeStrength, wakeViolence, currentOriginXZ);
        Bounds bounds = surfaceRenderer.bounds;
        runtimeSurfaceMaterial.SetVector("_FieldBounds",
            new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
        runtimeSurfaceMaterial.SetVector("_BallPosition", ball.transform.position);
        runtimeSurfaceMaterial.SetTexture("_FlowField", simulation.FieldTexture);
    }
}
