namespace FlockingSimulator.AIForVideogames
{
    internal sealed class SimulationRunningState
    {
        //state for running agent during the simulation
        private readonly SimulationFsmContext context;

        public SimulationRunningState(SimulationFsmContext fsmContext)
        {
            context = fsmContext;
        }

        public void Enter()
        {
            context.EnterRunning();
        }

        public void Stay()
        {
        }

        public void Exit()
        {
        }
    }
}
