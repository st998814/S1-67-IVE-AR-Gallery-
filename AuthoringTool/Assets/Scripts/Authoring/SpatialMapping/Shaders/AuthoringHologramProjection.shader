// Inspired by hologram techniques (alpha mask stripes, rim, scroll) — kept subtle for authoring guides.
// Reference: https://www.sharpcoderblog.com/blog/create-a-hologram-effect-in-unity-3d
Shader "ARGallery/Authoring/HologramProjection"
{
    Properties
    {
        [HDR] _BaseColor("Base Color", Color) = (0.52, 0.78, 0.9, 0.28)
        _AlphaTexture("Alpha Mask (R)", 2D) = "white" {}
        _AlphaScale("Alpha Tiling", Float) = 3.5
        _ScrollSpeedV("Alpha Scroll Speed", Range(0, 2)) = 0.35
        _GlowIntensity("Glow Intensity", Range(0, 0.35)) = 0.1
        _RimStrength("Rim Strength", Range(0, 0.45)) = 0.18
        _ScanlineFrequency("Scanline Frequency", Float) = 42
        _ScanlineStrength("Scanline Strength", Range(0, 0.25)) = 0.08
        _PulseSpeed("Pulse Speed", Float) = 0.55
        _PulseAmount("Pulse Amount", Range(0, 0.12)) = 0.03
        _GlitchSpeed("Glitch Speed", Range(0, 50)) = 0
        _GlitchIntensity("Glitch Intensity", Range(0, 0.05)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "HologramProjection"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_AlphaTexture);
            SAMPLER(sampler_AlphaTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _AlphaTexture_ST;
                float _AlphaScale;
                float _ScrollSpeedV;
                float _GlowIntensity;
                float _RimStrength;
                float _ScanlineFrequency;
                float _ScanlineStrength;
                float _PulseSpeed;
                float _PulseAmount;
                float _GlitchSpeed;
                float _GlitchIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;

                if (_GlitchIntensity > 0.0001)
                {
                    positionOS.z += sin(_Time.y * _GlitchSpeed * 5.0 + positionOS.y * 12.0) * _GlitchIntensity;
                }

                output.positionCS = TransformObjectToHClip(positionOS);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(input.viewDirWS);
                float3 normal = normalize(input.normalWS);
                float rim = 1.0 - saturate(dot(viewDir, normal));

                float3 viewPos = TransformWorldToView(input.positionWS);
                float2 alphaUv = float2(viewPos.x, viewPos.y + _Time.y * _ScrollSpeedV) * _AlphaScale;
                half alphaMask = SAMPLE_TEXTURE2D(_AlphaTexture, sampler_AlphaTexture, alphaUv).r;

                float scan = sin(input.uv.y * _ScanlineFrequency + _Time.y * 1.2) * 0.5 + 0.5;
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmount;

                half alpha = _BaseColor.a * alphaMask;
                alpha *= 1.0 + _ScanlineStrength * scan;
                alpha += pulse;
                alpha *= 0.78 + _RimStrength * rim;

                half3 rgb = _BaseColor.rgb * (_GlowIntensity + rim * 0.5 + 0.25);
                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _AlphaTexture;
            float4 _AlphaTexture_ST;
            fixed4 _BaseColor;
            float _AlphaScale;
            float _ScrollSpeedV;
            float _GlowIntensity;
            float _RimStrength;
            float _ScanlineFrequency;
            float _ScanlineStrength;
            float _PulseSpeed;
            float _PulseAmount;
            float _GlitchSpeed;
            float _GlitchIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 vertex = v.vertex;
                if (_GlitchIntensity > 0.0001)
                    vertex.z += sin(_Time.y * _GlitchSpeed * 5.0 + vertex.y * 12.0) * _GlitchIntensity;

                o.pos = UnityObjectToClipPos(vertex);
                o.worldPos = mul(unity_ObjectToWorld, vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float3 normal = normalize(i.worldNormal);
                float rim = 1.0 - saturate(dot(viewDir, normal));

                float3 viewPos = mul(UNITY_MATRIX_V, float4(i.worldPos, 1.0)).xyz;
                float2 alphaUv = float2(viewPos.x, viewPos.y + _Time.y * _ScrollSpeedV) * _AlphaScale;
                half alphaMask = tex2D(_AlphaTexture, alphaUv).r;

                float scan = sin(i.uv.y * _ScanlineFrequency + _Time.y * 1.2) * 0.5 + 0.5;
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmount;

                half alpha = _BaseColor.a * alphaMask;
                alpha *= 1.0 + _ScanlineStrength * scan;
                alpha += pulse;
                alpha *= 0.78 + _RimStrength * rim;

                half3 rgb = _BaseColor.rgb * (_GlowIntensity + rim * 0.5 + 0.25);
                return fixed4(rgb, saturate(alpha));
            }
            ENDCG
        }
    }

    FallBack Off
}
