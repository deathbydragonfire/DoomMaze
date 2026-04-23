#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UnderwaterShaderImportHelper
{
    static UnderwaterShaderImportHelper()
    {
        WriteShaderIfMissing(
            "Assets/Materials/UnderwaterFullscreen.shader",
            UnderwaterFullscreenShaderSource
        );
        WriteShaderIfMissing(
            "Assets/Materials/SpriteUnderwater.shader",
            SpriteUnderwaterShaderSource
        );
        WriteFileIfMissing(
            "Assets/Scripts/Core/UnderwaterRendererFeature.cs",
            UnderwaterRendererFeatureSource
        );
        AssetDatabase.Refresh(); // v4 - DISABLED feature write, shaders only
    }

    private const string UnderwaterFullscreenShaderSource = @"Shader ""DoomMaze/UnderwaterFullscreen""
{
    Properties
    {
        _WaveSpeed       (""Wave Speed"",      Float)        = 1.2
        _WaveAmplitude   (""Wave Amplitude"",  Range(0,0.03)) = 0.012
        _WaveFrequency   (""Wave Frequency"",  Float)        = 6.0
        _CausticSpeed    (""Caustic Speed"",   Float)        = 0.5
        _CausticScale    (""Caustic Scale"",   Float)        = 4.0
        _CausticStrength (""Caustic Strength"",Range(0,0.4)) = 0.15
        _UnderwaterTint  (""Underwater Tint"", Color)        = (0.05, 0.3, 0.45, 1)
        _TintStrength    (""Tint Strength"",   Range(0,1))   = 0.35
        _DarkenStrength  (""Darken Strength"", Range(0,1))   = 0.2
    }

    SubShader
    {
        Tags
        {
            ""RenderPipeline"" = ""UniversalPipeline""
            ""RenderType""     = ""Opaque""
            ""Queue""          = ""Geometry""
        }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name ""UnderwaterFullscreen""

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl""

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
}";

    private const string SpriteUnderwaterShaderSource = @"Shader ""DoomMaze/SpriteUnderwater""
{
    Properties
    {
        _MainTex         (""Sprite Texture"",  2D)           = ""white"" {}
        _Color           (""Tint"",            Color)        = (1,1,1,1)
        _GrungeStrength  (""Grunge Strength"", Range(0,1))   = 0.35
        _GrungeScale     (""Grunge Scale"",    Float)        = 4.0
        _WaveSpeed       (""Wave Speed"",      Float)        = 1.0
        _WaveAmplitude   (""Wave Amplitude"",  Range(0,0.05)) = 0.015
        _WaveFrequency   (""Wave Frequency"",  Float)        = 8.0
        _CausticSpeed    (""Caustic Speed"",   Float)        = 0.6
        _CausticScale    (""Caustic Scale"",   Float)        = 3.0
        _CausticStrength (""Caustic Strength"",Range(0,1))   = 0.25
        _FogColor        (""Fog Color"",       Color)        = (0.05, 0.2, 0.3, 1)
        _FogStart        (""Fog Start"",       Float)        = 1.0
        _FogEnd          (""Fog End"",         Float)        = 6.0
    }

    SubShader
    {
        Tags
        {
            ""Queue""          = ""Transparent""
            ""RenderType""     = ""Transparent""
            ""RenderPipeline"" = ""UniversalPipeline""
            ""IgnoreProjector""= ""True""
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull  Off
        ZWrite Off

        Pass
        {
            Name ""SpriteUnderwater""
            Tags { ""LightMode"" = ""UniversalForward"" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
                float4 color       : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _GrungeStrength;
                float  _GrungeScale;
                float  _WaveSpeed;
                float  _WaveAmplitude;
                float  _WaveFrequency;
                float  _CausticSpeed;
                float  _CausticScale;
                float  _CausticStrength;
                float4 _FogColor;
                float  _FogStart;
                float  _FogEnd;
            CBUFFER_END

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i,              float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1,0),float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0,1),float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1,1),float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v   += amp * valueNoise(p);
                    p   *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos   = ComputeScreenPos(posInputs.positionCS);
                OUT.color       = IN.color * _Color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 wave = float2(
                    sin(uv.y * _WaveFrequency + _Time.y * _WaveSpeed),
                    cos(uv.x * _WaveFrequency + _Time.y * _WaveSpeed * 0.8)
                ) * _WaveAmplitude;
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + wave) * IN.color;
                clip(col.a - 0.001);
                float grunge = fbm(uv * _GrungeScale + _Time.y * 0.05);
                col.rgb *= lerp(1.0, grunge, _GrungeStrength);
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float c1 = sin((screenUV.x + screenUV.y) * _CausticScale + _Time.y * _CausticSpeed);
                float c2 = sin((screenUV.x - screenUV.y) * _CausticScale * 1.4 + _Time.y * _CausticSpeed * 0.6 + 2.1);
                float caustic = saturate((c1 + c2) * 0.5 + 0.5) * _CausticStrength;
                col.rgb += caustic;
                float rawDepth   = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float spriteDepth = IN.screenPos.w;
                float depthDiff  = saturate((sceneDepth - spriteDepth) / max(_FogEnd - _FogStart, 0.001));
                float fogFactor  = saturate((spriteDepth - _FogStart) / max(_FogEnd - _FogStart, 0.001));
                col.rgb = lerp(col.rgb, _FogColor.rgb, fogFactor * depthDiff);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback ""Universal Render Pipeline/2D/Sprite-Unlit-Default""
}";

    private static void WriteShaderIfMissing(string assetPath, string source)
    {
        WriteFileIfMissing(assetPath, source);
    }

    private static void WriteFileIfMissing(string assetPath, string source)
    {
        string fullPath = System.IO.Path.Combine(
            Application.dataPath.Replace("/Assets", ""),
            assetPath
        );
        string marker = source.Substring(0, System.Math.Min(80, source.Length));
        if (System.IO.File.Exists(fullPath))
        {
            string existing = System.IO.File.ReadAllText(fullPath);
            if (existing.StartsWith(marker)) return;
        }
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
        System.IO.File.WriteAllText(fullPath, source);
        Debug.Log($"[UnderwaterShaderImportHelper] Updated file at {assetPath}");
    }

    private const string UnderwaterRendererFeatureSource =
@"using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class UnderwaterRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material _material;

    private UnderwaterPass _pass;

    public override void Create()
    {
        _pass = new UnderwaterPass(_material)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null)
        {
            Debug.LogError(""[UnderwaterRendererFeature] Material is not assigned."");
            return;
        }

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }

    private class UnderwaterPass : ScriptableRenderPass
    {
        private readonly Material _material;

        public UnderwaterPass(Material material)
        {
            _material = material;
        }

        private class PassData
        {
            internal TextureHandle src;
            internal Material      material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle src = resourceData.activeColorTexture;

            TextureDesc desc = renderGraph.GetTextureDesc(src);
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples     = MSAASamples.None;
            desc.name            = ""_UnderwaterTemp"";
            desc.clearBuffer     = false;

            TextureHandle temp = renderGraph.CreateTexture(desc);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(""UnderwaterPass_Blit"", out PassData passData))
            {
                passData.src      = src;
                passData.material = _material;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(temp, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                });
            }

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(""UnderwaterPass_Copy"", out PassData passData))
            {
                passData.src      = temp;
                passData.material = null;

                builder.UseTexture(temp, AccessFlags.Read);
                builder.SetRenderAttachment(src, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.src, new Vector4(1f, 1f, 0f, 0f), 0, false);
                });
            }
        }

        public void Dispose() { }
    }
}";
}
#endif
