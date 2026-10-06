Shader "FluidCurrents/World Surface"
{
    Properties
    {
        _Deep ("Deep near-black", Color) = (0.004, 0.003, 0.008, 1)
        _Purple ("Violet field", Color) = (0.16, 0.055, 0.21, 1)
        _Line ("Silver-lavender streamlines", Color) = (0.86, 0.72, 0.94, 1)
        _RedCurrent ("Vermilion current", Color) = (1, 0, 0, 1)
        _GoldCurrent ("Amber current", Color) = (1, 0.52, 0, 1)
        _FlowDirection ("Flow direction", Vector) = (0.22, -0.41, 0, 0)
        _CurrentOrigin ("Fixed current origin", Vector) = (0.22, 8, 0, 0)
        _FlowVelocity ("Flow velocity", Float) = 1
        _Strouhal ("Strouhal number", Float) = 0.245
        _Viscosity ("Kinematic viscosity", Float) = 0.028
        _LineFrequency ("Line frequency", Float) = 4.49
        _LineWidth ("Streamline width", Range(0.005, 0.2)) = 0.045
        _RedLineWidth ("Red current width", Range(0.02, 0.4)) = 0.16
        _BlackLineWidth ("Black outline width", Range(0, 0.2)) = 0.04
        _WakeStrength ("Wake strength", Float) = 2
        _WakeViolence ("Wake violence", Range(0.5, 5)) = 3
        _BallPosition ("Sphere position", Vector) = (0, 1.5, 8, 0)
        _BallRadius ("Sphere radius", Float) = 1.5
        _ElapsedTime ("Elapsed time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Opaque" }
        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Deep;
            fixed4 _Purple;
            fixed4 _Line;
            fixed4 _RedCurrent;
            fixed4 _GoldCurrent;
            float4 _FlowDirection;
            float4 _CurrentOrigin;
            float _FlowVelocity;
            float _Strouhal;
            float _Viscosity;
            float _LineFrequency;
            float _LineWidth;
            float _RedLineWidth;
            float _BlackLineWidth;
            float _WakeStrength;
            float _WakeViolence;
            float4 _BallPosition;
            float _BallRadius;
            float _ElapsedTime;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 worldPos : TEXCOORD0; float eyeDepth : TEXCOORD1; };

            v2f vert(appdata input)
            {
                v2f output;
                float4 world = mul(unity_ObjectToWorld, input.vertex);
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPos = world.xyz;
                output.eyeDepth = -UnityObjectToViewPos(input.vertex).z;
                return output;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + 1), f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0;
                float amplitude = 0.5;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p);
                    p = float2(p.x * 1.55 - p.y * 1.18, p.x * 1.18 + p.y * 1.55);
                    amplitude *= 0.5;
                }
                return value;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 world = input.worldPos.xz;
                float2 center = _BallPosition.xz;
                float2 flow = normalize(_FlowDirection.xy);
                float2 side = float2(-flow.y, flow.x);
                float2 relative = world - center;
                float along = dot(relative, flow);
                float across = dot(relative, side);
                float radius = max(_BallRadius, 0.01);
                float speed = max(_FlowVelocity, 0.01);
                float2 fieldRelative = world - _CurrentOrigin.xy;
                float fixedAlong = dot(fieldRelative, flow);
                float fixedAcross = dot(fieldRelative, side);

                // Keep the background streamlines fixed in world space and localize sphere influence.
                float r2 = max(along * along + across * across, radius * radius * 0.72);
                float potentialPsi = speed * across * (1.0 - radius * radius / r2);
                float localFalloff = exp(-dot(relative, relative) / (radius * radius * 18.0));
                float psi = speed * fixedAcross + (potentialPsi - speed * across) * localFalloff;

                // Alternating Gaussian-core vortices, shed at f = St * U / D.
                float halfPeriod = radius / max(_Strouhal * speed, 0.01);
                float phase = frac(_ElapsedTime / halfPeriod);
                float cycle = floor(_ElapsedTime / halfPeriod);
                float spacing = 0.86 * speed * halfPeriod;
                float circulation = 2.2 * speed * (2.0 * radius) * _WakeViolence;
                float wake = 0;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float age = (i + phase) * halfPeriod;
                    float shedDistance = radius * 1.08 + age * 0.86 * speed;
                    float parity = frac((i + cycle) * 0.5) * 2.0;
                    float vortexSign = parity < 1.0 ? 1.0 : -1.0;
                    float lateral = vortexSign * radius * (0.52 + 0.04 * _WakeViolence * sin(age * speed / radius));
                    float2 vortex = float2(shedDistance, lateral);
                    float2 d = float2(along, across) - vortex;
                    float core2 = radius * radius * (0.045 + 0.008 * _WakeViolence) + 4.0 * _Viscosity * age;
                    float attenuation = exp(-age / max(halfPeriod * 7.0, 0.01));
                    wake += vortexSign * circulation * 0.0796 * log(1.0 + dot(d, d) / max(core2, 0.0001)) * attenuation;
                }
                float wakeSpread = sqrt(max(_WakeViolence, 0.01));
                float wakeWidth = radius * (1.0 + max(along, 0.0) * 0.12 * wakeSpread);
                float wakeEnvelope = exp(-pow(max(-along, 0.0) / radius, 2.0))
                                   * exp(-pow(max(along, 0.0) / (radius * 20.0 * wakeSpread), 2.0))
                                   * exp(-pow(across / max(wakeWidth, 0.01), 2.0));
                psi += wake * _WakeStrength * wakeEnvelope;

                float fieldNoise = fbm(world * 0.54);
                psi += (fieldNoise - 0.5) * speed * radius * 0.045;
                float contourPhase = psi * _LineFrequency;
                float contourDistance = abs(asin(sin(contourPhase))) / max(_LineFrequency * speed, 0.001);
                float contourEdge = max(fwidth(contourDistance), 0.0005);
                float contour = 1.0 - smoothstep(_LineWidth, _LineWidth + contourEdge, contourDistance);
                float fill = smoothstep(0.14, 0.88, fieldNoise);
                float3 color = lerp(_Deep.rgb, _Purple.rgb, 0.25 + fill * 0.55);
                float contourShadow = 1.0 - smoothstep(_LineWidth + _BlackLineWidth, _LineWidth + _BlackLineWidth + contourEdge, contourDistance);
                color = lerp(color, float3(0.0, 0.0, 0.0), contourShadow * 0.92);
                color = lerp(color, _Line.rgb, contour * (0.58 + fill * 0.38));

                // Accent one of the same streamfunction contours so it bends with the sphere and wake.
                float currentAlong = fixedAlong;
                float currentTarget = -speed * radius * 1.05;
                float stripeDistance = abs(psi - currentTarget) / speed;
                float stripeEdge = max(fwidth(stripeDistance), 0.0005);
                float stripeShadow = 1.0 - smoothstep(_RedLineWidth + _BlackLineWidth, _RedLineWidth + _BlackLineWidth + stripeEdge, stripeDistance);
                color = lerp(color, float3(0.0, 0.0, 0.0), stripeShadow * 0.96);
                float stripe = 1.0 - smoothstep(_RedLineWidth, _RedLineWidth + stripeEdge, stripeDistance);
                float goldBlend = smoothstep(radius * 2.5, radius * 11.0, currentAlong);
                float3 accent = lerp(_RedCurrent.rgb, _GoldCurrent.rgb, goldBlend);
                color = lerp(color, accent, stripe * 0.96);

                // Contact shadow and distance haze give the flat field perspective depth.
                float verticalResponse = exp(-pow(_BallPosition.y / (radius * 2.0), 2.0));
                float contact = exp(-(along * along / (radius * radius * 7.0) + across * across / (radius * radius * 2.2))) * verticalResponse;
                color *= 1.0 - contact * 0.55;
                float haze = smoothstep(25.0, 95.0, input.eyeDepth);
                color = lerp(color, _Deep.rgb * 0.55, haze);
                float grain = hash21(floor(world * 95.0 + _ElapsedTime * 0.1)) - 0.5;
                color += grain * 0.018;
                return fixed4(saturate(color), 1);
            }
            ENDCG
        }
    }
}
