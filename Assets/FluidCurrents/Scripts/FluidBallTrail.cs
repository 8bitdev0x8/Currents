using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws a fading rainbow ribbon along the sphere's recent path over the fluid plane.</summary>
[RequireComponent(typeof(FluidBallInteraction))]
public sealed class FluidBallTrail : MonoBehaviour
{
    private const float SampleSpacing = 0.08f;
    private const float TrailLifetime = 8f;
    private const int MaximumSamples = 512;
    private const int GradientKeys = 8;

    private struct TrailSample
    {
        public Vector3 position;
        public float time;

        public TrailSample(Vector3 position, float time)
        {
            this.position = position;
            this.time = time;
        }
    }

    private readonly List<TrailSample> samples = new List<TrailSample>(MaximumSamples);
    private readonly GradientColorKey[] colorKeys = new GradientColorKey[GradientKeys];
    private readonly GradientAlphaKey[] alphaKeys = new GradientAlphaKey[GradientKeys];
    private readonly Gradient gradient = new Gradient();

    private FluidBallInteraction ball;
    private Renderer surfaceRenderer;
    private GameObject trailObject;
    private LineRenderer line;
    private Material trailMaterial;
    private Vector3[] positions = new Vector3[MaximumSamples];
    private float maxTrailDistance = 36f;
    private float rainbowCycleLength = 14f;
    private bool separatedFromPlane;

    public void Initialize(Renderer surface)
    {
        if (surface != null) surfaceRenderer = surface;
        if (ball == null) ball = GetComponent<FluidBallInteraction>();
        if (line != null || surfaceRenderer == null) return;

        trailObject = new GameObject("Sphere Rainbow Trail") { hideFlags = HideFlags.DontSave };
        line = trailObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.startWidth = 0.22f;
        line.endWidth = 0.08f;
        line.widthMultiplier = 1f;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.65f), new Keyframe(0.18f, 1f), new Keyframe(1f, 0.42f));
        line.colorGradient = gradient;
        line.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            trailMaterial = new Material(shader) { name = "Sphere Rainbow Trail Material", hideFlags = HideFlags.DontSave };
            line.sharedMaterial = trailMaterial;
        }
    }

    public void UpdateTrail(Renderer surface, float fadeDistance, float cycleLength)
    {
        Initialize(surface);
        if (line == null || ball == null || surfaceRenderer == null) return;

        maxTrailDistance = Mathf.Max(fadeDistance, 0.1f);
        rainbowCycleLength = Mathf.Max(cycleLength, 0.1f);
        float now = Time.time;
        float planeY = surfaceRenderer.bounds.max.y + 0.035f;
        Vector3 spherePosition = ball.transform.position;
        bool touchingPlane = spherePosition.y - surfaceRenderer.bounds.max.y <= ball.Radius;

        if (!touchingPlane)
        {
            separatedFromPlane = true;
            TrimTrail(now);
            RenderTrail(now);
            return;
        }

        if (separatedFromPlane)
        {
            samples.Clear();
            separatedFromPlane = false;
        }

        Vector3 position = new Vector3(spherePosition.x, planeY, spherePosition.z);
        if (samples.Count == 0)
        {
            samples.Add(new TrailSample(position, now));
        }
        else
        {
            Vector3 lastPosition = samples[samples.Count - 1].position;
            float distance = Vector3.Distance(lastPosition, position);
            if (distance >= SampleSpacing)
            {
                int divisions = Mathf.CeilToInt(distance / SampleSpacing);
                for (int i = 1; i <= divisions; i++)
                {
                    float t = i / (float)divisions;
                    Vector3 samplePosition = Vector3.Lerp(lastPosition, position, t);
                    samples.Add(new TrailSample(samplePosition, now));
                }
            }
            else if (distance > 0.002f)
            {
                TrailSample last = samples[samples.Count - 1];
                last.position = position;
                last.time = now;
                samples[samples.Count - 1] = last;
            }
        }

        TrimTrail(now);
        RenderTrail(now);
    }

    private void TrimTrail(float now)
    {
        while (samples.Count > 0 && now - samples[0].time > TrailLifetime)
            samples.RemoveAt(0);

        float length = 0f;
        for (int i = samples.Count - 1; i > 0; i--)
        {
            length += Vector3.Distance(samples[i].position, samples[i - 1].position);
            if (length <= maxTrailDistance) continue;
            samples.RemoveRange(0, i);
            break;
        }

        while (samples.Count > MaximumSamples)
            samples.RemoveAt(0);
    }

    private void RenderTrail(float now)
    {
        if (samples.Count < 2)
        {
            line.positionCount = 0;
            line.enabled = false;
            return;
        }

        float length = 0f;
        for (int i = 1; i < samples.Count; i++)
            length += Vector3.Distance(samples[i - 1].position, samples[i].position);

        for (int i = 0; i < samples.Count; i++)
            positions[i] = samples[i].position;
        line.positionCount = samples.Count;
        line.SetPositions(positions);
        UpdateGradient(now, length);
        line.colorGradient = gradient;
        line.enabled = true;
    }

    private void UpdateGradient(float now, float totalLength)
    {
        float walked = 0f;
        for (int key = 0; key < GradientKeys; key++)
        {
            float normalized = key / (float)(GradientKeys - 1);
            float targetDistance = totalLength * normalized;
            int sampleIndex = 0;
            walked = 0f;
            while (sampleIndex < samples.Count - 1 && walked < targetDistance)
            {
                walked += Vector3.Distance(samples[sampleIndex].position, samples[sampleIndex + 1].position);
                sampleIndex++;
            }

            TrailSample sample = samples[sampleIndex];
            float hue = Mathf.Repeat(walked / rainbowCycleLength, 1f);
            colorKeys[key] = new GradientColorKey(Color.HSVToRGB(hue, 0.88f, 1f), normalized);
            float ageAlpha = Mathf.Clamp01(1f - (now - sample.time) / TrailLifetime);
            alphaKeys[key] = new GradientAlphaKey(ageAlpha * 0.9f, normalized);
        }
        gradient.SetKeys(colorKeys, alphaKeys);
    }

    private void OnDestroy()
    {
        if (trailObject != null)
        {
            if (Application.isPlaying) Destroy(trailObject);
            else DestroyImmediate(trailObject);
        }
        if (trailMaterial != null)
        {
            if (Application.isPlaying) Destroy(trailMaterial);
            else DestroyImmediate(trailMaterial);
        }
    }
}
