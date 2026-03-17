# Academic Flocking Simulation – Project Documentation

## 1. Project Overview and Objectives

This project implements a real-time flock simulation in Unity where autonomous agents navigate a bounded 2D world (represented in Unity XZ space) while moving obstacles generate lethal regions the agents must avoid. The simulation combines two AI layers:

1. **Global navigation** through a grid-based pathfinding pipeline (A* + obstacle-aware occupancy + path simplification).
2. **Local steering** through Reynolds-inspired flocking behaviors (separation, alignment, cohesion), augmented with predictive obstacle and wall avoidance.

The primary objective appears to be the delivery of a didactic, assignment-oriented simulation architecture where:

- A target is spawned near a corner.
- A flock starts near the opposite corner.
- Obstacles move horizontally and create death zones.
- Agents attempt to survive and reach the target.
- Replanning and reinforcement spawning occur as the simulation progresses.

The codebase is structured around explicit manager components and a finite state machine (FSM) that coordinates lifecycle phases (boot, spawning, running, target handling, respawn, replan, failure). The design is intentionally modular and highly parameterized through Unity Inspector fields.

---

## 2. Scene and World Configuration

### 2.1 Geometric model

The environment is implemented as a **100 x 100 meter closed area**, validated by `WorldManager` against a required size constant (`RequiredWorldSize = 100f`). The world boundaries are inferred from four corner transforms (south-west, south-east, north-west, north-east), with fallback bounds also defined.

Although described as “2D” in assignment wording, the simulation runs in Unity 3D coordinates while constraining movement to a constant Y plane (`movementY`), effectively modeling 2D kinematics on XZ.

### 2.2 World entities

The runtime scene contains three categories of spawned entities:

- **Agents**, parented under an agents container transform.
- **Obstacles**, parented under an obstacles container transform.
- **Target**, parented under a targets container transform.

The system continuously clamps entities to world bounds and treats borders as hard limits. Agents do not pass through walls; their motion is clamped and additionally influenced by wall avoidance steering near edges.

### 2.3 Static requirements encoded in constants

`WorldManager` defines assignment-scale constants:

- `InitialAgentCount = 50`
- `MaxAgentCount = 50`
- `ObstacleCount = 10`
- `RequiredWorldSize = 100f`

This makes key assignment quantities explicit and centrally enforceable.

---

## 3. Main Architecture and Responsibilities

The architecture is manager-centric and separates concerns cleanly.

### 3.1 `SimulationManager` (orchestrator)

`SimulationManager` is the top-level runtime controller and owns:

- scene reference validation,
- manager initialization order,
- FSM construction and updates,
- periodic trigger checks in `FixedUpdate`,
- user-visible status GUI,
- high-level event notifications (`NotifyAgentKilled`, `NotifyTargetReached`).

It acts as the integration point between movement/AI logic and simulation lifecycle logic.

### 3.2 `WorldManager` (world + obstacle runtime)

`WorldManager` handles:

- world bounds, corners, and world geometry helpers,
- obstacle spawning and obstacle cleanup,
- obstacle spawn constraints (clearance from protected target/flock zones),
- death-area queries (`IsInsideDeathArea`),
- corner utilities (e.g., opposite corner selection).

It is both a geometry service and obstacle authority.

### 3.3 `FlockManager` (agent population + flock parameters)

`FlockManager` owns:

- runtime list of alive agents,
- initial and reinforcement spawning,
- neighborhood computation for boids components,
- all steering weights and flock tuning parameters,
- path-tracking reset for agents after replanning.

It provides the data abstractions each agent needs (neighbor vectors, path direction look-ahead, tuning constants).

### 3.4 `AgentController` (per-agent AI + kinematics)

`AgentController` is the core local intelligence unit. It performs, every physics step:

- death-area checks,
- flock neighborhood evaluation,
- path direction retrieval,
- steering fusion (path + boids + avoidance),
- urgent avoidance arbitration,
- turn-rate-limited heading update,
- kinematic integration (`position += velocity * dt`),
- target-reach detection and event signaling.

### 3.5 `TargetManager` (target lifecycle)

`TargetManager` handles:

- initial target spawning near a random corner,
- next target spawning near a different corner,
- target object management,
- reached-target counter.

### 3.6 `PathfindingManager` + pathfinding subsystem

The pathfinding stack includes:

- `PathGridGraph`: discretized world grid and neighbor generation,
- `PathOccupancyMap`: blocked cells and soft penalties from predicted obstacle positions,
- `PathAStarSolver`: weighted A* search,
- `PathPathSmoother`: post-process simplification with safety checks.

`PathfindingManager` coordinates data refresh, path computation, path safety checks, look-ahead direction extraction, and optional line rendering.

