# Debugging, testing, and validation

## Diagnose from evidence

Collect:

- Exact Console error and full first relevant stack trace.
- Script and line referenced.
- Reproduction steps.
- Expected versus actual behavior.
- Relevant GameObject hierarchy and Inspector values.
- Recent change that preceded the failure.

Fix compilation errors from top to bottom. Later errors may be consequences of the first.

Classify the issue:

- Compilation/API mismatch.
- Missing component or reference.
- Incorrect lifecycle/timing.
- Physics layer/collider/Rigidbody configuration.
- Animator state or transition.
- Render/sorting/camera.
- Input action or focus.
- Scene/prefab/serialization data.
- Algorithm/state logic.

Tie every conclusion to observed evidence. Mark hypotheses as hypotheses.

## Safe iteration

1. Reproduce the issue.
2. Record the current Console state.
3. Change one coherent cause.
4. Recompile.
5. Reproduce again.
6. Check regressions in adjacent behavior.

Do not hide errors by disabling systems, deleting components, or swallowing exceptions unless that is the intended fix.

## Tests

Prefer Edit Mode tests for pure logic and deterministic generation. Use Play Mode tests for lifecycle, scenes, physics, coroutines, and integrated behavior.

High-value tests include:

- Boundary and invalid input cases.
- State transitions.
- Deterministic seeded generation.
- Reachability/connectivity.
- Multi-tile footprint overlap.
- Save/load round trips.
- Regression tests for a fixed defect.

Do not write tests that merely duplicate implementation details.

## Validation ladder

Use all available levels:

1. Static inspection and reference search.
2. Unity script validation.
3. Unity compilation with zero new errors.
4. Edit Mode tests.
5. Play Mode tests.
6. Focused Play Mode reproduction.
7. Representative build.

Warnings are not automatically harmless. Distinguish pre-existing warnings from new warnings introduced by the change.

## Performance

Profile before broad optimization. Investigate:

- CPU frame time and spikes.
- Garbage collection allocations.
- Physics query volume.
- Rendering batches/overdraw.
- Object creation/destruction.
- Procedural generation duration.

Report the measured bottleneck, change, and before/after result. Avoid claiming improvement without measurement.

## Handoff checklist

- No new Console errors.
- Required references assigned.
- Prefab/scene saved.
- Input and physics configuration documented.
- Tests or reproduction completed.
- Remaining manual checks stated.
