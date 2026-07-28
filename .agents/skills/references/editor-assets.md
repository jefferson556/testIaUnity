# Unity Editor and asset workflows

## Inspect the project

Confirm:

- Unity version from `ProjectSettings/ProjectVersion.txt`.
- Installed packages from `Packages/manifest.json`.
- Active input handling and rendering pipeline when relevant.
- Assembly definition boundaries.
- Existing folder and naming conventions.
- Console state before and after changes.

Do not add or update packages unless the feature requires it and the user authorized implementation.

## Scenes and prefabs

- Prefer editing prefab sources instead of every instance.
- Apply overrides deliberately; do not apply unrelated overrides.
- Verify scene changes are saved.
- Avoid simultaneous edits to the same scene by collaborators.
- For reusable gameplay objects, prefer prefabs with explicit required components.
- Keep scene-specific coordinators in scenes; keep reusable behavior on prefabs.

When direct Editor access is unavailable, give exact steps:

```text
GameObject
→ Component
→ Field
→ Assigned object/value
```

Include hierarchy paths when duplicate object names could confuse the user.

## Serialized fields and Inspector

- Use `[Header]`, `[Tooltip]`, `[Min]`, or `[Range]` when they improve authoring.
- Do not use attributes as a substitute for runtime validation.
- Assign references explicitly and state whether the component belongs on the root object, visual child, or another object.
- Explain the difference between Tags, physics Layers, Sorting Layers, and Order in Layer when relevant.

## ScriptableObjects

Use them for reusable level data, item definitions, tuning profiles, tile sets, and authored patterns.

Recommended pattern:

```csharp
[CreateAssetMenu(
    fileName = "NewMovementSettings",
    menuName = "Game/Movement Settings"
)]
public sealed class MovementSettings : ScriptableObject
{
    [Min(0f)]
    [SerializeField] private float speed = 4f;

    public float Speed => speed;
}
```

Provide creation and assignment steps after adding a ScriptableObject type.

## Assets and source control

- Preserve `.meta` files.
- Version `Assets/`, `Packages/`, and `ProjectSettings/`.
- Ignore generated folders such as `Library/`, `Temp/`, `Logs/`, `Obj/`, `Build/`, and `Builds/` as appropriate.
- Commit before broad automated changes.
- Avoid committing secrets, local credentials, or machine-specific caches.

## Builds

Before claiming a build is ready:

- Confirm target scenes are in the Build Profile/scene list.
- Confirm platform and architecture.
- Check development-build settings.
- Compile without errors.
- Run at least one representative scene.
- Verify input, resolution/UI scaling, audio, scene transitions, and persistent data.

Report platform-specific validation separately.
