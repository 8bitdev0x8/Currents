using UnityEngine;

/// <summary>A 2D incompressible Navier-Stokes solver on a cell-centered grid.</summary>
public sealed class FluidSimulation2D : MonoBehaviour
{
    private const int Resolution = 96;
    private const int PressureIterations = 14;
    private const int DiffusionIterations = 4;
    private const int StreamfunctionIterations = 20;
    private float fixedStep = 1f / 30f;

    private int stride;
    private int arrayLength;
    private float cellSize;
    private float originX;
    private float originZ;
    private float surfaceHeight;
    private float accumulator;
    private float ambientX;
    private float ambientZ;
    private float viscosity;
    private float wakeStrength;
    private float wakeViolence;
    private float contact;
    private float obstacleRadius;
    private float ballX;
    private float ballZ;
    private float ballVelocityX;
    private float ballVelocityZ;
    private float currentOriginX;
    private float currentOriginZ;
    private bool velocityInitialized;
    private bool initialized;

    private float[] u;
    private float[] v;
    private float[] uSource;
    private float[] vSource;
    private float[] pressure;
    private float[] pressureNext;
    private float[] divergence;
    private float[] vorticity;
    private float[] streamfunction;
    private float[] streamfunctionNext;
    private bool[] solid;
    private Color[] pixels;
    private Texture2D fieldTexture;

    public Texture2D FieldTexture => fieldTexture;

