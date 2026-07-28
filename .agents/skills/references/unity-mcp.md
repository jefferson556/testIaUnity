# Unity MCP and Antigravity

## Connection checks

Before using Editor tools:

- Confirm the intended Unity project is open.
- Confirm Unity Bridge is running.
- Confirm the MCP client is connected.
- Read project data and Console before mutating anything.
- Respect the enabled tool set; do not attempt to bypass disabled actions.

If connection is unavailable, continue with file-level analysis when sufficient and give manual Editor steps. Do not claim to have inspected the scene or Console.

## Safe tool sequence

For diagnosis:

1. Read project/version data.
2. Read Console logs.
3. Inspect relevant scene objects/components.
4. Read relevant scripts/assets.
5. Explain findings without mutation.

For implementation:

1. Complete the diagnostic sequence.
2. State the intended files and Editor objects.
3. Apply small text edits or create the required script.
4. Validate the script.
5. Wait for Unity compilation.
6. Read Console again.
7. Modify GameObjects/scenes only when required.
8. Save intentionally.
9. Reinspect affected components and run focused tests.

## Approval boundaries

Require clear user intent before:

- Deleting scripts, assets, GameObjects, scenes, or packages.
- Renaming/moving assets in ways that can affect GUID references.
- Installing/updating packages.
- Applying broad prefab overrides.
- Changing build/platform settings.
- Running arbitrary commands.

Keep package execution, delete-script, and arbitrary-command tools disabled unless a specific authorized task needs them.

## MCP result handling

- Verify the active scene before scene operations.
- Use exact hierarchy paths when names repeat.
- Re-read the target after a mutation.
- Read Console after compilation and Play Mode actions.
- Do not interpret a tool's success response as proof of correct gameplay.
- Stop if Unity enters a compile-error state and fix the earliest relevant error before further scene changes.

## Suggested user prompts

Diagnosis:

```text
Use Unity MCP to inspect the Console and the active scene.
Explain the cause without modifying anything.
```

Implementation:

```text
Inspect the relevant scripts and scene first. Explain the plan, then implement
the smallest safe fix. Preserve serialized references and validate compilation.
```

Review:

```text
Review this Unity feature for correctness, maintainability, Inspector setup,
physics behavior, and regressions. Do not modify files.
```
