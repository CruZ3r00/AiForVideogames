# Academic Flocking Simulation – Project Documentation

## 1. Project Overview and Objectives

This project implements a real-time flock simulation in Unity in which a group of autonomous agents moves inside a closed **100 × 100 meter** world while trying to reach a sequence of targets and survive the presence of moving lethal obstacles. Although the assignment describes a 2D environment, the implementation uses Unity world space and constrains all gameplay movement to the **XZ plane** with a constant Y coordinate, effectively creating a planar simulation.

The AI architecture is explicitly hybrid and layered:

1. **Global navigation** is handled through a grid-based pathfinding pipeline built around a graph representation, a dynamic occupancy map, an A* solver, and path smoothing.
2. **Local steering** is handled per agent through Craig Reynolds flocking terms—**separation, alignment, and cohesion**—combined with path following, predictive obstacle avoidance, and wall avoidance.

The project is organized as an academic simulation rather than a game in the usual sense. The most important design goal is not visual spectacle but a clear implementation of course concepts: finite state machines, kinematic movement, boids, path planning, dynamic obstacle handling, and scene-driven Unity architecture.

At runtime, the simulation repeatedly executes the following loop:

- spawn a target near a corner of the map,
- spawn a flock near the opposite corner,
- spawn moving obstacles with lethal death areas,
- compute a safe path toward the current target,
- move the flock while blending flocking and avoidance behaviours,
- spawn a new target when one agent reaches it,
- respawn some agents after a successful target reach,
- end the simulation if all agents die.

The resulting project is a clear example of how **global planning** and **local AI steering** can be combined in a dynamic environment.

---

## 2. Scene and World Configuration

### 2.1 World model

The world is managed by `WorldManager`, which enforces the following assignment-level constants:

- `InitialAgentCount = 50`
- `MaxAgentCount = 50`
- `ObstacleCount = 10`
- `RequiredWorldSize = 100f`

The actual playable area is derived from four corner transforms assigned in the scene:

- south-west,
- south-east,
- north-west,
- north-east.

From these transforms, `WorldManager` computes:

- `WorldMin`
- `WorldMax`
- `WorldWidth`
- `WorldDepth`
- `WorldCenter`

The manager validates that the scene really defines a **100 × 100** meter area. If the corner transforms are missing or the size is wrong, the simulation is rejected during setup.

### 2.2 Scene-driven architecture

The implementation is **scene-driven**, not runtime-bootstrap driven. The scene must already contain:

- a `SimulationManager`,
- a `WorldManager`,
- a `PathfindingManager`,
- a `FlockManager`,
- a `TargetManager`,
- parent transforms for agents, obstacles, and targets,
- corner reference transforms.

This means the visual world, bounds, camera, and organizational GameObjects are configured directly in Unity, while the code handles logic, spawning, and runtime updates.

### 2.3 Runtime entities

There are three runtime entity categories:

- **Agents**, parented under `AgentsParent`
- **Obstacles**, parented under `ObstaclesParent`
- **Targets**, parented under `TargetsParent`

Agents and obstacles are instantiated from prefabs. The target is instantiated from a prefab as well, but only one active target exists at a time.

### 2.4 Borders as walls

The assignment requires the world borders to act as walls. In this implementation the effect is achieved in two complementary ways:

1. **Hard spatial enforcement** through `WorldManager.ClampInsideWorld`, which prevents agents and other entities from leaving the valid world bounds.
2. **Behavioural wall avoidance** inside `AgentController.ComputeWallAvoidance`, which pushes agents away from borders before they reach the clamp limit.

This combination creates both a strict geometric bound and a more natural steering response near the edges.

---

## 3. Main Architecture and Responsibilities

The codebase is organized around specialized manager components and per-entity controllers.

### 3.1 `SimulationManager`

`SimulationManager` is the top-level orchestrator of the project. Its responsibilities include:

- validating scene references,
- initializing the other managers,
- building and updating the FSM,
- monitoring running-state triggers,
- receiving notifications from agents,
- clearing runtime content,
- exposing runtime state through an on-screen GUI.

It stores the current simulation state using the `SimulationState` enum:

- `Boot`
- `SpawnInitialEntities`
- `Running`
- `TargetReached`
- `RespawnAgents`
- `Replan`
- `Failed`

