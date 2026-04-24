#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Editor-only component that draws the NavMeshAgent's current path in the
/// Scene view each frame using <see cref="Debug.DrawLine"/>. Zero runtime cost
/// in release builds — the entire class is stripped by the preprocessor.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshPathVisualizer : MonoBehaviour
{
    private NavMeshAgent _agent;
    private NavMeshPath  _path;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _path  = new NavMeshPath();
    }

    private void Update()
    {
        if (_agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return;

        _agent.CalculatePath(_agent.destination, _path);

        if (_path.status == NavMeshPathStatus.PathInvalid) return;

        Vector3[] corners = _path.corners;
        for (int i = 0; i < corners.Length - 1; i++)
            Debug.DrawLine(corners[i], corners[i + 1], Color.cyan);
    }
}
#endif
