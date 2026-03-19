using UnityEngine;

namespace FlockingSimulator.AIForVideogames
{
    //clss to hold the context of FSM through the simulation
    internal sealed class SimulationFsmContext
    {
        private readonly SimulationManager owner;
        private readonly WorldManager worldManager;
        private readonly PathfindingManager pathfindingManager;
        private readonly FlockManager flockManager;
        private readonly TargetManager targetManager;

        private float replanTimer;

        public SimulationFsmContext(
            SimulationManager simulationManager,
            WorldManager world,
            PathfindingManager pathfinding,
            FlockManager flock,
            TargetManager target)
        {
            owner = simulationManager;
            worldManager = world;
            pathfindingManager = pathfinding;
            flockManager = flock;
            targetManager = target;
        }

        public bool BootstrapComplete { get; private set; }
        public bool SpawnComplete { get; private set; }
        public bool TargetHandled { get; private set; }
        public bool RespawnComplete { get; private set; }
        public bool ReplanComplete { get; private set; }
        public bool ReplanRequested { get; private set; }
        public bool FailedRequested { get; private set; }
        public bool TargetReachedRequested { get; private set; }
        public Vector3 ReachedTargetPosition { get; private set; }

        public void ResetSession()
        {
            replanTimer = 0f;
            BootstrapComplete = false;
            SpawnComplete = false;
            TargetHandled = false;
            RespawnComplete = false;
            ReplanComplete = false;
            ReplanRequested = false;
            FailedRequested = false;
            TargetReachedRequested = false;
            ReachedTargetPosition = Vector3.zero;
        }

        public void QueueFailure()
        {
            FailedRequested = true;
        }

        //step into the target reached state by setting the variables for conditions
        public void QueueTargetReached(Vector3 reachedTargetPosition)
        {
            if (TargetReachedRequested || FailedRequested)
            {
                return;
            }

            TargetReachedRequested = true;
            TargetHandled = false;
            RespawnComplete = false;
            ReachedTargetPosition = reachedTargetPosition;
        }

        //set the variables to step into the replan state if needed
        public void UpdateRunningTriggers(float deltaTime)
        {
            if (FailedRequested)
            {
                return;
            }

            if (flockManager.AliveCount == 0)
            {
                QueueFailure();
                return;
            }

            replanTimer += deltaTime;
            if (replanTimer >= owner.ReplanInterval || pathfindingManager.IsCurrentPathUnsafe(worldManager.Obstacles))
            {
                ReplanRequested = true;
            }
        }

        public void RunBootstrap()
        {
            owner.SetCompatibilityState(SimulationState.Boot);
            BootstrapComplete = false;
            owner.ClearRuntimeScene();
            BootstrapComplete = true;
        }

        //spawn initial entities and set the variables for the transition conditions to step into the next state
        public void SpawnInitialEntities()
        {
            owner.SetCompatibilityState(SimulationState.SpawnInitialEntities);
            SpawnComplete = false;

            WorldCorner targetCorner = targetManager.SpawnInitialTarget();
            WorldCorner flockCorner = worldManager.GetOppositeCorner(targetCorner);

            flockManager.SpawnInitialFlock(flockCorner);
            worldManager.SpawnObstacles(targetManager.CurrentTargetPosition, flockManager.GetFlockCenter());

            SpawnComplete = true;
            ReplanRequested = true;
        }

        //reset the variables for the transition conditions to step into the next state
        public void EnterRunning()
        {
            owner.SetCompatibilityState(SimulationState.Running);
            ReplanRequested = false;
            ReplanComplete = false;
            TargetHandled = false;
            RespawnComplete = false;
            replanTimer = 0f;
        }

        //handle the target reached event, set the variables for the transition conditions 
        // to step into the next state
        public void HandleTargetReached()
        {
            owner.SetCompatibilityState(SimulationState.TargetReached);
            TargetHandled = false;

            if (TargetReachedRequested)
            {
                targetManager.SpawnNextTargetDifferentCorner();
            }

            TargetHandled = true;
        }

        //set the variables to step into the state who respawn agents when target is reached
        public void RespawnAgents()
        {
            owner.SetCompatibilityState(SimulationState.RespawnAgents);
            RespawnComplete = false;

            flockManager.SpawnAgentsNear(ReachedTargetPosition, 5);

            RespawnComplete = true;
            TargetReachedRequested = false;
            TargetHandled = false;
            ReplanRequested = true;
        }

        //set the variables to step into the following state
        public void ExecuteReplan()
        {
            owner.SetCompatibilityState(SimulationState.Replan);
            ReplanComplete = false;

            if (FailedRequested || flockManager.AliveCount == 0 || !targetManager.HasTarget)
            {
                FailedRequested = true;
                ReplanComplete = true;
                return;
            }

            pathfindingManager.TryBuildPath(
                flockManager.GetFlockCenter(),
                targetManager.CurrentTargetPosition,
                worldManager.Obstacles);

            flockManager.ResetAgentPathTracking();
            replanTimer = 0f;
            ReplanRequested = false;
            ReplanComplete = true;
        }

        //set to handle failing state
        public void EnterFailed()
        {
            owner.SetCompatibilityState(SimulationState.Failed);
            pathfindingManager.ClearPath();
        }
    }
}
