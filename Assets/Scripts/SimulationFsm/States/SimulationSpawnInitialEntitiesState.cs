namespace FlockingSimulator.AIForVideogames
{
    internal sealed class SimulationSpawnInitialEntitiesState
    {
        private readonly SimulationFsmContext context;

        public SimulationSpawnInitialEntitiesState(SimulationFsmContext fsmContext)
        {
            context = fsmContext;
        }

        public void Enter()
        {
            context.SpawnInitialEntities();
        }

        public void Stay()
        {
        }

        public void Exit()
        {
        }
    }
}
