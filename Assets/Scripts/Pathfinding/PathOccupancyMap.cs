using System.Collections.Generic;
using UnityEngine;

namespace FlockingSimulator.AIForVideogames
{
    // This class maintains a grid-based occupancy map for pathfinding, marking nodes as blocked 
    // or assigning penalties based on proximity to dynamic obstacles.
    internal sealed class PathOccupancyMap
    {
        private readonly bool[] blocked;
        private readonly int[] penalties;

        public PathOccupancyMap(int nodeCount)
        {
            blocked = new bool[nodeCount];
            penalties = new int[nodeCount];
        }

        public void Clear()
        {
            System.Array.Fill(blocked, false);
            System.Array.Fill(penalties, 0);
        }

        public void Rebuild(
            PathGridGraph graph,
            IReadOnlyList<ObstacleController> obstacles,
            float extraPenaltyRadius,
            float predictionTime,
            int predictionSteps)
        {
            Clear();

            int samples = Mathf.Max(1, predictionSteps);
            for (int i = 0; i < obstacles.Count; i++)
            {
                ObstacleController obstacle = obstacles[i];
                if (obstacle == null)
                {
                    continue;
                }

                float paddingRadius = obstacle.DeathRadius + extraPenaltyRadius;
                float blockedRadius = obstacle.DeathRadius + (graph.CellSize * 0.75f);
                for (int sample = 0; sample <= samples; sample++)
                {
                    float t = predictionTime * (sample / (float)samples);
                    StampObstacleInfluence(graph, obstacle.GetPredictedPosition(t), blockedRadius, paddingRadius);
                }
            }
        }

        public bool IsBlocked(PathGridNode node)
        {
            return node == null || blocked[node.Index];
        }

        public int GetPenalty(PathGridNode node)
        {
            return node == null ? int.MaxValue : penalties[node.Index];
        }

        public PathGridNode FindNearestWalkableNode(PathGridGraph graph, PathGridNode origin)
        {
            if (origin == null)
            {
                return null;
            }

            if (!IsBlocked(origin))
            {
                return origin;
            }

            int maxRadius = Mathf.Max(graph.Width, graph.Height);
            for (int radius = 1; radius < maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    PathGridNode candidateA = graph.GetNode(origin.X + dx, origin.Z - radius);
                    PathGridNode candidateB = graph.GetNode(origin.X + dx, origin.Z + radius);
                    if (candidateA != null && !IsBlocked(candidateA))
                    {
                        return candidateA;
                    }

                    if (candidateB != null && !IsBlocked(candidateB))
                    {
                        return candidateB;
                    }
                }

                for (int dz = -radius + 1; dz <= radius - 1; dz++)
                {
                    PathGridNode candidateA = graph.GetNode(origin.X - radius, origin.Z + dz);
                    PathGridNode candidateB = graph.GetNode(origin.X + radius, origin.Z + dz);
                    if (candidateA != null && !IsBlocked(candidateA))
                    {
                        return candidateA;
                    }

                    if (candidateB != null && !IsBlocked(candidateB))
                    {
                        return candidateB;
                    }
                }
            }

            return null;
        }

        private void StampObstacleInfluence(PathGridGraph graph, Vector3 obstaclePosition, float blockedRadius, float paddingRadius)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((obstaclePosition.x - paddingRadius - graph.WorldMin.x) / graph.CellSize), 0, graph.Width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((obstaclePosition.x + paddingRadius - graph.WorldMin.x) / graph.CellSize), 0, graph.Width - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((obstaclePosition.z - paddingRadius - graph.WorldMin.y) / graph.CellSize), 0, graph.Height - 1);
            int maxZ = Mathf.Clamp(Mathf.CeilToInt((obstaclePosition.z + paddingRadius - graph.WorldMin.y) / graph.CellSize), 0, graph.Height - 1);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    PathGridNode node = graph.GetNode(x, z);
                    float distance = Mathf.Sqrt(SimulationMath.DistanceXZSquared(node.WorldPosition, obstaclePosition));

                    if (distance <= blockedRadius)
                    {
                        blocked[node.Index] = true;
                    }
                    else if (distance <= paddingRadius)
                    {
                        int penalty = Mathf.CeilToInt((paddingRadius - distance) * 15f);
                        penalties[node.Index] = Mathf.Max(penalties[node.Index], penalty);
                    }
                }
            }
        }
    }
}
