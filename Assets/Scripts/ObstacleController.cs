using UnityEngine;

namespace FlockingSimulator.AIForVideogames
{
    [DefaultExecutionOrder(-50)]
    public class ObstacleController : MonoBehaviour
    {
        [SerializeField] private float radius = 1f;
        [SerializeField] private float deathRadius = 5f;

        private SimulationManager simulationManager;
        private WorldManager worldManager;
        private float speed;
        private float direction;
        private float minX;
        private float maxX;

        public Vector3 Position => transform.position;
        public Vector3 CurrentVelocity { get; private set; }
        public float Radius => radius;
        public float DeathRadius => deathRadius;

        public void Initialize(SimulationManager manager, WorldManager world, Vector3 startPosition, float obstacleSpeed)
        {
            simulationManager = manager;
            worldManager = world;
            speed = obstacleSpeed;
            direction = Random.value > 0.5f ? 1f : -1f;
            minX = worldManager.WorldMin.x + radius;
            maxX = worldManager.WorldMax.x - radius;

            transform.position = new Vector3(startPosition.x, worldManager.MovementY, startPosition.z);
            CurrentVelocity = new Vector3(direction * speed, 0f, 0f);
        }

        private void FixedUpdate()
        {
            if (simulationManager == null || !simulationManager.IsRunning)
            {
                return;
            }

            float newX = transform.position.x + (direction * speed * Time.fixedDeltaTime);
            if (newX <= minX)
            {
                newX = minX + (minX - newX);
                direction = 1f;
            }
            else if (newX >= maxX)
            {
                newX = maxX - (newX - maxX);
                direction = -1f;
            }

            transform.position = new Vector3(newX, worldManager.MovementY, transform.position.z);
            CurrentVelocity = new Vector3(direction * speed, 0f, 0f);
        }

        public Vector3 GetPredictedPosition(float secondsAhead)
        {
            if (speed <= 0f)
            {
                return transform.position;
            }

            float predictedX = transform.position.x;
            float predictedDirection = direction;
            float remainingTime = Mathf.Max(0f, secondsAhead);

            while (remainingTime > 0f)
            {
                float boundary = predictedDirection > 0f ? maxX : minX;
                float distanceToBoundary = Mathf.Abs(boundary - predictedX);
                float timeToBoundary = distanceToBoundary / speed;

                if (timeToBoundary > remainingTime)
                {
                    predictedX += predictedDirection * speed * remainingTime;
                    remainingTime = 0f;
                }
                else
                {
                    predictedX = boundary;
                    predictedDirection *= -1f;
                    remainingTime -= timeToBoundary;
                }
            }

            return new Vector3(predictedX, worldManager != null ? worldManager.MovementY : transform.position.y, transform.position.z);
        }

        public bool ContainsLethalPoint(Vector3 point, float radiusPadding)
        {
            float allowedRadius = deathRadius + radiusPadding;
            return SimulationMath.DistanceXZSquared(point, transform.position) <= allowedRadius * allowedRadius;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, deathRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + (CurrentVelocity.normalized * 2f));
        }
    }
}
