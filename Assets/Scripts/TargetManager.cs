using UnityEngine;

namespace AcademicFlockingSimulation
{
    public class TargetManager : MonoBehaviour
    {
        [SerializeField] private GameObject targetPrefab;
        [SerializeField] private float targetRadius = 0.1f;
        [SerializeField] private float maxCornerDistance = 10f;
        [SerializeField] private float spawnPadding = 1f;

        private WorldManager worldManager;
        private GameObject currentTarget;

        public int reachedTargetCount { get; private set; }
        public bool HasTarget => currentTarget != null;
        public WorldCorner CurrentCorner { get; private set; }
        public float TargetRadius => targetRadius;
        public Vector3 CurrentTargetPosition => currentTarget != null ? currentTarget.transform.position : Vector3.zero;

        public void Initialize(SimulationManager manager, WorldManager world)
        {
            worldManager = world;
            reachedTargetCount = 0;
        }

        public bool ValidateSceneSetup()
        {
            if (targetPrefab == null)
            {
                Debug.LogError("TargetManager requires a Target prefab.", this);
                return false;
            }

            return true;
        }

        public void ClearTarget()
        {
            if (currentTarget != null)
            {
                currentTarget.SetActive(false);
                Destroy(currentTarget);
                currentTarget = null;
            }

            if (worldManager != null && worldManager.TargetsParent != null)
            {
                for (int i = worldManager.TargetsParent.childCount - 1; i >= 0; i--)
                {
                    Transform child = worldManager.TargetsParent.GetChild(i);
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }

        public WorldCorner SpawnInitialTarget()
        {
            CurrentCorner = (WorldCorner)Random.Range(0, 4);
            SpawnTarget(CurrentCorner);
            return CurrentCorner;
        }

        public void SpawnNextTargetDifferentCorner()
        {
            reachedTargetCount++;
            WorldCorner previousCorner = CurrentCorner;
            WorldCorner nextCorner = previousCorner;

            while (nextCorner == previousCorner)
            {
                nextCorner = (WorldCorner)Random.Range(0, 4);
            }

            CurrentCorner = nextCorner;
            SpawnTarget(CurrentCorner);
        }

        private void SpawnTarget(WorldCorner corner)
        {
            if (currentTarget == null)
            {
                currentTarget = Instantiate(targetPrefab, worldManager.TargetsParent);
                currentTarget.name = targetPrefab.name;
            }

            currentTarget.transform.position = FindValidTargetPosition(corner);
            currentTarget.SetActive(true);
        }

        private Vector3 FindValidTargetPosition(WorldCorner corner)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                Vector3 candidate = worldManager.SampleNearCorner(corner, maxCornerDistance, spawnPadding);
                candidate.y = worldManager.MovementY;

                if (!worldManager.IsInsideDeathArea(candidate, targetRadius + 0.5f))
                {
                    return candidate;
                }
            }

            Vector3 fallback = worldManager.SampleNearCorner(corner, maxCornerDistance * 0.5f, spawnPadding);
            fallback.y = worldManager.MovementY;
            return fallback;
        }
    }
}
