namespace FlockingSimulator.AIForVideogames
{
    internal sealed class SimulationRespawnAgentsState
    {
        //state to reaspawn agent after target reached
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
