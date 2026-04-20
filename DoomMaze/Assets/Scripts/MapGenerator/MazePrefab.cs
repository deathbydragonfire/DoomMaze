using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds a socket definition for a maze room prefab.
/// Both vectors are expressed in the prefab's local space.
/// </summary>
[System.Serializable]
public struct MazeSocket
{
    [Tooltip("Local-space position of the connection point.")]
    public Vector3 Position;

    [Tooltip("Local-space outward-facing direction of the doorway.")]
    public Vector3 Forward;
}

/// <summary>
/// Attached to every maze room/hallway prefab. Declares the socket connection
/// points used by MazePopulator to chain rooms together.
///
/// Socket index 0 is always the entry socket. Remaining sockets (1…N-1) are
/// exit sockets that child rooms attach to.
/// </summary>
public class MazePrefab : MonoBehaviour
{
    [Tooltip("Socket index 0 = entry. Remaining indices = exits.")]
    [SerializeField] private List<MazeSocket> sockets = new();

    /// <summary>All declared sockets on this prefab.</summary>
    public IReadOnlyList<MazeSocket> Sockets => sockets;

    /// <summary>The entry socket (index 0).</summary>
    public MazeSocket EntrySocket => sockets[0];

    /// <summary>All exit sockets (indices 1…N-1).</summary>
    public IReadOnlyList<MazeSocket> ExitSockets
    {
        get
        {
            if (sockets.Count <= 1)
                return System.Array.Empty<MazeSocket>();
            return sockets.GetRange(1, sockets.Count - 1);
        }
    }

    /// <summary>Returns true if at least one socket is defined.</summary>
    public bool HasEntrySocket => sockets.Count > 0;

    /// <summary>Total number of declared sockets, including the entry socket.</summary>
    public int SocketCount => sockets.Count;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Matrix4x4 localToWorld = transform.localToWorldMatrix;

        for (int i = 0; i < sockets.Count; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(sockets[i].Position);
            Vector3 worldFwd = localToWorld.MultiplyVector(sockets[i].Forward).normalized;
            Color color = i == 0 ? Color.green : Color.cyan;

            DrawCross(worldPos, color);

            if (worldFwd == Vector3.zero)
                worldFwd = transform.forward;

            DrawArrow(worldPos, worldFwd, color);

            UnityEditor.Handles.Label(
                worldPos + Vector3.up * 0.3f,
                i == 0 ? "Entry" : $"Exit {i}");
        }
    }

    private static void DrawCross(Vector3 center, Color color)
    {
        const float halfSize = 0.12f;
        Gizmos.color = color;
        Gizmos.DrawLine(center - Vector3.right   * halfSize, center + Vector3.right   * halfSize);
        Gizmos.DrawLine(center - Vector3.up      * halfSize, center + Vector3.up      * halfSize);
        Gizmos.DrawLine(center - Vector3.forward * halfSize, center + Vector3.forward * halfSize);
    }

    private static void DrawArrow(Vector3 origin, Vector3 direction, Color color)
    {
        const float shaftLength = 0.8f;
        const float headLength  = 0.25f;
        const float headAngle   = 25f;

        Gizmos.color = color;

        Vector3 tip = origin + direction * shaftLength;
        Gizmos.DrawLine(origin, tip);

        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        if (right == Vector3.zero)
            right = Vector3.Cross(direction, Vector3.forward).normalized;

        Vector3 up = Vector3.Cross(right, direction).normalized;

        Quaternion leftRot  = Quaternion.AngleAxis( headAngle, up);
        Quaternion rightRot = Quaternion.AngleAxis(-headAngle, up);
        Quaternion upRot    = Quaternion.AngleAxis( headAngle, right);
        Quaternion downRot  = Quaternion.AngleAxis(-headAngle, right);

        Gizmos.DrawLine(tip, tip - leftRot  * direction * headLength);
        Gizmos.DrawLine(tip, tip - rightRot * direction * headLength);
        Gizmos.DrawLine(tip, tip - upRot    * direction * headLength);
        Gizmos.DrawLine(tip, tip - downRot  * direction * headLength);
    }
#endif
}
