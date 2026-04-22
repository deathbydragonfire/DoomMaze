using UnityEngine;

/// <summary>
/// Owns a world-space <see cref="LineRenderer"/> with two points — muzzle (0) and target (1).
/// Attach to a child of ViewmodelRoot that is NOT on the Viewmodel layer so the main
/// camera renders it in world space.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class GrappleTether : MonoBehaviour
{
    private LineRenderer _lineRenderer;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();

        if (_lineRenderer == null)
        {
            Debug.LogWarning("[GrappleTether] No LineRenderer found on this GameObject.");
            return;
        }

        _lineRenderer.positionCount = 2;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.enabled       = false;
    }

    /// <summary>Makes the tether visible, setting point 0 to <paramref name="muzzle"/> and point 1 to <paramref name="target"/>.</summary>
    public void ShowTether(Vector3 muzzle, Vector3 target)
    {
        if (_lineRenderer == null) return;

        _lineRenderer.SetPosition(0, muzzle);
        _lineRenderer.SetPosition(1, target);
        _lineRenderer.enabled = true;
    }

    /// <summary>Updates point 1 every frame during <c>EnemyHooked</c> state. Tether must already be visible.</summary>
    public void UpdateTarget(Vector3 target)
    {
        if (_lineRenderer == null) return;
        _lineRenderer.SetPosition(1, target);
    }

    /// <summary>Hides the <see cref="LineRenderer"/>.</summary>
    public void HideTether()
    {
        if (_lineRenderer == null) return;
        _lineRenderer.enabled = false;
    }
}
