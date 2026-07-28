# Project Overview
- **Game Title**: Cat Maze (Laberinto del Gato)
- **High-Level Concept**: A 2D top-down maze game where a Cat player navigates paths, collects tools (Axe, Key) to bypass obstacles and unlock the exit door to proceed to the next level.
- **Players**: Single player (using keyboard movement).
- **Inspiration / Reference Games**: Classic top-down maze/adventure games (e.g. Zelda, Bomberman exploration).
- **Tone / Art Direction**: Retro pixel art 2D with a cute cat protagonist.
- **Target Platform**: Standalone PC (Windows).
- **Screen Orientation / Resolution**: Landscape (1920x1080 or standard desktop).
- **Render Pipeline**: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
1. **Explore**: The player controls the Cat to navigate through the maze paths.
2. **Collect Axe**: The player finds and collects the Axe (`Hacha`).
3. **Chop Trees**: Using the Axe, the player can now stand in front of `BreakableObstacles` (trees blocking passages) and press `E` to cut them down.
4. **Collect Key**: The player accesses the newly opened paths to find and collect the `Key` (`Llave`).
5. **Unlock Door**: The player reaches the exit `Door` at the `House`, unlocks it with the Key (triggering the opening animation). When the animation completes (stops), the game pauses and triggers the level loader to transition to the next level.

## Controls and Input Methods
- **Movement**: WASD / Arrow keys (handled by existing `CatInputReader` and `CatMovement` scripts).
- **Axe Use**: Press `E` while facing a cuttable tree.
- **Door Unlocking**: Contact/collision with the `Door` when possessing the `Key`.

# UI
- Console-based debug logs for gameplay feedback.
- Seamless automatic transition to the next level upon completing the door animation and pausing the game.

# Key Asset & Context
We will create and update the following scripts:
1. **`NextLevelLoader.cs`**:
   - Built exactly using the user's provided code structure.
   - Attached to a new GameObject named `LevelFlow` in the scene.
2. **`MazeDoor.cs`**:
   - Attached to the `Door` child GameObject of `House`.
   - Checks if player is the colliding object and consumes the key.
   - Triggers the door's opening animation via `"Open"`.
   - Runs a coroutine to wait for the animation to complete (stop), pauses the game (`Time.timeScale = 0f`), and calls the connected `NextLevelLoader` to load the next scene.

# Implementation Steps

### Step 1: Create the NextLevelLoader Script
- **Description**: Create `Assets/scripts/NextLevelLoader.cs` containing the exact code provided by the user.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

### Step 2: Set up the LevelFlow GameObject
- **Description**: Create a new empty GameObject in the `laberinto` scene named `LevelFlow` and attach the `NextLevelLoader` component to it.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 3: Update the MazeDoor Script
- **Description**: Rewrite `Assets/scripts/MazeDoor.cs` to:
  - Add a serialized field to reference the `NextLevelLoader` component (`levelLoader`).
  - In `OnTriggerEnter2D`, check for `CatPlayer` and if they have the key.
  - If they do:
    - Consume the key and call `OpenDoor()`.
    - Set the animator trigger `"Open"`.
    - Run `CompleteLevelAfterDelay()` coroutine:
      - Wait for `completionDelay` (e.g. 1.0s or 1.5s, matching when the door animation stops).
      - Set `Time.timeScale = 0f` to pause the game.
      - Call `levelLoader.LoadNextLevel()` to transition to the next level.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 4: Configure the Door GameObject in Scene
- **Description**:
  - Add `BoxCollider2D` with `isTrigger = true` to the `Door` GameObject under `House`.
  - Attach the `MazeDoor.cs` component to the `Door` GameObject.
  - Connect the `LevelFlow` GameObject (with `NextLevelLoader`) to the `levelLoader` field of the `MazeDoor` component in the Inspector.
- **Assigned role**: developer
- **Dependencies**: Step 2, Step 3
- **Parallelizable**: No

# Verification & Testing
1. **Movement & Mechanics Check**: Verify player movement, axe collection, tree cutting, and key collection work as already implemented.
2. **Door Contact (Without Key)**: Stand against the `Door` without the Key. Verify the door stays closed and nothing happens.
3. **Door Contact (With Key)**: Stand against the `Door` with the Key. Verify that:
   - The door's Animator trigger `"Open"` is activated, and the door opens.
   - After the door animation completes (stops), the game pauses (`Time.timeScale` becomes `0f`).
   - The scene transition automatically triggers via `NextLevelLoader.LoadNextLevel()` to load the next level, resetting `Time.timeScale` back to `1f`.
