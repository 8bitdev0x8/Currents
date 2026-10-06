using UnityEngine;

/// <summary>Right-drag the sphere across the surface and use the wheel to raise or lower it.</summary>
[RequireComponent(typeof(SphereCollider))]
public class FluidBallInteraction : MonoBehaviour
{
    private Camera sceneCamera;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private bool dragging;
    private float targetHeight;
    private float heightSmoothVelocity;

    [Min(0.1f)] public float verticalScrollSpeed = 3f;
    [Min(0.01f)] public float heightSmoothTime = 0.12f;

    public float Radius => transform.lossyScale.x * 0.5f;

    private void Awake()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null) sceneCamera = FindObjectOfType<Camera>();
        targetHeight = transform.position.y;
    }

    private void Update()
    {
        if (sceneCamera == null) return;

        if (Input.GetMouseButtonDown(1))
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

        if (Input.GetMouseButtonUp(1)) dragging = false;
        if (dragging && Input.GetMouseButton(1))
        {
            targetHeight += Input.mouseScrollDelta.y * verticalScrollSpeed;
            Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float distance))
            {
                Vector3 hit = ray.GetPoint(distance) + grabOffset;
                transform.position = new Vector3(hit.x, transform.position.y, hit.z);
            }
        }

        float height = Mathf.SmoothDamp(transform.position.y, targetHeight, ref heightSmoothVelocity, heightSmoothTime);
        transform.position = new Vector3(transform.position.x, height, transform.position.z);
    }
}
