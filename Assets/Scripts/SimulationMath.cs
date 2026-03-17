using UnityEngine;

namespace AcademicFlockingSimulation
{
    public static class SimulationMath
    {
        public static float DistanceXZSquared(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return (dx * dx) + (dz * dz);
        }

        public static Vector3 Flatten(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        public static Vector3 RandomInsideCircleXZ(float radius)
        {
            Vector2 sample = Random.insideUnitCircle * radius;
            return new Vector3(sample.x, 0f, sample.y);
        }
    }
}
