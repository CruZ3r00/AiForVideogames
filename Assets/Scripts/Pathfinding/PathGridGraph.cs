using System.Collections.Generic;
using UnityEngine;

namespace FlockingSimulator.AIForVideogames
{
    internal sealed class PathGridGraph
    {
        private static readonly Vector2Int[] NeighborDirections =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 1)
        };

        private readonly PathGridNode[] nodes;

        public PathGridGraph(WorldManager worldManager, float cellSize)
        {
            WorldMin = worldManager.WorldMin;
            MovementY = worldManager.MovementY;
            CellSize = cellSize;
            Width = Mathf.Max(1, Mathf.CeilToInt(worldManager.WorldWidth / cellSize));
            Height = Mathf.Max(1, Mathf.CeilToInt(worldManager.WorldDepth / cellSize));
            nodes = new PathGridNode[Width * Height];

            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = ToIndex(x, z);
                    nodes[index] = new PathGridNode(x, z, index, GridToWorld(x, z));
                }
            }
        }

        public int Width { get; }
        public int Height { get; }
        public int NodeCount => nodes.Length;
        public float CellSize { get; }
        public Vector2 WorldMin { get; }
        public float MovementY { get; }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((worldPosition.x - WorldMin.x) / CellSize), 0, Width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((worldPosition.z - WorldMin.y) / CellSize), 0, Height - 1);
            return new Vector2Int(x, z);
        }

        public Vector3 GridToWorld(int x, int z)
        {
            return new Vector3(
                WorldMin.x + ((x + 0.5f) * CellSize),
                MovementY,
                WorldMin.y + ((z + 0.5f) * CellSize));
        }

        public bool InBounds(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Height;
        }

        public int ToIndex(int x, int z)
        {
            return (z * Width) + x;
        }

        public PathGridNode GetNode(int x, int z)
        {
            return InBounds(x, z) ? nodes[ToIndex(x, z)] : null;
        }

        public PathGridNode GetNode(int index)
        {
            return index >= 0 && index < nodes.Length ? nodes[index] : null;
        }

        public PathGridNode GetNode(Vector2Int cell)
        {
            return GetNode(cell.x, cell.y);
        }

        public IEnumerable<PathGridNode> GetNeighbours(PathGridNode node)
        {
            for (int i = 0; i < NeighborDirections.Length; i++)
            {
                int nextX = node.X + NeighborDirections[i].x;
                int nextZ = node.Z + NeighborDirections[i].y;
                if (InBounds(nextX, nextZ))
                {
                    yield return GetNode(nextX, nextZ);
                }
            }
        }

        public int CalculateHeuristic(PathGridNode from, PathGridNode to)
        {
            int dx = Mathf.Abs(from.X - to.X);
            int dz = Mathf.Abs(from.Z - to.Z);
            int diagonal = Mathf.Min(dx, dz);
            int straight = Mathf.Abs(dx - dz);
            return (14 * diagonal) + (10 * straight);
        }
    }
}