It also exposes two important status booleans:

- `IsRunning`
- `IsFailed`

### 3.2 `WorldManager`

`WorldManager` is responsible for geometry, scene references, and obstacle management. More precisely, it handles:

- world bounds,
- corner references,
- obstacle spawning and cleanup,
- sampling positions near corners,
- lethal-area checks,
- clamping positions inside the world,
- selecting opposite corners.

It is therefore the main service for spatial reasoning in the simulation.

### 3.3 `FlockManager`

`FlockManager` controls the flock as a runtime population and as a shared parameter source. Its responsibilities include:

- spawning the initial flock,
- spawning reinforcements,
- maintaining the list of alive agents,
- computing neighborhood terms,
- exposing all flocking and steering weights,
- providing path-following direction through the pathfinding subsystem,
- resetting agent path tracking after replanning.

### 3.4 `AgentController`

`AgentController` is the main AI class for each individual agent. It is responsible for:

- storing agent state (`AgentId`, `IsAlive`, radius, speed),
- per-frame steering computation,
- path progress tracking (`pathIndex`, `pathVersion`),
- death detection,
- target reach detection,
- heading rotation and movement integration,
- debug visualization of path, desired direction, and avoidance.

### 3.5 `TargetManager`

`TargetManager` controls target lifecycle and progression. It handles:

- initial target spawning,
- subsequent target spawning on a different corner,
- target destruction and cleanup,
- reached-target counting,
- logical target radius management.

### 3.6 `ObstacleController`

`ObstacleController` is the per-obstacle logic class. It manages:

- horizontal motion,
- speed and direction,
- future position prediction,
- lethal area geometry,
- debug gizmos.

### 3.7 `PathfindingManager` and its helper classes

`PathfindingManager` coordinates the navigation stack. Its core collaborators are:

- `PathGridGraph`
- `PathGridNode`
- `PathOccupancyMap`
- `PathAStarSolver`
- `PathPathSmoother`

From the code and the provided screenshots, this subsystem clearly follows a graph-based pathfinding architecture with explicit node representation and post-processing.

### 3.8 FSM subsystem

The simulation flow uses a dedicated FSM layer implemented through:

- `FSMCondition`
- `FSMAction`
- `FSMTransition`
- `FSMState`
- `FSM`

The state logic is then distributed across concrete state classes such as:

- `SimulationBootState`
- `SimulationSpawnInitialEntitiesState`
- `SimulationRunningState`
- `SimulationTargetReachedState`
- `SimulationRespawnAgentsState`
- `SimulationReplanState`
- `SimulationFailedState`

This matches the academic style of separating **generic state machine mechanics** from **project-specific state behaviour**.

---

## 4. Agent AI Design

The agent AI is built as a **hybrid steering architecture** with a shared global plan and locally reactive execution.

### 4.1 Initialization phase

When an agent is instantiated, `AgentController.Initialize` assigns:

- its numeric ID,
- references to the manager objects,
- path tracking state,
- alive state,
- initial heading,
- initial velocity.

The initial heading is computed from the current target position. If the vector toward the target is almost zero, the agent falls back to the vector from its position to the world center. This prevents undefined initial orientation.

### 4.2 Main update loop

The AI logic runs in `FixedUpdate`, consistent with the kinematic requirement. In each step, the agent:

1. checks whether it is alive and whether the simulation is running;
2. checks whether it is already inside a death area;
3. retrieves the current neighborhood steering components from `FlockManager`;
4. queries a path-following direction;
5. computes a blended steering vector;
6. computes obstacle and wall avoidance;
7. applies urgent or non-urgent steering arbitration;
8. rotates its heading toward the desired direction;
9. integrates movement using fixed speed;
10. clamps position inside the world;
11. re-checks death-area intersection after movement;
12. checks whether the target has been reached.

This structure makes the agent logic deterministic and easy to inspect.

### 4.3 Direction sources

Each agent combines five distinct behaviour sources:

- **Separation**: local repulsion from nearby agents.
- **Alignment**: heading agreement with neighbors.
- **Cohesion**: attraction toward neighborhood center.
- **Path following**: movement toward the shared global path.
- **Obstacle avoidance**: predictive reaction to moving lethal obstacles plus wall repulsion.

