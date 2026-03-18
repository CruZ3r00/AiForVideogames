using System.Collections.Generic;
using UnityEngine;

namespace FlockingSimulator.AIForVideogames
{
    internal sealed class PathAStarSolver
    {
        private readonly List<int> openSet;
        private readonly int[] gCost;
        private readonly int[] hCost;
        private readonly int[] parent;
        private readonly byte[] state;

        public PathAStarSolver(int nodeCount)
        {
            openSet = new List<int>(1024);
            gCost = new int[nodeCount];
            hCost = new int[nodeCount];
            parent = new int[nodeCount];
            state = new byte[nodeCount];
        }

        public bool TryFindPath(
            PathGridGraph graph,
            PathOccupancyMap occupancy,
            PathGridNode start,
            PathGridNode goal,
            List<Vector3> rawPath)
        {
            System.Array.Fill(gCost, int.MaxValue);
            System.Array.Fill(hCost, 0);
            System.Array.Fill(parent, -1);
            System.Array.Fill(state, (byte)0);
            openSet.Clear();
            rawPath.Clear();

            gCost[start.Index] = 0;
            hCost[start.Index] = graph.CalculateHeuristic(start, goal);
            state[start.Index] = 1;
            openSet.Add(start.Index);

            while (openSet.Count > 0)
            {
                int currentIndex = ExtractBestOpenNode();
                PathGridNode currentNode = graph.GetNode(currentIndex);
                if (currentIndex == goal.Index)
                {
                    ReconstructPath(graph, start.Index, goal.Index, rawPath);
                    return true;
                }

                state[currentIndex] = 2;
                foreach (PathGridNode nextNode in graph.GetNeighbours(currentNode))
                {
                    if (occupancy.IsBlocked(nextNode) || state[nextNode.Index] == 2)
                    {
                        continue;
                    }

                    bool isOrthogonal = currentNode.X == nextNode.X || currentNode.Z == nextNode.Z;
                    int movementCost = isOrthogonal ? 10 : 14;
                    int tentativeCost = gCost[currentIndex] + movementCost + occupancy.GetPenalty(nextNode);
                    if (tentativeCost >= gCost[nextNode.Index])
                    {
                        continue;
                    }

                    parent[nextNode.Index] = currentIndex;
                    gCost[nextNode.Index] = tentativeCost;
                    hCost[nextNode.Index] = graph.CalculateHeuristic(nextNode, goal);

                    if (state[nextNode.Index] != 1)
                    {
                        state[nextNode.Index] = 1;
                        openSet.Add(nextNode.Index);
                    }
                }
            }

            return false;
        }

        private int ExtractBestOpenNode()
        {
            int bestIndex = 0;
            int bestNode = openSet[0];
            int bestScore = gCost[bestNode] + hCost[bestNode];

            for (int i = 1; i < openSet.Count; i++)
            {
                int candidate = openSet[i];
                int candidateScore = gCost[candidate] + hCost[candidate];
                if (candidateScore < bestScore ||
                    (candidateScore == bestScore && hCost[candidate] < hCost[bestNode]))
                {
                    bestScore = candidateScore;
                    bestNode = candidate;
                    bestIndex = i;
                }
            }

            openSet.RemoveAt(bestIndex);
            return bestNode;
        }

        private void ReconstructPath(PathGridGraph graph, int startIndex, int goalIndex, List<Vector3> rawPath)
        {
            int currentIndex = goalIndex;
            while (currentIndex >= 0)
            {
                rawPath.Add(graph.GetNode(currentIndex).WorldPosition);
                if (currentIndex == startIndex)
                {
                    break;
                }

                currentIndex = parent[currentIndex];
            }

            rawPath.Reverse();
        }
    }
}