    public void Initialize(Renderer surface)
    {
        if (initialized || surface == null) return;

        stride = Resolution + 2;
        arrayLength = stride * stride;
        Bounds bounds = surface.bounds;
        cellSize = Mathf.Max(bounds.size.x, bounds.size.z) / Resolution;
        cellSize = Mathf.Max(cellSize, 0.01f);
        float domainSize = cellSize * Resolution;
        originX = bounds.center.x - domainSize * 0.5f;
        originZ = bounds.center.z - domainSize * 0.5f;
        surfaceHeight = surface.transform.position.y;

        u = new float[arrayLength];
        v = new float[arrayLength];
        uSource = new float[arrayLength];
        vSource = new float[arrayLength];
        pressure = new float[arrayLength];
        pressureNext = new float[arrayLength];
        divergence = new float[arrayLength];
        vorticity = new float[arrayLength];
        streamfunction = new float[arrayLength];
        streamfunctionNext = new float[arrayLength];
        solid = new bool[arrayLength];
        pixels = new Color[Resolution * Resolution];
        fieldTexture = new Texture2D(Resolution, Resolution, TextureFormat.RGBAFloat, false, true)
        {
            name = "Live Fluid Velocity and Streamfunction",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        initialized = true;
        UploadField();
    }

    public void Advance(
        float deltaTime,
        FluidBallInteraction ball,
        Vector2 flowDirection,
        float flowVelocity,
        float kinematicViscosity,
        float currentWakeStrength,
        float currentWakeViolence,
        Vector2 currentOrigin,
        float simulationRate)
    {
        if (!initialized || ball == null) return;

        Vector2 direction = flowDirection.sqrMagnitude > 0.0001f ? flowDirection.normalized : Vector2.right;
        ambientX = direction.x * Mathf.Max(flowVelocity, 0.01f);
        ambientZ = direction.y * Mathf.Max(flowVelocity, 0.01f);
        viscosity = Mathf.Clamp(kinematicViscosity, 0.0001f, 0.5f);
        wakeStrength = Mathf.Max(currentWakeStrength, 0f);
        wakeViolence = Mathf.Max(currentWakeViolence, 0.1f);
        ballX = ball.transform.position.x;
        ballZ = ball.transform.position.z;
        ballVelocityX = ball.Velocity.x;
        ballVelocityZ = ball.Velocity.z;
        currentOriginX = currentOrigin.x;
        currentOriginZ = currentOrigin.y;
        fixedStep = 1f / Mathf.Clamp(simulationRate, 15f, 60f);
        bool firstVelocityFrame = !velocityInitialized;
        if (!velocityInitialized)
        {
            for (int z = 1; z <= Resolution; z++)
            {
                for (int x = 1; x <= Resolution; x++)
                {
                    int index = Index(x, z);
                    u[index] = ambientX;
                    v[index] = ambientZ;
                }
            }
            SetVelocityBoundaries();
            velocityInitialized = true;
        }

        float radius = Mathf.Max(ball.Radius, 0.01f);
        float verticalOffset = ball.transform.position.y - surfaceHeight;
        contact = Mathf.Sqrt(Mathf.Clamp01(1f - (verticalOffset * verticalOffset) / (radius * radius)));
        obstacleRadius = radius * contact;

        accumulator += Mathf.Clamp(deltaTime, 0f, 0.1f);
        int steps = 0;
        while (accumulator >= fixedStep && steps < 3)
        {
            SimulateStep(fixedStep);
            accumulator -= fixedStep;
            steps++;
        }

        if (steps == 3 && accumulator >= fixedStep)
            accumulator = 0f;

        // These are the expensive field reconstruction and GPU upload stages.
        // Skip them on render frames where the 30 Hz fluid step did not advance.
        if (steps > 0 || firstVelocityFrame)
        {
            ComputeStreamfunction();
            UploadField();
        }
    }

    private void SimulateStep(float dt)
    {
        BuildObstacleMask();
        ApplyAmbientRecovery(dt);
        ApplySolidVelocity();

        System.Array.Copy(u, uSource, arrayLength);
        System.Array.Copy(v, vSource, arrayLength);
        float diffusion = viscosity * dt / (cellSize * cellSize);
        Diffuse(u, uSource, diffusion, ambientX);
        Diffuse(v, vSource, diffusion, ambientZ);
        Project();

        System.Array.Copy(u, uSource, arrayLength);
        System.Array.Copy(v, vSource, arrayLength);
        Advect(u, uSource, vSource, dt, ambientX);
        Advect(v, vSource, uSource, dt, ambientZ);
        ApplySolidVelocity();
        AddVorticityConfinement(dt);
        Project();
    }

    private void BuildObstacleMask()
    {
        float radiusSquared = obstacleRadius * obstacleRadius;
        bool hasObstacle = contact > 0.01f && obstacleRadius > cellSize * 0.5f;
        for (int z = 1; z <= Resolution; z++)
        {
            float worldZ = originZ + (z - 0.5f) * cellSize;
            for (int x = 1; x <= Resolution; x++)
            {
                int index = Index(x, z);
                float worldX = originX + (x - 0.5f) * cellSize;
                float dx = worldX - ballX;
                float dz = worldZ - ballZ;
                solid[index] = hasObstacle && dx * dx + dz * dz < radiusSquared;
            }
        }
    }

    private void ApplyAmbientRecovery(float dt)
    {
        float recovery = 1f - Mathf.Exp(-0.18f * dt);
        for (int z = 1; z <= Resolution; z++)
        {
            for (int x = 1; x <= Resolution; x++)
            {
                int index = Index(x, z);
                if (solid[index]) continue;
                u[index] += (ambientX - u[index]) * recovery;
                v[index] += (ambientZ - v[index]) * recovery;
            }
        }
        SetVelocityBoundaries();
    }

    private void Diffuse(float[] field, float[] source, float coefficient, float ambientBoundary)
    {
        if (coefficient <= 0.000001f)
        {
            System.Array.Copy(source, field, arrayLength);
            SetVelocityBoundaries();
            return;
        }

        for (int iteration = 0; iteration < DiffusionIterations; iteration++)
        {
            for (int z = 1; z <= Resolution; z++)
            {
                for (int x = 1; x <= Resolution; x++)
                {
                    int index = Index(x, z);
                    if (solid[index])
                    {
                        field[index] = field == u ? ballVelocityX : ballVelocityZ;
                        continue;
                    }

                    float left = NeighborVelocity(field, x - 1, z, ambientBoundary);
                    float right = NeighborVelocity(field, x + 1, z, ambientBoundary);
                    float down = NeighborVelocity(field, x, z - 1, ambientBoundary);
                    float up = NeighborVelocity(field, x, z + 1, ambientBoundary);
                    field[index] = (source[index] + coefficient * (left + right + down + up)) / (1f + 4f * coefficient);
                }
            }
            SetVelocityBoundaries();
        }
    }

    private void Project()
    {
        float scale = 0.5f / cellSize;
        for (int z = 1; z <= Resolution; z++)
        {
            for (int x = 1; x <= Resolution; x++)
            {
                int index = Index(x, z);
                pressure[index] = 0f;
                if (solid[index])
                {
                    divergence[index] = 0f;
                    continue;
                }

                float left = NeighborVelocity(u, x - 1, z, ambientX);
                float right = NeighborVelocity(u, x + 1, z, ambientX);
                float down = NeighborVelocity(v, x, z - 1, ambientZ);
                float up = NeighborVelocity(v, x, z + 1, ambientZ);
                divergence[index] = (right - left + up - down) * scale;
            }
        }

        for (int iteration = 0; iteration < PressureIterations; iteration++)
        {
            for (int z = 1; z <= Resolution; z++)
            {
                for (int x = 1; x <= Resolution; x++)
                {
                    int index = Index(x, z);
                    if (solid[index])
                    {
                        pressureNext[index] = 0f;
                        continue;
                    }

                    float center = pressure[index];
                    float left = NeighborPressure(x - 1, z, center);
                    float right = NeighborPressure(x + 1, z, center);
                    float down = NeighborPressure(x, z - 1, center);
                    float up = NeighborPressure(x, z + 1, center);
                    pressureNext[index] = (left + right + down + up - divergence[index] * cellSize * cellSize) * 0.25f;
                }
            }
            Swap(ref pressure, ref pressureNext);
        }

        for (int z = 1; z <= Resolution; z++)
        {
            for (int x = 1; x <= Resolution; x++)
            {
                int index = Index(x, z);
                if (solid[index])
                {
                    u[index] = ballVelocityX;
                    v[index] = ballVelocityZ;
                    continue;
                }

                float center = pressure[index];
                u[index] -= scale * (NeighborPressure(x + 1, z, center) - NeighborPressure(x - 1, z, center));
                v[index] -= scale * (NeighborPressure(x, z + 1, center) - NeighborPressure(x, z - 1, center));
            }
        }
        SetVelocityBoundaries();
    }

    private void Advect(float[] field, float[] sourceU, float[] sourceV, float dt, float ambientBoundary)
    {
        for (int z = 1; z <= Resolution; z++)
        {
            for (int x = 1; x <= Resolution; x++)
            {
                int index = Index(x, z);
                if (solid[index])
                {
                    field[index] = field == u ? ballVelocityX : ballVelocityZ;
                    continue;
                }

                float backX = Mathf.Clamp(x - dt * sourceU[index] / cellSize, 0.5f, Resolution + 0.5f);
                float backZ = Mathf.Clamp(z - dt * sourceV[index] / cellSize, 0.5f, Resolution + 0.5f);
                field[index] = Sample(sourceU, backX, backZ);
            }
        }
        SetVelocityBoundaries();
    }

    private void AddVorticityConfinement(float dt)
    {
        ComputeVorticity();
        float strength = 0.025f * wakeStrength * wakeViolence;
        for (int z = 2; z < Resolution; z++)
        {
            for (int x = 2; x < Resolution; x++)
            {
                int index = Index(x, z);
                if (solid[index]) continue;
                float magnitudeLeft = Mathf.Abs(vorticity[Index(x - 1, z)]);
                float magnitudeRight = Mathf.Abs(vorticity[Index(x + 1, z)]);
                float magnitudeDown = Mathf.Abs(vorticity[Index(x, z - 1)]);
                float magnitudeUp = Mathf.Abs(vorticity[Index(x, z + 1)]);
                float gradientX = (magnitudeRight - magnitudeLeft) * 0.5f / cellSize;
                float gradientZ = (magnitudeUp - magnitudeDown) * 0.5f / cellSize;
                float magnitude = Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ) + 0.0001f;
                float normalX = gradientX / magnitude;
                float normalZ = gradientZ / magnitude;
                float curl = vorticity[index];
                u[index] += -normalZ * curl * strength * dt;
                v[index] += normalX * curl * strength * dt;
            }
        }
    }

