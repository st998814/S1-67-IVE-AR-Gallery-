Shader "Skyboxes/HdriFlatSky2"
{
    HLSLINCLUDE

        ///////////////////////////////////////////////////////////////////
        // FRAGMENT SHADER
        ///////////////////////////////////////////////////////////////////
        float4 FragBaking(Varyings input) : SV_Target
        {
            float3 viewDirWS = GetSkyViewDirWS(input.positionCS.xy);

            // Reverse it to point into the scene
            float3 dir = -viewDirWS;

            // Spherically map UVs
            float u = atan2(dir.x, dir.z) / TWO_PI + 0.5;
            float v = dir.y * 0.5 + 0.5;

            if (u > 0.5)
            {
                u = 1 - u;
            }

            return tex2D(_MainTex, float2(2 * u, v));
        }

        float4 FragRender(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = tex2D(_MainTex, float2(input.positionCS.x / _ScreenParams.x, input.positionCS.y / _ScreenParams.y));
            color.rgb *= 1;// GetCurrentExposureMultiplier();
            return color;
        }
        
    ENDHLSL

    SubShader
    {
        // For cubemap
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FragBaking
            ENDHLSL
        }

        // For fullscreen Sky
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FragRender
            ENDHLSL
        }
    }

    Fallback Off
}