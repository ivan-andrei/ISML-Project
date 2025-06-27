using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering.Universal;
using UnityEngine.AI;
using NavMeshPlus.Components;

public class DungeonGenerator : MonoBehaviour
{
    #region Unchanged Fields
    struct Edge
    {
        public Vector2 p1, p2;

        public Edge(Vector2 p1, Vector2 p2)
        {
            this.p1 = p1;
            this.p2 = p2;
        }
        public override bool Equals(object obj)
        {
            if (obj is Edge other)
            {
                return (p1.Equals(other.p1) && p2.Equals(other.p2)) || (p1.Equals(other.p2) && p2.Equals(other.p1));
            }
            return false;
        }
        public override int GetHashCode()
        {
            return p1.GetHashCode() ^ p2.GetHashCode();
        }
    }

    [Header("Dungeon Layout")]
    public int numberOfRooms = 10;

    [Header("Room Parameters")]
    public GameObject tilePrefab;
    public GameObject floorTilePrefab;
    public Camera mainCamera;
    public int roomIterations = 100;
    public int walkLength = 50;
    public int padding = 2;
    public int wallThickness = 2;
    public static Vector2Int roomCellSize;
    public static List<Vector2Int> RoomGridPositions { get; private set; }

    [Header("Spawning")]
    public GameObject enemyPrefab;
    public int minEnemiesPerRoom = 1;
    public int maxEnemiesPerRoom = 4;

