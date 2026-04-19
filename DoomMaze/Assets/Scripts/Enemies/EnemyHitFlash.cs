using System.Collections;
using UnityEngine;

/// <summary>
/// Flashes the enemy sprite fully white on hit by driving the <c>_FlashAmount</c>
/// property on the <c>DoomMaze/SpriteFlash</c> shader via <see cref="MaterialPropertyBlock"/>.
/// Assign <see cref="_flashMaterial"/> (Mat_SpriteFlash) to the enemy's
/// <see cref="SpriteRenderer"/> — this happens automatically in <see cref="Awake"/>.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyHitFlash : MonoBehaviour
{
    [SerializeField] private Material _flashMaterial;
    [SerializeField] private float    _flashDuration = 0.12f;

    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    private SpriteRenderer       _spriteRenderer;
    private MaterialPropertyBlock _propertyBlock;
    private Coroutine            _flashRoutine;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _propertyBlock  = new MaterialPropertyBlock();

        if (_flashMaterial != null)
            _spriteRenderer.material = _flashMaterial;
    }

    /// <summary>Triggers a white flash. Interrupts any flash already in progress.</summary>
    public void Flash()
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        SetFlash(1f);

        float elapsed = 0f;
        while (elapsed < _flashDuration)
        {
            elapsed += Time.deltaTime;
            SetFlash(1f - Mathf.Clamp01(elapsed / _flashDuration));
            yield return null;
        }

        SetFlash(0f);
        _flashRoutine = null;
    }

    private void SetFlash(float amount)
    {
        _spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(FlashAmountId, amount);
        _spriteRenderer.SetPropertyBlock(_propertyBlock);
    }
}
