# Unity 2D gameplay

## Rigidbody movement

- Use one authoritative movement component per character.
- Read input in `Update`; apply Rigidbody motion in `FixedUpdate`.
- Prefer `Rigidbody2D.linearVelocity` for direct velocity control on supported Unity versions; follow the project's current API when compatibility matters.
- Freeze unwanted rotation.
- Do not mix transform movement with a Dynamic Rigidbody2D.
- Use collision detection and interpolation appropriate to speed and camera smoothness.

For ground checks, use a small overlap/cast with an explicit LayerMask. Do not infer grounded state from vertical velocity alone.

## Colliders

- Match collider shapes to gameplay, not sprite transparency.
- Keep visual child scale at `(1,1,1)` when animation sprites must remain consistent; size sprites through import settings and pixels per unit.
- Use CompositeCollider2D for large static Tilemap collision regions when appropriate.
- Ensure decorative tiles have no collider unless they intentionally block gameplay.

## Animator

- Keep gameplay state authoritative; the Animator presents it.
- Use parameters such as `Speed`, `VerticalSpeed`, `IsGrounded`, and triggers for one-shot actions.
- Avoid transition conditions that are simultaneously true.
- Disable `Has Exit Time` for responsive locomotion transitions unless the design requires completion.
- Verify animation clips do not keyframe Transform scale/position accidentally.
- Use consistent sprite import settings across every frame.

## Camera and pixel art

- Use Orthographic cameras for conventional 2D.
- Keep sprite pixels per unit consistent.
- Use Pixel Perfect Camera when the art direction requires pixel-accurate rendering.
- Diagnose black/blue screens through camera position, culling mask, clipping planes, sorting, enabled renderers, and scene lighting/render pipeline.
- Configure Canvas Scaler intentionally for different resolutions.

## Tilemaps and sorting

Distinguish:

- Physics Layer: collision/filtering.
- Sorting Layer: broad rendering group.
- Order in Layer: order inside one Sorting Layer.
- Tilemap Renderer mode and sorting axis: ordering within a Tilemap.

Use separate Tilemaps when data or behavior differs, for example:

```text
Grid
├── Ground
├── Paths
├── DecorationBack
├── Walls
├── DecorationFront
└── Collisions
```

Do not depend on render order to solve invalid spatial placement. Reserve occupied cells before placing multi-tile patterns, prevent overlaps, and keep logical collision separate from purely visual gaps when necessary.

## Procedural generation

Use deterministic seeds for reproducible debugging. Separate:

1. Logical layout generation.
2. Validation/connectivity.
3. Gameplay placement.
4. Visual rendering.
5. Decoration.

Represent occupied footprints for multi-tile patterns. Check bounds and overlap before placement. Ensure required start, exit, key, objective, and teleport locations remain reachable. Retry with a bounded attempt count and report failure instead of looping indefinitely.

Keep decoration from changing logical paths unless the design explicitly requests it.

## Input System

- Use generated input action classes or explicit `InputActionReference` consistently.
- Enable/disable action maps with the owning component lifecycle.
- Avoid mixing old and new input APIs accidentally.
- When character control is locked for dialogue or cutscenes, disable gameplay intent and clear cached movement so the character does not continue moving.