    public static DungeonGenerator Instance { get; private set; }
    private HashSet<Vector2Int> allFloorPositions;
    private HashSet<Vector2Int> spawnedRooms;
    #endregion

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        spawnedRooms = new HashSet<Vector2Int>();
        spawnedRooms.Add(Vector2Int.zero); 
    }

    void Start()
    {
        if (transform.position != Vector3.zero)
        {
            Debug.LogWarning("DungeonGenerator's transform is not at (0,0,0). This will offset the entire dungeon. Please reset its position.", this.gameObject);
        }
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        GenerateDungeon();

        NavMeshSurface surface = FindFirstObjectByType<NavMeshSurface>();
        if (surface != null)
        {
            Debug.Log("DungeonGenerator: Baking NavMesh for the generated dungeon...");
            surface.BuildNavMesh();
            Debug.Log("DungeonGenerator: NavMesh baking complete.");
        }
        else
        {
            Debug.LogWarning("DungeonGenerator: Could not find a NavMeshSurface2d component in the scene. AI Navigation will not be functional.", this);
        }
    }
    
    #region Unchanged Methods
    public void GenerateDungeon()
    {
        float camHalfHeight = mainCamera.orthographicSize;
        float camHalfWidth = camHalfHeight * mainCamera.aspect;
        DungeonGenerator.roomCellSize = new Vector2Int(Mathf.FloorToInt(camHalfWidth * 2), Mathf.FloorToInt(camHalfHeight * 2));

        RoomGridPositions = PlaceRooms();

        allFloorPositions = CreateRooms(new HashSet<Vector2Int>(RoomGridPositions), roomCellSize);

        HashSet<Vector2Int> corridorFloors = ConnectRooms(RoomGridPositions, roomCellSize);
        allFloorPositions.UnionWith(corridorFloors);

        CreateFloor(allFloorPositions);

        HashSet<Vector2Int> allWallPositions = CreateWalls(allFloorPositions);

        GenerateCollider(allWallPositions);
    }

    public void SpawnEnemiesInRoom(Vector2Int roomGridPosition)
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("Enemy Prefab is not assigned in DungeonGenerator. Skipping enemy spawning.", this);
            return;
        }

        if (spawnedRooms.Contains(roomGridPosition))
        {
            return;
        }

        spawnedRooms.Add(roomGridPosition);

        Vector2Int roomCenter = new Vector2Int(
            roomGridPosition.x * roomCellSize.x,
            roomGridPosition.y * roomCellSize.y
        );
        RectInt roomBounds = new RectInt(
            roomCenter.x - roomCellSize.x / 2,
            roomCenter.y - roomCellSize.y / 2,
            roomCellSize.x,
            roomCellSize.y
        );

        List<Vector2Int> spawnPoints = allFloorPositions
                                        .Where(pos => roomBounds.Contains(pos))
                                        .ToList();
        
        float safeRadius = 5f;
        spawnPoints.RemoveAll(pos => Vector2.Distance(pos, roomCenter) < safeRadius);

        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning($"No valid spawn points found in room {roomGridPosition}. Cannot spawn enemies.", this);
            return;
        }

        int enemiesToSpawn = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
        int spawnCount = Mathf.Min(enemiesToSpawn, spawnPoints.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            int randomIndex = Random.Range(0, spawnPoints.Count);
            Vector2Int spawnPos = spawnPoints[randomIndex];

            GameObject enemyGO = Instantiate(enemyPrefab, new Vector3(spawnPos.x, spawnPos.y, 0), Quaternion.identity, this.transform);
            EnemyAI enemyAI = enemyGO.GetComponent<EnemyAI>();

            if (enemyAI != null)
            {
                enemyAI.Initialize(roomGridPosition, roomCellSize);

                enemyAI.behaviorType = (Random.Range(0, 2) == 0) ? EnemyAI.BehaviorType.Melee : EnemyAI.BehaviorType.Ranged;
            }

            spawnPoints.RemoveAt(randomIndex);
        }
    }

    private void GenerateCollider(HashSet<Vector2Int> allWallPositions)
    {
        HashSet<Edge> boundaryEdges = new HashSet<Edge>();
        foreach (var wallPos in allWallPositions)
        {
            foreach (var dir in Direction2D.cardinalDirectionsList)
            {
                var neighborPos = wallPos + dir;
                if (!allWallPositions.Contains(neighborPos))
                {
                    Vector2 p1, p2;
                    if (dir == Vector2Int.up)
                    {
                        p1 = new Vector2(wallPos.x - 0.5f, wallPos.y + 0.5f);
                        p2 = new Vector2(wallPos.x + 0.5f, wallPos.y + 0.5f);
                    }
                    else if (dir == Vector2Int.right)
                    {
                        p1 = new Vector2(wallPos.x + 0.5f, wallPos.y + 0.5f);
                        p2 = new Vector2(wallPos.x + 0.5f, wallPos.y - 0.5f);
                    }
                    else if (dir == Vector2Int.down)
                    {
                        p1 = new Vector2(wallPos.x + 0.5f, wallPos.y - 0.5f);
                        p2 = new Vector2(wallPos.x - 0.5f, wallPos.y - 0.5f);
                    }
                    else
                    {
                        p1 = new Vector2(wallPos.x - 0.5f, wallPos.y - 0.5f);
                        p2 = new Vector2(wallPos.x - 0.5f, wallPos.y + 0.5f);
                    }
                    boundaryEdges.Add(new Edge(p1, p2));
                }
            }
        }

        Dictionary<Vector2, List<Vector2>> edgeConnections = new Dictionary<Vector2, List<Vector2>>();
        foreach (var edge in boundaryEdges)
        {
            if (!edgeConnections.ContainsKey(edge.p1)) edgeConnections[edge.p1] = new List<Vector2>();
            if (!edgeConnections.ContainsKey(edge.p2)) edgeConnections[edge.p2] = new List<Vector2>();
            edgeConnections[edge.p1].Add(edge.p2);
            edgeConnections[edge.p2].Add(edge.p1);
        }

        List<List<Vector2>> paths = new List<List<Vector2>>();
        while (edgeConnections.Count > 0)
        {
            List<Vector2> currentPath = new List<Vector2>();
            Vector2 startPoint = edgeConnections.Keys.First();
            Vector2 currentPoint = startPoint;

            do
            {
                currentPath.Add(currentPoint);
                Vector2 nextPoint = edgeConnections[currentPoint][0];

                edgeConnections[currentPoint].Remove(nextPoint);
                edgeConnections[nextPoint].Remove(currentPoint);

                if (edgeConnections[currentPoint].Count == 0) edgeConnections.Remove(currentPoint);
                if (edgeConnections[nextPoint].Count == 0) edgeConnections.Remove(nextPoint);

                currentPoint = nextPoint;
            } while (currentPoint != startPoint);

            paths.Add(currentPath);
        }

        GameObject wallColliderObject = new GameObject("WallCollider");
        wallColliderObject.layer = LayerMask.NameToLayer("Wall");
        wallColliderObject.transform.parent = transform;
        wallColliderObject.transform.position = transform.position;

        var polygonCollider = wallColliderObject.AddComponent<PolygonCollider2D>();
        var rb2d = wallColliderObject.AddComponent<Rigidbody2D>();
        rb2d.bodyType = RigidbodyType2D.Static;

        polygonCollider.pathCount = paths.Count;
        for (int i = 0; i < paths.Count; i++)
        {
            polygonCollider.SetPath(i, paths[i].ToArray());
        }
        wallColliderObject.AddComponent<ShadowCaster2D>();
    }

    private void CreateFloor(HashSet<Vector2Int> floorPositions)
    {
        if (floorTilePrefab == null)
        {
            Debug.LogWarning("Floor Tile Prefab is not assigned in DungeonGenerator. Skipping floor instantiation.", this.gameObject);
            return;
        }

        foreach (var position in floorPositions)
        {
            Instantiate(floorTilePrefab, new Vector3(position.x, position.y, 0), Quaternion.identity, transform);
        }
    }

    private List<Vector2Int> PlaceRooms()
    {
        List<Vector2Int> roomPositions = new List<Vector2Int>();
        HashSet<Vector2Int> occupiedPositions = new HashSet<Vector2Int>();

        roomPositions.Add(Vector2Int.zero);
        occupiedPositions.Add(Vector2Int.zero);

        while (roomPositions.Count < numberOfRooms)
        {
            Vector2Int randomExistingRoom = roomPositions[Random.Range(0, roomPositions.Count)];

            List<Vector2Int> directions = Direction2D.GetShuffledCardinalDirections();

            foreach (var direction in directions)
            {
                Vector2Int potentialPosition = randomExistingRoom + direction;

                if (!occupiedPositions.Contains(potentialPosition))
                {
                    roomPositions.Add(potentialPosition);
                    occupiedPositions.Add(potentialPosition);
                    break;
                }
            }
        }
        return roomPositions;
    }

    private HashSet<Vector2Int> CreateRooms(HashSet<Vector2Int> roomGridPositions, Vector2Int roomCellSize)
    {
        HashSet<Vector2Int> allFloorPositions = new HashSet<Vector2Int>();
        foreach (var gridPos in roomGridPositions)
        {
            int roomWidth = roomCellSize.x - (padding * 2);
            int roomHeight = roomCellSize.y - (padding * 2);
            Vector2Int roomOffset = new Vector2Int(gridPos.x * roomCellSize.x, gridPos.y * roomCellSize.y);

            RectInt roomBounds = new RectInt(
                roomOffset.x - roomWidth / 2,
                roomOffset.y - roomHeight / 2,
                roomWidth,
                roomHeight
            );

            HashSet<Vector2Int> roomFloors = RunRandomWalk(roomBounds);
            allFloorPositions.UnionWith(roomFloors);
        }
        return allFloorPositions;
    }

    private HashSet<Vector2Int> ConnectRooms(List<Vector2Int> roomGridPositions, Vector2Int roomCellSize)
    {
        HashSet<Vector2Int> corridorFloors = new HashSet<Vector2Int>();
        HashSet<Vector2Int> roomGridSet = new HashSet<Vector2Int>(roomGridPositions);

        foreach (var roomPos in roomGridPositions)
        {
            Vector2Int neighborRight = roomPos + Vector2Int.right;
            if (roomGridSet.Contains(neighborRight))
            {
                Vector2Int room1Center = new Vector2Int(roomPos.x * roomCellSize.x, roomPos.y * roomCellSize.y);
                Vector2Int room2Center = new Vector2Int(neighborRight.x * roomCellSize.x, neighborRight.y * roomCellSize.y);
                var corridorPath = GetCorridorPath(room1Center, room2Center);
                corridorFloors.UnionWith(corridorPath);
            }

            Vector2Int neighborUp = roomPos + Vector2Int.up;
            if (roomGridSet.Contains(neighborUp))
            {
                Vector2Int room1Center = new Vector2Int(roomPos.x * roomCellSize.x, roomPos.y * roomCellSize.y);
                Vector2Int room2Center = new Vector2Int(neighborUp.x * roomCellSize.x, neighborUp.y * roomCellSize.y);
                var corridorPath = GetCorridorPath(room1Center, room2Center);
                corridorFloors.UnionWith(corridorPath);
            }
        }
        return corridorFloors;
    }
    
    private HashSet<Vector2Int> GetCorridorPath(Vector2Int start, Vector2Int end)
    {
        HashSet<Vector2Int> corridor = new HashSet<Vector2Int>();
        Vector2Int currentPos = start;
        corridor.Add(currentPos);
        
        while (currentPos.x != end.x)
        {
            currentPos.x += (int)Mathf.Sign(end.x - currentPos.x);
            corridor.Add(currentPos);
        }
        while (currentPos.y != end.y)
        {
            currentPos.y += (int)Mathf.Sign(end.y - currentPos.y);
            corridor.Add(currentPos);
        }
        return corridor;
    }

    protected HashSet<Vector2Int> RunRandomWalk(RectInt bounds)
    {
        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
        for (int i = 0; i < roomIterations; i++)
        {
            var startPosition = new Vector2Int(
                Random.Range(bounds.xMin, bounds.xMax),
                Random.Range(bounds.yMin, bounds.yMax)
            );
            var path = ProceduralGenerationAlgorithms.BoundedRandomWalk(startPosition, walkLength, bounds);
            floorPositions.UnionWith(path);
        }
        return floorPositions;
    }

    private HashSet<Vector2Int> CreateWalls(HashSet<Vector2Int> floorPositions)
    {
        var innerWallPositions = FindWallsInDirections(floorPositions, Direction2D.cardinalDirectionsList);
        HashSet<Vector2Int> allWallPositions = new HashSet<Vector2Int>(innerWallPositions);

        for (int i = 1; i < wallThickness; i++)
        {
            HashSet<Vector2Int> newWallLayer = new HashSet<Vector2Int>();
            foreach (var wallPos in allWallPositions)
            {
                foreach (var direction in Direction2D.cardinalDirectionsList)
                {
                    var neighborPos = wallPos + direction;
                    if (!floorPositions.Contains(neighborPos))
                    {
                        newWallLayer.Add(neighborPos);
                    }
                }
            }
            allWallPositions.UnionWith(newWallLayer);
        }

        foreach (var position in allWallPositions)
        {
            Instantiate(tilePrefab, new Vector3(position.x, position.y, 0), Quaternion.identity, transform);
        }

        return allWallPositions;
    }

    private HashSet<Vector2Int> FindWallsInDirections(HashSet<Vector2Int> positions, List<Vector2Int> directionList)
    {
        HashSet<Vector2Int> wallPositions = new HashSet<Vector2Int>();
        foreach (var position in positions)
        {
            foreach (var direction in directionList)
            {
                var neighbourPosition = position + direction;
                if (positions.Contains(neighbourPosition) == false)
                {
                    wallPositions.Add(neighbourPosition);
                }
            }
        }
        return wallPositions;
    }
    #endregion
}

