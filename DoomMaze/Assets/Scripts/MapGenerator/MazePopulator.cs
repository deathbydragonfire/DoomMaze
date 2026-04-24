using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Consumes the tree produced by MapGenerator and instantiates room prefabs
/// into the scene, attempting to avoid overlaps by inserting corner or straight
/// hallway connectors to steer around already-placed rooms.
/// </summary>
[RequireComponent(typeof(MapGenerator))]
public class MazePopulator : MonoBehaviour
{
    private const string MeleeEnemyPrefabPath = "Assets/Prefabs/Enemies/New Enemy Prefabs/Enemy_MeleeGrunt_NEw.prefab";
    private const string RangedEnemyPrefabPath = "Assets/Prefabs/Enemies/New Enemy Prefabs/Enemy_RangedGrunt New.prefab";
    private const string GeneratedDoorPrefabPath = "Assets/Prefabs/World/Door.prefab";
    private const string RoomSplashFontPath = "Assets/Fonts/Unutterable_Font_1_07/TrueType (.ttf)/Unutterable-Regular SDF 1.asset";

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

    [Header("Procedural Enemy Rooms")]
    [SerializeField] private GameObject meleeEnemyPrefab;
    [SerializeField] private GameObject rangedEnemyPrefab;
    [SerializeField] private Door generatedDoorPrefab;
    [SerializeField] [Range(0f, 1f)] private float waveSurvivalRoomChance = 0.3f;
    [SerializeField] private Vector3 generatedDoorScale = new(8f, 5f, 0.5f);
    [SerializeField] private float generatedDoorVerticalOffset = 2.5f;

    [Header("Room Splash")]
    [SerializeField] private TMP_FontAsset roomSplashFont;

    [Header("Eliminate All Defaults")]
    [SerializeField] private int eliminateEnemyCount = 18;
    [SerializeField] private float eliminateSpawnDuration = 8f;
    [SerializeField] [Range(0f, 1f)] private float eliminateMeleeWeight = 0.85f;

    [Header("Wave Survival Defaults")]
    [SerializeField] private float waveDuration = 30f;
    [SerializeField] private float waveSpawnInterval = 0.9f;
    [SerializeField] private int waveMaxAlive = 14;
    [SerializeField] [Range(0f, 1f)] private float waveMeleeWeight = 0.75f;

    [Header("Upgrade Rooms")]
    [SerializeField] private UpgradeDatabase upgradeDatabase;
    [SerializeField] private UpgradePickup upgradePickupPrefab;
    [SerializeField] private Vector3[] upgradeChoiceLocalOffsets =
    {
        new Vector3(-3f, 1.2f, 0f),
        new Vector3(0f, 1.2f, 0f),
        new Vector3(3f, 1.2f, 0f),
    };
    [SerializeField] private AudioClip upgradeRoomMusic;
    [SerializeField] [Range(0f, 1f)] private float upgradeRoomMusicVolume = 0.85f;
    [SerializeField] private float upgradeRoomMusicFadeOutDuration = 0.45f;
    [SerializeField] private float upgradeRoomMusicFadeInDuration = 0.45f;
    [SerializeField] private float upgradeRoomMusicFadeOutOnExitDuration = 0.35f;

    [Header("Runtime NavMesh")]
    [SerializeField] private bool buildRuntimeNavMesh = false;
    [SerializeField] private LayerMask navMeshLayerMask = ~0;

    [Header("Player Start")]
    [SerializeField] private bool placePlayerInStartRoom = true;
    [SerializeField] private Vector3 playerStartLocalOffset = new(0f, -8f, 0f);
    [SerializeField] private float playerStartNavMeshSearchRadius = 6f;
    [SerializeField] private float playerStartGroundOffset = 1f;

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
    private readonly List<(GameObject instance, PlacedRoom placed)> _spawnedRoomRecords = new();
    private NavMeshSurface _runtimeNavMeshSurface;

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