### 3.7 FSM layer

The simulation lifecycle is modeled through:

- generic FSM primitives in `SimulationFSMCore.cs`,
- context object `SimulationFsmContext` carrying flags and actions,
- explicit state classes for boot/spawn/run/target-reached/respawn/replan/failure.

This design decouples “what state means” from generic transition mechanics.

---

## 4. Agent AI Design

The AI is hybrid and hierarchical:

- **Global intention:** path from flock center to current target.
- **Local realization:** each agent computes individual steering under flock/social and safety constraints.

### 4.1 Guidance sources

Each agent derives movement from five directional influences:

1. **Path following** direction from look-ahead path node.
2. **Separation** from nearby agents.
3. **Alignment** with neighbors’ forward velocities.
4. **Cohesion** toward neighborhood center.
5. **Avoidance** from obstacle collision prediction + wall repulsion.

### 4.2 Arbitration logic

Normal condition:

`steering = path*w_path + sep*w_sep + align*w_align + coh*w_coh + avoid*w_avoid`

Urgent-threat condition (if predicted close danger):

`steering = avoid*w_urgent + path*(w_path*0.5) + sep*(w_sep*0.5)`

This explicit mode switch prioritizes survivability over formation quality.

### 4.3 Orientation and movement model

The desired steering is normalized and then applied through turn-rate-limited heading changes using `Vector3.RotateTowards`. Movement then uses fixed-speed kinematic integration without force accumulation from Unity physics.

Hence the implementation follows a deterministic steering + kinematic locomotion model, with smooth turning bounded by maximum angular speed.

---

## 5. Craig Reynolds Flocking Implementation

The flocking implementation is clearly derived from classical boids, with configurable radii and weights.

### 5.1 Neighborhood model

For each agent, `FlockManager.ComputeNeighborhood` iterates all alive peers and applies:

- **neighbor inclusion** if distance <= `neighborRadius`,
- **separation contribution** if distance <= `separationRadius`.

This enables dense short-range repulsion and wider-range social coupling.

### 5.2 Separation

Separation accumulates inverse-distance weighted repulsion:

`separation -= offset / distance²`

This strongly increases repulsion at short ranges and helps avoid local crowding and overlap pressure.

### 5.3 Alignment

Alignment averages neighbors’ forward velocities (`other.ForwardVelocity`) and uses the normalized result as heading consensus.

### 5.4 Cohesion

Cohesion is computed as vector from current position to average neighbor position:

`cohesion = mean(neighborPositions) - selfPosition`

### 5.5 Weighting and tuning

Default inspector values in `FlockManager` indicate intended balance:

- separation weight > alignment and cohesion,
- strong path and avoidance priorities,
- additional urgent avoidance mode.

This combination makes the flock goal-directed while retaining group structure.

---

## 6. Integration of Flocking with Target Seeking and Path Following

### 6.1 Global path generation target

Path planning is performed from **flock center** to current target, not per-agent start nodes. This produces one shared corridor for group coherence and lower computational cost.

### 6.2 Per-agent path progression

Each agent tracks its own path index and path version. On replan, version changes and agents remap to their closest node in the new path. During motion, each agent:

- advances index when a waypoint is reached,
- may skip ahead if the next point is closer,
- uses configurable look-ahead steps to avoid over-reactive motion.

### 6.3 Fallback guidance

If path direction is unavailable but target exists, the agent falls back to direct target seeking (flattened vector to target). This prevents complete stalling when a path is temporarily empty.

### 6.4 Social-vs-goal behavior

Because path weight is high and combined with flock terms, the system behaves as a goal-seeking flock rather than independent boids. Social terms modulate local spacing and heading consistency around a shared global intention.

---

## 7. Obstacle Avoidance Design and Implementation

Obstacle avoidance is **not** limited to boids separation; it is implemented as predictive hazard assessment against moving obstacles.

### 7.1 Predictive relative motion

For each obstacle, the agent computes:

- relative position,
- relative velocity (obstacle velocity - predicted agent velocity),
- time-to-closest-approach (clamped to look-ahead horizon).

It then evaluates future positions for both agent and obstacle at that time and checks against a safety radius:

`safetyRadius = obstacleDeathRadius + agentRadius + clearance`

### 7.2 Threat scoring and response

If predicted distance violates safety radius, the candidate is scored by urgency and temporal proximity. The highest-scoring threat generates avoidance direction.

Urgent events are detected when impact is imminent (`timeToClosest < 0.2`) or very near lethal radius.

### 7.3 Wall avoidance

Agents also compute a wall repulsion vector within a configurable border buffer distance, steering away from near-edge regions. This operates in parallel with obstacle avoidance and prevents wall trapping.

### 7.4 Avoidance in steering fusion