The path direction is obtained through `flockManager.GetPathDirection`, which internally delegates to `PathfindingManager.GetLookAheadDirection`.

### 4.4 Behaviour arbitration

The normal steering equation is:

```text
steering =
    pathDirection * PathWeight +
    separationDirection * SeparationWeight +
    alignmentDirection * AlignmentWeight +
    cohesionDirection * CohesionWeight
```

Then avoidance is integrated.

If no urgent collision is detected, avoidance is simply added:

```text
steering += avoidanceDirection * AvoidanceWeight
```

If an urgent risk is detected, the agent switches to a different arbitration rule:

```text
steering =
    avoidanceDirection * UrgentAvoidanceWeight +
    pathDirection * (PathWeight * 0.5f) +
    separationDirection * (SeparationWeight * 0.5f)
```

This is an important detail of the implementation: **urgent avoidance is not just a larger additive weight**, but a different behavioural mode that suppresses part of the normal steering blend.

### 4.5 Turn-rate-limited kinematics

After computing the desired steering direction, the agent does not snap instantly to it. Instead, `Vector3.RotateTowards` is used to rotate the current heading toward the desired direction with an angular speed cap given by `TurnRateRadians`.

This produces smoother and more believable motion than directly assigning the new direction.

### 4.6 Velocity and movement

Once the heading has been updated, the movement model is simply:

```text
velocity = currentForward * speed
nextPosition = currentPosition + velocity * fixedDeltaTime
```

This is a pure kinematic integration scheme and is fully compliant with the “no Unity physics for final movement” requirement.

---

## 5. Craig Reynolds Flocking Implementation

The implementation clearly follows the classic Craig Reynolds flocking model based on three core terms.

### 5.1 Neighborhood collection

Neighborhood computation is centralized in `FlockManager.ComputeNeighborhood`. For each alive agent in the list, the manager:

- discards null references,
- ignores the querying agent itself,
- ignores dead agents,
- checks distance against `neighborRadius`,
- optionally contributes to separation if distance is below `separationRadius`.

This is a direct all-pairs neighborhood scan over the alive agent list.

### 5.2 Separation

Separation is computed as a repulsive vector when another agent is within the short-range separation radius:

```text
separation -= offset / distanceSquared
```

Because the term is inversely proportional to distance squared, repulsion grows strongly at close range. This prevents local overlap and encourages spacing.

### 5.3 Alignment

Alignment is computed as the average of the neighbors’ forward velocity vectors:

```text
alignment += other.ForwardVelocity
alignment /= neighborCount
```

This encourages agents to turn toward the local heading consensus.

### 5.4 Cohesion

Cohesion is computed by averaging neighbor positions and subtracting the current agent position:

```text
cohesion = averageNeighborPosition - selfPosition
```

This creates attraction toward the center of the local group.

### 5.5 Configurable weights

The flocking terms are all weighted through Inspector-exposed parameters in `FlockManager`:

- `separationWeight`
- `alignmentWeight`
- `cohesionWeight`

The code therefore implements Reynolds flocking directly, but as part of a larger steering blend that also includes path guidance and avoidance.

---

## 6. Integration of Flocking with Target Seeking and Path Following

A major strength of this project is that it does not treat flocking and navigation as unrelated systems.

### 6.1 Shared global path

The pathfinding system produces a shared path that the flock can follow. The exact path is stored in `PathfindingManager.CurrentPath`, while agents receive only a direction extracted from it.

This means the path is not computed independently per agent. Instead, the flock follows a common navigation structure while each agent still reacts locally in its own way.

### 6.2 Per-agent path progress

Each agent stores:

- `pathIndex`
- `pathVersion`

If the global path changes, the path version changes as well. When this happens, the agent remaps itself to the closest path node through `PathfindingManager.GetLookAheadDirection`.

This is a practical way to handle replanning in dynamic environments without forcing all agents back to the beginning of the path.

### 6.3 Look-ahead path following

The path-following method does not simply aim at the current waypoint. Instead, it uses:

- waypoint reach checks,
- closest-node correction,
- configurable look-ahead steps.

