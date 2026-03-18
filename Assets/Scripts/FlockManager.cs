using System.Collections.Generic;
using UnityEngine;

namespace FlockingSimulator.AIForVideogames
{
    public class FlockManager : MonoBehaviour
    {
        [Header("Agent Prefab")]
        [SerializeField] private AgentController agentPrefab;

        [Header("Spawn")]
        [SerializeField] private float initialSpawnRadius = 6f;
        [SerializeField] private float reinforcementSpawnRadius = 3f;

        [Header("Neighborhood")]
        [SerializeField] private float neighborRadius = 6f;
        [SerializeField] private float separationRadius = 2.2f;

        [Header("Steering")]
        [SerializeField] private float separationWeight = 2.1f;
        [SerializeField] private float alignmentWeight = 1.0f;
        [SerializeField] private float cohesionWeight = 0.9f;
        [SerializeField] private float pathWeight = 4.2f;
        [SerializeField] private float avoidanceWeight = 5.5f;
        [SerializeField] private float urgentAvoidanceWeight = 10f;
        [SerializeField] private float wallAvoidanceWeight = 2.5f;
        [SerializeField] private float waypointReachDistance = 1.25f;
        [SerializeField] private int pathLookAheadSteps = 2;
        [SerializeField] private float avoidanceLookAheadTime = 0.8f;
        [SerializeField] private float avoidanceClearance = 0.75f;
        [SerializeField] private float maxTurnRateDegrees = 360f;
        [SerializeField] private float wallBufferDistance = 3f;

        private readonly List<AgentController> agents = new List<AgentController>(WorldManager.MaxAgentCount);

        private SimulationManager simulationManager;
        private WorldManager worldManager;
        private TargetManager targetManager;
        private PathfindingManager pathfindingManager;
        private int nextAgentId;

        public int AliveCount => agents.Count;
        public int MaxAgents => WorldManager.MaxAgentCount;
        public IReadOnlyList<AgentController> Agents => agents;

        public float NeighborRadius => neighborRadius;
        public float SeparationRadius => separationRadius;
        public float SeparationWeight => separationWeight;
        public float AlignmentWeight => alignmentWeight;
        public float CohesionWeight => cohesionWeight;
        public float PathWeight => pathWeight;
        public float AvoidanceWeight => avoidanceWeight;
        public float UrgentAvoidanceWeight => urgentAvoidanceWeight;
        public float WallAvoidanceWeight => wallAvoidanceWeight;
        public float WaypointReachDistance => waypointReachDistance;
        public int PathLookAheadSteps => pathLookAheadSteps;
        public float AvoidanceLookAheadTime => avoidanceLookAheadTime;
        public float AvoidanceClearance => avoidanceClearance;
        public float TurnRateRadians => maxTurnRateDegrees * Mathf.Deg2Rad;
        public float WallBufferDistance => wallBufferDistance;

        //initialize refernces to other managers
        public void Initialize(
            SimulationManager manager,
            WorldManager world,
            TargetManager target,
            PathfindingManager pathfinding)
        {
            simulationManager = manager;
            worldManager = world;
            targetManager = target;
            pathfindingManager = pathfinding;
        }

        //validate the scene setup for the flock manager, ensuring that the agent prefab is assigned
        public bool ValidateSceneSetup()
        {
            if (agentPrefab == null)
            {
                Debug.LogError("FlockManager requires an Agent prefab.", this);
                return false;
            }

            return true;
        }

        //cleanup any agents that were spawned during runtime, ensuring a fresh state for the simulation
        public void ClearRuntimeAgents()
        {
            if (worldManager != null && worldManager.AgentsParent != null)
            {
                AgentController[] spawnedAgents = worldManager.AgentsParent.GetComponentsInChildren<AgentController>(true);
                for (int i = 0; i < spawnedAgents.Length; i++)
                {
                    if (spawnedAgents[i] != null)
                    {
                        spawnedAgents[i].gameObject.SetActive(false);
                        Destroy(spawnedAgents[i].gameObject);
                    }
                }
            }

            agents.Clear();
            nextAgentId = 0;
        }

        //spawn the initial flock of agents near a specified corner of the world, using the world manager to find a suitable spawn location
        public void SpawnInitialFlock(WorldCorner spawnCorner)
        {
            Vector3 spawnCenter = worldManager.SampleNearCorner(spawnCorner, 10f, agentPrefab.Radius + 1f);
            SpawnAgentsInternal(spawnCenter, WorldManager.InitialAgentCount, initialSpawnRadius);
        }

