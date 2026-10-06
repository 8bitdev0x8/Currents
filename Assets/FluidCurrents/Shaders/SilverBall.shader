Shader "FluidCurrents/Silver Ball"
{
    Properties
    {
        _Silver ("Silver", Color) = (0.46, 0.46, 0.49, 1)
        _Highlight ("Highlight", Color) = (0.98, 0.96, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Silver;
            fixed4 _Highlight;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 position : SV_POSITION; float3 worldNormal : TEXCOORD0; float3 viewDir : TEXCOORD1; };

            v2f vert(appdata input)
            {
                v2f output;
                float3 worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.viewDir = _WorldSpaceCameraPos.xyz - worldPos;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 n = normalize(input.worldNormal);
                float3 v = normalize(input.viewDir);
                float3 lightA = normalize(float3(-0.45, 0.72, -0.62));
                float3 lightB = normalize(float3(0.68, 0.35, -0.64));
                float diffuse = 0.12 + 0.22 * saturate(dot(n, lightA)) + 0.10 * saturate(dot(n, lightB));
                float specA = pow(saturate(dot(n, normalize(lightA + v))), 90.0) * 1.5;
                float specB = pow(saturate(dot(n, normalize(lightB + v))), 48.0) * 0.85;
                float broad = pow(saturate(dot(n, normalize(float3(-0.35, 0.62, -0.70) + v))), 7.0) * 0.18;
                float rim = pow(1.0 - saturate(dot(n, v)), 2.4) * 0.18;
                float3 color = _Silver.rgb * diffuse + _Highlight.rgb * (specA + specB + broad + rim);
                return fixed4(saturate(color), 1);
            }
            ENDCG
        }
    }
}
