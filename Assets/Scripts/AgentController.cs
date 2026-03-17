using UnityEngine;

namespace AcademicFlockingSimulation
{
    [DefaultExecutionOrder(100)]
    public class AgentController : MonoBehaviour
    {
        [SerializeField] private float radius = 0.35f;
        [SerializeField] private float speed = 10f;
        [SerializeField] private float targetReachPadding = 0.15f;

        private SimulationManager simulationManager;
        private WorldManager worldManager;
        private FlockManager flockManager;
        private TargetManager targetManager;

        private int pathIndex;
        private int pathVersion = -1;

        private Vector3 currentForward = Vector3.forward;
        private Vector3 velocity;
        private Vector3 debugDesiredDirection;
        private Vector3 debugAvoidanceDirection;
        private Vector3 debugPathDirection;

        public int AgentId { get; private set; }
        public bool IsAlive { get; private set; }
        public float Radius => radius;
        public Vector3 Position => transform.position;
        public Vector3 ForwardVelocity => currentForward;

        public void Initialize(
            int agentId,
            SimulationManager manager,
            WorldManager world,
            FlockManager flock,
            TargetManager target)
        {
            AgentId = agentId;
            simulationManager = manager;
            worldManager = world;
            flockManager = flock;
            targetManager = target;

            pathIndex = 0;
            pathVersion = -1;
            IsAlive = true;
            enabled = true;

            Vector3 initialDirection = SimulationMath.Flatten(targetManager.CurrentTargetPosition - transform.position);
            if (initialDirection.sqrMagnitude < 0.001f)
            {
                initialDirection = SimulationMath.Flatten(worldManager.WorldCenter - transform.position);
            }

            currentForward = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector3.forward;
            velocity = currentForward * speed;

            Vector3 position = transform.position;
            position.y = worldManager.MovementY;
            transform.position = position;
            transform.forward = currentForward;
            gameObject.SetActive(true);
        }

        private void FixedUpdate()
        {
            if (!IsAlive || simulationManager == null || !simulationManager.IsRunning)
            {
                return;
            }

            if (worldManager.IsInsideDeathArea(transform.position, radius))
            {
                Die();
                return;
            }

            flockManager.ComputeNeighborhood(this, out Vector3 separation, out Vector3 alignment, out Vector3 cohesion, out _);

            Vector3 separationDirection = NormalizeFlat(separation);
            Vector3 alignmentDirection = NormalizeFlat(alignment);
            Vector3 cohesionDirection = NormalizeFlat(cohesion);
            Vector3 pathDirection = flockManager.GetPathDirection(transform.position, ref pathIndex, ref pathVersion);
            if (pathDirection.sqrMagnitude < 0.001f && targetManager.HasTarget)
            {
                pathDirection = NormalizeFlat(targetManager.CurrentTargetPosition - transform.position);
            }

            debugPathDirection = pathDirection;

            Vector3 steering =
                (pathDirection * flockManager.PathWeight) +
                (separationDirection * flockManager.SeparationWeight) +
                (alignmentDirection * flockManager.AlignmentWeight) +
                (cohesionDirection * flockManager.CohesionWeight);

            Vector3 avoidanceDirection = Vector3.zero;
            bool urgentAvoidance = TryComputeAvoidance(
                pathDirection.sqrMagnitude > 0.001f ? pathDirection : currentForward,
                out avoidanceDirection);

            if (urgentAvoidance)
            {
                steering =
                    (avoidanceDirection * flockManager.UrgentAvoidanceWeight) +
                    (pathDirection * flockManager.PathWeight * 0.5f) +
                    (separationDirection * flockManager.SeparationWeight * 0.5f);
            }
            else
            {
                steering += avoidanceDirection * flockManager.AvoidanceWeight;
            }

            if (steering.sqrMagnitude < 0.001f)
            {
                steering = pathDirection.sqrMagnitude > 0.001f
                    ? pathDirection
                    : currentForward;
            }

            Vector3 desiredDirection = NormalizeFlat(steering);
            if (desiredDirection.sqrMagnitude < 0.001f)
            {
                desiredDirection = currentForward;
            }

            debugDesiredDirection = desiredDirection;
            debugAvoidanceDirection = avoidanceDirection;

            currentForward = Vector3.RotateTowards(
                currentForward,
                desiredDirection,
                flockManager.TurnRateRadians * Time.fixedDeltaTime,
                0f);

            currentForward = NormalizeFlat(currentForward);
            if (currentForward.sqrMagnitude < 0.001f)
            {
                currentForward = desiredDirection;
            }

            velocity = currentForward * speed;

            Vector3 nextPosition = transform.position + (velocity * Time.fixedDeltaTime);
            nextPosition = worldManager.ClampInsideWorld(nextPosition, radius);
            nextPosition.y = worldManager.MovementY;
            transform.position = nextPosition;
            transform.forward = currentForward;

            if (worldManager.IsInsideDeathArea(transform.position, radius))
            {
                Die();
                return;
            }

            float reachDistance = radius + targetManager.TargetRadius + targetReachPadding;
            if (SimulationMath.DistanceXZSquared(transform.position, targetManager.CurrentTargetPosition) <= reachDistance * reachDistance)
            {
                simulationManager.NotifyTargetReached(this);
            }
        }

