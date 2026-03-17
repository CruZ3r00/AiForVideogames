namespace AcademicFlockingSimulation
{
    internal sealed class SimulationFailedState
    {
        private readonly SimulationFsmContext context;

        public SimulationFailedState(SimulationFsmContext fsmContext)
        {
            context = fsmContext;
        }

        public void Enter()
        {
            context.EnterFailed();
        }

        public void Stay()
        {
        }

        public void Exit()
        {
        }
    }
}
