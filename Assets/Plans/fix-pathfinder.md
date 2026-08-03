# Project Overview
- Game Title: Cat Maze Procedural Level
- High-Level Concept: A 2D top-down maze-solver game where a cat player navigates a procedurally generated maze, teleports through cave portals, breaks destructible barriers with an axe, collects a key, and reaches the exit door.
- Players: Single player
- Inspiration / Reference Games: Classic 2D maze games
- Tone / Art Direction: Cute, playful, 2D pixel-art style
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: Landscape
- Render Pipeline: URP

# Game Mechanics
## Core Gameplay Loop
The player spawns, navigates the maze to reach a cave portal, teleports to an area containing an axe, collects the axe, destroys barriers blocking the path to the key, collects the key, and then navigates to the exit door (the goal) to complete the level.
## Controls and Input Methods
Standard 2D top-down grid movement using Keyboard (WASD or Arrow keys), with an action/interaction button ("E" key) used to swing the axe and destroy breakable barriers.

# UI
Standard HUD displaying collected items (Axe, Key) and debug console metrics, alongside overlay panels for level completion and metrics tracking.

# Key Asset & Context
- `Assets/scripts/Algorithm/MazePathfinder.cs`: Contains the main BFS/Dijkstra pathfinding algorithm used to compute optimal walking and mechanic routes from the key to the goal.
- `Assets/scripts/Validation/LevelValidator.cs`: Contains the validation-time pathfinding algorithms used during procedural level generation.
- `Assets/scripts/Generation/DynamicLevelManager.cs`: Handles procedural level construction, item placement, metric tracking on level completion, and triggers errors if pathfinding consistency checks fail.

# Bug Analysis & Validation Trace
1. **Procedural Level Generation & Validation (Pre-Instantiation)**:
   - Before actual prefab spawning, `DynamicLevelManager` validates that the generated grid is solvable using `LevelValidator.CanPathfind(..., hasAxe = true)`.
   - At this stage, the barriers are not yet physical objects and have NOT been marked as occupied in `MazeData`.
   - `LevelValidator.CanPathfind` checks if a neighbor cell is walkable. Since the barrier cells are not yet marked as occupied, `mazeData.IsWalkable` returns `true`.
   - It then hits `if (barriers.Contains(neighbor))` and correctly verifies that `hasAxe == true`, successfully passing the validation check.
2. **Physical Instantiation & Occupancy Marking**:
   - Once validated, physical objects are spawned, and `mazeData.MarkCellsAsOccupied(bar, 1, 1)` is called for all barriers.
   - This marks those cells as occupied, causing `mazeData.IsWalkable` to return `false` from that point onward.
3. **Key Collection & Runtime Pathfinding**:
   - When the player collects the key, `DynamicLevelManager.OnKeyCollectedForPathfinding` is invoked, which calculates the optimal remaining walking path:
     `keyToGoalWalkingResult = MazePathfinder.FindWalkingPath(..., hasAxe = true)`
   - Within `MazePathfinder.CanEnter`:
     ```csharp
     if (barriers != null && barriers.Contains(cell) && !hasAxe) return false;
     ...
     return mazeData.IsWalkable(cell.x, cell.y);
     ```
     Since `hasAxe` is `true`, it bypasses the first check and returns `mazeData.IsWalkable(cell.x, cell.y)`.
   - But because the barrier cell is physically marked as occupied, `IsWalkable` returns `false`!
   - Consequently, the pathfinder is completely blocked from passing through any barrier cells even though `hasAxe` is `true`. This results in `keyToGoalWalkingResult.PathExists = false` and `Start/Goal walkable neighbors = 0`.
4. **Level Completion & Inconsistency Error**:
   - The player, using physical mechanics, swings the axe to destroy barriers.
   - Destroying a barrier calls `mazeData.UnmarkCellAsOccupied`, allowing the player to reach the exit door.
   - Upon opening the door, `OnLevelCompletedFromDoor()` checks `keyToGoalWalkingResult.PathExists`. Since it was computed at the key collection moment (where it incorrectly failed), it throws the `[CRITICAL INCONSISTENCY]` error.

# Implementation Steps
## Step 1: Fix `MazePathfinder.cs`
- **Description**: Update `CanEnter` and `IsWalkableForPathfinder` in `MazePathfinder.cs` to correctly check if a cell is a barrier. If it is a barrier and `hasAxe` is `true`, return `mazeData.IsCellWalkableIgnoreOccupied(cell.x, cell.y)` to bypass the barrier's own occupancy state.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Fix `LevelValidator.cs`
- **Description**: Update all 4 pathfinding loops in `LevelValidator.cs` to handle the same occupancy-bypassing logic for barriers when `hasAxe` is `true`.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Local Validation Check
- **Description**: Verify compilation of the modified scripts in the Unity Editor.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

# Verification & Testing
1. **Compilation Check**: Confirm that `MazePathfinder.cs` and `LevelValidator.cs` compile with zero errors in the editor console.
2. **Gameplay Testing / Execution**: Enter Play Mode, complete a procedurally generated maze by collecting the axe, breaking barriers, getting the key, and reaching the goal.
3. **Log verification**: Check the Unity Console to ensure that the level finishes and loads the next level, and specifically verify that the critical error:
   `[CRITICAL INCONSISTENCY] El jugador llegó a la meta pero el pathfinder no encontró ruta navegable caminando.`
   is no longer logged.
