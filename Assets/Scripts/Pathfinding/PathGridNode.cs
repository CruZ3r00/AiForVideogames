using UnityEngine;

namespace AcademicFlockingSimulation
{
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
