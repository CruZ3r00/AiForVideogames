using System.Collections.Generic;
using UnityEngine;

namespace FlockingSimulator.AIForVideogames
{
    public class WorldManager : MonoBehaviour
    {
        public const int InitialAgentCount = 50;
        public const int MaxAgentCount = 50;
        public const int ObstacleCount = 10;
        public const float RequiredWorldSize = 100f;

        [Header("World Bounds")]
        [SerializeField] private Vector2 fallbackWorldMin = Vector2.zero;
        [SerializeField] private Vector2 fallbackWorldMax = new Vector2(100f, 100f);
        [SerializeField] private float movementY = 0f;

        [Header("Scene Parents")]
        [SerializeField] private Transform agentsParent;
        [SerializeField] private Transform obstaclesParent;
        [SerializeField] private Transform targetsParent;

        [Header("Corner References")]
        [SerializeField] private Transform southWestCorner;
        [SerializeField] private Transform southEastCorner;
        [SerializeField] private Transform northWestCorner;
        [SerializeField] private Transform northEastCorner;

        [Header("Obstacle Prefab")]
        [SerializeField] private ObstacleController obstaclePrefab;
        [SerializeField] private float obstacleSpawnPadding = 7f;
        [SerializeField] private float obstacleSpawnClearance = 12f;
        [SerializeField] private float obstacleMinSpeed = 5f;
        [SerializeField] private float obstacleMaxSpeed = 10f;

        [Header("Debug")]
        [SerializeField] private bool drawBoundsGizmo = true;

        private readonly List<ObstacleController> obstacles = new List<ObstacleController>(ObstacleCount);

        private SimulationManager simulationManager;

        public IReadOnlyList<ObstacleController> Obstacles => obstacles;
        public Transform AgentsParent => agentsParent;
        public Transform ObstaclesParent => obstaclesParent;
        public Transform TargetsParent => targetsParent;
        public float MovementY => movementY;

        public Vector2 WorldMin
        {
            get
            {
                if (HasAllCornerReferences())
                {
                    return new Vector2(
                        Mathf.Min(southWestCorner.position.x, northWestCorner.position.x),
                        Mathf.Min(southWestCorner.position.z, southEastCorner.position.z));
                }

                return fallbackWorldMin;
            }
        }

        public Vector2 WorldMax
        {
            get
            {
                if (HasAllCornerReferences())
                {
                    return new Vector2(
                        Mathf.Max(southEastCorner.position.x, northEastCorner.position.x),
                        Mathf.Max(northWestCorner.position.z, northEastCorner.position.z));
                }

                return fallbackWorldMax;
            }
        }

        public float WorldWidth => WorldMax.x - WorldMin.x;
        public float WorldDepth => WorldMax.y - WorldMin.y;
        public Vector3 WorldCenter => new Vector3((WorldMin.x + WorldMax.x) * 0.5f, movementY, (WorldMin.y + WorldMax.y) * 0.5f);

        //initialization
        public void Initialize(SimulationManager manager)
        {
            simulationManager = manager;
        }

        //validate scene setup for world 
        public bool ValidateSceneSetup()
        {
            bool isValid = true;

            if (agentsParent == null)
            {
                Debug.LogError("WorldManager requires an Agents Parent transform.", this);
                isValid = false;
            }

            if (obstaclesParent == null)
            {
                Debug.LogError("WorldManager requires an Obstacles Parent transform.", this);
                isValid = false;
            }

            if (targetsParent == null)
            {
                Debug.LogError("WorldManager requires a Targets Parent transform.", this);
                isValid = false;
            }

            if (obstaclePrefab == null)
            {
                Debug.LogError("WorldManager requires an Obstacle prefab.", this);
                isValid = false;
            }

            if (!HasAllCornerReferences())
            {
                Debug.LogError("WorldManager requires all four corner references to be assigned.", this);
                isValid = false;
            }

            if (WorldWidth <= 0f || WorldDepth <= 0f)
            {
                Debug.LogError("WorldManager has invalid world bounds.", this);
                isValid = false;
            }

            if (Mathf.Abs(WorldWidth - RequiredWorldSize) > 0.01f ||
                Mathf.Abs(WorldDepth - RequiredWorldSize) > 0.01f)
            {
                Debug.LogError(
                    $"WorldManager requires a {RequiredWorldSize}x{RequiredWorldSize} meters world. Current bounds are {WorldWidth}x{WorldDepth}.",
                    this);
                isValid = false;
            }

            return isValid;
        }

