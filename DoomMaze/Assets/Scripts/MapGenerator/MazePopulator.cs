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

    [Tooltip("How many corner/hallway redirect attempts to make per room before " +
             "declaring the branch unsolvable.")]
    [SerializeField] [Range(0, 8)] private int maxRedirectAttempts = 4;

    [Tooltip("How many times to fully regenerate the map (new seed, new tree) when " +
             "a clean layout cannot be found. The last attempt always places rooms " +
             "even if some overlap, so a map is always produced.")]
    [SerializeField] [Range(0, 64)] private int maxFullRestarts = 35;

    // -------------------------------------------------------------------------
    // Internal layout types
    // -------------------------------------------------------------------------

    /// <summary>
    /// A resolved placement: the world transform for one room instance.
    /// </summary>
    private class PlacedRoom
    {
        public Vector3    Position;
        public Quaternion Rotation;
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
    /// MapGenerator's current tree. If the layout cannot be solved without overlaps,
    /// the map is fully regenerated (new seed, new tree) up to
    /// <see cref="maxFullRestarts"/> times. A map is always produced.
    /// </summary>
    public void Populate()
    {
        ClearSpawnedRooms();

        if (_generator == null)
            _generator = GetComponent<MapGenerator>();

        Transform parent = roomParent != null ? roomParent : transform;

        // Phase 1 — solve world-space layout, restarting from scratch when needed.
        bool solved = false;
        for (int attempt = 0; attempt <= maxFullRestarts; attempt++)
        {
            MapGenerator.RoomNode root = _generator.Root;
            if (root == null)
            {
                Debug.LogError("[MazePopulator] MapGenerator has no root node. " +
                               "Call MapGenerator.Generate() before Populate().");
                return;
            }

            _placedRooms.Clear();
            solved = SolveLayout(root, parentSocket: null);

            if (solved)
                break;

            if (attempt < maxFullRestarts)
            {
                Debug.Log($"[MazePopulator] Layout attempt {attempt + 1} could not resolve " +
                          "all overlaps — regenerating map from scratch.");
                _generator.Generate();
            }
        }

        if (!solved)
        {
            Debug.LogWarning("[MazePopulator] Could not produce a clean layout after " +
                             $"{maxFullRestarts + 1} attempts. Forcing placement of last attempt.");
            _placedRooms.Clear();
            SolveLayoutForced(_generator.Root, parentSocket: null);
        }

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

    /// <summary>
    /// Recursively resolves world-space placements for <paramref name="node"/> and
    /// all its descendants. Returns false if any room in the subtree could not be
    /// placed without overlapping an already-placed room, rolling back all placements
    /// made in this subtree so the caller can trigger a full restart.
    /// Before propagating failure, the method exhausts every available prefab for
    /// the conflicting node, maximising the chance of finding a fit without
    /// discarding the entire tree.
    /// </summary>
    private bool SolveLayout(MapGenerator.RoomNode node, MazeSocket? parentSocket)
    {
        List<MazePrefab> pool = GetPrefabPool(node);
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[MazePopulator] No prefab for {node.Type} " +
                             $"(connections: {node.ConnectionCount}). Skipping.");
            return true; // Config issue, not a layout failure.
        }

        // Shuffle the pool so every run explores prefab variants in a different order
        // without repetition, which avoids wasting retries on the same geometry.
        List<MazePrefab> shuffled = ShuffledCopy(pool);

        foreach (MazePrefab prefab in shuffled)
        {
            (Vector3 pos, Quaternion rot) = ComputeTransform(prefab, parentSocket);

            PlacedRoom placed = new()
            {
                Position = pos,
                Rotation = rot,
                Prefab   = prefab,
                Node     = node,
            };

            // If this prefab overlaps, try connector-based steering before moving on.
            if (Overlaps(placed))
            {
                if (maxRedirectAttempts <= 0 || parentSocket == null)
                    continue; // Try next prefab variant.

                PlacedRoom redirected = TryRedirect(node, parentSocket);
                if (redirected != null)
                    placed = redirected;
                else
                    continue; // No connector path cleared — try next prefab.
            }

            // Commit this room; record the index so children can roll it back.
            int committedAt = _placedRooms.Count;
            _placedRooms.Add(placed);

            // Resolve exit sockets in world space and recurse into children.
            IReadOnlyList<MazeSocket> exitSockets = placed.Prefab.ExitSockets;
            bool childrenSolved = true;
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

                if (!SolveLayout(node.Children[i], exitSocket))
                {
                    childrenSolved = false;
                    break;
                }
            }

            if (childrenSolved)
                return true;

            // This prefab choice caused a downstream failure — roll it back and try the next.
            _placedRooms.RemoveRange(committedAt, _placedRooms.Count - committedAt);
        }

        // All prefab variants exhausted — signal failure so the caller can restart.
        return false;
    }

    /// <summary>
    /// Unconditional layout pass used only as a last resort when all restarts are
    /// exhausted. Places rooms regardless of overlap — a map is always produced.
    /// </summary>
    private void SolveLayoutForced(MapGenerator.RoomNode node, MazeSocket? parentSocket)
    {
        MazePrefab prefab = PickPrefab(node);
        if (prefab == null) return;

        (Vector3 pos, Quaternion rot) = ComputeTransform(prefab, parentSocket);
        PlacedRoom placed = new()
        {
            Position = pos,
            Rotation = rot,
            Prefab   = prefab,
            Node     = node,
        };

        if (maxRedirectAttempts > 0 && Overlaps(placed))
        {
            PlacedRoom redirected = TryRedirect(node, parentSocket);
            if (redirected != null)
                placed = redirected;
            // Overlapping is acceptable here — we must produce a map.
        }

        _placedRooms.Add(placed);

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
            SolveLayoutForced(node.Children[i], exitSocket);
        }
    }

    /// <summary>
    /// Attempts to steer around an overlap by inserting one or two connector prefabs
    /// before the conflicting room. Exhausts all connector combinations and all room
    /// prefab variants at the redirected socket. Returns the successfully placed
    /// non-overlapping room (with <see cref="PlacedRoom.Node"/> set correctly), or
    /// null if all strategies fail.
    /// </summary>
    private PlacedRoom TryRedirect(
        MapGenerator.RoomNode node,
        MazeSocket? parentSocket)
    {
        if (parentSocket == null) return null;

        List<MazePrefab> roomPool = GetPrefabPool(node);
        if (roomPool == null || roomPool.Count == 0) return null;

        // Each strategy is a pair of connector pools; a null second pool means single-connector mode.
        var strategies = new (List<MazePrefab> first, List<MazePrefab> second)[]
        {
            (cornerLeftPrefabs,   cornerRightPrefabs),
            (cornerRightPrefabs,  cornerLeftPrefabs),
            (cornerLeftPrefabs,   cornerLeftPrefabs),
            (cornerRightPrefabs,  cornerRightPrefabs),
            (hallway2DoorPrefabs, cornerLeftPrefabs),
            (hallway2DoorPrefabs, cornerRightPrefabs),
            (cornerLeftPrefabs,   null),
            (cornerRightPrefabs,  null),
            (hallway2DoorPrefabs, null),
        };

        foreach (var (firstPool, secondPool) in strategies)
        {
            if (firstPool == null || firstPool.Count == 0) continue;

            foreach (MazePrefab first in ShuffledCopy(firstPool))
            {
                (Vector3 p1, Quaternion r1) = ComputeTransform(first, parentSocket);
                PlacedRoom connector1 = new()
                {
                    Position = p1,
                    Rotation = r1,
                    Prefab   = first,
                    Node     = null,
                };

                if (Overlaps(connector1)) continue;

                MazeSocket? bridgeSocket = WorldExitSocket(connector1, exitIndex: 0);
                if (bridgeSocket == null) continue;

                // --- Single-connector path ---
                if (secondPool == null || secondPool.Count == 0)
                {
                    PlacedRoom candidate = TryPlaceRoom(node, roomPool, bridgeSocket);
                    if (candidate != null)
                    {
                        _placedRooms.Add(connector1);
                        return candidate;
                    }
                    continue;
                }

                // --- Two-connector path ---
                foreach (MazePrefab second in ShuffledCopy(secondPool))
                {
                    (Vector3 p2, Quaternion r2) = ComputeTransform(second, bridgeSocket);
                    PlacedRoom connector2 = new()
                    {
                        Position = p2,
                        Rotation = r2,
                        Prefab   = second,
                        Node     = null,
                    };

                    if (Overlaps(connector2)) continue;

                    MazeSocket? targetSocket = WorldExitSocket(connector2, exitIndex: 0);
                    if (targetSocket == null) continue;

                    PlacedRoom candidate = TryPlaceRoom(node, roomPool, targetSocket);
                    if (candidate != null)
                    {
                        _placedRooms.Add(connector1);
                        _placedRooms.Add(connector2);
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Tries every prefab in <paramref name="pool"/> (in shuffled order) at
    /// <paramref name="socket"/> and returns the first one that fits without overlap,
    /// with <see cref="PlacedRoom.Node"/> set to <paramref name="node"/>.
    /// Returns null if nothing fits. Does NOT commit to <see cref="_placedRooms"/>.
    /// </summary>
    private PlacedRoom TryPlaceRoom(
        MapGenerator.RoomNode node,
        List<MazePrefab> pool,
        MazeSocket? socket)
    {
        foreach (MazePrefab prefab in ShuffledCopy(pool))
        {
            (Vector3 rp, Quaternion rr) = ComputeTransform(prefab, socket);
            PlacedRoom candidate = new()
            {
                Position = rp,
                Rotation = rr,
                Prefab   = prefab,
                Node     = node,
            };
            if (!Overlaps(candidate))
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

    /// <summary>Returns true if <paramref name="candidate"/> overlaps any already-placed room.</summary>
    private bool Overlaps(PlacedRoom candidate)
    {
        foreach (PlacedRoom placed in _placedRooms)
        {
            float threshold = Mathf.Max(candidate.Prefab.OverlapThreshold, placed.Prefab.OverlapThreshold);
            if (Vector3.Distance(candidate.Position, placed.Position) < threshold)
                return true;
        }
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

    /// <summary>
    /// Returns a new list containing the same elements as <paramref name="source"/>
    /// in a randomised order (Fisher-Yates shuffle). The original list is not modified.
    /// </summary>
    private static List<MazePrefab> ShuffledCopy(List<MazePrefab> source)
    {
        if (source == null) return new List<MazePrefab>();
        var copy = new List<MazePrefab>(source);
        for (int i = copy.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
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

    // -------------------------------------------------------------------------
    // Public query API
    // -------------------------------------------------------------------------

    /// <summary>A pair of overlapping rooms found in the current layout.</summary>
    public struct OverlapPair
    {
        public string RoomA;
        public string RoomB;
        public MapGenerator.RoomType TypeA;
        public MapGenerator.RoomType TypeB;
        public float Distance;
    }

    /// <summary>Total number of rooms (including injected connectors) in the current layout.</summary>
    public int PlacedRoomCount => _placedRooms.Count;

    /// <summary>
    /// Returns true if at least one placed room has the given node type and its
    /// prefab name contains <paramref name="prefabNameContains"/> (case-insensitive).
    /// </summary>
    public bool HasPlacedRoomOfType(MapGenerator.RoomType type, string prefabNameContains)
    {
        foreach (PlacedRoom placed in _placedRooms)
        {
            if (placed.Node == null || placed.Node.Type != type) continue;
            if (placed.Prefab == null) continue;
            if (placed.Prefab.name.IndexOf(prefabNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if every placed room with the given node type has a prefab name
    /// that contains <paramref name="prefabNameContains"/> (case-insensitive).
    /// Also returns false when no rooms of that type exist.
    /// </summary>
    public bool AllPlacedRoomsOfTypeMatch(MapGenerator.RoomType type, string prefabNameContains)
    {
        bool found = false;
        foreach (PlacedRoom placed in _placedRooms)
        {
            if (placed.Node == null || placed.Node.Type != type) continue;
            if (placed.Prefab == null) return false;
            if (placed.Prefab.name.IndexOf(prefabNameContains, System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// Returns the number of placed rooms with the given node type whose prefab name
    /// does NOT contain <paramref name="prefabNameContains"/> (case-insensitive).
    /// </summary>
    public int CountPlacedRoomTypeMismatch(MapGenerator.RoomType type, string prefabNameContains)
    {
        int count = 0;
        foreach (PlacedRoom placed in _placedRooms)
        {
            if (placed.Node == null || placed.Node.Type != type) continue;
            if (placed.Prefab == null) { count++; continue; }
            if (placed.Prefab.name.IndexOf(prefabNameContains, System.StringComparison.OrdinalIgnoreCase) < 0)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Returns every pair of spawned rooms whose pivot positions are closer than
    /// <paramref name="distanceThreshold"/>. Works reliably when all rooms share a
    /// consistent size and socket layout.
    /// </summary>
    public List<OverlapPair> FindOverlaps(float distanceThreshold)
    {
        var results = new List<OverlapPair>();
        int count = Mathf.Min(_spawnedRooms.Count, _placedRooms.Count);

        for (int i = 0; i < count; i++)
        {
            if (_spawnedRooms[i] == null) continue;
            Vector3 posA = _spawnedRooms[i].transform.position;

            for (int j = i + 1; j < count; j++)
            {
                if (_spawnedRooms[j] == null) continue;

                float dist = Vector3.Distance(posA, _spawnedRooms[j].transform.position);
                if (dist >= distanceThreshold) continue;

                results.Add(new OverlapPair
                {
                    RoomA    = _placedRooms[i].Prefab != null ? _placedRooms[i].Prefab.name : "Unknown",
                    RoomB    = _placedRooms[j].Prefab != null ? _placedRooms[j].Prefab.name : "Unknown",
                    TypeA    = _placedRooms[i].Node   != null ? _placedRooms[i].Node.Type   : MapGenerator.RoomType.Hallway,
                    TypeB    = _placedRooms[j].Node   != null ? _placedRooms[j].Node.Type   : MapGenerator.RoomType.Hallway,
                    Distance = dist,
                });
            }
        }

        return results;
    }
}
