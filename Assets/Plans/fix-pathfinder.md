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

# Implementation Steps
## Step 1: Fix `MazePathfinder.cs`
- **Description**: Update `CanEnter` and `IsWalkableForPathfinder` to correctly use `IsCellWalkableIgnoreOccupied` when a cell is a breakable barrier and the player has the axe (`hasAxe == true`).
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Fix `LevelValidator.cs`
- **Description**: Update the four pathfinding overloads (`CanPathfind` and `GetPath` for both single and multiple portal lists) to ensure that when a cell is a barrier and the player has the axe (`hasAxe == true`), it correctly resolves to walk-accessible via `IsCellWalkableIgnoreOccupied`.
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
