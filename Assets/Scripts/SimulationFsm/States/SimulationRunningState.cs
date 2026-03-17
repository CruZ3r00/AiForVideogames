namespace AcademicFlockingSimulation
{
    internal sealed class SimulationRunningState
    {
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
