using UnityEngine;

/// <summary>Creates a camera-filling procedural current effect at runtime.</summary>
[RequireComponent(typeof(Camera))]
public class FluidCurrentDemo : MonoBehaviour
{
    [Header("Look")]
    public Color deepColor = new Color(0.012f, 0.008f, 0.025f);
    public Color middleColor = new Color(0.19f, 0.07f, 0.28f);
    public Color highlightColor = new Color(0.78f, 0.58f, 0.91f);
    public Color redCurrent = new Color(0.95f, 0.035f, 0.12f);
    public Color goldCurrent = new Color(1f, 0.58f, 0.06f);
    [Range(1f, 12f)] public float flowScale = 3f;
    [Range(0f, 2f)] public float speed = 0.14f;
    [Range(0f, 2f)] public float swirl = 0.85f;
    [Range(2f, 30f)] public float contourBands = 19f;
    [Range(0f, 2f)] public float ballInfluence = 1f;

    private Camera targetCamera;
    private GameObject surface;
    private Material materialInstance;

    private void OnEnable()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = Color.black;
        CreateSurface();
        ApplySettings();
    }

    private void OnDisable()
    {
        if (surface != null)
        {
            if (Application.isPlaying) Destroy(surface);
            else DestroyImmediate(surface);
        }
        if (materialInstance != null)
        {
            if (Application.isPlaying) Destroy(materialInstance);
            else DestroyImmediate(materialInstance);
        }
    }

    private void Update()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (surface == null) CreateSurface();
        FitSurface();
        ApplySettings();
    }

    private void CreateSurface()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        Shader shader = Shader.Find("FluidCurrents/Procedural Currents");
        if (shader == null)
        {
            Debug.LogError("Fluid Currents shader not found. Ensure FluidCurrents.shader is included in the project.", this);
            return;
        }

        surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        surface.name = "Procedural Fluid Currents";
        surface.transform.SetParent(transform, false);
        Collider collider = surface.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
        materialInstance = new Material(shader) { name = "Fluid Currents (Runtime)" };
        surface.GetComponent<Renderer>().sharedMaterial = materialInstance;
        FitSurface();
    }

    private void FitSurface()
    {
        if (surface == null || targetCamera == null || targetCamera.orthographic) return;
        const float distance = 50f;
        float height = 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        surface.transform.localPosition = new Vector3(0f, 0f, distance);
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = new Vector3(height * targetCamera.aspect, height, 1f);
    }

    private void ApplySettings()
    {
        if (materialInstance == null) return;
        materialInstance.SetColor("_ColorA", deepColor);
        materialInstance.SetColor("_ColorB", middleColor);
        materialInstance.SetColor("_ColorC", highlightColor);
        materialInstance.SetColor("_AccentRed", redCurrent);
        materialInstance.SetColor("_AccentGold", goldCurrent);
        materialInstance.SetFloat("_Scale", flowScale);
        materialInstance.SetFloat("_Speed", speed);
        materialInstance.SetFloat("_Swirl", swirl);
        materialInstance.SetFloat("_BandCount", contourBands);
        materialInstance.SetFloat("_BallStrength", ballInfluence);
    }
}
