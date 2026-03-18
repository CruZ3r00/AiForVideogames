namespace FlockingSimulator.AIForVideogames
{
    public enum SimulationState
    {
        Boot,
        SpawnInitialEntities,
        Running,
        TargetReached,
        RespawnAgents,
        Replan,
        Failed
    }
}
