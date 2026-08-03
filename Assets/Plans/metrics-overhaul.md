# Project Overview
- Game Title: Cat Maze Procedural Level
- High-Level Concept: OVERHAUL of Key -> Goal metrics and Fast Travel cave portal evaluation to simplify metrics, resolve the "Lost" classification bug on minor detours, and evaluate teleport utility through real logic.
- Players: Single player
- Target Platform: PC (StandaloneWindows64)
- Render Pipeline: URP

# Game Mechanics
## Overhauled "Key to Goal" Telemetry
The segment between key collection and reaching the exit door is tracked and analyzed to understand player search patterns.
- Distinguish between mandatory mission caves (ignored for strategy) and fast-travel optional caves.
- Measure actual traversed walking steps (cell transitions) avoiding frame rate or static occupancy dependencies.
- Evaluate each fast-travel teleport when it occurs using Dijkstra pathfinding costs with and without the current connection.

# UI
Not directly modified, but debug console output and serialized JSON outputs are simplified and structured.

# Key Asset & Context
- `Assets/scripts/Difficulty/KeyToGoalMetrics.cs`: Defines the metrics structure of the Key -> Goal segment.
- `Assets/scripts/Difficulty/KeyToGoalTracker.cs`: Tracks cell traversal, teleports, and computes the overhauled metrics on completion.

# Implementation Steps

## Step 1: Overhaul `KeyToGoalMetrics.cs`
- **Description**: Add the new fields requested for `keyToGoal` structure and define the new `NavigationStyle` enum values (`NotEvaluated`, `Efficient`, `Exploratory`, `Struggling`). Keep old fields as `[System.Obsolete]`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## Step 2: Overhaul `KeyToGoalTracker.cs`
- **Description**: Implement cell-movement tracking that counts actual walking steps and detects teleports to avoid adding physical distances. Evaluate each portal connection at the moment of teleport using Dijkstra.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

## Step 3: Verification & Compilation
- **Description**: Compile and verify changes to ensure everything compiles and runs perfectly.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

# Verification & Testing
1. **Compilation Check**: Confirm everything compiles without any errors.
2. **JSON Metric Verification**: Play a level, collect the key, use fast travel portals, open the door, and inspect the logged and saved metrics JSON structure.
3. **Log checks**: Verify that the portal evaluation log matches the requested formatting.
