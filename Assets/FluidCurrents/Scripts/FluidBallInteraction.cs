using UnityEngine;

/// <summary>Drags the sphere across the fluid plane; the surface shader reads its live world position.</summary>
[RequireComponent(typeof(SphereCollider))]
public class FluidBallInteraction : MonoBehaviour
{
    private Camera sceneCamera;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private bool dragging;

    public float Radius => transform.lossyScale.x * 0.5f;

    private void Awake()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null) sceneCamera = FindObjectOfType<Camera>();
    }

    private void OnMouseDown()
    {
        if (sceneCamera == null) return;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, Radius, 0f));
        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            grabOffset = transform.position - ray.GetPoint(distance);
            dragging = true;
        }
    }

    private void OnMouseUp()
    {
        dragging = false;
    }

    private void Update()
    {
        if (!dragging || sceneCamera == null) return;
        if (!Input.GetMouseButton(0))
        {
            dragging = false;
            return;
        }

        Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
        if (dragPlane.Raycast(ray, out float distance))
        {
            Vector3 hit = ray.GetPoint(distance) + grabOffset;
            transform.position = new Vector3(hit.x, Radius, hit.z);
        }
    }
}
