namespace FlockingSimulator.AIForVideogames
{
    //support enum for fsm states
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
