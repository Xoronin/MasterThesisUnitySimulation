Shader "Custom/TerrainSignalHeatmap_UltraFast"
{
    Properties
    {
        _MainTex   ("Heatmap Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 2)) = 1.0
        _Alpha     ("Alpha", Range(0, 1)) = 0.8
        _EdgeFade  ("Edge Fade (UV)", Range(0, 1)) = 0.2
        _UseDepthFade ("Use Depth Fade", Float) = 0 // 0=off, 1=on
        _DepthFade ("Depth Fade Strength", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1

        Pass
        {
            Name "UnlitHeatmap"
            Tags { "LightMode"="UniversalForward" } // still fine, but no lighting code

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            // Keep it lean: no lighting/fog/ssao/additional lights keywords.
            #pragma target 2.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half   _Intensity;
                half   _Alpha;
                half   _EdgeFade;
                half   _UseDepthFade; // 0 or 1
                half   _DepthFade;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS);
                OUT.positionHCS = p.positionCS;
                OUT.screenPos   = ComputeScreenPos(p.positionCS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            inline half SampleDepthFade(float4 screenPos, half strength)
            {
                #if defined(_CameraDepthTexture)
                    float2 uv = screenPos.xy / screenPos.w;
                    // PointClamp sampler is defined in URP library
                    float sceneRaw = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv).r;
                    float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                    float pixEye   = LinearEyeDepth(screenPos.z / screenPos.w, _ZBufferParams);
                    return saturate((sceneEye - pixEye) * strength);
                #else
                    return 1.0h;
                #endif
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Soft UV edge fade without branching
                if (_EdgeFade > 0.0h)
                {
                    half2 ed = saturate(min(IN.uv, 1.0h - IN.uv) / _EdgeFade);
                    c.a *= (ed.x * ed.y);
                }

                // Optional depth fade (toggle with _UseDepthFade)
                if (_UseDepthFade > 0.5h)
                {
                    c.a *= SampleDepthFade(IN.screenPos, _DepthFade);
                }

                c.rgb *= _Intensity;
                c.a   *= _Alpha;

                // No lighting, no fog
                return c;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
