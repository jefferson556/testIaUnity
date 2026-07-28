# Unity C# architecture

## Component boundaries

Use small collaborating components:

- Input reader: translates devices/actions into game intent.
- Motor or movement: applies movement to physics or transforms.
- Ability: owns one capability such as jump, interact, attack, or switch.
- Animation presenter: converts gameplay state into Animator parameters.
- State/coordinator: owns transitions involving several components.
- Data asset: holds reusable designer-authored configuration.

Do not split trivial behavior merely to satisfy a pattern. Split when responsibilities change for different reasons, need independent testing, or are reused.

## Data and state

- Keep configuration in serialized fields or ScriptableObjects.
- Keep runtime state on scene/component instances.
- Avoid modifying shared ScriptableObject assets during Play Mode. Clone runtime data if mutation is required.
- Prefer explicit state types or enums to loosely related booleans when states are mutually exclusive.
- Keep save-data DTOs free of Unity object references.

## Dependencies

Resolve stable same-object dependencies in `Awake`.
Resolve scene wiring through serialized references.
Use `TryGetComponent` for optional dependencies.
Avoid `FindObjectOfType`, `GameObject.Find`, and tag searches in repeated paths.
When an object may be absent, define the fallback intentionally instead of allowing a later null reference.

## Lifecycle

- `Awake`: local references and invariants.
- `OnEnable`/`OnDisable`: subscribe and unsubscribe events symmetrically.
- `Start`: work requiring other objects to have completed `Awake`.
- `Update`: input sampling, timers, non-physics presentation.
- `FixedUpdate`: Rigidbody movement and physics forces.
- `LateUpdate`: camera follow or work that must occur after movement.

Never assume Unity lifecycle order between different objects unless configured or explicitly orchestrated.

## Public API and naming

- Classes, methods, properties, events: PascalCase.
- Private fields and locals: camelCase.
- Serialized private fields: camelCase.
- Boolean names: `isGrounded`, `canMove`, `hasKey`.
- Events: describe what occurred, such as `Jumped` or `HealthChanged`.
- Methods: verbs that reveal intent.

Keep fields private. Expose read-only properties or narrow methods. Avoid public setters for state other components should not control.

## Error handling

Use early validation for required dependencies:

```csharp
private void Awake()
{
    if (!TryGetComponent(out Rigidbody2D body))
    {
        Debug.LogError(
            $"{nameof(PlayerMovement)} requires a Rigidbody2D.",
            this
        );
        enabled = false;
        return;
    }

    rigidbody2D = body;
}
```

Do not log every frame. Include the component context when it helps locate the object.

## Performance

Optimize measured or obviously hot code:

- Cache references and Animator parameter hashes.
- Reuse collections when generation repeats.
- Prefer non-allocating physics queries where the result count is bounded.
- Pool objects that are created/destroyed frequently.
- Do not prematurely pool rare objects or rewrite readable code without evidence.

## Refactoring

Preserve serialized field values and UnityEvents. Search all references before renaming or moving a type. When changing an interface, update every caller in one coherent change and validate compilation immediately.
