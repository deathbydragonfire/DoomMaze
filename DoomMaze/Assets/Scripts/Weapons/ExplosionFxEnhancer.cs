using System.Collections;
using UnityEngine;

/// <summary>
/// Adds a short HDR glow burst and point-light pulse to a spawned explosion FX instance.
/// Intended for one-shot explosion prefabs that do not already use bloom-friendly materials.
/// </summary>
public class ExplosionFxEnhancer : MonoBehaviour
{
    private const string ParticleAdditiveShaderName = "Particles/Additive";
    private const string LegacyParticleAdditiveShaderName = "Legacy Shaders/Particles/Additive";
    private const string UrpParticleUnlitShaderName = "Universal Render Pipeline/Particles/Unlit";

    private Material _glowMaterial;
    private ParticleSystem _glowParticleSystem;
    private Light _pulseLight;
    private Coroutine _lightRoutine;

    public void Play(
        Color glowColor,
        float glowSize,
        float glowDuration,
        Color lightColor,
        float lightIntensity,
        float lightRange,
        float lightDuration)
    {
        EnsureGlowParticleSystem(glowColor, glowSize, glowDuration);
        EmitGlow(glowColor, glowSize, glowDuration);
        PulseLight(lightColor, lightIntensity, lightRange, lightDuration);
    }

    private void EnsureGlowParticleSystem(Color glowColor, float glowSize, float glowDuration)
    {
        if (_glowParticleSystem != null)
        {
            UpdateGlowMaterial(glowColor);
            return;
        }

        GameObject glowObject = new GameObject("ExplosionGlow");
        glowObject.transform.SetParent(transform, false);

        _glowParticleSystem = glowObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = glowObject.GetComponent<ParticleSystemRenderer>();

        ConfigureGlowParticleSystem(_glowParticleSystem, glowSize, glowDuration);

        _glowMaterial = CreateGlowMaterial(glowColor);
        if (_glowMaterial != null)
            renderer.sharedMaterial = _glowMaterial;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.alignment = ParticleSystemRenderSpace.View;
    }

    private void ConfigureGlowParticleSystem(ParticleSystem particleSystem, float glowSize, float glowDuration)
    {
        var main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = Mathf.Max(0.01f, glowDuration);
        main.startLifetime = Mathf.Max(0.01f, glowDuration);
        main.startSpeed = 0f;
        main.startSize = Mathf.Max(0.01f, glowSize);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 1;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var shape = particleSystem.shape;
        shape.enabled = false;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.5f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.25f, 0.85f),
            new Keyframe(1f, 1.35f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }

    private void EmitGlow(Color glowColor, float glowSize, float glowDuration)
    {
        if (_glowParticleSystem == null)
            return;

        UpdateGlowMaterial(glowColor);

        var main = _glowParticleSystem.main;
        main.startLifetime = Mathf.Max(0.01f, glowDuration);
        main.startSize = Mathf.Max(0.01f, glowSize);

        _glowParticleSystem.transform.position = transform.position;
        _glowParticleSystem.Clear(withChildren: true);
        _glowParticleSystem.Emit(1);
    }

    private void PulseLight(Color lightColor, float lightIntensity, float lightRange, float lightDuration)
    {
        if (_pulseLight == null)
        {
            GameObject lightObject = new GameObject("ExplosionLight");
            lightObject.transform.SetParent(transform, false);
            _pulseLight = lightObject.AddComponent<Light>();
            _pulseLight.type = LightType.Point;
            _pulseLight.shadows = LightShadows.None;
        }

        _pulseLight.color = lightColor;
        _pulseLight.range = Mathf.Max(0.01f, lightRange);

        if (_lightRoutine != null)
            StopCoroutine(_lightRoutine);

        _lightRoutine = StartCoroutine(LightPulseRoutine(
            Mathf.Max(0f, lightIntensity),
            Mathf.Max(0.01f, lightDuration)));
    }

    private IEnumerator LightPulseRoutine(float peakIntensity, float duration)
    {
        if (_pulseLight == null)
            yield break;

        _pulseLight.enabled = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            _pulseLight.intensity = Mathf.Lerp(peakIntensity, 0f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _pulseLight.intensity = 0f;
        _pulseLight.enabled = false;
        _lightRoutine = null;
    }

    private Material CreateGlowMaterial(Color glowColor)
    {
        Shader shader = FindGlowShader();
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "ExplosionGlowRuntime"
        };

        Texture mainTexture = ResolveGlowTexture();

        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", mainTexture);

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", mainTexture);

        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", glowColor);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", glowColor);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", glowColor);

        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", glowColor);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 2f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", 5f);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", 1f);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        return material;
    }

    private void UpdateGlowMaterial(Color glowColor)
    {
        if (_glowMaterial == null)
            return;

        if (_glowMaterial.HasProperty("_TintColor"))
            _glowMaterial.SetColor("_TintColor", glowColor);

        if (_glowMaterial.HasProperty("_Color"))
            _glowMaterial.SetColor("_Color", glowColor);

        if (_glowMaterial.HasProperty("_BaseColor"))
            _glowMaterial.SetColor("_BaseColor", glowColor);

        if (_glowMaterial.HasProperty("_EmissionColor"))
            _glowMaterial.SetColor("_EmissionColor", glowColor);
    }

    private Texture ResolveGlowTexture()
    {
        ParticleSystemRenderer[] particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ParticleSystemRenderer particleRenderer = particleRenderers[i];
            if (particleRenderer == null || particleRenderer.sharedMaterial == null)
                continue;

            Material sourceMaterial = particleRenderer.sharedMaterial;

            if (sourceMaterial.HasProperty("_MainTex"))
            {
                Texture mainTex = sourceMaterial.GetTexture("_MainTex");
                if (mainTex != null)
                    return mainTex;
            }

            if (sourceMaterial.HasProperty("_BaseMap"))
            {
                Texture baseMap = sourceMaterial.GetTexture("_BaseMap");
                if (baseMap != null)
                    return baseMap;
            }
        }

        return Texture2D.whiteTexture;
    }

    private static Shader FindGlowShader()
    {
        Shader shader = Shader.Find(ParticleAdditiveShaderName);
        if (shader != null)
            return shader;

        shader = Shader.Find(LegacyParticleAdditiveShaderName);
        if (shader != null)
            return shader;

        return Shader.Find(UrpParticleUnlitShaderName);
    }

    private void OnDestroy()
    {
        if (_glowMaterial != null)
            Destroy(_glowMaterial);
    }
}
