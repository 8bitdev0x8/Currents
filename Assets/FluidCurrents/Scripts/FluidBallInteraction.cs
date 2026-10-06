using UnityEngine;

/// <summary>Right-drag the sphere across the surface and use the wheel to raise or lower it.</summary>
[RequireComponent(typeof(SphereCollider))]
public class FluidBallInteraction : MonoBehaviour
{
    private Camera sceneCamera;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private bool dragging;

    [Min(0.1f)] public float verticalScrollSpeed = 3f;

    public float Radius => transform.lossyScale.x * 0.5f;

    private void Awake()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null) sceneCamera = FindObjectOfType<Camera>();
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
                if (dragPlane.Raycast(pickRay, out float distance))
                {
                    grabOffset = transform.position - pickRay.GetPoint(distance);
                    dragging = true;
                }
            }
        }

        if (Input.GetMouseButtonUp(1)) dragging = false;
        if (!dragging || !Input.GetMouseButton(1)) return;

        float height = Mathf.Max(Radius, transform.position.y + Input.mouseScrollDelta.y * verticalScrollSpeed);
        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            Vector3 hit = ray.GetPoint(distance) + grabOffset;
            transform.position = new Vector3(hit.x, height, hit.z);
        }
    }
}
