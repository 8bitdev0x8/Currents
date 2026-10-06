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
    [Range(0.0001f, 2f)] public float normalFlowRecovery = 0.001f;

    [Header("Show parameters in overlay")]
    public bool showFlowVelocityInOverlay = true;
    public bool showViscosityInOverlay;
    public bool showWakeStrengthInOverlay = true;
    public bool showWakeViolenceInOverlay;
    public bool showLineFrequencyInOverlay = true;
    public bool showLineWidthInOverlay;
    public bool showRedLineWidthInOverlay;
    public bool showBlackLineWidthInOverlay;
    public bool showEdgeFadeInOverlay;
    public bool showRainbowFadeInOverlay;
    public bool showRainbowCycleInOverlay;
    public bool showSimulationRateInOverlay;
    public bool showFlowRecoveryInOverlay = true;

    [Header("Flow direction")]
    public Vector2 flowDirection = new Vector2(0.22f, -0.41f);

    [Header("Fixed accent current")]
    public Vector2 currentOriginXZ = new Vector2(0.22f, 8f);

    private Camera targetCamera;
    private Renderer surfaceRenderer;
    private Material runtimeSurfaceMaterial;
    private FluidBallInteraction ball;
    private FluidBallTrail sphereTrail;
    private FluidSimulation2D simulation;
    private bool settingFlowDirection;
    private Vector3 flowDirectionDragStart;
    private float displayedFps;
    private GUIStyle fpsStyle;
    private GUIStyle overlayButtonStyle;
    private GUIStyle restoreButtonStyle;
    private GUIStyle overlaySectionStyle;
    private GUIStyle overlayLabelStyle;
    private GUIStyle overlayValueStyle;
    private GUIStyle sliderStyle;
    private GUIStyle sliderThumbStyle;
    private Texture2D panelTexture;
    private Texture2D hoverTexture;
    private Texture2D trackTexture;
    private Texture2D accentTexture;
    private bool overlayOpen;
    private Vector2 overlayScroll;

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
        DestroyGuiTexture(panelTexture);
        DestroyGuiTexture(hoverTexture);
        DestroyGuiTexture(trackTexture);
        DestroyGuiTexture(accentTexture);
    }

    private void FindSceneObjects()
    {
        GameObject surface = GameObject.Find("Fluid Surface");
        surfaceRenderer = surface != null ? surface.GetComponent<Renderer>() : null;
        ball = FindObjectOfType<FluidBallInteraction>();
        if (ball != null && surfaceRenderer != null)
        {
            sphereTrail = ball.GetComponent<FluidBallTrail>();
            if (sphereTrail == null) sphereTrail = ball.gameObject.AddComponent<FluidBallTrail>();
            sphereTrail.Initialize(surfaceRenderer);
        }
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
        EnsureGuiStyles();
        DrawParameterOverlay();
        Rect fpsRect = new Rect(Screen.width - 120f, 12f, 108f, 34f);
        GUI.DrawTexture(fpsRect, panelTexture);
        GUI.DrawTexture(new Rect(fpsRect.x, fpsRect.y, 2f, fpsRect.height), accentTexture);
        GUI.Label(fpsRect, $"{displayedFps:0} FPS", fpsStyle);
    }

    private void DrawParameterOverlay()
    {
        float panelWidth = Mathf.Min(330f, Screen.width - 24f);
        if (GUI.Button(new Rect(12f, 12f, panelWidth, 38f),
            overlayOpen ? "CURRENTS     /     CONTROLS     −" : "CURRENTS     /     CONTROLS     +", overlayButtonStyle))
            overlayOpen = !overlayOpen;
        if (!overlayOpen) return;

        float panelHeight = Mathf.Min(560f, Mathf.Max(150f, Screen.height - 76f));
        Rect panelRect = new Rect(12f, 56f, panelWidth, panelHeight);
        GUI.DrawTexture(panelRect, panelTexture);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, 3f, panelRect.height), accentTexture);
        GUI.Label(new Rect(28f, 66f, panelWidth - 42f, 18f), "LIVE SIMULATION PARAMETERS", overlaySectionStyle);
        GUILayout.BeginArea(new Rect(28f, 88f, panelWidth - 44f, panelHeight - 42f));
        overlayScroll = GUILayout.BeginScrollView(overlayScroll);

        GUILayout.BeginHorizontal();
        GUILayout.Label("ADJUST SETTINGS", overlaySectionStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("RESTORE DEFAULTS", restoreButtonStyle, GUILayout.Width(126f), GUILayout.Height(25f)))
            RestoreDefaultValues();
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);

        if (showFlowVelocityInOverlay) DrawOverlaySlider("Flow velocity", ref flowVelocity, 0.1f, 4f);
        if (showViscosityInOverlay) DrawOverlaySlider("Kinematic viscosity", ref kinematicViscosity, 0.001f, 0.15f);
        if (showWakeStrengthInOverlay) DrawOverlaySlider("Wake strength", ref wakeStrength, 0f, 2f);
        if (showWakeViolenceInOverlay) DrawOverlaySlider("Wake violence", ref wakeViolence, 0.5f, 5f);
        if (showLineFrequencyInOverlay) DrawOverlaySlider("Line frequency", ref lineFrequency, 1f, 12f);
        if (showLineWidthInOverlay) DrawOverlaySlider("Line width", ref lineWidth, 0.005f, 0.2f);
        if (showRedLineWidthInOverlay) DrawOverlaySlider("Red line width", ref redLineWidth, 0.02f, 0.4f);
        if (showBlackLineWidthInOverlay) DrawOverlaySlider("Black line width", ref blackLineWidth, 0f, 0.2f);
        if (showEdgeFadeInOverlay) DrawOverlaySlider("Edge fade distance", ref edgeFadeDistance, 1f, 40f);
        if (showRainbowFadeInOverlay) DrawOverlaySlider("Rainbow trail length", ref rainbowFadeDistance, 8f, 60f);
        if (showRainbowCycleInOverlay) DrawOverlaySlider("Rainbow cycle length", ref rainbowCycleLength, 4f, 40f);
        if (showSimulationRateInOverlay) DrawOverlaySlider("Simulation rate", ref simulationRate, 15f, 60f);
        if (showFlowRecoveryInOverlay) DrawRecoverySlider();

        GUILayout.Space(8f);
        GUILayout.Label("RIGHT-DRAG SPHERE   ·   LEFT-DRAG FLOW", overlaySectionStyle);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void EnsureGuiStyles()
    {
        if (panelTexture == null)
        {
            panelTexture = CreateGuiTexture(new Color(0.035f, 0.025f, 0.055f, 0.96f));
            hoverTexture = CreateGuiTexture(new Color(0.11f, 0.065f, 0.15f, 1f));
            trackTexture = CreateGuiTexture(new Color(0.18f, 0.12f, 0.23f, 1f));
            accentTexture = CreateGuiTexture(new Color(1f, 0.69f, 0.26f, 1f));
        }

        if (overlayButtonStyle != null) return;
        overlayButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(14, 10, 5, 5),
            normal = { background = panelTexture, textColor = new Color(0.95f, 0.82f, 1f) },
            hover = { background = hoverTexture, textColor = Color.white },
            active = { background = hoverTexture, textColor = Color.white }
        };
        restoreButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(5, 5, 3, 3),
            normal = { background = accentTexture, textColor = new Color(0.08f, 0.035f, 0.01f) },
            hover = { background = hoverTexture, textColor = new Color(1f, 0.78f, 0.38f) },
            active = { background = hoverTexture, textColor = Color.white }
        };
        overlaySectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.69f, 0.26f) }
        };
        overlayLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.79f, 0.72f, 0.85f) }
        };
        overlayValueStyle = new GUIStyle(overlayLabelStyle)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.98f, 0.93f, 1f) }
        };
        sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
        {
            fixedHeight = 8f,
            normal = { background = trackTexture },
            hover = { background = trackTexture },
            active = { background = trackTexture }
        };
        sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            fixedWidth = 13f,
            fixedHeight = 13f,
            normal = { background = accentTexture },
            hover = { background = accentTexture },
            active = { background = accentTexture }
        };
        fpsStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 13,
            normal = { textColor = new Color(1f, 0.82f, 0.46f) }
        };
    }

    private static Texture2D CreateGuiTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void DestroyGuiTexture(Texture2D texture)
    {
        if (texture == null) return;
        if (Application.isPlaying) Destroy(texture);
        else DestroyImmediate(texture);
    }

    private void DrawOverlaySlider(string label, ref float value, float minimum, float maximum)
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.Label(label.ToUpperInvariant(), overlayLabelStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label(value.ToString("0.###"), overlayValueStyle, GUILayout.Width(54f));
        GUILayout.EndHorizontal();
        value = GUILayout.HorizontalSlider(value, minimum, maximum, sliderStyle, sliderThumbStyle);
        GUILayout.Space(5f);
        GUILayout.EndVertical();
    }

    private void DrawRecoverySlider()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.Label("FLOW RECOVERY", overlayLabelStyle);
        GUILayout.FlexibleSpace();
        GUILayout.Label(normalFlowRecovery.ToString("0.####"), overlayValueStyle, GUILayout.Width(54f));
        GUILayout.EndHorizontal();
        float minimum = Mathf.Log10(0.0001f);
        float maximum = Mathf.Log10(2f);
        float logarithmicValue = Mathf.Log10(Mathf.Clamp(normalFlowRecovery, 0.0001f, 2f));
        logarithmicValue = GUILayout.HorizontalSlider(logarithmicValue, minimum, maximum, sliderStyle, sliderThumbStyle);
        normalFlowRecovery = Mathf.Pow(10f, logarithmicValue);
        GUILayout.Space(5f);
        GUILayout.EndVertical();
    }

    private void RestoreDefaultValues()
    {
        deepColor = new Color(0.004f, 0.003f, 0.008f);
        purpleColor = new Color(0.16f, 0.055f, 0.21f);
        lineColor = new Color(0.86f, 0.72f, 0.94f);
        redCurrent = new Color(1f, 0f, 0f);
        goldCurrent = new Color(1f, 0.52f, 0f);
        flowVelocity = 1f;
        kinematicViscosity = 0.028f;
        lineFrequency = 4.49f;
        wakeStrength = 2f;
        wakeViolence = 3f;
        lineWidth = 0.045f;
        redLineWidth = 0.16f;
        blackLineWidth = 0.04f;
        edgeFadeDistance = 12f;
        rainbowFadeDistance = 36f;
        rainbowCycleLength = 14f;
        simulationRate = 30f;
        normalFlowRecovery = 0.001f;
        flowDirection = new Vector2(0.22f, -0.41f);
        currentOriginXZ = new Vector2(0.22f, 8f);
        if (targetCamera != null) targetCamera.backgroundColor = deepColor;
    }

    private void HandleFlowDirectionInput()
    {
        if (surfaceRenderer == null || targetCamera == null) return;
        if (IsPointerOverOverlay())
        {
            if (Input.GetMouseButtonUp(0)) settingFlowDirection = false;
            return;
        }

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

    private bool IsPointerOverOverlay()
    {
        Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        float panelWidth = Mathf.Min(340f, Screen.width - 24f);
        if (new Rect(12f, 12f, panelWidth, 38f).Contains(mouse)) return true;
        if (!overlayOpen) return false;
        float panelHeight = Mathf.Min(560f, Mathf.Max(150f, Screen.height - 76f));
        return new Rect(12f, 56f, panelWidth, panelHeight).Contains(mouse);
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
        if (sphereTrail != null)
            sphereTrail.UpdateTrail(surfaceRenderer, rainbowFadeDistance, rainbowCycleLength);
        Bounds bounds = surfaceRenderer.bounds;
        runtimeSurfaceMaterial.SetVector("_FieldBounds",
            new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
        runtimeSurfaceMaterial.SetTexture("_FlowField", simulation.FieldTexture);
    }
}