- Normal mode: avoidance added as weighted term.
- Urgent mode: avoidance dominates and non-safety terms are reduced.

This layered logic provides both continuous safety bias and emergency evasive behavior.

---

## 8. Pathfinding and Navigation System

### 8.1 Grid representation

The world is discretized by `cellSize` (default 1 meter) into a 2D grid graph with 8-connected neighbors (orthogonal + diagonal moves).

### 8.2 Dynamic occupancy and penalties

`PathOccupancyMap.Rebuild` samples each moving obstacle across multiple future time samples over prediction horizon. For each sampled obstacle position:

- nodes within blocked radius are marked unwalkable,
- nearby nodes within extra penalty radius receive cost penalties.

This yields a time-expanded approximation of obstacle risk without full spatiotemporal planning.

### 8.3 A* solver

`PathAStarSolver` uses classic A* with:

- octile-like heuristic (`14` diagonal, `10` orthogonal),
- movement costs (10/14),
- additive occupancy penalty costs.

Blocked nodes are excluded; high-penalty nodes are still possible but disfavored.

### 8.4 Path simplification

After A*, `PathPathSmoother` applies a line-of-sight style simplification (“keep furthest safe node”) where safety is validated by sampling segments against blocked and high-penalty thresholds.

### 8.5 Replanning policy

During running state, replanning is requested when either:

- replan interval elapses (`replanInterval`), or
- current path becomes unsafe under refreshed dynamic occupancy.

This periodic/reactive replanning is key for moving-obstacle environments.

---

## 9. Kinematic Movement Implementation

The project honors the kinematic requirement by computing motion explicitly, not through physics-driven forces.

### 9.1 Kinematic update equation

For each agent:

1. compute desired heading from steering,
2. rotate current heading toward desired using max turn rate,
3. set velocity = heading * fixed speed,
4. integrate: `nextPosition = currentPosition + velocity * fixedDeltaTime`.

### 9.2 Constraints and projection

- All vectors are flattened to XZ for behavior logic.
- Y is fixed to world movement plane.
- World bounds are enforced by clamping with radius padding.

### 9.3 Physics usage

No Rigidbody force integration appears in movement logic; motion is transform-driven in `FixedUpdate`. Thus agent position is not computed by Unity physics subsystem.

---

## 10. Obstacle Spawning and Movement

### 10.1 Spawn strategy

Ten obstacles are instantiated per session. Spawn points are sampled per shuffled lane to spread obstacles across depth (`z`) bands and avoid dense overlap.

Additional constraints include clearance from:

- initial target zone,
- initial flock center,
- previously spawned obstacles.

### 10.2 Obstacle kinematics

Each obstacle moves horizontally (x-axis) with speed sampled in `[5, 10]` and bounces at world x boundaries by reflecting overshoot and reversing direction.

### 10.3 Lethal region

Each obstacle defines:

- body radius (`1` by default),
- death radius (`5` by default),

and provides a geometric lethal-point test used by agents and spawn filters.

---

## 11. Death Area Handling and Agent Removal

Agents check lethal inclusion before and after movement each fixed step. On lethal entry:

1. `IsAlive` set false,
2. agent removed from flock list,
3. object disabled and destroyed,
4. `SimulationManager.NotifyAgentKilled` invoked.

If flock alive count reaches zero, FSM failure is requested and simulation transitions to `Failed`, where path is cleared and failure UI can be shown.

This ensures deterministic elimination and consistent simulation termination semantics.

---

## 12. Target Spawning and Progression Logic

### 12.1 Initial target

Initial target corner is sampled uniformly among four corners, then a position is sampled near that corner within max distance (`10`). Invalid target positions inside death areas are retried.

### 12.2 Corner progression rule

After a target is reached, `TargetManager` increments reached counter and selects a new corner different from previous corner (not merely different position).

### 12.3 Reinforcement

When target-reached state transitions to respawn state, flock manager spawns up to 5 additional agents around reached target position, capped by global max 50.

This supports assignment-style replenishment after casualties.

---

## 13. Simulation Flow and State Management

The FSM sequence is:

1. **Boot**: clear runtime entities/path.
2. **SpawnInitialEntities**: spawn target, initial flock, obstacles.
3. **Replan**: compute first path and reset path tracking.
4. **Running**: agents/obstacles move; triggers monitored.
5. **TargetReached**: spawn next target.
6. **RespawnAgents**: add reinforcements near reached target.
7. **Replan**: compute path to new target.
8. repeat running loop.
9. **Failed**: entered when all agents are dead.

Immediate states are advanced automatically in a single frame loop (`AdvanceImmediateStates`) so the simulation quickly settles into running unless blocked/failing.