As a result, agents tend to follow a **future direction along the path** rather than oscillating around the nearest node.

### 6.4 Fallback target seeking

If the path direction is temporarily unavailable but a target still exists, the agent uses a direct target vector:

```text
targetManager.CurrentTargetPosition - transform.position
```

This avoids complete loss of guidance when the path is empty or being recomputed.

---

## 7. Obstacle Avoidance Design and Implementation

The assignment explicitly states that obstacle avoidance cannot rely only on the separation term of boids. The implementation respects this requirement through a dedicated predictive avoidance system.

### 7.1 Combined wall and obstacle avoidance

`TryComputeAvoidance` begins by computing wall avoidance through `ComputeWallAvoidance`. This produces a repulsion force from nearby borders based on a configurable `WallBufferDistance`.

Then the method evaluates moving obstacles one by one.

### 7.2 Relative motion prediction

For each obstacle, the agent computes:

- relative position,
- relative velocity,
- time to closest approach,
- future agent position,
- future obstacle position.

This uses the obstacle’s current velocity and the predicted agent velocity along the current travel direction.

### 7.3 Safety radius

The avoidance check uses a conservative safety radius:

```text
safetyRadius = obstacle.DeathRadius + agentRadius + AvoidanceClearance
```

So the agent is not just checking the exact lethal boundary, but a buffered version of it.

### 7.4 Threat selection

If the future distance is inside the safety radius, the obstacle becomes a threat candidate. Each candidate is scored using:

- urgency based on proximity,
- temporal proximity based on `timeToClosest`.

The best-scoring threat defines the final avoidance direction.

### 7.5 Urgent avoidance trigger

Urgency becomes “critical” when either:

- `timeToClosest < 0.2f`
- or the closest distance is already below `obstacle.DeathRadius + radius`

This triggers the special urgent-avoidance steering mode described earlier.

### 7.6 Obstacle prediction support

The obstacle controller also provides `GetPredictedPosition(secondsAhead)`, which simulates future obstacle position under horizontal bouncing motion. This ensures that local avoidance uses obstacle motion prediction rather than treating obstacles as static.

---

## 8. Pathfinding and Navigation System

The pathfinding subsystem is one of the most important parts of the project because it connects assignment-level navigation requirements with runtime AI behaviour.

### 8.1 Grid graph representation

`PathfindingManager.Initialize` builds the navigation stack using:

- `PathGridGraph`
- `PathOccupancyMap`
- `PathAStarSolver`
- `PathPathSmoother`

The grid is parameterized by `cellSize`, which defaults to **1 meter**, making the graph resolution naturally aligned with the metric scale of the world.

### 8.2 World/grid conversion

The manager exposes:

- `WorldToGrid(Vector3 worldPosition)`
- `GridToWorld(int x, int z)`

These functions bridge the continuous world used by the agents and the discrete graph used by the planner.

### 8.3 Dynamic occupancy map

Before building or validating a path, `PathfindingManager` calls `RefreshDynamicData`, which rebuilds the occupancy map from the current obstacles.

The occupancy map uses:

- `extraPenaltyRadius`
- `dynamicObstaclePredictionTime`
- `dynamicObstaclePredictionSteps`

This indicates that the path planner is not just reacting to current obstacle positions, but to a short future prediction horizon.

### 8.4 Path search and smoothing

The planning sequence is:

1. rebuild dynamic occupancy,
2. map start and goal to graph nodes,
3. find nearest walkable alternatives if necessary,
4. run A* search,
5. simplify the resulting raw path,
6. update the current path and path version.

This is visible in `TryBuildPath`, which stores both a `rawPath` and a simplified `currentPath`.

### 8.5 Path safety monitoring

The method `IsCurrentPathUnsafe` reevaluates the current path against the updated dynamic occupancy. If a path node becomes blocked or receives too high a penalty, the path is considered unsafe.

This is important because obstacles are moving continuously, so a previously good path may become dangerous later.

### 8.6 Optional visualization

If a `LineRenderer` is assigned, the current path is drawn in world space. This is useful both for debugging and for academic demonstration, because it makes the relationship between A* planning and flock motion visible.

---

## 9. Kinematic Movement Implementation

