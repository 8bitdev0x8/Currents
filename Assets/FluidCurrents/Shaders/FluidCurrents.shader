Shader "FluidCurrents/Procedural Currents"
{
    Properties
    {
        _ColorA ("Deep color", Color) = (0.012, 0.008, 0.025, 1)
        _ColorB ("Middle color", Color) = (0.19, 0.07, 0.28, 1)
        _ColorC ("Highlight color", Color) = (0.78, 0.58, 0.91, 1)
        _AccentRed ("Red current", Color) = (0.95, 0.035, 0.12, 1)
        _AccentGold ("Gold current", Color) = (1, 0.58, 0.06, 1)
        _Scale ("Flow scale", Range(1, 12)) = 3
        _Speed ("Flow speed", Range(0, 2)) = 0.14
        _Swirl ("Swirl", Range(0, 2)) = 0.85
        _BandCount ("Contour bands", Range(2, 30)) = 19
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            fixed4 _AccentRed;
            fixed4 _AccentGold;
            float _Scale;
            float _Speed;
            float _Swirl;
            float _BandCount;
            float4 _BallUV;
            float _BallRadius;
            float _BallStrength;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
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
                [unroll] for (int i = 0; i < 5; i++)
                {
                    value += amplitude * noise(p);
                    p = float2(p.x * 1.6 - p.y * 1.2, p.x * 1.2 + p.y * 1.6);
                    amplitude *= 0.5;
                }
                return value;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 p = input.uv - 0.5;
                p.x *= _ScreenParams.x / _ScreenParams.y;
                float t = _Time.y * _Speed;

                float2 warp;
                warp.x = fbm(p * _Scale + float2(t * 0.18, -t * 0.11));
                warp.y = fbm(p * _Scale + float2(5.2 - t * 0.13, 2.8 + t * 0.16));
                float2 q = p + (warp - 0.5) * _Swirl;

                float2 ballP = _BallUV.xy - 0.5;
                ballP.x *= _ScreenParams.x / _ScreenParams.y;
                float2 fromBall = p - ballP;
                float ballDistance = max(length(fromBall), 0.0001);
                float ballMask = exp(-ballDistance * ballDistance / max(_BallRadius * _BallRadius * 2.5, 0.0001));
                float2 radial = fromBall / ballDistance;
                q += radial * ballMask * _BallRadius * _BallStrength;
                q += float2(-radial.y, radial.x) * ballMask * _BallRadius * _BallStrength * 0.45;

                float field = fbm(q * _Scale + float2(t * 0.1, -t * 0.08));
                float phase = (q.x * 1.0 + q.y * 0.62 + (field - 0.5) * 0.46) * _BandCount * 6.28318;
                float contourLine = pow(saturate(1.0 - abs(sin(phase))), 22.0);
                float contourFill = smoothstep(0.18, 0.82, field);
                float3 color = lerp(_ColorA.rgb, _ColorB.rgb, contourFill * 0.8);
                color = lerp(color, _ColorC.rgb, contourLine * (0.5 + 0.45 * contourFill));

                float redPath = abs(p.x + p.y * 0.52 + 0.22);
                float redStripe = (1.0 - smoothstep(0.012, 0.028, redPath)) * smoothstep(-0.48, -0.24, p.y);
                color = lerp(color, _AccentRed.rgb, redStripe * 0.96);
                float goldPath = abs(p.x - p.y * 0.72 - 0.37);
                float goldStripe = (1.0 - smoothstep(0.012, 0.026, goldPath)) * smoothstep(-0.48, -0.30, p.y);
                color = lerp(color, _AccentGold.rgb, goldStripe * 0.94);
                color += contourLine * 0.1;
                return fixed4(saturate(color), 1);
            }
            ENDCG
        }
    }
}