        public void SpawnAgentsNear(Vector3 center, int additionalAgentCount)
        {
            int spawnCount = Mathf.Min(additionalAgentCount, WorldManager.MaxAgentCount - agents.Count);
            if (spawnCount <= 0)
            {
                return;
            }

            SpawnAgentsInternal(center, spawnCount, reinforcementSpawnRadius);
        }

        //function to get the center of the flock
        public Vector3 GetFlockCenter()
        {
            if (agents.Count == 0)
            {
                return worldManager.WorldCenter;
            }

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < agents.Count; i++)
            {
                AgentController agent = agents[i];
                if (agent != null)
                {
                    sum += agent.Position;
                }
            }

            sum /= Mathf.Max(1, agents.Count);
            sum.y = worldManager.MovementY;
            return sum;
        }

        //compute the neighborhood information for a given agent, calculating the separation, alignment, and cohesion vectors based on nearby agents within the specified radius
        public void ComputeNeighborhood(
            AgentController agent,
            out Vector3 separation,
            out Vector3 alignment,
            out Vector3 cohesion,
            out int neighborCount)
        {
            separation = Vector3.zero;
            alignment = Vector3.zero;
            cohesion = Vector3.zero;
            neighborCount = 0;

            float neighborRadiusSqr = neighborRadius * neighborRadius;
            float separationRadiusSqr = separationRadius * separationRadius;
            Vector3 position = agent.Position;

            for (int i = 0; i < agents.Count; i++)
            {
                AgentController other = agents[i];
                if (other == null || other == agent || !other.IsAlive)
                {
                    continue;
                }

                Vector3 offset = other.Position - position;
                offset.y = 0f;
                float distanceSqr = offset.sqrMagnitude;
                if (distanceSqr > neighborRadiusSqr || distanceSqr < 0.0001f)
                {
                    continue;
                }

                neighborCount++;
                alignment += other.ForwardVelocity;
                cohesion += other.Position;

                if (distanceSqr <= separationRadiusSqr)
                {
                    separation -= offset / distanceSqr;
                }
            }

            if (neighborCount > 0)
            {
                alignment /= neighborCount;
                alignment.y = 0f;

                cohesion = (cohesion / neighborCount) - position;
                cohesion.y = 0f;

                separation.y = 0f;
            }
        }

        //compute the direction of the flock towards the next waypoint in the path, using the pathfinding manager to find the look-ahead direciton
        public Vector3 GetPathDirection(Vector3 position, ref int pathIndex, ref int pathVersion)
        {
            return pathfindingManager.GetLookAheadDirection(
                position,
                ref pathIndex,
                ref pathVersion,
                pathLookAheadSteps,
                waypointReachDistance);
        }

        public void ResetAgentPathTracking()
        {
            for (int i = 0; i < agents.Count; i++)
            {
                AgentController agent = agents[i];
                if (agent != null)
                {
                    agent.ResetPathTracking();
                }
            }
        }

        //function to remove an agent if it dies
        public void RemoveAgent(AgentController agent)
        {
            agents.Remove(agent);
        }

        private void SpawnAgentsInternal(Vector3 center, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPosition = FindAgentSpawnPoint(center, radius);
                AgentController agent = Instantiate(agentPrefab, spawnPosition, Quaternion.identity, worldManager.AgentsParent);
                agent.name = $"{agentPrefab.name}_{nextAgentId:000}";
                agent.Initialize(
                    nextAgentId,
                    simulationManager,
                    worldManager,
                    this,
                    targetManager);

                agents.Add(agent);
                nextAgentId++;
            }
        }

        //find a spawn point for agent given a center and radius
        private Vector3 FindAgentSpawnPoint(Vector3 center, float radius)
        {
            float agentRadius = agentPrefab.Radius;

            for (int attempt = 0; attempt < 150; attempt++)
            {
                Vector3 candidate = worldManager.ClampInsideWorld(center + SimulationMath.RandomInsideCircleXZ(radius), agentRadius + 0.25f);
                if (worldManager.IsInsideDeathArea(candidate, agentRadius))
                {
                    continue;
                }

                bool overlaps = false;
                for (int i = 0; i < agents.Count; i++)
                {
                    AgentController agent = agents[i];
                    if (agent != null &&
                        SimulationMath.DistanceXZSquared(candidate, agent.Position) < (agentRadius * 2f) * (agentRadius * 2f))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    candidate.y = worldManager.MovementY;
                    return candidate;
                }
            }

            Vector3 fallback = worldManager.ClampInsideWorld(center, agentRadius + 0.25f);
            fallback.y = worldManager.MovementY;
            return fallback;
        }
    }
}
