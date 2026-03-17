using System.Collections.Generic;
using UnityEngine;

namespace AcademicFlockingSimulation
{
    internal sealed class PathPathSmoother
    {
        private const int UnsafePenaltyThreshold = 20;

        public void Simplify(
            PathGridGraph graph,
            PathOccupancyMap occupancy,
            List<Vector3> sourcePath,
            List<Vector3> simplifiedPath)
        {
            simplifiedPath.Clear();
            if (sourcePath.Count == 0)
            {
                return;
            }

            int anchor = 0;
            simplifiedPath.Add(sourcePath[anchor]);

            while (anchor < sourcePath.Count - 1)
            {
                int furthestSafeIndex = anchor + 1;
                // "Walking the Path" style simplification: keep the furthest visible node.
                for (int candidate = sourcePath.Count - 1; candidate > anchor + 1; candidate--)
                {
                    if (IsSegmentSafe(graph, occupancy, sourcePath[anchor], sourcePath[candidate]))
                    {
                        furthestSafeIndex = candidate;
                        break;
                    }
                }

                simplifiedPath.Add(sourcePath[furthestSafeIndex]);
                anchor = furthestSafeIndex;
            }
        }

        public bool IsSegmentSafe(PathGridGraph graph, PathOccupancyMap occupancy, Vector3 start, Vector3 end)
        {
            float distance = Vector3.Distance(start, end);
            int steps = Mathf.Max(2, Mathf.CeilToInt(distance / (graph.CellSize * 0.5f)));

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 sample = Vector3.Lerp(start, end, t);
                PathGridNode node = graph.GetNode(graph.WorldToGrid(sample));
                if (occupancy.IsBlocked(node) || occupancy.GetPenalty(node) > UnsafePenaltyThreshold)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
