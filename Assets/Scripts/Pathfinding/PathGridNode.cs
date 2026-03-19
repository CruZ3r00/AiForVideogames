using UnityEngine;

namespace FlockingSimulator.AIForVideogames
{
    //definition of a single node in the pathfinding grid
    internal sealed class PathGridNode
    {
        public PathGridNode(int x, int z, int index, Vector3 worldPosition)
        {
            X = x;
            Z = z;
            Index = index;
            WorldPosition = worldPosition;
        }

        public int X { get; }
        public int Z { get; }
        public int Index { get; }
        public Vector3 WorldPosition { get; }
    }
}