#region Unchanged Helper Classes
public static class ProceduralGenerationAlgorithms
{
    public static HashSet<Vector2Int> BoundedRandomWalk(Vector2Int startPosition, int walkLength, RectInt bounds)
    {
        HashSet<Vector2Int> path = new HashSet<Vector2Int>();
        var currentPosition = startPosition;

        for (int i = 0; i < walkLength; i++)
        {
            if (bounds.Contains(currentPosition))
            {
                path.Add(currentPosition);
            }
            var newPosition = currentPosition + Direction2D.GetRandomCardinalDirection();
            currentPosition = newPosition;
        }
        return path;
    }
}

public static class Direction2D
{
    public static List<Vector2Int> cardinalDirectionsList = new List<Vector2Int>
    {
        new Vector2Int(0,1), //UP
        new Vector2Int(1,0), //RIGHT
        new Vector2Int(0, -1), //DOWN
        new Vector2Int(-1, 0) //LEFT
    };

    public static Vector2Int GetRandomCardinalDirection()
    {
        return cardinalDirectionsList[Random.Range(0, cardinalDirectionsList.Count)];
    }
    
    public static List<Vector2Int> GetShuffledCardinalDirections()
    {
        List<Vector2Int> shuffledDirections = new List<Vector2Int>(cardinalDirectionsList);
        
        for (int i = 0; i < shuffledDirections.Count; i++)
        {
            int randomIndex = Random.Range(i, shuffledDirections.Count);
            Vector2Int temp = shuffledDirections[i];
            shuffledDirections[i] = shuffledDirections[randomIndex];
            shuffledDirections[randomIndex] = temp;
        }
        return shuffledDirections;
    }
}
#endregion