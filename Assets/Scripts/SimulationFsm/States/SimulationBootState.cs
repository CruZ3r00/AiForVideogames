namespace FlockingSimulator.AIForVideogames
{
    internal sealed class SimulationBootState
    {
        private readonly SimulationFsmContext context;

        public SimulationBootState(SimulationFsmContext fsmContext)
        {
            context = fsmContext;
        }

        public void Enter()
        {
            context.RunBootstrap();
        }

        public void Stay()
        {
        }

        public void Exit()
        {
        }
    }
}
