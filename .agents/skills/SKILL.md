---
name: develop-unity-games
description: Develop, review, debug, refactor, and configure Unity projects using production-minded C# and safe Editor workflows. Use for Unity scripts, MonoBehaviours, ScriptableObjects, scenes, prefabs, components, Input System, Animator, physics 2D/3D, cameras, UI, Tilemaps, procedural generation, tests, performance, builds, compilation errors, Inspector setup, or Unity MCP/Antigravity actions. Apply when the user asks to implement a feature, diagnose broken gameplay, improve architecture, inspect a Unity project, or receive exact Editor configuration steps.
---

# Develop Unity Games

Build Unity features safely, preserve existing project intent, and leave the project in a verifiable state. Write code and identifiers in English unless the project already uses another convention. Explain Unity configuration in the user's language.

## Core workflow

1. Establish the task boundary.
   - Distinguish explanation, diagnosis, implementation, and Editor configuration.
   - Do not modify files for an explanation-only or diagnosis-only request.
   - Ask only when a missing choice materially changes behavior.
2. Inspect before proposing changes.
   - Locate the Unity project root through `Assets/`, `Packages/`, and `ProjectSettings/`.
   - Read applicable project instructions and relevant scripts completely.
   - Inspect direct dependencies, serialized field names, prefab/scene references, assembly definitions, package versions, and current Console errors when available.
   - Check version control status and preserve unrelated user changes.
3. State a short plan for non-trivial changes.
   - Identify scripts/assets to add or modify.
   - Explain any required Inspector, prefab, scene, layer, tag, input action, or package configuration.
   - Prefer the smallest coherent change that solves the actual problem.
4. Implement incrementally.
   - Preserve public APIs and serialized data unless the change requires migration.
   - Avoid editing Unity-generated files, `Library/`, `Temp/`, `Logs/`, or IDE project files.
   - Never create duplicate class files such as `Player (1).cs`.
5. Validate in layers.
   - Check syntax and project conventions.
   - Use Unity script validation, compilation, Console, tests, and Play Mode when available.
   - Treat a successful text edit as unverified until Unity recompiles it.
6. Hand off clearly.
   - Lead with the outcome.
   - List changed files.
   - Give exact Inspector steps and field assignments.
   - Report validation performed and remaining Unity-only checks.

## Choose the relevant guidance

- Read [architecture-csharp.md](references/architecture-csharp.md) before creating or substantially refactoring runtime C# architecture.
- Read [editor-assets.md](references/editor-assets.md) before changing scenes, prefabs, ScriptableObjects, serialized fields, assets, packages, layers, tags, or builds.
- Read [gameplay-2d.md](references/gameplay-2d.md) for 2D movement, physics, animation, cameras, Tilemaps, procedural levels, and sorting.
- Read [debugging-testing.md](references/debugging-testing.md) for errors, broken behavior, tests, performance, and validation.
- Read [unity-mcp.md](references/unity-mcp.md) before acting through Unity MCP or Antigravity.

Load only the references relevant to the request.

## Engineering rules

- Keep each component focused on one responsibility.
- Prefer composition over large inheritance hierarchies.
- Keep game rules independent from presentation and input when practical.
- Use ScriptableObjects for reusable authoring data, not mutable per-instance runtime state unless intentionally cloned.
- Use events or explicit orchestration for cross-system communication; avoid repeated scene-wide searches.
- Cache component references obtained repeatedly.
- Put physics movement in `FixedUpdate`; collect frame input in `Update`.
- Use `Time.deltaTime` for frame-rate-independent non-physics movement.
- Avoid per-frame allocations, LINQ, string construction, and repeated `GetComponent` in hot paths.
- Do not introduce a service locator, singleton, event bus, dependency-injection framework, or generic abstraction without a concrete need.
- Prefer serialized private fields over public mutable fields.
- Add `[RequireComponent]` only for true invariants.
- Validate required references and fail with actionable messages.
- Use namespaces only if the existing project uses them or the project scale justifies them.

## Serialization safety

- Treat renaming a serialized field as a data migration. Use `[FormerlySerializedAs("oldName")]` when preserving existing Inspector values.
- Do not rename a `MonoBehaviour` class or its file independently; Unity requires matching names.
- Preserve `.meta` files and GUIDs.
- Do not replace an existing asset merely to simplify editing.
- Avoid hand-editing scene or prefab YAML. Prefer Unity Editor/MCP operations when available.
- If a destructive change is required, identify exact targets and obtain clear authorization.

## Response contract

For implemented work, provide:

1. What now works.
2. Files created or modified.
3. Unity Editor configuration, using exact object/component/field names.
4. Validation results.
5. Any remaining manual Play Mode check.

For diagnosis, provide:

1. Most likely cause, tied to observed evidence.
2. Why the current configuration causes the symptom.
3. Minimal fix options, ordered by recommendation.
4. Exact checks the user can perform in Unity.

Never claim Unity compilation, scene behavior, or build success if it was not actually observed.