        public void ClearRuntimeObstacles()
        {
            if (obstaclesParent != null)
            {
                ObstacleController[] spawnedObstacles = obstaclesParent.GetComponentsInChildren<ObstacleController>(true);
                for (int i = 0; i < spawnedObstacles.Length; i++)
                {
                    if (spawnedObstacles[i] != null)
                    {
                        spawnedObstacles[i].gameObject.SetActive(false);
                        Destroy(spawnedObstacles[i].gameObject);
                    }
                }
            }

            obstacles.Clear();
        }

        //function to spawn obstacles in the world, ensuring they don't spawn too close to the target or flock
        public void SpawnObstacles(Vector3 protectedTargetPosition, Vector3 protectedFlockPosition)
        {
            ClearRuntimeObstacles();
            int[] laneOrder = BuildShuffledLaneOrder();

            for (int i = 0; i < ObstacleCount; i++)
            {
                Vector3 spawnPosition = FindObstacleSpawnPoint(laneOrder[i], protectedTargetPosition, protectedFlockPosition);
                ObstacleController obstacle = Instantiate(obstaclePrefab, spawnPosition, Quaternion.identity, obstaclesParent);
                obstacle.name = $"{obstaclePrefab.name}_{i:00}";
                obstacle.Initialize(
                    simulationManager,
                    this,
                    spawnPosition,
                    Random.Range(obstacleMinSpeed, obstacleMaxSpeed));

                obstacles.Add(obstacle);
            }
        }

        public Vector3 ClampInsideWorld(Vector3 position, float padding)
        {
            Vector2 min = WorldMin;
            Vector2 max = WorldMax;

            position.x = Mathf.Clamp(position.x, min.x + padding, max.x - padding);
            position.z = Mathf.Clamp(position.z, min.y + padding, max.y - padding);
            position.y = movementY;
            return position;
        }

        //function to get the opposite corner of the world given a corner
        public WorldCorner GetOppositeCorner(WorldCorner corner)
        {
            switch (corner)
            {
                case WorldCorner.SouthWest:
                    return WorldCorner.NorthEast;
                case WorldCorner.SouthEast:
                    return WorldCorner.NorthWest;
                case WorldCorner.NorthWest:
                    return WorldCorner.SouthEast;
                default:
                    return WorldCorner.SouthWest;
            }
        }

        //function to get the world position of a corner, using the corner references
        public Vector3 GetCornerPosition(WorldCorner corner)
        {
            Transform cornerTransform = GetCornerTransform(corner);
            if (cornerTransform != null)
            {
                Vector3 position = cornerTransform.position;
                position.y = movementY;
                return position;
            }

            Vector2 min = WorldMin;
            Vector2 max = WorldMax;
            switch (corner)
            {
                case WorldCorner.SouthWest:
                    return new Vector3(min.x, movementY, min.y);
                case WorldCorner.SouthEast:
                    return new Vector3(max.x, movementY, min.y);
                case WorldCorner.NorthWest:
                    return new Vector3(min.x, movementY, max.y);
                default:
                    return new Vector3(max.x, movementY, max.y);
            }
        }

        //function to find the initial flock's spawn postion
        public Vector3 SampleNearCorner(WorldCorner corner, float maxDistance, float padding)
        {
            float angle = Random.Range(0f, Mathf.PI * 0.5f);
            float distance = Mathf.Sqrt(Random.value) * maxDistance;
            float offsetX = Mathf.Cos(angle) * distance;
            float offsetZ = Mathf.Sin(angle) * distance;

            Vector3 cornerPosition = GetCornerPosition(corner);
            Vector3 candidate;

            switch (corner)
            {
                case WorldCorner.SouthWest:
                    candidate = new Vector3(cornerPosition.x + offsetX, movementY, cornerPosition.z + offsetZ);
                    break;
                case WorldCorner.SouthEast:
                    candidate = new Vector3(cornerPosition.x - offsetX, movementY, cornerPosition.z + offsetZ);
                    break;
                case WorldCorner.NorthWest:
                    candidate = new Vector3(cornerPosition.x + offsetX, movementY, cornerPosition.z - offsetZ);
                    break;
                default:
                    candidate = new Vector3(cornerPosition.x - offsetX, movementY, cornerPosition.z - offsetZ);
                    break;
            }

            return ClampInsideWorld(candidate, padding);
        }

