using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Consumes the tree produced by MapGenerator and instantiates room prefabs
/// into the scene, attempting to avoid overlaps by inserting corner or straight
/// hallway connectors to steer around already-placed rooms.
/// </summary>
[RequireComponent(typeof(MapGenerator))]
public class MazePopulator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Serialized prefab lists
    // -------------------------------------------------------------------------

    [Header("Room Prefabs")]
    [SerializeField] private List<MazePrefab> startPrefabs       = new();
    [SerializeField] private List<MazePrefab> enemyRoomPrefabs   = new();
    [SerializeField] private List<MazePrefab> upgradeRoomPrefabs = new();
    [SerializeField] private List<MazePrefab> bossRoomPrefabs    = new();
    [SerializeField] private List<MazePrefab> exitRoomPrefabs    = new();
    [SerializeField] private List<MazePrefab> deadEndPrefabs     = new();

    [Header("Hallway Prefabs — by Door Count")]
    [SerializeField] private List<MazePrefab> hallway2DoorPrefabs = new();
    [SerializeField] private List<MazePrefab> hallway3DoorPrefabs = new();
    [SerializeField] private List<MazePrefab> hallway4DoorPrefabs = new();

    [Header("Corner Prefabs")]
    [Tooltip("Left-turn corner prefabs (entry socket 0, exit socket 1 turns left).")]
    [SerializeField] private List<MazePrefab> cornerLeftPrefabs  = new();
    [Tooltip("Right-turn corner prefabs (entry socket 0, exit socket 1 turns right).")]
    [SerializeField] private List<MazePrefab> cornerRightPrefabs = new();

    [Header("Population Settings")]
    [Tooltip("Root transform under which all instantiated rooms are placed. " +
             "Defaults to this transform if left empty.")]
    [SerializeField] private Transform roomParent;

    [Tooltip("World-space size used for AABB overlap testing when a prefab has no Renderer or Collider. " +
             "Set this to match the footprint of your room meshes.")]
    [SerializeField] private Vector3 fallbackRoomSize = new(10f, 4f, 10f);

    [Tooltip("How many corner/hallway redirect attempts to make before giving up " +
             "and placing a room even if it overlaps.")]
    [SerializeField] [Range(0, 8)] private int maxRedirectAttempts = 4;

    [Tooltip("Padding added around each room's AABB for overlap testing.")]
    [SerializeField] private float overlapPadding = 0.1f;

    // -------------------------------------------------------------------------
    // Internal layout types
    // -------------------------------------------------------------------------

    /// <summary>Axis-aligned bounding box used for overlap detection.</summary>
    private readonly struct RoomBounds
    {
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public RoomBounds(Vector3 center, Vector3 size)
        {
            Min = center - size * 0.5f;
            Max = center + size * 0.5f;
        }

        public bool Overlaps(RoomBounds other, float padding)
        {
            return Min.x - padding < other.Max.x && Max.x + padding > other.Min.x &&
                   Min.y - padding < other.Max.y && Max.y + padding > other.Min.y &&
                   Min.z - padding < other.Max.z && Max.z + padding > other.Min.z;
        }
    }

    /// <summary>
    /// A resolved placement: the world transform and bounds for one room instance.
    /// </summary>
    private class PlacedRoom
    {
        public Vector3    Position;
        public Quaternion Rotation;
        public RoomBounds Bounds;
        public MazePrefab Prefab;
        public MapGenerator.RoomNode Node; // null for injected connectors
    }

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private MapGenerator              _generator;
    private readonly List<GameObject> _spawnedRooms = new();
    private readonly List<PlacedRoom> _placedRooms  = new();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake() => _generator = GetComponent<MapGenerator>();
    private void Start() => Populate();

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clears any previously spawned rooms and repopulates the scene from the
    /// MapGenerator's current tree.
    /// </summary>
    public void Populate()
    {
        ClearSpawnedRooms();

        if (_generator == null)
            _generator = GetComponent<MapGenerator>();

        MapGenerator.RoomNode root = _generator.Root;
        if (root == null)
        {
            Debug.LogError("[MazePopulator] MapGenerator has no root node. " +
                           "Call MapGenerator.Generate() before Populate().");
            return;
        }

        Transform parent = roomParent != null ? roomParent : transform;

        // Phase 1 — solve world-space layouts, inserting connectors to avoid overlaps.
        _placedRooms.Clear();
        SolveLayout(root, parentSocket: null);

        // Phase 2 — instantiate everything.
        foreach (PlacedRoom placed in _placedRooms)
            InstantiateRoom(placed, parent);
    }

    /// <summary>Destroys all rooms spawned by this populator.</summary>
    public void ClearSpawnedRooms()
    {
        foreach (GameObject room in _spawnedRooms)
            if (room != null) DestroyImmediate(room);

        _spawnedRooms.Clear();
        _placedRooms.Clear();
    }

    // -------------------------------------------------------------------------
    // Phase 1 — layout solving
    // -------------------------------------------------------------------------

    private void SolveLayout(MapGenerator.RoomNode node, MazeSocket? parentSocket)
    {
        MazePrefab prefab = PickPrefab(node);
        if (prefab == null)
        {
            Debug.LogWarning($"[MazePopulator] No prefab for {node.Type} " +
                             $"(connections: {node.ConnectionCount}). Skipping.");
            return;
        }

        (Vector3 pos, Quaternion rot) = ComputeTransform(prefab, parentSocket);

        PlacedRoom placed = new()
        {
            Position = pos,
            Rotation = rot,
            Bounds   = ComputeBounds(prefab, pos, rot),
            Prefab   = prefab,
            Node     = node,
        };

        // Check overlap; if found, try inserting redirecting connectors.
        if (maxRedirectAttempts > 0 && Overlaps(placed))
        {
            PlacedRoom redirected = TryRedirect(node, parentSocket, maxRedirectAttempts);
            if (redirected != null)
                placed = redirected;
            // If all redirects fail, fall through with the original overlapping placement.
        }

        _placedRooms.Add(placed);

        // Resolve exit sockets in world space and recurse into children.
        IReadOnlyList<MazeSocket> exitSockets = placed.Prefab.ExitSockets;
        for (int i = 0; i < node.Children.Count; i++)
        {
            MazeSocket? exitSocket = null;
            if (i < exitSockets.Count)
            {
                MazeSocket local = exitSockets[i];
                exitSocket = new MazeSocket
                {
                    Position = placed.Position + placed.Rotation * local.Position,
                    Forward  = (placed.Rotation * local.Forward).normalized,
                };
            }
            else
            {
                Debug.LogWarning($"[MazePopulator] {placed.Prefab.name} has fewer exit sockets " +
                                 $"than children. Child {i} placed at room origin.");
            }

            SolveLayout(node.Children[i], exitSocket);
        }
    }

    /// <summary>
    /// Attempts to insert one or two connector prefabs (corner or straight hallway)
    /// before the conflicting room, steering the chain to avoid the overlap.
    /// Returns the successfully placed non-overlapping PlacedRoom, or null if all
    /// strategies fail.
    /// </summary>
    private PlacedRoom TryRedirect(
        MapGenerator.RoomNode node,
        MazeSocket? parentSocket,
        int attemptsLeft)
    {
        if (attemptsLeft <= 0 || parentSocket == null)
            return null;

        // Each strategy is a pair of connector pools to try inserting in sequence.
        var strategies = new[]
        {
            (cornerLeftPrefabs,   cornerRightPrefabs),
            (cornerRightPrefabs,  cornerLeftPrefabs),
            (hallway2DoorPrefabs, cornerLeftPrefabs),
            (hallway2DoorPrefabs, cornerRightPrefabs),
        };

        foreach (var (firstPool, secondPool) in strategies)
        {
            MazePrefab first = PickFromPool(firstPool);
            if (first == null) continue;

            (Vector3 p1, Quaternion r1) = ComputeTransform(first, parentSocket);
            PlacedRoom connector1 = new()
            {
                Position = p1,
                Rotation = r1,
                Bounds   = ComputeBounds(first, p1, r1),
                Prefab   = first,
                Node     = null,
            };

            if (Overlaps(connector1)) continue;

            MazeSocket? bridgeSocket = WorldExitSocket(connector1, exitIndex: 0);
            if (bridgeSocket == null) continue;

            // Optionally insert a second connector for more steering.
            MazeSocket? targetSocket = bridgeSocket;
            PlacedRoom connector2    = null;

            MazePrefab second = PickFromPool(secondPool);
            if (second != null)
            {
                (Vector3 p2, Quaternion r2) = ComputeTransform(second, bridgeSocket);
                connector2 = new()
                {
                    Position = p2,
                    Rotation = r2,
                    Bounds   = ComputeBounds(second, p2, r2),
                    Prefab   = second,
                    Node     = null,
                };

                if (Overlaps(connector2))
                    connector2 = null;
                else
                    targetSocket = WorldExitSocket(connector2, exitIndex: 0);
            }

            // Test the actual room at the redirected socket.
            MazePrefab roomPrefab = PickPrefab(node);
            if (roomPrefab == null) continue;

            (Vector3 rp, Quaternion rr) = ComputeTransform(roomPrefab, targetSocket);
            PlacedRoom candidate = new()
            {
                Position = rp,
                Rotation = rr,
                Bounds   = ComputeBounds(roomPrefab, rp, rr),
                Prefab   = roomPrefab,
                Node     = node,
            };

            if (Overlaps(candidate)) continue;

            // Success — commit the connectors before returning the room.
            _placedRooms.Add(connector1);
            if (connector2 != null)
                _placedRooms.Add(connector2);

            return candidate;
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Phase 2 — instantiation
    // -------------------------------------------------------------------------

    private void InstantiateRoom(PlacedRoom placed, Transform parent)
    {
        GameObject instance = Object.Instantiate(placed.Prefab.gameObject, parent);
        instance.transform.SetPositionAndRotation(placed.Position, placed.Rotation);
        _spawnedRooms.Add(instance);
    }

    // -------------------------------------------------------------------------
    // Geometry helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes the world position and rotation that aligns a prefab's entry socket
    /// with the given parent socket. Returns (origin, identity) for the root room.
    /// </summary>
    private static (Vector3 position, Quaternion rotation) ComputeTransform(
        MazePrefab prefab,
        MazeSocket? parentSocket)
    {
        if (parentSocket == null || !prefab.HasEntrySocket)
            return (Vector3.zero, Quaternion.identity);

        MazeSocket parent = parentSocket.Value;
        MazeSocket entry  = prefab.EntrySocket;

        Vector3 entryFwdNorm = entry.Forward.normalized;
        Quaternion entryLocalRot = entryFwdNorm != Vector3.zero
            ? Quaternion.LookRotation(entryFwdNorm, Vector3.up)
            : Quaternion.identity;

        Quaternion rotation = Quaternion.LookRotation(-parent.Forward, Vector3.up)
                              * Quaternion.Inverse(entryLocalRot);

        Vector3 entryWorldOffset = rotation * entry.Position;
        Vector3 position         = parent.Position - entryWorldOffset;

        return (position, rotation);
    }

    /// <summary>
    /// Returns the world-space exit socket at <paramref name="exitIndex"/> for an
    /// already-placed room, or null if the index is out of range.
    /// </summary>
    private static MazeSocket? WorldExitSocket(PlacedRoom placed, int exitIndex)
    {
        IReadOnlyList<MazeSocket> exits = placed.Prefab.ExitSockets;
        if (exitIndex >= exits.Count) return null;

        MazeSocket local = exits[exitIndex];
        return new MazeSocket
        {
            Position = placed.Position + placed.Rotation * local.Position,
            Forward  = (placed.Rotation * local.Forward).normalized,
        };
    }

    /// <summary>
    /// Computes a world-space AABB by reading the combined renderer bounds on the prefab,
    /// then rotating and translating them to the placed position/rotation.
    /// Falls back to collider bounds, then to <see cref="fallbackRoomSize"/>.
    /// </summary>
    private RoomBounds ComputeBounds(MazePrefab prefab, Vector3 position, Quaternion rotation)
    {
        // Try to get a size estimate from renderers on the prefab asset.
        Vector3 localSize = GetPrefabLocalSize(prefab);

        // Rotate the local extents into world space (takes the largest axis-aligned projection).
        Vector3 worldSize = RotateExtents(localSize * 0.5f, rotation) * 2f;

        return new RoomBounds(position, worldSize);
    }

    /// <summary>
    /// Returns the local-space combined bounds size of all renderers on the prefab,
    /// falling back to colliders, then to <see cref="fallbackRoomSize"/>.
    /// </summary>
    private Vector3 GetPrefabLocalSize(MazePrefab prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].localBounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].localBounds);
            return combined.size;
        }

        Collider[] colliders = prefab.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            // Collider.bounds is world-space on instances; for prefabs use BoxCollider.size directly.
            if (colliders[0] is BoxCollider box)
                return box.size;
        }

        return fallbackRoomSize;
    }

    /// <summary>
    /// Converts local half-extents through a rotation to get world-space half-extents
    /// (the axis-aligned bounding box of a rotated box).
    /// </summary>
    private static Vector3 RotateExtents(Vector3 halfExtents, Quaternion rotation)
    {
        Matrix4x4 m = Matrix4x4.Rotate(rotation);
        return new Vector3(
            Mathf.Abs(m.m00) * halfExtents.x + Mathf.Abs(m.m01) * halfExtents.y + Mathf.Abs(m.m02) * halfExtents.z,
            Mathf.Abs(m.m10) * halfExtents.x + Mathf.Abs(m.m11) * halfExtents.y + Mathf.Abs(m.m12) * halfExtents.z,
            Mathf.Abs(m.m20) * halfExtents.x + Mathf.Abs(m.m21) * halfExtents.y + Mathf.Abs(m.m22) * halfExtents.z
        );
    }

    /// <summary>Returns true if <paramref name="candidate"/> overlaps any already-placed room.</summary>
    private bool Overlaps(PlacedRoom candidate)
    {
        foreach (PlacedRoom placed in _placedRooms)
            if (candidate.Bounds.Overlaps(placed.Bounds, overlapPadding))
                return true;
        return false;
    }

    // -------------------------------------------------------------------------
    // Prefab selection
    // -------------------------------------------------------------------------

    private MazePrefab PickPrefab(MapGenerator.RoomNode node)
        => PickFromPool(GetPrefabPool(node));

    private static MazePrefab PickFromPool(List<MazePrefab> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    private List<MazePrefab> GetPrefabPool(MapGenerator.RoomNode node)
    {
        return node.Type switch
        {
            MapGenerator.RoomType.Start   => startPrefabs,
            MapGenerator.RoomType.Enemy   => enemyRoomPrefabs,
            MapGenerator.RoomType.Upgrade => upgradeRoomPrefabs,
            MapGenerator.RoomType.Boss    => bossRoomPrefabs,
            MapGenerator.RoomType.Exit    => exitRoomPrefabs,
            MapGenerator.RoomType.DeadEnd => deadEndPrefabs,
            MapGenerator.RoomType.Corner  => CombinedCornerPool(),
            MapGenerator.RoomType.Hallway => GetHallwayPool(node.ConnectionCount),
            _                             => null,
        };
    }

    private List<MazePrefab> CombinedCornerPool()
    {
        var combined = new List<MazePrefab>(cornerLeftPrefabs.Count + cornerRightPrefabs.Count);
        combined.AddRange(cornerLeftPrefabs);
        combined.AddRange(cornerRightPrefabs);
        return combined;
    }

    private List<MazePrefab> GetHallwayPool(int connectionCount)
    {
        return connectionCount switch
        {
            2 => hallway2DoorPrefabs,
            3 => hallway3DoorPrefabs,
            4 => hallway4DoorPrefabs,
            _ => hallway2DoorPrefabs,
        };
    }
}
