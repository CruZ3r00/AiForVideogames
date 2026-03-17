namespace AcademicFlockingSimulation
{
    internal sealed class SimulationRespawnAgentsState
    {
        private readonly SimulationFsmContext context;

        public SimulationRespawnAgentsState(SimulationFsmContext fsmContext)
        {
            context = fsmContext;
        }

        public void Enter()
        {
            context.RespawnAgents();
        }

        public void Stay()
        {
        }

        public void Exit()
        {
        }
    }
}
