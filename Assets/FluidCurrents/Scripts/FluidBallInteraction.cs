using UnityEngine;

/// <summary>Lets the silver sphere follow mouse/touch-like pointer dragging and warp the current field.</summary>
[RequireComponent(typeof(SphereCollider))]
public class FluidBallInteraction : MonoBehaviour
{
    private Camera sceneCamera;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private bool dragging;
    private float dragDepth;

    private void Awake()
    {
        sceneCamera = Camera.main;
        if (sceneCamera == null) sceneCamera = FindObjectOfType<Camera>();
    }

    private void OnMouseDown()
    {
        if (sceneCamera == null) return;
        dragDepth = Vector3.Dot(transform.position - sceneCamera.transform.position, sceneCamera.transform.forward);
        dragPlane = new Plane(sceneCamera.transform.forward, transform.position);
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
        if (sceneCamera == null) return;

        if (dragging && Input.GetMouseButton(0))
        {
            Ray ray = sceneCamera.ScreenPointToRay(Input.mousePosition);
            if (dragPlane.Raycast(ray, out float distance))
            {
                Vector3 position = ray.GetPoint(distance) + grabOffset;
                Vector3 viewport = sceneCamera.WorldToViewportPoint(position);
                float radius = Mathf.Clamp01(transform.lossyScale.x * 0.5f / (2f * dragDepth * Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)));
                viewport.x = Mathf.Clamp(viewport.x, radius, 1f - radius);
                viewport.y = Mathf.Clamp(viewport.y, radius, 1f - radius);
                transform.position = sceneCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, dragDepth));
            }
        }

        Vector3 ballViewport = sceneCamera.WorldToViewportPoint(transform.position);
        float visibleHeight = 2f * ballViewport.z * Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float normalizedRadius = visibleHeight > 0f ? transform.lossyScale.x * 0.5f / visibleHeight : 0f;
        Shader.SetGlobalVector("_BallUV", new Vector4(ballViewport.x, ballViewport.y, ballViewport.z, 0f));
        Shader.SetGlobalFloat("_BallRadius", normalizedRadius);
    }
}
