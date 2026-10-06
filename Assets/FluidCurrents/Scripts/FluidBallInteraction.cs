using UnityEngine;

/// <summary>Left-drag the sphere across the surface and use the wheel to raise or lower it.</summary>
[RequireComponent(typeof(SphereCollider))]
public class FluidBallInteraction : MonoBehaviour
{
    private Camera sceneCamera;
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
        targetHeight = transform.position.y;
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (sceneCamera != null)
        {
            UpdateDragging();
        }

        float height = Mathf.SmoothDamp(transform.position.y, targetHeight, ref heightSmoothVelocity, heightSmoothTime);
        transform.position = new Vector3(transform.position.x, height, transform.position.z);

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 measuredVelocity = Vector3.ClampMagnitude((transform.position - lastPosition) / deltaTime, 8f);
        velocity = Vector3.Lerp(velocity, measuredVelocity, 1f - Mathf.Exp(-deltaTime * 10f));
        movementIntensity = Mathf.MoveTowards(movementIntensity, Mathf.Clamp01(velocity.magnitude / 4f), deltaTime * 3f);
        lastPosition = transform.position;
    }

    private void UpdateDragging()
    {

        if (Input.GetMouseButtonDown(0))
        {
            Ray pickRay = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(pickRay, out RaycastHit pick) && pick.collider.gameObject == gameObject)
            {
                dragPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
                if (dragPlane.Raycast(pickRay, out float pickDistance))
                {
                    grabOffset = transform.position - pickRay.GetPoint(pickDistance);
                    dragging = true;
                }
            }
        }

        if (Input.GetMouseButtonUp(0)) dragging = false;
        if (dragging && Input.GetMouseButton(0))
        {
            targetHeight += Input.mouseScrollDelta.y * verticalScrollSpeed;
            Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float distance))
            {
                Vector3 hit = ray.GetPoint(distance) + grabOffset;
                transform.position = new Vector3(hit.x, transform.position.y, hit.z);
            }
        }

    }
}