        ResolveProceduralRoomReferences();
        BuildRuntimeNavMesh();
        ConfigureGeneratedEnemyRooms();
        ConfigureGeneratedUpgradeRooms();
        if (PlacePlayerAtStartRoom(out Vector3 playerStartPosition))
            ArmEnemyRoomsFromPlayerStart(playerStartPosition);
        else
            ArmEnemyRoomsFromCurrentPlayer();
    }

    /// <summary>Destroys all rooms spawned by this populator.</summary>
    public void ClearSpawnedRooms()
    {
        foreach (GameObject room in _spawnedRooms)
            if (room != null) DestroyImmediate(room);

        _spawnedRooms.Clear();
        _spawnedRoomRecords.Clear();
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

            // Capture the rollback index BEFORE any redirect, so that connector
            // rooms committed by TryRedirect are also removed on downstream failure.
            int committedAt = _placedRooms.Count;

            // The direct parent of this room is the last room committed before this
            // iteration — skip it in the overlap check since they are intentionally adjacent.
            int parentRoomIndex = committedAt - 1;

            // If this prefab overlaps, try connector-based steering before moving on.
            if (Overlaps(placed, parentRoomIndex))
            {
                if (maxRedirectAttempts <= 0 || parentSocket == null)
                    continue; // Try next prefab variant.

                PlacedRoom redirected = TryRedirect(node, parentSocket, parentRoomIndex);
                if (redirected != null)
                    placed = redirected;
                else
                    continue; // No connector path cleared — try next prefab.
            }

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

        if (maxRedirectAttempts > 0 && Overlaps(placed, parentIndex: _placedRooms.Count - 1))
        {
            PlacedRoom redirected = TryRedirect(node, parentSocket, parentRoomIndex: _placedRooms.Count - 1);
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
        MazeSocket? parentSocket,
        int parentRoomIndex = -1)
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

                // The connector is directly connected to the parent — skip the parent in its check.
                if (Overlaps(connector1, parentRoomIndex)) continue;

                MazeSocket? bridgeSocket = WorldExitSocket(connector1, exitIndex: 0);
                if (bridgeSocket == null) continue;

                // --- Single-connector path ---
                if (secondPool == null || secondPool.Count == 0)
                {
                    // The room candidate's parent is connector1, which isn't committed yet —
                    // pass -1 and instead skip connector1 by index after it's added temporarily.
                    PlacedRoom candidate = TryPlaceRoom(node, roomPool, bridgeSocket, skipIndex: -1);
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

                    // connector2's parent is connector1 — skip it by position comparison
                    // since it isn't committed yet. Use parentRoomIndex to still skip the
                    // original parent of the whole redirect chain.
                    if (Overlaps(connector2, parentRoomIndex)) continue;

                    MazeSocket? targetSocket = WorldExitSocket(connector2, exitIndex: 0);
                    if (targetSocket == null) continue;

                    PlacedRoom candidate = TryPlaceRoom(node, roomPool, targetSocket, skipIndex: -1);
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
    /// <paramref name="skipIndex"/> is forwarded to <see cref="Overlaps"/> to exclude
    /// the direct parent room from the check. Pass -1 to check against all placed rooms.
    /// Returns null if nothing fits. Does NOT commit to <see cref="_placedRooms"/>.
    /// </summary>
    private PlacedRoom TryPlaceRoom(
        MapGenerator.RoomNode node,
        List<MazePrefab> pool,
        MazeSocket? socket,
        int skipIndex = -1)
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
            if (!Overlaps(candidate, skipIndex))
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
        _spawnedRoomRecords.Add((instance, placed));

    }

    private void BuildRuntimeNavMesh()
    {
        if (!buildRuntimeNavMesh)
            return;

        if (_runtimeNavMeshSurface == null)
        {
            _runtimeNavMeshSurface = GetComponent<NavMeshSurface>();
            if (_runtimeNavMeshSurface == null)
                _runtimeNavMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        }

        _runtimeNavMeshSurface.collectObjects = CollectObjects.Children;
        _runtimeNavMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        _runtimeNavMeshSurface.layerMask = navMeshLayerMask;
        _runtimeNavMeshSurface.ignoreNavMeshAgent = true;
        _runtimeNavMeshSurface.ignoreNavMeshObstacle = true;
        _runtimeNavMeshSurface.BuildNavMesh();
    }

    private void ResolveProceduralRoomReferences()
    {
#if UNITY_EDITOR
        if (meleeEnemyPrefab == null)
            meleeEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeleeEnemyPrefabPath);

        if (rangedEnemyPrefab == null)
            rangedEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RangedEnemyPrefabPath);

        if (generatedDoorPrefab == null)
            generatedDoorPrefab = AssetDatabase.LoadAssetAtPath<Door>(GeneratedDoorPrefabPath);

        if (roomSplashFont == null)
            roomSplashFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RoomSplashFontPath);