The assignment explicitly requires a kinematic movement model. The implementation satisfies this requirement clearly.

### 9.1 No physics-based final motion

The agents do not rely on:

- `Rigidbody` force integration,
- `AddForce`,
- `MovePosition`,
- or Unity physics for the final motion update.

The final position is computed directly in `AgentController.FixedUpdate`.

### 9.2 Movement pipeline

The motion pipeline is:

1. compute steering,
2. normalize steering direction,
3. rotate heading using `Vector3.RotateTowards`,
4. set velocity from heading and speed,
5. integrate new position manually,
6. clamp inside the world.

### 9.3 Constant speed

Each agent uses a serialized `speed` field set by default to **10f**, matching the assignment requirement.

### 9.4 Obstacle motion is also kinematic

Obstacle movement is also manually integrated inside `ObstacleController.FixedUpdate` by advancing the X coordinate and reflecting motion at world boundaries.

So the entire simulation obeys a kinematic update philosophy.

---

## 10. Obstacle Spawning and Movement

### 10.1 Runtime spawning

`WorldManager.SpawnObstacles` instantiates exactly `ObstacleCount` obstacles, i.e. **10** obstacles per simulation session.

### 10.2 Spawn constraints

Obstacle spawning protects both the initial target area and the initial flock area through `obstacleSpawnClearance`. It also uses lane-based placement across the Z axis to reduce overlap and improve distribution.

### 10.3 Horizontal motion

Each obstacle moves only along the X axis. The movement logic is:

- move in the current horizontal direction,
- if the obstacle exceeds the left bound, reflect position and invert direction,
- if the obstacle exceeds the right bound, reflect position and invert direction.

This creates the required back-and-forth horizontal motion.

### 10.4 Speed range

Obstacle speed is sampled in:

```text
Random.Range(obstacleMinSpeed, obstacleMaxSpeed)
```

with defaults:

- minimum speed = `5f`
- maximum speed = `10f`

This matches the assignment.

### 10.5 Lethal radius

Each obstacle stores:

- `radius = 1f`
- `deathRadius = 5f`

The lethal radius is used by `ContainsLethalPoint` and therefore by both:

- agent death checks,
- spawn safety validation.

---

## 11. Death Area Handling and Agent Removal

Death handling is implemented explicitly and consistently.

### 11.1 Death checks

An agent checks lethal-area intersection twice per physics step:

1. at the beginning of `FixedUpdate`,
2. after the movement update.

This reduces the chance that a fast-moving agent survives a lethal crossing because it started outside but ended inside the death area.

### 11.2 Death procedure

When the agent dies, `Die()` performs the following actions:

1. sets `IsAlive = false`,
2. disables the component,
3. removes the agent from `FlockManager`,
4. disables the GameObject,
5. notifies `SimulationManager`,
6. destroys the GameObject.

This ensures that dead agents:

- are not rendered anymore,
- are not included in neighbor computation,
- are not counted as active flock members,
- no longer move or reach targets.

### 11.3 Failure condition

`SimulationManager.NotifyAgentKilled` checks whether the flock is empty. If `AliveCount == 0`, the simulation requests failure and eventually transitions to the `Failed` state.

This is fully consistent with the assignment requirement that the simulation ends if all agents die.

---

## 12. Target Spawning and Progression Logic

### 12.1 Initial target

The initial target is spawned through `TargetManager.SpawnInitialTarget`, which:

- chooses one of the four corners uniformly at random,
- stores that corner in `CurrentCorner`,
- spawns the target within `maxCornerDistance` from that corner.

The default `maxCornerDistance` is **10f**, which matches the assignment.

### 12.2 Target validity

Target placement is not accepted blindly. `FindValidTargetPosition` retries candidate positions and rejects those that would fall inside an obstacle death area.

This is a sensible practical extension, because otherwise the simulation might generate unwinnable or unfair target states.

### 12.3 Next target rule

When a target is reached, `SpawnNextTargetDifferentCorner`:

- increments the reached-target count,
- remembers the previous corner,
- selects a new corner until it differs from the previous one,
- spawns the new target there.

This follows the assignment rule that the current corner must be excluded when placing the next target.

### 12.4 Reinforcement spawning