This stateful orchestration makes runtime behavior explicit, testable, and easier to reason about.

---

## 14. Inspector-Exposed Parameters and Tuning Choices

The implementation exposes many assignment-relevant tunables.

### 14.1 Flocking and steering parameters (`FlockManager`)

- neighborhood and separation radii,
- boids weights,
- path/avoidance/urgent-avoidance weights,
- waypoint reach distance and path look-ahead,
- avoidance prediction horizon and clearance,
- max turning rate,
- wall buffer and wall weight,
- initial/reinforcement spawn radii.

### 14.2 Pathfinding parameters (`PathfindingManager`)

- grid cell size,
- obstacle extra penalty radius,
- dynamic prediction horizon and sample count,
- optional path rendering offset.

### 14.3 World and obstacle parameters (`WorldManager` + `ObstacleController`)

- obstacle speed range,
- obstacle spawn padding/clearance,
- movement plane Y,
- obstacle radius/death radius.

### 14.4 Target parameters (`TargetManager`)

- target radius,
- max distance from selected corner,
- spawn padding.

The breadth of these parameters indicates a design optimized for iterative tuning and assignment demonstrations.

---

## 15. Specification Compliance (Concise)

### Overall verdict

**Partially compliant** with the assignment specification.

### Matches observed

- 100x100 bounded world enforced.
- 10 moving obstacles, horizontal back-and-forth motion.
- Obstacle speed sampled between 5 and 10.
- Obstacle death radius represented and used for lethal checks.
- Initial target near random corner (distance-limited).
- Initial flock size 50 near opposite corner.
- Agent nominal speed set to 10.
- Kinematic movement (transform integration; no physics-driven final position).
- Obstacle avoidance implemented beyond pure separation.
- Target progression and agent reinforcement (up to +5, cap 50).
- Simulation failure when all agents die.

### Non-literal / ambiguous / potential mismatches

1. **Obstacle non-collision guarantee is not strict**: obstacle spawning uses lane separation + clearance heuristics, but runtime movement does not explicitly enforce obstacle-obstacle non-collision constraints beyond initial spacing.
2. **Path planning origin uses flock center, not each agent**: acceptable as group planning approximation, but assignment text can be interpreted as path for flock movement in aggregate; this is an implementation choice.
3. **New target corner exclusion**: implementation excludes only the previous corner, matching stated exclusion of current corner; however exact randomness distribution and corner policy granularity are implementation-defined.
4. **Target and obstacle visual properties** (colors/radii in rendered mesh/material scale) cannot be fully verified from scripts alone; logical radii are present in code.
5. **Obstacle avoidance certainty**: although predictive avoidance is implemented, success in “keeping all agents alive” is not guaranteed algorithmically, only attempted behaviorally.

---

## 16. Strengths, Limitations, and Possible Improvements

### 16.1 Strengths

- Clear modular architecture with strong separation of concerns.
- Robust lifecycle control via FSM.
- Hybrid AI combining global planning and local reactive steering.
- Predictive obstacle modeling used both in pathfinding and local avoidance.
- Good inspector parameterization for experimental tuning.
- Explicit assignment constants improve traceability.

### 16.2 Limitations

- O(N²) neighbor search in flocking can become expensive for larger swarms.
- Single shared path from flock center may disadvantage outlier agents.
- Dynamic obstacle treatment is sampled approximation, not full time-parameterized planning.
- Obstacle non-collision over time is not guaranteed by runtime controller.
- No explicit recovery policy for chronic pathfinding failure (beyond repeated replanning).

### 16.3 Improvements

- Spatial partitioning (grid/hash) for neighborhood queries.
- Multi-path or subgroup planning for heterogeneous flock spread.
- Velocity-obstacle / ORCA-like local collision avoidance for stronger guarantees.
- Runtime obstacle deconfliction to enforce non-collision constraints literally.
- Adaptive replan interval based on hazard density and path volatility.
- Metrics logging (survival rate, average detour, target time) for quantitative evaluation.

---

## 17. Conclusion

The repository presents a coherent academic implementation of flock-based autonomous navigation in a hazardous dynamic environment. The central AI contribution is the integration of Reynolds-style social steering with predictive safety behavior and dynamic path planning, coordinated through a finite-state simulation loop.

From a pedagogical perspective, the project is strong: it exposes key AI concepts (local vs global control, reactive vs deliberative navigation, state-driven orchestration, dynamic hazard modeling) in a readable and configurable codebase. The implementation is largely aligned with assignment goals and constraints, while still containing a few non-literal aspects and practical approximations typical of real-time simulation systems.

Overall, this is a solid university-level project with a clear focus on autonomous agent AI and a technically meaningful balance between behavioral realism, computational feasibility, and software structure.