#endif
    }

    private void ConfigureGeneratedEnemyRooms()
    {
        foreach ((GameObject instance, PlacedRoom placed) in _spawnedRoomRecords)
        {
            if (instance == null || placed.Node == null || placed.Node.Type != MapGenerator.RoomType.Enemy)
                continue;

            EnemyRoomController controller = instance.GetComponent<EnemyRoomController>();
            if (controller == null)
                controller = instance.AddComponent<EnemyRoomController>();

            EnemyRoomObjective objective = Random.value < waveSurvivalRoomChance
                ? EnemyRoomObjective.WaveSurvival
                : EnemyRoomObjective.EliminateAll;

            controller.Configure(
                placed.Prefab,
                objective,
                meleeEnemyPrefab,
                rangedEnemyPrefab,
                generatedDoorPrefab,
                generatedDoorScale,
                generatedDoorVerticalOffset,
                roomSplashFont,
                eliminateEnemyCount,
                eliminateSpawnDuration,
                eliminateMeleeWeight,
                waveDuration,
                waveSpawnInterval,
                waveMaxAlive,
                waveMeleeWeight);
        }
    }

    private void ConfigureGeneratedUpgradeRooms()
    {
        foreach ((GameObject instance, PlacedRoom placed) in _spawnedRoomRecords)
        {
            if (instance == null || placed.Node == null || placed.Node.Type != MapGenerator.RoomType.Upgrade)
                continue;

            UpgradeRoomController controller = instance.GetComponent<UpgradeRoomController>();
            if (controller == null)
                controller = instance.AddComponent<UpgradeRoomController>();

            controller.Configure(upgradeDatabase, upgradePickupPrefab, upgradeChoiceLocalOffsets);
            controller.ConfigureMusic(
                upgradeRoomMusic,
                upgradeRoomMusicVolume,
                upgradeRoomMusicFadeOutDuration,
                upgradeRoomMusicFadeInDuration,
                upgradeRoomMusicFadeOutOnExitDuration);
        }
    }

    private bool PlacePlayerAtStartRoom(out Vector3 playerStartPosition)
    {
        playerStartPosition = default;

        if (!placePlayerInStartRoom)
            return false;

        foreach ((GameObject instance, PlacedRoom placed) in _spawnedRoomRecords)
        {
            if (instance == null || placed.Node == null || placed.Node.Type != MapGenerator.RoomType.Start)
                continue;

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                return false;

            Vector3 startPosition = instance.transform.TransformPoint(playerStartLocalOffset);
            if (TrySampleNavMeshInsideRoom(instance, startPosition, playerStartNavMeshSearchRadius, out Vector3 sampledStart))
                startPosition = sampledStart + Vector3.up * playerStartGroundOffset;

            Quaternion startRotation = instance.transform.rotation;

            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (movement != null)
                movement.TeleportTo(startPosition, startRotation);
            else
                player.transform.SetPositionAndRotation(startPosition, startRotation);

            playerStartPosition = startPosition;
            return true;
        }

        return false;
    }

    private void ArmEnemyRoomsFromPlayerStart(Vector3 playerStartPosition)
    {
        foreach ((GameObject instance, PlacedRoom placed) in _spawnedRoomRecords)
        {
            if (instance == null || placed.Node == null || placed.Node.Type != MapGenerator.RoomType.Enemy)
                continue;

            EnemyRoomController controller = instance.GetComponent<EnemyRoomController>();
            if (controller != null)
                controller.ArmFromPlayerStart(playerStartPosition);
        }
    }

    private void ArmEnemyRoomsFromCurrentPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        ArmEnemyRoomsFromPlayerStart(player.transform.position);
    }

    private static bool TrySampleNavMeshInsideRoom(GameObject room, Vector3 target, float radius, out Vector3 sampledPosition)
    {
        sampledPosition = default;

        if (room == null)
            return false;

        if (!TryGetWorldBounds(room, out Bounds roomBounds))
            return false;

        float searchRadius = Mathf.Max(0.5f, radius);
        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            return false;

        if (!IsInsideBoundsXZ(roomBounds, hit.position))
            return false;

        sampledPosition = hit.position;
        return true;
    }

    private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = new Bounds(root.transform.position, Vector3.zero);

        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
            return true;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool IsInsideBoundsXZ(Bounds bounds, Vector3 position)
    {
        return position.x >= bounds.min.x &&
               position.x <= bounds.max.x &&
               position.z >= bounds.min.z &&
               position.z <= bounds.max.z;
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
    /// Returns true if <paramref name="candidate"/> overlaps any already-placed room,
    /// skipping the room at <paramref name="parentIndex"/> (the direct parent this
    /// candidate is connected to, which is intentionally adjacent).
    /// Pass -1 to check against every placed room.
    /// </summary>
    private bool Overlaps(PlacedRoom candidate, int parentIndex = -1)
    {
        for (int i = 0; i < _placedRooms.Count; i++)
        {
            if (i == parentIndex) continue;

            PlacedRoom placed = _placedRooms[i];
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
    /// Returns every pair of placed rooms whose pivot positions are closer than the
    /// larger of the two rooms' <see cref="MazePrefab.OverlapThreshold"/> values —
    /// the same rule used by the solver. The <paramref name="distanceThreshold"/>
    /// parameter is used only as a fallback when a prefab reference is missing.
    /// </summary>
    public List<OverlapPair> FindOverlaps(float distanceThreshold)
    {
        var results = new List<OverlapPair>();
        int count = _placedRooms.Count;

        for (int i = 0; i < count; i++)
        {
            PlacedRoom a = _placedRooms[i];

            for (int j = i + 1; j < count; j++)
            {
                PlacedRoom b = _placedRooms[j];

                float threshold = (a.Prefab != null && b.Prefab != null)
                    ? Mathf.Max(a.Prefab.OverlapThreshold, b.Prefab.OverlapThreshold)
                    : distanceThreshold;

                float dist = Vector3.Distance(a.Position, b.Position);
                if (dist >= threshold) continue;

                results.Add(new OverlapPair
                {
                    RoomA    = a.Prefab != null ? a.Prefab.name : "Unknown",
                    RoomB    = b.Prefab != null ? b.Prefab.name : "Unknown",
                    TypeA    = a.Node   != null ? a.Node.Type   : MapGenerator.RoomType.Hallway,
                    TypeB    = b.Node   != null ? b.Node.Type   : MapGenerator.RoomType.Hallway,
                    Distance = dist,
                });
            }
        }

        return results;
    }
}
