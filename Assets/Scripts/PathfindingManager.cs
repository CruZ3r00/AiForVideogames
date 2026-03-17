using System.Collections.Generic;
using UnityEngine;

namespace AcademicFlockingSimulation
{
    public class PathfindingManager : MonoBehaviour
    {
        private const int UnsafePenaltyThreshold = 20;

        [Header("Grid")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float extraPenaltyRadius = 3f;
        [SerializeField] private float dynamicObstaclePredictionTime = 0.8f;
        [SerializeField] private int dynamicObstaclePredictionSteps = 4;

        [Header("Optional Scene Debug")]
        [SerializeField] private LineRenderer pathRenderer;
        [SerializeField] private float pathDebugYOffset = 0.12f;

        private readonly List<Vector3> currentPath = new List<Vector3>(256);
        private readonly List<Vector3> rawPath = new List<Vector3>(256);

        private WorldManager worldManager;
        private PathGridGraph graph;
        private PathOccupancyMap occupancy;
        private PathAStarSolver solver;
        private PathPathSmoother smoother;

        public IReadOnlyList<Vector3> CurrentPath => currentPath;
        public int PathVersion { get; private set; }

        public void Initialize(SimulationManager manager, WorldManager world)
        {
            worldManager = world;
            graph = new PathGridGraph(worldManager, cellSize);
            occupancy = new PathOccupancyMap(graph.NodeCount);
            solver = new PathAStarSolver(graph.NodeCount);
            smoother = new PathPathSmoother();

            if (pathRenderer != null)
            {
                pathRenderer.useWorldSpace = true;
                pathRenderer.positionCount = 0;
            }
        }

        public bool ValidateSceneSetup()
        {
            if (cellSize <= 0f)
            {
                Debug.LogError("PathfindingManager requires a positive cell size.", this);
                return false;
            }

            if (dynamicObstaclePredictionTime < 0f || dynamicObstaclePredictionSteps < 1)
            {
                Debug.LogError("PathfindingManager requires a non-negative prediction time and at least one prediction step.", this);
                return false;
            }

            return true;
        }

        public void ClearPath()
        {
            currentPath.Clear();
            rawPath.Clear();
            PathVersion++;
            UpdatePathRenderer();
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            EnsureRuntimeDependencies();
            if (graph == null)
            {
                return Vector2Int.zero;
            }

            return graph.WorldToGrid(worldPosition);
        }

        public Vector3 GridToWorld(int x, int z)
        {
            EnsureRuntimeDependencies();
            if (graph == null)
            {
                return GetWorldPositionFallback();
            }

            return graph.GridToWorld(x, z);
        }

        public bool TryBuildPath(Vector3 startWorld, Vector3 goalWorld, IReadOnlyList<ObstacleController> obstacles)
        {
            EnsureRuntimeDependencies();
            if (graph == null || occupancy == null || solver == null || smoother == null)
            {
                return false;
            }

            RefreshDynamicData(obstacles);

            PathGridNode startNode = occupancy.FindNearestWalkableNode(graph, graph.GetNode(graph.WorldToGrid(startWorld)));
            PathGridNode goalNode = occupancy.FindNearestWalkableNode(graph, graph.GetNode(graph.WorldToGrid(goalWorld)));
            if (startNode == null || goalNode == null)
            {
                ClearPath();
                return false;
            }

            bool pathFound = solver.TryFindPath(graph, occupancy, startNode, goalNode, rawPath);
            if (pathFound)
            {
                smoother.Simplify(graph, occupancy, rawPath, currentPath);
                PathVersion++;
            }
            else
            {
                currentPath.Clear();
                rawPath.Clear();
            }

            UpdatePathRenderer();
            return pathFound;
        }

        public bool IsCurrentPathUnsafe(IReadOnlyList<ObstacleController> obstacles)
        {
            EnsureRuntimeDependencies();
            if (graph == null || occupancy == null)
            {
                return true;
            }

            if (currentPath.Count < 2)
            {
                return true;
            }

            RefreshDynamicData(obstacles);
            for (int i = 0; i < currentPath.Count; i++)
            {
                PathGridNode node = graph.GetNode(graph.WorldToGrid(currentPath[i]));
                if (occupancy.IsBlocked(node) || occupancy.GetPenalty(node) > UnsafePenaltyThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        public Vector3 GetLookAheadDirection(
            Vector3 position,
            ref int pathIndex,
            ref int knownPathVersion,
            int lookAheadSteps,
            float waypointReachDistance)
        {
            if (currentPath.Count == 0)
            {
                return Vector3.zero;
            }

            if (knownPathVersion != PathVersion)
            {
                knownPathVersion = PathVersion;
                pathIndex = FindClosestPathIndex(position);
            }

            pathIndex = Mathf.Clamp(pathIndex, 0, currentPath.Count - 1);
            float waypointReachDistanceSqr = waypointReachDistance * waypointReachDistance;

            while (pathIndex < currentPath.Count - 1 &&
                   SimulationMath.DistanceXZSquared(position, currentPath[pathIndex]) <= waypointReachDistanceSqr)
            {
                pathIndex++;
            }

            while (pathIndex < currentPath.Count - 1 &&
                   SimulationMath.DistanceXZSquared(position, currentPath[pathIndex + 1]) <
                   SimulationMath.DistanceXZSquared(position, currentPath[pathIndex]))
            {
                pathIndex++;
            }

            int targetIndex = Mathf.Min(currentPath.Count - 1, pathIndex + Mathf.Max(0, lookAheadSteps));
            Vector3 direction = currentPath[targetIndex] - position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
        }

        private void RefreshDynamicData(IReadOnlyList<ObstacleController> obstacles)
        {
            if (occupancy == null)
            {
                return;
            }

            if (obstacles == null)
            {
                occupancy.Clear();
                return;
            }

            occupancy.Rebuild(
                graph,
                obstacles,
                extraPenaltyRadius,
                dynamicObstaclePredictionTime,
                dynamicObstaclePredictionSteps);
        }

        private int FindClosestPathIndex(Vector3 position)
        {
            int bestIndex = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < currentPath.Count; i++)
            {
                float distance = SimulationMath.DistanceXZSquared(position, currentPath[i]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void EnsureRuntimeDependencies()
        {
            if (graph != null && occupancy != null && solver != null && smoother != null)
            {
                return;
            }

            if (worldManager == null)
            {
                worldManager = FindFirstObjectByType<WorldManager>();
            }

            if (worldManager == null)
            {
                return;
            }

            graph = new PathGridGraph(worldManager, cellSize);
            occupancy = new PathOccupancyMap(graph.NodeCount);
            solver = new PathAStarSolver(graph.NodeCount);
            smoother = new PathPathSmoother();
        }

        private void UpdatePathRenderer()
        {
            if (pathRenderer == null)
            {
                return;
            }

            pathRenderer.positionCount = currentPath.Count;
            if (worldManager == null)
            {
                return;
            }

            for (int i = 0; i < currentPath.Count; i++)
            {
                Vector3 point = currentPath[i];
                point.y = worldManager.MovementY + pathDebugYOffset;
                pathRenderer.SetPosition(i, point);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }
        }

        private Vector3 GetWorldPositionFallback()
        {
            return worldManager != null ? worldManager.WorldCenter : Vector3.zero;
        }
    }
}