    private void ComputeVorticity()
    {
        float scale = 0.5f / cellSize;
        for (int z = 1; z <= Resolution; z++)
        {
            for (int x = 1; x <= Resolution; x++)
            {
                int index = Index(x, z);
                float vRight = NeighborVelocity(v, x + 1, z, ambientZ);
                float vLeft = NeighborVelocity(v, x - 1, z, ambientZ);
                float uUp = NeighborVelocity(u, x, z + 1, ambientX);
                float uDown = NeighborVelocity(u, x, z - 1, ambientX);
                vorticity[index] = (vRight - vLeft - uUp + uDown) * scale;
            }
        }
    }

    private void ComputeStreamfunction()
    {
        ComputeVorticity();
        for (int z = 0; z <= Resolution + 1; z++)
        {
            float worldZ = originZ + (z - 0.5f) * cellSize;
            for (int x = 0; x <= Resolution + 1; x++)
            {
                float worldX = originX + (x - 0.5f) * cellSize;
                // Fix the streamfunction gauge at the accent origin so its value is meaningful
                // to the shader when it selects the red streamline around the obstacle.
                float basePsi = ambientX * (worldZ - currentOriginZ) - ambientZ * (worldX - currentOriginX);
                streamfunction[Index(x, z)] = basePsi;
                streamfunctionNext[Index(x, z)] = basePsi;
            }
        }

        float spherePsi = ambientX * (ballZ - currentOriginZ) - ambientZ * (ballX - currentOriginX);
        for (int iteration = 0; iteration < StreamfunctionIterations; iteration++)
        {
            for (int z = 1; z <= Resolution; z++)
            {
                for (int x = 1; x <= Resolution; x++)
                {
                    int index = Index(x, z);
                    if (solid[index])
                    {
                        streamfunctionNext[index] = spherePsi;
                        continue;
                    }
                    float sum = streamfunction[Index(x - 1, z)] + streamfunction[Index(x + 1, z)]
                              + streamfunction[Index(x, z - 1)] + streamfunction[Index(x, z + 1)];
                    streamfunctionNext[index] = 0.25f * (sum + vorticity[index] * cellSize * cellSize);
                }
            }
            Swap(ref streamfunction, ref streamfunctionNext);
        }
    }

