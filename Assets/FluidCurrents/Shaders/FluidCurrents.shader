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
        _LineFrequency ("Line frequency", Float) = 4.49
        _LineWidth ("Streamline width", Range(0.005, 0.2)) = 0.045
        _RedLineWidth ("Red current width", Range(0.02, 0.4)) = 0.16
        _BlackLineWidth ("Black outline width", Range(0, 0.2)) = 0.04
        _EdgeFadeDistance ("Surface edge fade distance", Range(1, 40)) = 12
        _RainbowEnabled ("Enable rainbow wake", Float) = 0
        _BallRadius ("Sphere radius", Float) = 1.5
        _FieldBounds ("Flow field world bounds", Vector) = (-30, -20, 60, 60)
        _ElapsedTime ("Elapsed time", Float) = 0
        [NoScaleOffset] _FlowField ("Simulated flow field", 2D) = "black" {}
        [NoScaleOffset] _RainbowField ("Advected rainbow wake", 2D) = "black" {}
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
            float _LineFrequency;
            float _LineWidth;
            float _RedLineWidth;
            float _BlackLineWidth;
            float _EdgeFadeDistance;
            float _RainbowEnabled;
            float _BallRadius;
            float _ElapsedTime;
            sampler2D _FlowField;
            sampler2D _RainbowField;

            float4 _FieldBounds;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 worldPos : TEXCOORD0; float eyeDepth : TEXCOORD1; };

            v2f vert(appdata input)
            {
                v2f output;
                float scaleX = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));
                float scaleY = length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21));
                float scaleZ = length(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22));
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPos = input.vertex.xyz * float3(scaleX, scaleY, scaleZ);
                output.eyeDepth = -UnityObjectToViewPos(input.vertex).z;
                return output;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 hsvToRgb(float hue)
            {
                float3 phase = frac(hue + float3(0.0, 0.6666667, 0.3333333));
                return saturate(abs(phase * 6.0 - 3.0) - 1.0);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 world = input.worldPos.xz;
                float2 flow = normalize(_FlowDirection.xy);
                float radius = max(_BallRadius, 0.01);
                float speed = max(_FlowVelocity, 0.01);
                float2 fieldUv = (world - _FieldBounds.xy) / max(_FieldBounds.zw, float2(0.0001, 0.0001));
                float4 flowField = tex2D(_FlowField, saturate(fieldUv));
                float psi = flowField.r;
                float simulatedSpeed = flowField.g;
                float vorticity = flowField.b;
                float obstacle = flowField.a;
                float2 fieldRelative = world - _CurrentOrigin.xy;
                float fixedAlong = dot(fieldRelative, flow);
                float currentAlong = fixedAlong;
                float contourPhase = psi * _LineFrequency;
                float contourDistance = abs(asin(sin(contourPhase))) / max(_LineFrequency * speed, 0.001);
                float contourEdge = max(fwidth(contourDistance), 0.0005);
                float contour = 1.0 - smoothstep(_LineWidth, _LineWidth + contourEdge, contourDistance);
                float turbulence = saturate(abs(vorticity) * 2.5);
                float fill = saturate(0.22 + simulatedSpeed * 0.44 + turbulence * 0.28);
                float3 color = lerp(_Deep.rgb, _Purple.rgb, 0.25 + fill * 0.55);
                float contourShadow = 1.0 - smoothstep(_LineWidth + _BlackLineWidth, _LineWidth + _BlackLineWidth + contourEdge, contourDistance);
                color = lerp(color, float3(0.0, 0.0, 0.0), contourShadow * 0.92);
                color = lerp(color, _Line.rgb, contour * (0.58 + fill * 0.38 + turbulence * 0.18));

                // Rainbow dye is injected along the sphere's swept contact path and
                // advected by the velocity solver. Color therefore remains where the
                // fluid carried it instead of following the sphere as a moving mask.
                if (_RainbowEnabled > 0.5)
                {
                    float4 rainbowField = tex2D(_RainbowField, saturate(fieldUv));
                    float2 hueVector = rainbowField.rg * 2.0 - 1.0;
                    float rainbowHue = frac(atan2(hueVector.y, hueVector.x) * 0.15915494 + 0.5);
                    float rainbowAmount = contour * rainbowField.b * 0.96;
                    color = lerp(color, hsvToRgb(rainbowHue), rainbowAmount);
                }

                // Keep the accent on the fixed psi=0 contour set by Current Origin.
                // The sphere changes the sampled field and bends this contour, but
                // moving the sphere cannot translate the accent's upstream path.
                float currentTarget = 0.0;
                float stripeDistance = abs(psi - currentTarget) / speed;
                float stripeEdge = max(fwidth(stripeDistance), 0.0005);
                float stripeShadow = 1.0 - smoothstep(_RedLineWidth + _BlackLineWidth, _RedLineWidth + _BlackLineWidth + stripeEdge, stripeDistance);
                color = lerp(color, float3(0.0, 0.0, 0.0), stripeShadow * 0.96);
                float stripe = 1.0 - smoothstep(_RedLineWidth, _RedLineWidth + stripeEdge, stripeDistance);
                float goldBlend = smoothstep(radius * 2.5, radius * 11.0, currentAlong);
                float3 accent = lerp(_RedCurrent.rgb, _GoldCurrent.rgb, goldBlend);
                color = lerp(color, accent, stripe * 0.96);

                // Fade the finite plane perimeter and distant surface into the camera's
                // deep-color background so its rectangular edge does not read as a cutout.
                color *= 1.0 - obstacle * 0.5;
                float grain = hash21(floor(world * 95.0 + _ElapsedTime * 0.1)) - 0.5;
                color += grain * 0.018;
                float2 edgeFromMin = world - _FieldBounds.xy;
                float2 edgeFromMax = _FieldBounds.xy + _FieldBounds.zw - world;
                float edgeDistance = min(min(edgeFromMin.x, edgeFromMin.y), min(edgeFromMax.x, edgeFromMax.y));
                float edgeFade = smoothstep(0.0, max(_EdgeFadeDistance, 0.01), edgeDistance);
                float distanceFade = 1.0 - smoothstep(25.0, 95.0, input.eyeDepth);
                color = lerp(_Deep.rgb, color, edgeFade * distanceFade);
                return fixed4(saturate(color), 1);
            }
            ENDCG
        }
    }
}
