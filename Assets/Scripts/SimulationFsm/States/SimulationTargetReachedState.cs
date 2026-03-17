namespace AcademicFlockingSimulation
{
    internal sealed class SimulationTargetReachedState
    {
        private readonly SimulationFsmContext context;

        public SimulationTargetReachedState(SimulationFsmContext fsmContext)
        {
            context = fsmContext;
        }

        public void Enter()
        {
            context.HandleTargetReached();
        }

        public void Stay()
        {
        }

        public void Exit()
        {
        }
    }
}
