Shader "DoomMaze/UnderwaterFullscreen"
{
    Properties
    {
        _WaveSpeed       ("Wave Speed",      Float)        = 1.2
        _WaveAmplitude   ("Wave Amplitude",  Range(0,0.03)) = 0.012
        _WaveFrequency   ("Wave Frequency",  Float)        = 6.0
        _CausticSpeed    ("Caustic Speed",   Float)        = 0.5
        _CausticScale    ("Caustic Scale",   Float)        = 4.0
        _CausticStrength ("Caustic Strength",Range(0,0.4)) = 0.15
        _UnderwaterTint  ("Underwater Tint", Color)        = (0.05, 0.3, 0.45, 1)
        _TintStrength    ("Tint Strength",   Range(0,1))   = 0.35
        _DarkenStrength  ("Darken Strength", Range(0,1))   = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "UnderwaterFullscreen"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _WaveSpeed;
                float  _WaveAmplitude;
                float  _WaveFrequency;
                float  _CausticSpeed;
                float  _CausticScale;
                float  _CausticStrength;
                float4 _UnderwaterTint;
                float  _TintStrength;
                float  _DarkenStrength;
            CBUFFER_END

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float2 wave = float2(
                    sin(uv.y * _WaveFrequency + _Time.y * _WaveSpeed),
                    cos(uv.x * _WaveFrequency + _Time.y * _WaveSpeed * 0.8)
                ) * _WaveAmplitude;

                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + wave);

                float c1 = sin((uv.x + uv.y) * _CausticScale + _Time.y * _CausticSpeed);
                float c2 = sin((uv.x - uv.y) * _CausticScale * 1.4 + _Time.y * _CausticSpeed * 0.6 + 2.1);
                float caustic = saturate((c1 + c2) * 0.5 + 0.5) * _CausticStrength;
                col.rgb += caustic;

                float luma = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float tintMask = luma * (1.0 - luma * _DarkenStrength);
                col.rgb = lerp(col.rgb, _UnderwaterTint.rgb, _TintStrength * tintMask + _TintStrength * 0.2);

                return col;
            }
            ENDHLSL
        }
    }
}