        public void ResetPathTracking()
        {
            pathIndex = 0;
            pathVersion = -1;
        }

        private bool TryComputeAvoidance(Vector3 guidanceDirection, out Vector3 avoidanceDirection)
        {
            Vector3 wallAvoidance = ComputeWallAvoidance() * flockManager.WallAvoidanceWeight;
            avoidanceDirection = wallAvoidance;

            Vector3 travelDirection = guidanceDirection.sqrMagnitude > 0.001f
                ? guidanceDirection.normalized
                : currentForward;

            if (travelDirection.sqrMagnitude < 0.001f)
            {
                return avoidanceDirection.sqrMagnitude > 0.6f;
            }

            Vector3 predictedVelocity = travelDirection * speed;
            float bestScore = 0f;
            bool urgent = false;

            for (int i = 0; i < worldManager.Obstacles.Count; i++)
            {
                ObstacleController obstacle = worldManager.Obstacles[i];
                if (obstacle == null)
                {
                    continue;
                }

                Vector3 relativePosition = obstacle.Position - transform.position;
                relativePosition.y = 0f;

                Vector3 relativeVelocity = obstacle.CurrentVelocity - predictedVelocity;
                relativeVelocity.y = 0f;

                float timeToClosest = 0f;
                float relativeSpeedSqr = relativeVelocity.sqrMagnitude;
                if (relativeSpeedSqr > 0.001f)
                {
                    timeToClosest = Mathf.Clamp(
                        -Vector3.Dot(relativePosition, relativeVelocity) / relativeSpeedSqr,
                        0f,
                        flockManager.AvoidanceLookAheadTime);
                }

                Vector3 futureAgentPosition = transform.position + (predictedVelocity * timeToClosest);
                Vector3 futureObstaclePosition = obstacle.GetPredictedPosition(timeToClosest);
                float safetyRadius = obstacle.DeathRadius + radius + flockManager.AvoidanceClearance;
                float closestDistanceSqr = SimulationMath.DistanceXZSquared(futureAgentPosition, futureObstaclePosition);

                if (closestDistanceSqr > safetyRadius * safetyRadius)
                {
                    continue;
                }

                float closestDistance = Mathf.Sqrt(closestDistanceSqr);
                float urgency = 1f - Mathf.Clamp01(closestDistance / safetyRadius);
                Vector3 away = futureAgentPosition - futureObstaclePosition;
                away.y = 0f;

                if (away.sqrMagnitude < 0.001f)
                {
                    away = Vector3.Cross(Vector3.up, travelDirection);
                }

                float score = urgency + (flockManager.AvoidanceLookAheadTime - timeToClosest);
                if (score > bestScore)
                {
                    bestScore = score;
                    avoidanceDirection = NormalizeFlat(away) + wallAvoidance;
                    urgent = timeToClosest < 0.2f || closestDistance < obstacle.DeathRadius + radius;
                }
            }

            avoidanceDirection = ClampFlatMagnitude(avoidanceDirection, 1f);
            return urgent;
        }

        private Vector3 ComputeWallAvoidance()
        {
            float buffer = flockManager.WallBufferDistance;
            Vector3 force = Vector3.zero;
            Vector2 min = worldManager.WorldMin;
            Vector2 max = worldManager.WorldMax;
            Vector3 position = transform.position;

            if (position.x < min.x + buffer)
            {
                force += Vector3.right * (1f - ((position.x - min.x) / buffer));
            }
            else if (position.x > max.x - buffer)
            {
                force += Vector3.left * (1f - ((max.x - position.x) / buffer));
            }

            if (position.z < min.y + buffer)
            {
                force += Vector3.forward * (1f - ((position.z - min.y) / buffer));
            }
            else if (position.z > max.y - buffer)
            {
                force += Vector3.back * (1f - ((max.y - position.z) / buffer));
            }

            return force;
        }

        private void Die()
        {
            if (!IsAlive)
            {
                return;
            }

            IsAlive = false;
            enabled = false;
            flockManager.RemoveAgent(this);
            gameObject.SetActive(false);
            simulationManager.NotifyAgentKilled(this);
            Destroy(gameObject);
        }

        private static Vector3 NormalizeFlat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 0.001f ? vector.normalized : Vector3.zero;
        }

        private static Vector3 ClampFlatMagnitude(Vector3 vector, float maxMagnitude)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > maxMagnitude * maxMagnitude ? vector.normalized * maxMagnitude : vector;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + (debugPathDirection * 2.5f));
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, transform.position + (debugDesiredDirection * 2.5f));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + (debugAvoidanceDirection * 2.5f));
        }
    }
}