        //function to check if  a position is inside a death area defined by the obstacles, with an optional radius padding
        public bool IsInsideDeathArea(Vector3 position, float radiusPadding = 0f)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                ObstacleController obstacle = obstacles[i];
                if (obstacle != null && obstacle.ContainsLethalPoint(position, radiusPadding))
                {
                    return true;
                }
            }

            return false;
        }

        public Transform GetCornerTransform(WorldCorner corner)
        {
            switch (corner)
            {
                case WorldCorner.SouthWest:
                    return southWestCorner;
                case WorldCorner.SouthEast:
                    return southEastCorner;
                case WorldCorner.NorthWest:
                    return northWestCorner;
                default:
                    return northEastCorner;
            }
        }

        //check if all corner references are assigned
        private bool HasAllCornerReferences()
        {
            return southWestCorner != null &&
                   southEastCorner != null &&
                   northWestCorner != null &&
                   northEastCorner != null;
        }

        //function to build a shuffled lane order for obstacle spawning
        private int[] BuildShuffledLaneOrder()
        {
            int[] laneOrder = new int[ObstacleCount];
            for (int i = 0; i < laneOrder.Length; i++)
            {
                laneOrder[i] = i;
            }

            for (int i = laneOrder.Length - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                int temp = laneOrder[i];
                laneOrder[i] = laneOrder[swapIndex];
                laneOrder[swapIndex] = temp;
            }

            return laneOrder;
        }

        //function to find a valid spawn point for obstacles
        private Vector3 FindObstacleSpawnPoint(int laneIndex, Vector3 protectedTargetPosition, Vector3 protectedFlockPosition)
        {
            Vector2 min = WorldMin;
            Vector2 max = WorldMax;
            float laneMin = min.y + obstacleSpawnPadding;
            float laneMax = max.y - obstacleSpawnPadding;
            float laneStep = (laneMax - laneMin) / Mathf.Max(1, ObstacleCount);
            float laneZ = Mathf.Lerp(laneMin, laneMax, (laneIndex + 0.5f) / ObstacleCount);

            for (int attempt = 0; attempt < 500; attempt++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(min.x + obstacleSpawnPadding, max.x - obstacleSpawnPadding),
                    movementY,
                    laneZ);

                if (SimulationMath.DistanceXZSquared(candidate, protectedTargetPosition) < obstacleSpawnClearance * obstacleSpawnClearance)
                {
                    continue;
                }

                if (SimulationMath.DistanceXZSquared(candidate, protectedFlockPosition) < obstacleSpawnClearance * obstacleSpawnClearance)
                {
                    continue;
                }

                bool overlaps = false;
                for (int i = 0; i < obstacles.Count; i++)
                {
                    ObstacleController obstacle = obstacles[i];
                    if (obstacle == null)
                    {
                        continue;
                    }

                    if (Mathf.Abs(candidate.z - obstacle.Position.z) < laneStep * 0.5f)
                    {
                        overlaps = true;
                        break;
                    }

                    if (SimulationMath.DistanceXZSquared(candidate, obstacle.Position) < obstacleSpawnClearance * obstacleSpawnClearance)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    return candidate;
                }
            }

            return new Vector3(
                Mathf.Clamp(WorldCenter.x, min.x + obstacleSpawnPadding, max.x - obstacleSpawnPadding),
                movementY,
                laneZ);
        }

        private void OnDrawGizmos()
        {
            if (!drawBoundsGizmo)
            {
                return;
            }

            Vector2 min = WorldMin;
            Vector2 max = WorldMax;
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(
                new Vector3((min.x + max.x) * 0.5f, movementY + 0.1f, (min.y + max.y) * 0.5f),
                new Vector3(max.x - min.x, 0.2f, max.y - min.y));
        }
    }
}
