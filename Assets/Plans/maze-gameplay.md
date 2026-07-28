# Project Overview
- **Game Title**: Cat Maze (Laberinto del Gato)
- **High-Level Concept**: A 2D top-down maze game where a Cat player navigates paths, collects tools (Axe, Key) to bypass obstacles and unlock the exit door.
- **Players**: Single player (using keyboard movement).
- **Inspiration / Reference Games**: Classic 2D top-down maze/adventure games (e.g. Zelda, Bomberman exploration).
- **Tone / Art Direction**: Retro pixel art 2D with a cute cat protagonist.
- **Target Platform**: Standalone PC (Windows).
- **Screen Orientation / Resolution**: Landscape (1920x1080 or standard desktop).
- **Render Pipeline**: Universal Render Pipeline (URP).

# Game Mechanics
## Core Gameplay Loop
1. **Explore**: The player controls the Cat to navigate through the maze paths.
2. **Collect Axe**: The player finds and collects the Axe (`Hacha`).
3. **Chop Trees**: Using the Axe, the player can now run into `BreakableObstacles` (trees blocking passages) to make them disappear.
4. **Collect Key**: The player accesses the newly opened paths to find and collect the `Key` (`Llave`).
5. **Unlock Door**: The player reaches the exit door at the House, unlocks it with the Key (which plays the opening animation), and completes the game, pausing gameplay.

## Controls and Input Methods
- **Movement**: WASD / Arrow keys (handled by existing `CatInputReader` and `CatMovement` scripts).
- **Interaction**: Contact-based collision and triggers (running into items collects them, running into breakable tiles chops them, running into the door triggers it if player has the key).

# UI
- No full UI is requested for this iteration. 
- A visual indicator can be printed on the Console (`Debug.Log`) to confirm key states (e.g., "¡Hacha recolectada!", "¡Llave recolectada!", "¡Puerta abierta!").
- Optionally, we can display a simple screen-space text or pause the game immediately so the player knows they won.

# Key Asset & Context
We will create five scripts in `Assets/scripts/` to support the modular gameplay components:
1. **`CatInventory.cs`**:
   - Attached to `CatPlayer`.
   - Tracks `bool hasAxe` and `bool hasKey`.
2. **`CollectibleItem.cs`**:
   - Attached to `Hacha` and `Key` GameObjects.
   - Enums: `Axe`, `Key`.
   - Adds collider with `isTrigger = true` to enable collision detection.
3. **`AxeObstacleBreaker.cs`**:
   - Attached to `CatPlayer`.
   - Handles tile clearing upon colliding with the `BreakableObstacles` tilemap if the player has the axe.
4. **`NextLevelLoader.cs`**:
   - Level transition manager matching the user's exact specification.
   - Attached to a new GameObject in the scene named `LevelFlow`.
5. **`MazeDoor.cs`**:
   - Attached to the `door` GameObject.
   - Adds a trigger collider, checks for the Key, and sets the `"Open"` animator parameter.
   - Connects to `NextLevelLoader` to trigger the scene change.
   - Runs a coroutine to wait for the door's opening animation to complete, then pauses the game (`Time.timeScale = 0f`) and loads the next level (or waits for user confirmation/auto-proceeds).

# Implementation Steps

### Step 1: Create the CatInventory Script
- **Description**: Create `Assets/scripts/CatInventory.cs` to hold and manage player collection states (`hasAxe`, `hasKey`).
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

### Step 2: Create the CollectibleItem Script and Configure Collectibles
- **Description**: Create `Assets/scripts/CollectibleItem.cs`. 
  - Attach this script to `Hacha` and `Key` GameObjects.
  - Add `BoxCollider2D` or `CircleCollider2D` configured as a Trigger to both GameObjects.
  - Set their types appropriately in the Inspector.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No (needs CatInventory reference)

### Step 3: Implement Axe-Based Tile Destruction
- **Description**: Create `Assets/scripts/AxeObstacleBreaker.cs` and attach it to the `CatPlayer`.
  - In `OnCollisionEnter2D`, if colliding with "BreakableObstacles" and player `hasAxe` is true:
    - Get the `Tilemap` component.
    - Find the precise hit cell by using contact points and normal direction: `Vector3 hitPoint = contact.point - contact.normal * 0.05f;`
    - Convert to cell space: `tilemap.WorldToCell(hitPoint);`
    - Clear the tile: `tilemap.SetTile(cellPos, null);`
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 4: Create and Configure NextLevelLoader
- **Description**: Create `Assets/scripts/NextLevelLoader.cs` with the level loading script provided by the user.
  - Create a new empty GameObject in the `laberinto` scene named `LevelFlow` and attach the `NextLevelLoader` component to it.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

### Step 5: Implement Door Unlocking, Animation Detection, and Level Transition
- **Description**: Create `Assets/scripts/MazeDoor.cs` and attach it to the `door` child of `House`.
  - Add a `BoxCollider2D` with `isTrigger = true` to the `door` child GameObject.
  - Reference the door's existing `Animator` component and add a serialized field for the `NextLevelLoader` instance in the scene.
  - In `OnTriggerEnter2D`, check if the colliding object is `CatPlayer` and if the player has the key.
  - If they do:
    - Set the animator's `"Open"` parameter to `true`.
    - Start a Coroutine that:
      1. Waits for the transition to finish or for a fixed duration matching the door opening animation (e.g. 1.0 - 1.5 seconds) using standard time.
      2. Once the animation is complete, pauses the game (`Time.timeScale = 0f`).
      3. Calls `levelLoader.LoadNextLevel()` to transition to the next level (or waits for a keypress like Space/Enter during the pause before calling it).
- **Assigned role**: developer
- **Dependencies**: Step 1, Step 4
- **Parallelizable**: No

# Verification & Testing
1. **Movement Check**: Verify the player still moves correctly in `laberinto.unity` using WASD/Arrow keys.
2. **Axe Collection Check**: Move the player to the Axe (`Hacha`) at `(-6.98, -1.53)`. Verify it is collected (disappears from scene) and a log message "¡Hacha recolectada!" is displayed in the Console.
3. **Obstacle Clearing Check (Without Axe)**: Try to walk into `BreakableObstacles` before picking up the Axe. Verify they are solid and do not disappear.
4. **Obstacle Clearing Check (With Axe)**: Pick up the Axe, then walk into `BreakableObstacles`. Verify that only the trees you collide with disappear, allowing you to pass.
5. **Key Collection Check**: Move through the cleared path to the Key (`Key`) at `(-8.13, -11.99)`. Verify it is collected and log message "¡Llave recolectada!" is shown.
6. **Door Unlocking Check (Without Key)**: Move to the door at `(23.41, -2.56)` without collecting the Key first. Verify the door does not open, the animation does not play, and the game does not pause or load the next level.
7. **Door Unlocking Check (With Key)**: Walk to the door with the Key. Verify that:
   - The door's opening animation plays and runs to completion.
   - The game pauses (`Time.timeScale` set to `0`).
   - The scene transition triggers to load the next level (either automatically or upon pressing a confirmation key, depending on final choice), calling `NextLevelLoader.LoadNextLevel()`.
8. **Build Settings Check**: Ensure that the `laberinto` scene and the next scene are correctly added to the Build Settings so `NextLevelLoader` can load them successfully.
