using System.Collections;
using UnityEngine;

/// <summary>
/// White hit flash for 3D enemies that use MeshRenderer or SkinnedMeshRenderer.
/// Sprite enemies should keep using EnemyHitFlash.
/// </summary>
public class EnemyModelHitFlash : MonoBehaviour
{
    [SerializeField] private float _flashDuration = 0.2f;
    [SerializeField] private Color _flashColor = Color.white;
    [SerializeField] private float _emissionIntensity = 1.8f;

    private struct RendererMaterialState
    {
        public Renderer Renderer;
        public Material[] SharedMaterials;
    }

    private Renderer[] _renderers;
    private RendererMaterialState[] _rendererStates;
    private Material _flashMaterial;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        CacheRenderers();
    }

    private void OnDisable()
    {
        RestoreRendererMaterials();

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
    }

    public void Flash()
    {
        Flash(_flashDuration);
    }

    public void Flash(float duration)
    {
        if (_renderers == null || _renderers.Length == 0)
            CacheRenderers();

        if (_renderers == null || _renderers.Length == 0)
            return;

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            RestoreRendererMaterials();
        }

        _flashRoutine = StartCoroutine(FlashRoutine(Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FlashRoutine(float duration)
    {
        CacheRendererStates();
        ApplyFlashMaterial();

        yield return new WaitForSeconds(duration);

        RestoreRendererMaterials();
        _flashRoutine = null;
    }

    private void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void CacheRendererStates()
    {
        if (_renderers == null)
        {
            _rendererStates = System.Array.Empty<RendererMaterialState>();
            return;
        }

        int count = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer != null && renderer is not ParticleSystemRenderer && renderer is not SpriteRenderer)
                count++;
        }

        RendererMaterialState[] states = new RendererMaterialState[count];
        int stateIndex = 0;
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer || renderer is SpriteRenderer)
                continue;

            states[stateIndex++] = new RendererMaterialState
            {
                Renderer = renderer,
                SharedMaterials = renderer.sharedMaterials
            };
        }

        _rendererStates = states;
    }

    private void ApplyFlashMaterial()
    {
        if (_rendererStates == null)
            return;

        Material flashMaterial = GetFlashMaterial();
        if (flashMaterial == null)
            return;

        for (int i = 0; i < _rendererStates.Length; i++)
        {
            RendererMaterialState state = _rendererStates[i];
            if (state.Renderer == null)
                continue;

            int materialCount = Mathf.Max(1, state.SharedMaterials != null ? state.SharedMaterials.Length : 0);
            Material[] overrideMaterials = new Material[materialCount];
            for (int j = 0; j < overrideMaterials.Length; j++)
                overrideMaterials[j] = flashMaterial;

            state.Renderer.sharedMaterials = overrideMaterials;
        }
    }

    private void RestoreRendererMaterials()
    {
        if (_rendererStates == null)
            return;

        for (int i = 0; i < _rendererStates.Length; i++)
        {
            RendererMaterialState state = _rendererStates[i];
            if (state.Renderer != null && state.SharedMaterials != null)
                state.Renderer.sharedMaterials = state.SharedMaterials;
        }
    }

    private Material GetFlashMaterial()
    {
        if (_flashMaterial != null)
            return _flashMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        _flashMaterial = new Material(shader)
        {
            color = _flashColor
        };

        if (_flashMaterial.HasProperty("_BaseColor"))
            _flashMaterial.SetColor("_BaseColor", _flashColor);
        if (_flashMaterial.HasProperty("_Color"))
            _flashMaterial.SetColor("_Color", _flashColor);
        if (_flashMaterial.HasProperty("_EmissionColor"))
        {
            _flashMaterial.EnableKeyword("_EMISSION");
            _flashMaterial.SetColor("_EmissionColor", _flashColor * _emissionIntensity);
        }

        return _flashMaterial;
    }
}
