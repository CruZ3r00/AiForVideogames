namespace FlockingSimulator.AIForVideogames
{
    internal sealed class SimulationReplanState
    {
        //state to replan the path
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