    private void UploadField()
    {
        if (fieldTexture == null) return;
        float ambientMagnitude = Mathf.Sqrt(ambientX * ambientX + ambientZ * ambientZ);
        float speedScale = Mathf.Max(ambientMagnitude * 2f, 0.1f);
        float curlScale = Mathf.Max(ambientMagnitude * 4f, 0.1f);
        for (int z = 1; z <= Resolution; z++)
        {
            for (int x = 1; x <= Resolution; x++)
            {
                int index = Index(x, z);
                float speed = Mathf.Sqrt(u[index] * u[index] + v[index] * v[index]);
                float normalizedSpeed = Mathf.Clamp01(speed / speedScale);
                float normalizedVorticity = Mathf.Clamp(vorticity[index] / curlScale, -1f, 1f);
                float obstacle = solid[index] ? contact : 0f;
                pixels[(z - 1) * Resolution + x - 1] = new Color(streamfunction[index], normalizedSpeed, normalizedVorticity, obstacle);
            }
        }
        fieldTexture.SetPixels(pixels);
        fieldTexture.Apply(false, false);
    }

    private float NeighborVelocity(float[] field, int x, int z, float ambientBoundary)
    {
        x = Mathf.Clamp(x, 0, Resolution + 1);
        z = Mathf.Clamp(z, 0, Resolution + 1);
        int index = Index(x, z);
        if (x == 0 || x == Resolution + 1 || z == 0 || z == Resolution + 1)
            return ambientBoundary;
        if (solid[index]) return field == u ? ballVelocityX : ballVelocityZ;
        return field[index];
    }

    private float NeighborPressure(int x, int z, float center)
    {
        x = Mathf.Clamp(x, 0, Resolution + 1);
        z = Mathf.Clamp(z, 0, Resolution + 1);
        int index = Index(x, z);
        if (x == 0 || x == Resolution + 1 || z == 0 || z == Resolution + 1 || solid[index])
            return center;
        return pressure[index];
    }

    private float Sample(float[] field, float x, float z)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, Resolution + 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt(z), 0, Resolution + 1);
        int x1 = Mathf.Min(x0 + 1, Resolution + 1);
        int z1 = Mathf.Min(z0 + 1, Resolution + 1);
        float tx = x - x0;
        float tz = z - z0;
        float lower = Mathf.Lerp(field[Index(x0, z0)], field[Index(x1, z0)], tx);
        float upper = Mathf.Lerp(field[Index(x0, z1)], field[Index(x1, z1)], tx);
        return Mathf.Lerp(lower, upper, tz);
    }

    private void SetVelocityBoundaries()
    {
        for (int i = 0; i <= Resolution + 1; i++)
        {
            u[Index(0, i)] = ambientX;
            u[Index(Resolution + 1, i)] = ambientX;
            u[Index(i, 0)] = ambientX;
            u[Index(i, Resolution + 1)] = ambientX;
            v[Index(0, i)] = ambientZ;
            v[Index(Resolution + 1, i)] = ambientZ;
            v[Index(i, 0)] = ambientZ;
            v[Index(i, Resolution + 1)] = ambientZ;
        }
    }

    private void ApplySolidVelocity()
    {
        for (int z = 1; z <= Resolution; z++)
        {
            for (int x = 1; x <= Resolution; x++)
            {
                int index = Index(x, z);
                if (!solid[index]) continue;
                u[index] = ballVelocityX;
                v[index] = ballVelocityZ;
            }
        }
    }

    private int Index(int x, int z) => x + stride * z;

    private static void Swap(ref float[] first, ref float[] second)
    {
        float[] temporary = first;
        first = second;
        second = temporary;
    }

    private void OnDestroy()
    {
        if (fieldTexture != null)
        {
            if (Application.isPlaying) Destroy(fieldTexture);
            else DestroyImmediate(fieldTexture);
        }
    }
}
