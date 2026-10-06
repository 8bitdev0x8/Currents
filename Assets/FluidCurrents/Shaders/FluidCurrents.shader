Shader "FluidCurrents/World Surface"
{
    Properties
    {
        _Deep ("Deep near-black", Color) = (0.008, 0.006, 0.014, 1)
        _Purple ("Violet field", Color) = (0.18, 0.075, 0.24, 1)
        _Line ("Silver-lavender streamlines", Color) = (0.82, 0.69, 0.91, 1)
        _RedCurrent ("Vermilion current", Color) = (0.96, 0.035, 0.075, 1)
        _GoldCurrent ("Amber current", Color) = (1, 0.44, 0.035, 1)
        _FlowDirection ("Flow direction", Vector) = (0.88, -0.47, 0, 0)
        _FlowVelocity ("Flow velocity", Float) = 2.6
        _Strouhal ("Strouhal number", Float) = 0.2
        _Viscosity ("Kinematic viscosity", Float) = 0.025
        _LineFrequency ("Line frequency", Float) = 11
        _WakeStrength ("Wake strength", Float) = 1
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
            float _FlowVelocity;
            float _Strouhal;
            float _Viscosity;
            float _LineFrequency;
            float _WakeStrength;
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

                // Potential flow around a circular section of the sphere.
                float r2 = max(along * along + across * across, radius * radius * 0.72);
                float psi = speed * across * (1.0 - radius * radius / r2);

                // Alternating Gaussian-core vortices, shed at f = St * U / D.
                float halfPeriod = radius / max(_Strouhal * speed, 0.01);
                float phase = frac(_ElapsedTime / halfPeriod);
                float cycle = floor(_ElapsedTime / halfPeriod);
                float spacing = 0.86 * speed * halfPeriod;
                float circulation = 2.2 * speed * (2.0 * radius);
                float wake = 0;
                [unroll] for (int i = 0; i < 8; i++)
                {
                    float age = (i + phase) * halfPeriod;
                    float shedDistance = radius * 1.08 + age * 0.86 * speed;
                    float parity = frac((i + cycle) * 0.5) * 2.0;
                    float vortexSign = parity < 1.0 ? 1.0 : -1.0;
                    float lateral = vortexSign * radius * (0.52 + 0.06 * sin(age * speed / radius));
                    float2 vortex = float2(shedDistance, lateral);
                    float2 d = float2(along, across) - vortex;
                    float core2 = radius * radius * 0.045 + 4.0 * _Viscosity * age;
                    float attenuation = exp(-age / max(halfPeriod * 7.0, 0.01));
                    wake += vortexSign * circulation * 0.0796 * log(1.0 + dot(d, d) / max(core2, 0.0001)) * attenuation;
                }
                psi += wake * _WakeStrength;

                float fieldNoise = fbm(world * 0.54 + flow * (_ElapsedTime * 0.09));
                psi += (fieldNoise - 0.5) * speed * radius * 0.045;
                float contour = pow(saturate(1.0 - abs(sin(psi * _LineFrequency))), 15.0);
                float fill = smoothstep(0.14, 0.88, fieldNoise);
                float3 color = lerp(_Deep.rgb, _Purple.rgb, 0.25 + fill * 0.55);
                color = lerp(color, _Line.rgb, contour * (0.58 + fill * 0.38));

                float stripeTarget = -speed * radius * 1.05;
                float stripe = 1.0 - smoothstep(speed * radius * 0.035, speed * radius * 0.09, abs(psi - stripeTarget));
                float goldBlend = smoothstep(radius * 2.5, radius * 11.0, along);
                float3 accent = lerp(_RedCurrent.rgb, _GoldCurrent.rgb, goldBlend);
                color = lerp(color, accent, stripe * 0.96);

                // Contact shadow and distance haze give the flat field perspective depth.
                float contact = exp(-(along * along / (radius * radius * 7.0) + across * across / (radius * radius * 2.2)));
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
