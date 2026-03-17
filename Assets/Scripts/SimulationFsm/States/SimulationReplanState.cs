namespace AcademicFlockingSimulation
{
    internal sealed class SimulationReplanState
    {
        private readonly SimulationFsmContext context;

        public SimulationReplanState(SimulationFsmContext fsmContext)
        {
            context = fsmContext;
        }

        public void Enter()
        {
            context.ExecuteReplan();
        }

        public void Stay()
        {
        }

        public void Exit()
        {
        }
    }
}