After a successful target reach, the flock can receive reinforcements through `FlockManager.SpawnAgentsNear`. The number of new agents is capped so that the total number of alive agents never exceeds 50.

---

## 13. Simulation Flow and State Management

The overall runtime flow is controlled through an FSM.

### 13.1 FSM core

The FSM core is minimal and course-oriented:

- `FSMTransition` stores one condition and optional actions,
- `FSMState` stores enter/stay/exit actions and outgoing transitions,
- `FSM` updates the current state and fires the first valid transition.

This structure is very close to the standard academic FSM style discussed in AI/game-development courses.

### 13.2 Simulation states

`SimulationManager.CreateSimulationFsm` builds the following states:

- Boot
- SpawnInitialEntities
- Running
- TargetReached
- RespawnAgents
- Replan
- Failed

These states are mapped to the `SimulationState` enum and to dedicated logic classes.

### 13.3 Transition logic

The key transitions are:

- `Boot -> SpawnInitialEntities`
- `SpawnInitialEntities -> Replan`
- `Replan -> Running`
- `Running -> TargetReached`
- `Running -> Replan`
- `Running -> Failed`
- `TargetReached -> RespawnAgents`
- `RespawnAgents -> Replan`
- and failure transitions from several states.

This flow cleanly separates setup, runtime motion, success handling, and failure handling.

### 13.4 Immediate states

Some states are meant to execute immediately without waiting for many frames. `SimulationManager.AdvanceImmediateStates` repeatedly updates the FSM until the simulation reaches a non-immediate state or no further transition occurs.

This is a smart implementation detail because it prevents the simulation from getting visually stuck in short setup states.

### 13.5 Runtime GUI

`SimulationManager.OnGUI` displays useful runtime information, including:

- current state,
- alive agents,
- obstacle count,
- path node count,
- current target corner,
- reached target count,
- failure message if applicable.

This is useful both for debugging and for presentation during an exam.

---

## 14. Inspector-Exposed Parameters and Tuning Choices

One of the strengths of the project is how many important behavioural values are exposed in the Inspector.

### 14.1 Flock tuning (`FlockManager`)

The following parameters are exposed:

- initial spawn radius,
- reinforcement spawn radius,
- neighbor radius,
- separation radius,
- separation weight,
- alignment weight,
- cohesion weight,
- path weight,
- avoidance weight,
- urgent avoidance weight,
- wall avoidance weight,
- waypoint reach distance,
- path look-ahead steps,
- avoidance look-ahead time,
- avoidance clearance,
- max turn rate degrees,
- wall buffer distance.

The default values in the current scripts are:

- separation weight = `2.1`
- alignment weight = `1.0`
- cohesion weight = `0.9`
- path weight = `4.2`
- avoidance weight = `5.5`
- urgent avoidance weight = `10.0`

These values indicate a strongly goal-oriented and safety-oriented flock, with comparatively lower cohesion by default.

### 14.2 Pathfinding tuning (`PathfindingManager`)

Exposed parameters include:

- `cellSize = 1f`
- `extraPenaltyRadius = 3f`
- `dynamicObstaclePredictionTime = 0.8f`
- `dynamicObstaclePredictionSteps = 4`

These values show that the planner operates at 1-meter grid resolution with moderate future prediction.

### 14.3 World and obstacle tuning (`WorldManager`, `ObstacleController`)

Important exposed values include:

- obstacle spawn padding,
- obstacle spawn clearance,
- obstacle speed range,
- movement Y level,
- obstacle radius,
- obstacle death radius.

### 14.4 Target tuning (`TargetManager`)

Exposed target parameters include:

- target radius,
- max corner distance,
- spawn padding.

Because these values are editable from the Inspector, the project supports meaningful experimentation and balancing.

---

## 15. Specification Compliance

### 15.1 Overall verdict

The project is **largely compliant** with the assignment and is very close to literal compliance in most of the important technical areas. The main possible deviations are implementation-level approximations rather than conceptual violations.

### 15.2 Requirements satisfied clearly

The following requirements are directly reflected in the code:

- **100 × 100 world** enforced by `WorldManager.ValidateSceneSetup`
- **50 initial agents** through `WorldManager.InitialAgentCount`
- **maximum flock size of 50** through `WorldManager.MaxAgentCount`
- **10 obstacles** through `WorldManager.ObstacleCount`
- **horizontal obstacle motion** in `ObstacleController.FixedUpdate`
- **obstacle speed in [5, 10]** via `WorldManager`
- **death radius of 5** through `ObstacleController.deathRadius`
- **target near a random corner within 10 meters** through `TargetManager`
- **initial flock near opposite corner** through `WorldManager.GetOppositeCorner`
- **agent speed of 10 m/s** through `AgentController.speed`
- **Craig Reynolds flocking terms** through `FlockManager.ComputeNeighborhood`
- **pathfinding-based target navigation** through `PathfindingManager`
- **kinematic movement** through manual transform integration
- **obstacle avoidance not reduced to separation** through `AgentController.TryComputeAvoidance`
- **respawn of up to 5 agents after target reach** through `FlockManager.SpawnAgentsNear`
- **simulation failure when all agents die** through `SimulationManager.NotifyAgentKilled`

### 15.3 Points that are not perfectly literal

There are, however, a few aspects that are best described as approximations or implementation details rather than exact literal guarantees:

1. **Obstacle non-collision during runtime**
   
   The assignment requires obstacles to move horizontally without colliding with each other. The implementation tries to enforce separation at spawn time using lanes and clearance constraints, but there is no explicit runtime obstacle-obstacle collision resolution. In practice the lane design reduces the problem strongly, but the guarantee is not formalized as a dedicated runtime collision system.

2. **Path planning origin**
   
   The project uses a shared path and per-agent local path following rather than fully independent path computation per agent. This is a sensible flock-level interpretation of the assignment, but it is still an implementation choice.

3. **Visual verification of colors and mesh radii**
   
   The scripts define logical radii and behaviour, but colors and exact visual mesh scale depend on the scene and prefabs, not only on the scripts.

4. **“Trying to keep all agents alive”**
   
   The AI clearly attempts survival through predictive avoidance and replanning, but of course the code does not guarantee that no agent will ever die. This is normal and should not be interpreted as a violation.

### 15.4 Final compliance assessment

From the code provided, the project should be considered **academically compliant and technically well aligned with the assignment**, with the only notable caveat being the absence of an explicit runtime obstacle-obstacle deconfliction mechanism.

---

## 16. Strengths, Limitations, and Possible Improvements

### 16.1 Strengths

The strongest points of the project are:

- clear modular architecture,
- proper separation between global planning and local steering,
- faithful use of Reynolds flocking,
- dedicated predictive avoidance beyond simple boid separation,
- scene-driven Unity setup,
- fully kinematic motion,
- explicit FSM-based simulation flow,
- good Inspector configurability for tuning.

### 16.2 Limitations

The most important limitations are:

- neighborhood search is a simple all-pairs scan and therefore scales as O(N²),
- shared-path navigation may disadvantage strongly separated outlier agents,
- obstacle future handling is predictive but still approximate,
- runtime obstacle-obstacle non-collision is not enforced explicitly,
- default steering values may cause the flock to be more spread out than desired unless tuned.

### 16.3 Possible improvements

Reasonable future extensions would include:

- spatial partitioning for faster neighbor queries,
- stronger flock-regrouping logic for distant outliers,
- more advanced local collision avoidance techniques,
- explicit runtime obstacle lane deconfliction,
- richer runtime metrics such as survival percentage, travel time, and average dispersion.

---

## 17. Conclusion

This project is a strong academic implementation of a flocking-and-navigation simulation in Unity. Its design is not based on shortcuts such as NavMesh agents or physics-driven movement. Instead, it explicitly combines:

- kinematic locomotion,
- finite state machine orchestration,
- grid-based pathfinding,
- dynamic obstacle prediction,
- and Reynolds-style flocking.

The most interesting aspect of the project is the way in which **global path planning** and **local flock intelligence** are integrated. The flock does not simply seek the target directly, nor does it behave as a pure boids toy model detached from the goal. Instead, each agent contributes to a coordinated group that follows a shared path while still reacting to local neighbors, borders, and moving lethal threats.

For a university AI/game-development assignment, this is exactly the kind of project that shows understanding of both theoretical course concepts and practical Unity implementation.
