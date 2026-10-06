using UnityEngine;

/// <summary>Right-drag the sphere across the surface and use the wheel to raise or lower it.</summary>
[RequireComponent(typeof(SphereCollider))]
public class FluidBallInteraction : MonoBehaviour
{
    private Camera sceneCamera;
    private Renderer surfaceRenderer;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private bool dragging;
    private float targetHeight;
    private float heightSmoothVelocity;
    private Vector3 lastPosition;
    private Vector3 velocity;
    private float movementIntensity;

    [Min(0.1f)] public float verticalScrollSpeed = 3f;
    [Min(0.01f)] public float heightSmoothTime = 0.12f;

    public float Radius => transform.lossyScale.x * 0.5f;
    public float MovementIntensity => movementIntensity;
    public Vector3 Velocity => velocity;

    private void Awake()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null) sceneCamera = FindObjectOfType<Camera>();
        GameObject surface = GameObject.Find("Fluid Surface");
        if (surface != null) surfaceRenderer = surface.GetComponent<Renderer>();
        targetHeight = PlaneOffset(transform.position);
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (sceneCamera != null)
        {
            UpdateDragging();
        }

        float height = Mathf.SmoothDamp(PlaneOffset(transform.position), targetHeight, ref heightSmoothVelocity, heightSmoothTime);
        transform.position = ConstrainToSurface(transform.position) + PlaneNormal() * height;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 measuredVelocity = Vector3.ClampMagnitude((transform.position - lastPosition) / deltaTime, 8f);
        velocity = Vector3.Lerp(velocity, measuredVelocity, 1f - Mathf.Exp(-deltaTime * 10f));
        movementIntensity = Mathf.MoveTowards(movementIntensity, Mathf.Clamp01(velocity.magnitude / 4f), deltaTime * 3f);
        lastPosition = transform.position;
    }

    private Vector3 ConstrainToSurface(Vector3 position)
    {
        if (surfaceRenderer == null)
        {
            GameObject surface = GameObject.Find("Fluid Surface");
            if (surface != null) surfaceRenderer = surface.GetComponent<Renderer>();
        }
        if (surfaceRenderer == null) return position;

        Transform surface = surfaceRenderer.transform;
        MeshFilter meshFilter = surfaceRenderer.GetComponent<MeshFilter>();
        Bounds bounds = meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.bounds
            : new Bounds(Vector3.zero, new Vector3(10f, 0.1f, 10f));
        Vector3 scale = surface.lossyScale;
        Vector3 local = surface.InverseTransformPoint(position);
        float localRadiusX = Radius / Mathf.Max(Mathf.Abs(scale.x), 0.0001f);
        float localRadiusZ = Radius / Mathf.Max(Mathf.Abs(scale.z), 0.0001f);
        float minX = bounds.min.x + localRadiusX;
        float maxX = bounds.max.x - localRadiusX;
        float minZ = bounds.min.z + localRadiusZ;
        float maxZ = bounds.max.z - localRadiusZ;
        local.x = minX <= maxX ? Mathf.Clamp(local.x, minX, maxX) : bounds.center.x;
        local.z = minZ <= maxZ ? Mathf.Clamp(local.z, minZ, maxZ) : bounds.center.z;
        local.y = 0f;
        return surface.TransformPoint(local);
    }

    private void UpdateDragging()
    {

        if (Input.GetMouseButtonDown(1))
        {
            Ray pickRay = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(pickRay, out RaycastHit pick) && pick.collider.gameObject == gameObject)
            {
                dragPlane = new Plane(PlaneNormal(), transform.position);
                if (dragPlane.Raycast(pickRay, out float pickDistance))
                {
                    grabOffset = transform.position - pickRay.GetPoint(pickDistance);
                    dragging = true;
                }
            }
        }

        if (Input.GetMouseButtonUp(1)) dragging = false;
        if (dragging && Input.GetMouseButton(1))
        {
            targetHeight += Input.mouseScrollDelta.y * verticalScrollSpeed;
            Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float distance))
            {
                Vector3 hit = ray.GetPoint(distance) + grabOffset;
                transform.position = ConstrainToSurface(hit) + PlaneNormal() * PlaneOffset(transform.position);
            }
        }
    }

    private Vector3 PlaneNormal()
    {
        return surfaceRenderer != null ? surfaceRenderer.transform.up : Vector3.up;
    }

    private float PlaneOffset(Vector3 position)
    {
        if (surfaceRenderer == null) return position.y;
        return Vector3.Dot(position - surfaceRenderer.transform.position, surfaceRenderer.transform.up);
    }
}
