using UnityEngine;

namespace AcademicFlockingSimulation
{
    [DefaultExecutionOrder(-1000)]
    public class SimulationManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private WorldManager worldManager;
        [SerializeField] private PathfindingManager pathfindingManager;
        [SerializeField] private FlockManager flockManager;
        [SerializeField] private TargetManager targetManager;

        [Header("Runtime")]
        [SerializeField] private bool startSimulationOnStart = true;
        [SerializeField] private float replanInterval = 0.5f;

        private FSM simulationFsm;
        private SimulationFsmContext fsmContext;

        public SimulationState CurrentState { get; private set; }
        public bool IsRunning => CurrentState == SimulationState.Running;
        public bool IsFailed => CurrentState == SimulationState.Failed;
        internal float ReplanInterval => replanInterval;

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        private void Awake()
        {
            if (!ValidateSceneSetup())
            {
                enabled = false;
                return;
            }

            worldManager.Initialize(this);
            pathfindingManager.Initialize(this, worldManager);
            targetManager.Initialize(this, worldManager);
            flockManager.Initialize(this, worldManager, targetManager, pathfindingManager);
            fsmContext = new SimulationFsmContext(this, worldManager, pathfindingManager, flockManager, targetManager);
        }

        private void Start()
        {
            if (startSimulationOnStart && enabled)
            {
                BeginSimulation();
            }
        }

        private void FixedUpdate()
        {
            if (simulationFsm == null)
            {
                return;
            }

            if (IsRunning)
            {
                fsmContext.UpdateRunningTriggers(Time.fixedDeltaTime);
            }

            simulationFsm.Update();
            AdvanceImmediateStates();
        }

        public void BeginSimulation()
        {
            if (!ValidateSceneSetup())
            {
                enabled = false;
                return;
            }

            fsmContext.ResetSession();
            simulationFsm = CreateSimulationFsm();
            AdvanceImmediateStates();
        }

        public void NotifyAgentKilled(AgentController agent)
        {
            if (simulationFsm == null || IsFailed)
            {
                return;
            }

            if (flockManager.AliveCount == 0)
            {
                fsmContext.QueueFailure();
            }
        }

        public void NotifyTargetReached(AgentController agent)
        {
            if (simulationFsm == null || !IsRunning || IsFailed)
            {
                return;
            }

            fsmContext.QueueTargetReached(targetManager.CurrentTargetPosition);
        }

        internal void ClearRuntimeScene()
        {
            flockManager.ClearRuntimeAgents();
            targetManager.ClearTarget();
            worldManager.ClearRuntimeObstacles();
            pathfindingManager.ClearPath();
        }

        private bool ValidateSceneSetup()
        {
            bool hasReferences =
                worldManager != null &&
                pathfindingManager != null &&
                flockManager != null &&
                targetManager != null;

            if (!hasReferences)
            {
                Debug.LogError("SimulationManager requires WorldManager, PathfindingManager, FlockManager, and TargetManager references.", this);
                return false;
            }

            return worldManager.ValidateSceneSetup() &&
                   pathfindingManager.ValidateSceneSetup() &&
                   flockManager.ValidateSceneSetup() &&
                   targetManager.ValidateSceneSetup();
        }

        internal void SetCompatibilityState(SimulationState newState)
        {
            CurrentState = newState;
        }

        private FSM CreateSimulationFsm()
        {
            SimulationBootState bootStateLogic = new SimulationBootState(fsmContext);
            SimulationSpawnInitialEntitiesState spawnStateLogic = new SimulationSpawnInitialEntitiesState(fsmContext);
            SimulationRunningState runningStateLogic = new SimulationRunningState(fsmContext);
            SimulationTargetReachedState targetReachedStateLogic = new SimulationTargetReachedState(fsmContext);
            SimulationRespawnAgentsState respawnStateLogic = new SimulationRespawnAgentsState(fsmContext);
            SimulationReplanState replanStateLogic = new SimulationReplanState(fsmContext);
            SimulationFailedState failedStateLogic = new SimulationFailedState(fsmContext);

            FSMState bootState = new FSMState();
            bootState.enterActions.Add(bootStateLogic.Enter);
            bootState.stayActions.Add(bootStateLogic.Stay);
            bootState.exitActions.Add(bootStateLogic.Exit);

            FSMState spawnState = new FSMState();
            spawnState.enterActions.Add(spawnStateLogic.Enter);
            spawnState.stayActions.Add(spawnStateLogic.Stay);
            spawnState.exitActions.Add(spawnStateLogic.Exit);

            FSMState runningState = new FSMState();
            runningState.enterActions.Add(runningStateLogic.Enter);
            runningState.stayActions.Add(runningStateLogic.Stay);
            runningState.exitActions.Add(runningStateLogic.Exit);

            FSMState targetReachedState = new FSMState();
            targetReachedState.enterActions.Add(targetReachedStateLogic.Enter);
            targetReachedState.stayActions.Add(targetReachedStateLogic.Stay);
            targetReachedState.exitActions.Add(targetReachedStateLogic.Exit);

            FSMState respawnState = new FSMState();
            respawnState.enterActions.Add(respawnStateLogic.Enter);
            respawnState.stayActions.Add(respawnStateLogic.Stay);
            respawnState.exitActions.Add(respawnStateLogic.Exit);

            FSMState replanState = new FSMState();
            replanState.enterActions.Add(replanStateLogic.Enter);
            replanState.stayActions.Add(replanStateLogic.Stay);
            replanState.exitActions.Add(replanStateLogic.Exit);

            FSMState failedState = new FSMState();
            failedState.enterActions.Add(failedStateLogic.Enter);
            failedState.stayActions.Add(failedStateLogic.Stay);
            failedState.exitActions.Add(failedStateLogic.Exit);

            bootState.AddTransition(new FSMTransition(() => fsmContext.BootstrapComplete), spawnState);

            spawnState.AddTransition(new FSMTransition(() => fsmContext.FailedRequested), failedState);
            spawnState.AddTransition(new FSMTransition(() => fsmContext.SpawnComplete), replanState);

            runningState.AddTransition(new FSMTransition(() => fsmContext.FailedRequested), failedState);
            runningState.AddTransition(new FSMTransition(() => fsmContext.TargetReachedRequested), targetReachedState);
            runningState.AddTransition(new FSMTransition(() => fsmContext.ReplanRequested), replanState);

            targetReachedState.AddTransition(new FSMTransition(() => fsmContext.FailedRequested), failedState);
            targetReachedState.AddTransition(new FSMTransition(() => fsmContext.TargetHandled), respawnState);

            respawnState.AddTransition(new FSMTransition(() => fsmContext.FailedRequested), failedState);
            respawnState.AddTransition(new FSMTransition(() => fsmContext.RespawnComplete), replanState);

            replanState.AddTransition(new FSMTransition(() => fsmContext.FailedRequested), failedState);
            replanState.AddTransition(new FSMTransition(() => fsmContext.ReplanComplete), runningState);

            return new FSM(bootState);
        }

        private void AdvanceImmediateStates()
        {
            if (simulationFsm == null)
            {
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                if (!IsImmediateState(CurrentState))
                {
                    break;
                }

                SimulationState before = CurrentState;
                simulationFsm.Update();
                if (CurrentState == before)
                {
                    break;
                }
            }
        }

        private static bool IsImmediateState(SimulationState state)
        {
            return state == SimulationState.Boot ||
                   state == SimulationState.SpawnInitialEntities ||
                   state == SimulationState.TargetReached ||
                   state == SimulationState.RespawnAgents ||
                   state == SimulationState.Replan;
        }

        private void AutoAssignReferences()
        {
            if (worldManager == null)
            {
                worldManager = FindFirstObjectByType<WorldManager>();
            }

            if (pathfindingManager == null)
            {
                pathfindingManager = FindFirstObjectByType<PathfindingManager>();
            }

            if (flockManager == null)
            {
                flockManager = FindFirstObjectByType<FlockManager>();
            }

            if (targetManager == null)
            {
                targetManager = FindFirstObjectByType<TargetManager>();
            }
        }

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle failStyle;

        private void OnGUI()
        {
            if (!enabled)
            {
                return;
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
                labelStyle = new GUIStyle(GUI.skin.label);
                failStyle = new GUIStyle(GUI.skin.box);

                boxStyle.fontSize = 28;
                labelStyle.fontSize = 24;
                failStyle.fontSize = 22;

                boxStyle.alignment = TextAnchor.UpperCenter;
                failStyle.alignment = TextAnchor.MiddleCenter;
            }

            GUI.Box(new Rect(60f, 60f, 600f, 300f), "Simulation", boxStyle);
            GUI.Label(new Rect(90f, 110f, 500f, 40f), $"State: {CurrentState}", labelStyle);
            GUI.Label(new Rect(90f, 150f, 500f, 40f), $"Agents Alive: {flockManager.AliveCount}/{flockManager.MaxAgents}", labelStyle);
            GUI.Label(new Rect(90f, 190f, 500f, 40f), $"Obstacles: {worldManager.Obstacles.Count}", labelStyle);
            GUI.Label(new Rect(90f, 230f, 500f, 40f), $"Path Nodes: {pathfindingManager.CurrentPath.Count}", labelStyle);
            GUI.Label(new Rect(90f, 270f, 500f, 40f), $"Target Corner: {targetManager.CurrentCorner}", labelStyle);
            GUI.Label(new Rect(90f, 310f, 500f, 40f), $"Target Reached: {targetManager.reachedTargetCount}", labelStyle);

            if (IsFailed)
            {
                GUI.Box(new Rect(Screen.width - 420f, 20f, 400f, 80f), "Failure: all agents died", failStyle);
            }
        }
    }
}
