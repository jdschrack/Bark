# Code Quality Improvement Plan

**Created**: January 2026
**Source**: Code-Review-Analysis.md
**Overall Code Health Assessment**: 4/10

---

## Table of Contents

1. [Overview](#overview)
2. [Branch Naming Convention](#branch-naming-convention)
3. [Phase 1: Low Priority - Magic Numbers](#phase-1-low-priority---magic-numbers)
4. [Phase 2: High Priority - Object Pooling](#phase-2-high-priority---object-pooling)
5. [Phase 3: Medium Priority - Architecture Refactor](#phase-3-medium-priority---architecture-refactor)
6. [Phase 4: Critical Priority - Exception Handling](#phase-4-critical-priority---exception-handling)
7. [Testing Strategy](#testing-strategy)
8. [Risk Assessment](#risk-assessment)

---

## Overview

This plan addresses four categories of issues identified in the code review analysis:

| Priority | Category | Issue | Scope | Estimated Effort |
|----------|----------|-------|-------|------------------|
| **Critical** | Best Practices | Broad `catch (Exception e)` everywhere | 112 active occurrences in 43 files (116 including commented-out lines) | Large |
| **High** | Performance | Runtime `AddComponent`/`Instantiate`/`Destroy` | 3 modules | Medium |
| **Medium** | Architecture | `BananaGun` God Class | 1 file (~200 lines) | Medium |
| **Low** | Code Style | Magic numbers | GrapplingHooks + others | Small |

**Execution Order**: We will tackle these in a modified order (Medium → Low → High → Critical) because:
1. **Medium priority (architecture) first** - Refactoring BananaGun into smaller components will simplify the magic number extraction and pooling work, as the logic will be cleaner and more isolated
2. Low priority (magic numbers) becomes easier after architecture is cleaner
3. High priority (pooling) can then be applied to well-structured components
4. Critical priority (exceptions) is the largest undertaking and benefits from improved code structure

> **Note**: The original plan proposed Low → High → Medium → Critical, but reviewer feedback correctly identified that GrapplingHooks.cs would be modified in three separate phases. Doing the architecture refactor first reduces redundant work on the same file.

---

## Branch Naming Convention

All branches will follow this pattern:
```
refactor/<priority>-<short-description>
```

| Phase | Branch Name | Original Priority |
|-------|-------------|-------------------|
| Phase 1 | `refactor/medium-bananagun-srp` | Medium |
| Phase 2 | `refactor/low-extract-magic-numbers` | Low |
| Phase 3 | `refactor/high-implement-object-pooling` | High |
| Phase 4 | `refactor/critical-exception-handling` | Critical |

---

## Phase 1: Low Priority - Magic Numbers

**Branch**: `refactor/low-extract-magic-numbers`

### Objective
Extract hardcoded numeric values into well-named constants to improve code readability and maintainability.

### Scope

#### Primary Target: GrapplingHooks.cs
31 magic numbers identified across physics, positioning, and configuration:

| Category | Current Code | Proposed Constant |
|----------|--------------|-------------------|
| **Holster Position** | `new Vector3(0.15f, -0.15f, 0.15f)` | `HOLSTER_OFFSET` |
| **Gun Model Position** | `new Vector3(.55f, 0, .85f)` | `GUN_MODEL_LOCAL_POSITION` |
| **Raycast Radius** | `.5f` | `GRAPPLE_RAYCAST_RADIUS` |
| **Default Pull Force** | `10f` | `DEFAULT_PULL_FORCE` |
| **Default Steer Force** | `5f` | `DEFAULT_STEER_FORCE` |
| **Default Max Length** | `30f` | `DEFAULT_MAX_LENGTH` |
| **Spring Multiplier** | `* 2` | `SPRING_FORCE_MULTIPLIER` |
| **Steering Divisor** | `/ 2f` | `STEERING_FORCE_DIVISOR` |
| **Max Length Multiplier** | `* 5` | `MAX_LENGTH_MULTIPLIER` |
| **Elastic Max Distance** | `0.8f` | `ELASTIC_MAX_DISTANCE` |
| **Elastic Min Distance** | `0.25f` | `ELASTIC_MIN_DISTANCE` |
| **Elastic Damper** | `7f` | `ELASTIC_DAMPER` |
| **Elastic Mass Scale** | `4.5f` | `ELASTIC_MASS_SCALE` |
| **Static Damper** | `100f` | `STATIC_DAMPER` |
| **Static Mass Scale** | `4.5f` | `STATIC_MASS_SCALE` |
| **Haptic Audio ID** | `96` | `GRAPPLE_HAPTIC_AUDIO_ID` |
| **Haptic Intensity** | `0.05f` | `GRAPPLE_HAPTIC_INTENSITY` |

#### Secondary Targets (if time permits)
- **Rockets.cs**: Physics constants for rocket propulsion
- **Potions.cs**: Size scaling constants
- **Platforms.cs**: Already partially addressed (has `SurfaceOverrideIndex`, scale constants)

### Implementation Steps

1. **Create constants region** at the top of each class
   ```csharp
   #region Constants
   private const float GRAPPLE_RAYCAST_RADIUS = 0.5f;
   private static readonly Vector3 HOLSTER_OFFSET = new Vector3(0.15f, -0.15f, 0.15f);
   // ... etc
   #endregion
   ```

2. **Group constants logically**:
   - Physics constants
   - Visual/positioning constants
   - Audio/haptic constants
   - Configuration defaults

3. **Replace all magic numbers** with their named constants

4. **Add XML documentation** for non-obvious constants:
   ```csharp
   /// <summary>
   /// Radius used for sphere cast when detecting grapple targets.
   /// Larger values make grappling more forgiving.
   /// </summary>
   private const float GRAPPLE_RAYCAST_RADIUS = 0.5f;
   ```

### Acceptance Criteria
- [ ] All numeric literals in GrapplingHooks.cs replaced with named constants
- [ ] Constants are logically grouped and documented
- [ ] No behavioral changes (pure refactor)
- [ ] Code compiles without warnings
- [ ] Manual testing confirms grappling hooks work identically

### Deliverable
- Pull Request: `refactor/low-extract-magic-numbers` → `master`

---

## Phase 2: High Priority - Object Pooling

**Branch**: `refactor/high-implement-object-pooling`

### Objective
Implement an object pooling system to reduce garbage collection pressure from frequent `Instantiate`/`Destroy` calls during gameplay.

### Scope

#### Files Requiring Pooling

| File | Objects to Pool | Frequency |
|------|-----------------|-----------|
| **GrapplingHooks.cs** | `SpringJoint` on player | Per grapple action |
| **Rockets.cs** | Rocket prefab instances | Per rocket fire |
| **Potions.cs** | Potion bottle prefabs, `SizeChanger` objects | Per module enable |

### Implementation Steps

#### Step 1: Create Generic Object Pool with Interface

Create new file: `Tools/ObjectPool.cs`

```csharp
namespace Bark.Tools
{
    /// <summary>
    /// Non-generic interface to manage pools without knowing their generic type.
    /// Avoids reflection in PoolManager.ClearAll().
    /// </summary>
    public interface IPool
    {
        void Clear();
        int Count { get; }
    }

    public class ObjectPool<T> : IPool where T : Component
    {
        private readonly Queue<T> pool = new Queue<T>();
        private readonly Func<T> createFunc;
        private readonly Action<T> onGet;
        private readonly Action<T> onRelease;
        private readonly int maxSize;

        public int Count => pool.Count;

        public ObjectPool(Func<T> createFunc, Action<T> onGet = null,
                          Action<T> onRelease = null, int initialSize = 5,
                          int maxSize = 20)
        {
            this.createFunc = createFunc;
            this.onGet = onGet;
            this.onRelease = onRelease;
            this.maxSize = maxSize;

            // Pre-warm the pool
            for (int i = 0; i < initialSize; i++)
            {
                var obj = createFunc();
                obj.gameObject.SetActive(false);
                pool.Enqueue(obj);
            }
        }

        public T Get()
        {
            T obj = pool.Count > 0 ? pool.Dequeue() : createFunc();
            obj.gameObject.SetActive(true);
            onGet?.Invoke(obj);
            return obj;
        }

        public void Release(T obj)
        {
            if (pool.Count < maxSize)
            {
                onRelease?.Invoke(obj);
                obj.gameObject.SetActive(false);
                pool.Enqueue(obj);
            }
            else
            {
                UnityEngine.Object.Destroy(obj.gameObject);
            }
        }

        public void Clear()
        {
            while (pool.Count > 0)
            {
                var obj = pool.Dequeue();
                if (obj != null)
                    UnityEngine.Object.Destroy(obj.gameObject);
            }
        }
    }
}
```

#### Step 2: Create Specialized Pool Manager

Create new file: `Tools/PoolManager.cs`

```csharp
namespace Bark.Tools
{
    public static class PoolManager
    {
        // Use IPool interface instead of object to avoid reflection
        private static Dictionary<string, IPool> pools = new Dictionary<string, IPool>();

        public static ObjectPool<T> GetOrCreatePool<T>(string key, Func<T> createFunc,
            int initialSize = 5) where T : Component
        {
            if (!pools.TryGetValue(key, out var pool))
            {
                var newPool = new ObjectPool<T>(createFunc, initialSize: initialSize);
                pools[key] = newPool;
                return newPool;
            }
            return (ObjectPool<T>)pool;
        }

        public static void ClearAll()
        {
            // No reflection needed - IPool interface provides Clear()
            foreach (var pool in pools.Values)
            {
                pool.Clear();
            }
            pools.Clear();
        }
    }
}
```

#### Step 3: Refactor GrapplingHooks.cs (SpringJoint Caching)

**Current code (line 256)**:
```csharp
joint = Player.Instance.gameObject.AddComponent<SpringJoint>();
```

**Refactored code**:
```csharp
// IMPORTANT: Each BananaGun instance needs its OWN cached SpringJoint.
// The original code correctly creates a joint per gun, allowing dual-wielded grappling.
// A single global cached joint would break this functionality.

public class BananaGun : BarkGrabbable
{
    // Per-instance cached joint - NOT shared between guns
    private SpringJoint cachedJoint;

    void StartSwing()
    {
        // Ensure THIS instance has a joint on the player
        if (cachedJoint == null)
            cachedJoint = Player.Instance.gameObject.AddComponent<SpringJoint>();

        cachedJoint.enabled = true;
        // Configure this specific joint...
    }

    void EndSwing()
    {
        if (cachedJoint != null)
            cachedJoint.enabled = false;
    }

    void OnDestroy()
    {
        // Clean up the component THIS instance created
        if (cachedJoint != null)
            UnityEngine.Object.Destroy(cachedJoint);
    }
}
```

> **Warning**: An earlier version of this plan proposed a single `cachedJoint` on the Player object. This was incorrect - it would break dual-wielded grappling since both guns would control the same joint. Each BananaGun instance must manage its own SpringJoint.

#### Step 4: Refactor Rockets.cs

**Current code**:
```csharp
rocketL = SetupRocket(Instantiate(rocketPrefab), true);
```

**Refactored code**:
```csharp
private static ObjectPool<Rocket> rocketPool;

void Awake()
{
    rocketPool ??= new ObjectPool<Rocket>(
        createFunc: () => {
            var obj = Instantiate(rocketPrefab);
            return obj.AddComponent<Rocket>();
        },
        onRelease: (r) => r.Reset(),
        initialSize: 4
    );
}

Rocket GetRocket(bool isLeft)
{
    var rocket = rocketPool.Get();
    rocket.Init(isLeft);
    return rocket;
}
```

#### Step 5: Refactor Potions.cs

Similar pattern for potion bottles and SizeChanger objects.

### Acceptance Criteria
- [ ] `ObjectPool<T>` generic class created and tested
- [ ] GrapplingHooks reuses SpringJoint instead of Add/Destroy
- [ ] Rockets uses object pool for rocket instances
- [ ] Potions uses object pool for bottle instances
- [ ] Memory profiling shows reduced GC allocations during gameplay
- [ ] No behavioral changes to gameplay

### Deliverable
- Pull Request: `refactor/high-implement-object-pooling` → `master`

---

## Phase 3: Medium Priority - Architecture Refactor

**Branch**: `refactor/medium-bananagun-srp`

### Objective
Refactor the `BananaGun` class to follow the Single Responsibility Principle by separating its concerns into focused components.

### Current State Analysis

The `BananaGun` class (lines 182-378 in GrapplingHooks.cs) currently handles:

1. **Input/Interaction** - Activation, deactivation, selection events
2. **Physics** - SpringJoint creation, configuration, force application
3. **Visuals** - LineRenderer management, model state (open/closed)
4. **State Management** - Grappling state, holstering, mode selection

### Proposed Architecture

```
GrapplingHooks (Module)
    │
    ├── BananaGun (Coordinator)
    │       ├── BananaGunPhysics (SpringJoint, forces)
    │       ├── BananaGunVisuals (LineRenderer, models)
    │       └── Uses: BarkGrabbable (inherited interaction)
    │
    └── GrappleMode (enum: ELASTIC, STATIC)
```

### Implementation Steps

#### Step 1: Extract BananaGunPhysics

Create new file or nested class: `BananaGunPhysics`

```csharp
public class BananaGunPhysics
{
    private SpringJoint joint;
    private readonly Transform playerTransform;
    private readonly Rigidbody playerRigidbody;

    public bool IsGrappling { get; private set; }
    public Vector3 GrapplePoint { get; private set; }

    public BananaGunPhysics(Transform player, Rigidbody rb)
    {
        playerTransform = player;
        playerRigidbody = rb;
    }

    public void StartGrapple(Vector3 point, GrappleMode mode, float pullForce)
    {
        GrapplePoint = point;
        IsGrappling = true;
        EnsureJointExists();
        ConfigureJoint(mode, pullForce, point);
    }

    public void EndGrapple()
    {
        IsGrappling = false;
        if (joint != null)
            joint.enabled = false;
    }

    public void ApplySteering(Vector3 direction, float force)
    {
        if (IsGrappling)
            playerRigidbody.AddForce(direction * force, ForceMode.Acceleration);
    }

    private void EnsureJointExists() { /* ... */ }
    private void ConfigureJoint(GrappleMode mode, float force, Vector3 anchor) { /* ... */ }
}
```

#### Step 2: Extract BananaGunVisuals

Create new file or nested class: `BananaGunVisuals`

```csharp
public class BananaGunVisuals
{
    private readonly GameObject openModel;
    private readonly GameObject closedModel;
    private readonly LineRenderer ropeLine;
    private readonly LineRenderer laserLine;

    /// <summary>
    /// Constructor takes pre-resolved references instead of using transform.Find().
    /// This decouples BananaGunVisuals from the prefab hierarchy, making it more
    /// robust to hierarchy changes and easier to unit test.
    /// </summary>
    /// <param name="openModel">The open/firing state model</param>
    /// <param name="closedModel">The closed/holstered state model</param>
    /// <param name="ropeLine">LineRenderer for the grapple rope</param>
    /// <param name="laserLine">LineRenderer for the targeting laser</param>
    public BananaGunVisuals(GameObject openModel, GameObject closedModel,
                            LineRenderer ropeLine, LineRenderer laserLine)
    {
        this.openModel = openModel;
        this.closedModel = closedModel;
        this.ropeLine = ropeLine;
        this.laserLine = laserLine;
    }

    public void SetGrappleState(bool isGrappling)
    {
        openModel.SetActive(isGrappling);
        closedModel.SetActive(!isGrappling);
    }

    public void UpdateRope(Vector3 start, Vector3 end, float playerScale)
    {
        ropeLine.enabled = true;
        ropeLine.SetPosition(0, start);
        ropeLine.SetPosition(1, end);
        ropeLine.startWidth = 0.02f * playerScale;
        ropeLine.endWidth = 0.02f * playerScale;
    }

    public void UpdateLaser(Vector3 start, Vector3 direction, float maxLength, float playerScale)
    {
        laserLine.enabled = true;
        laserLine.SetPosition(0, start);
        laserLine.SetPosition(1, start + direction * maxLength);
        // ... width configuration
    }

    public void HideLines()
    {
        ropeLine.enabled = false;
        laserLine.enabled = false;
    }
}
```

> **Design Note**: The original proposal used `transform.Find()` in the constructor, which is brittle - if the prefab hierarchy changes, the code breaks. This version uses dependency injection: BananaGun finds the objects and passes them to the constructor, decoupling BananaGunVisuals from the specific hierarchy.

#### Step 3: Simplify BananaGun

The refactored `BananaGun` becomes a coordinator:

```csharp
public class BananaGun : BarkGrabbable
{
    private BananaGunPhysics physics;
    private BananaGunVisuals visuals;

    // Configuration (set by parent GrapplingHooks module)
    public float PullForce { get; set; }
    public float SteerForce { get; set; }
    public float MaxLength { get; set; }
    public GrappleMode Mode { get; set; }

    protected override void Awake()
    {
        base.Awake();
        physics = new BananaGunPhysics(Player.Instance.transform,
                                        Player.Instance.bodyCollider.attachedRigidbody);

        // BananaGun finds the objects, then injects them into BananaGunVisuals
        // This keeps hierarchy knowledge in one place
        visuals = new BananaGunVisuals(
            openModel: transform.Find("Open").gameObject,
            closedModel: transform.Find("Closed").gameObject,
            ropeLine: transform.Find("Rope").GetComponent<LineRenderer>(),
            laserLine: transform.Find("Laser").GetComponent<LineRenderer>()
        );
    }

    public void OnActivate()
    {
        if (TryGetGrapplePoint(out Vector3 point))
        {
            physics.StartGrapple(point, Mode, PullForce);
            visuals.SetGrappleState(true);
        }
    }

    public void OnDeactivate()
    {
        physics.EndGrapple();
        visuals.SetGrappleState(false);
        visuals.HideLines();
    }

    void FixedUpdate()
    {
        if (physics.IsGrappling)
        {
            Vector3 steerDir = GetSteeringDirection();
            physics.ApplySteering(steerDir, SteerForce * Time.fixedDeltaTime);
        }
    }

    void OnRenderUpdate()
    {
        if (physics.IsGrappling)
            visuals.UpdateRope(GetGunTip(), physics.GrapplePoint, Player.Instance.scale);
        else if (isSelected)
            visuals.UpdateLaser(GetGunTip(), transform.forward, MaxLength, Player.Instance.scale);
    }

    // ... helper methods
}
```

### Acceptance Criteria
- [ ] `BananaGunPhysics` class handles all SpringJoint and force logic
- [ ] `BananaGunVisuals` class handles all LineRenderer and model state logic
- [ ] `BananaGun` class is reduced to ~100 lines as a coordinator
- [ ] All existing functionality preserved
- [ ] Unit tests can be written for Physics and Visuals independently

### Deliverable
- Pull Request: `refactor/medium-bananagun-srp` → `master`

---

## Phase 4: Critical Priority - Exception Handling

**Branch**: `refactor/critical-exception-handling`

### Objective
Replace systemic `catch (Exception e)` blocks with specific, intentional exception handling while maintaining mod stability.

### Scope
112 active occurrences across 43 files (116 including commented-out lines).

### Strategy

Given this is a game mod, we need to balance between:
- **Proper error handling** (catching specific exceptions)
- **Mod stability** (not crashing the host game)

**Approach**: Implement a tiered exception handling strategy:

#### Tier 1: Initialization Code
Can use broader catches with proper logging, as initialization failures should be visible but not crash the game.

```csharp
// Acceptable for initialization
try
{
    InitializeModule();
}
catch (Exception e)
{
    Logging.Exception(e);
    this.enabled = false; // Disable the module gracefully
}
```

#### Tier 2: Gameplay Code (Update/FixedUpdate)
Should use specific catches or let exceptions propagate to Unity's handler.

```csharp
// Before - BAD
void Update()
{
    try
    {
        DoGameplayLogic();
    }
    catch (Exception e) { Logging.Exception(e); }
}

// After - BETTER
void Update()
{
    if (rig == null) return; // Guard clause instead of try/catch

    try
    {
        DoGameplayLogic();
    }
    catch (InvalidOperationException e)
    {
        // Handle specific, expected error
        Logging.Warning($"Gameplay logic failed: {e.Message}");
    }
    // Let unexpected exceptions propagate
}
```

#### Tier 3: Event Handlers/Callbacks
Should catch specific exceptions to prevent breaking event chains.

```csharp
// Acceptable for event handlers
void OnPlayerJoined(Player player)
{
    try
    {
        HandlePlayerJoined(player);
    }
    catch (KeyNotFoundException)
    {
        Logging.Warning($"Player {player.NickName} data not found");
    }
    catch (Exception e)
    {
        // Log but don't propagate to avoid breaking other listeners
        Logging.Exception(e);
    }
}
```

### Implementation Steps

#### Step 1: Audit and Categorize

Create a spreadsheet/document categorizing each of the 115 occurrences:

| File | Line | Context | Category | Action |
|------|------|---------|----------|--------|
| Plugin.cs | 45 | Start() | Initialization | Keep broad catch |
| Rockets.cs | 67 | FixedUpdate | Gameplay | Add guard clauses |
| ... | ... | ... | ... | ... |

#### Step 2: Add Guard Clauses

Replace try/catch with null checks where appropriate:

```csharp
// Before
void FixedUpdate()
{
    try
    {
        rig.leftThumb.calcT; // Could be null
    }
    catch (Exception e) { }
}

// After
void FixedUpdate()
{
    if (rig == null) return;
    rig.leftThumb.calcT;
}
```

#### Step 3: Specify Exception Types

Where try/catch is necessary, catch specific types:

```csharp
// Before
catch (Exception e)
{
    Logging.Exception(e);
}

// After
catch (NullReferenceException e)
{
    Logging.Warning($"Reference was null: {e.Message}");
}
catch (InvalidOperationException e)
{
    Logging.Warning($"Invalid operation: {e.Message}");
}
```

#### Step 4: Create Exception Helpers (USE WITH CAUTION)

Add utility methods to `Logging` class:

```csharp
public static class Logging
{
    /// <summary>
    /// Executes an action and catches any exception, logging it.
    ///
    /// WARNING: This method should ONLY be used in contexts where ignoring an error
    /// and continuing is the intended and safe behavior, such as:
    /// - Event handlers where propagating would break other listeners (Tier 3)
    /// - Fire-and-forget operations where failure is acceptable
    ///
    /// DO NOT use this as a general-purpose replacement for proper try-catch blocks
    /// in initialization or gameplay code. It risks re-introducing the original problem
    /// of silencing exceptions without proper context.
    /// </summary>
    public static void SafeExecute(Action action, string context = null)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Exception($"Error in {context ?? "unknown context"}: {e}");
        }
    }

    /// <summary>
    /// Executes a function and catches any exception, returning a default value.
    /// See SafeExecute(Action) for usage warnings.
    /// </summary>
    public static T SafeExecute<T>(Func<T> func, T defaultValue = default, string context = null)
    {
        try
        {
            return func();
        }
        catch (Exception e)
        {
            Exception($"Error in {context ?? "unknown context"}: {e}");
            return defaultValue;
        }
    }
}
```

> **Important Usage Guidelines for SafeExecute**:
> - **Appropriate**: Event handlers, fire-and-forget operations, cleanup code
> - **Inappropriate**: Initialization code (use specific catches), gameplay loops (use guard clauses)
> - If you find yourself using `SafeExecute` frequently, that's a code smell - prefer proper exception handling

### Files by Priority

**High-traffic files to address first**:
1. `Plugin.cs` (12 occurrences)
2. `Potions.cs` (10 occurrences)
3. `Platforms.cs` (5 occurrences)
4. `Rockets.cs` (4 occurrences)
5. `MenuController.cs` (3 occurrences)

### Acceptance Criteria
- [ ] All 115 occurrences reviewed and categorized
- [ ] Guard clauses added where null checks suffice
- [ ] Specific exception types used where try/catch is needed
- [ ] Initialization code retains broad catches with proper logging
- [ ] No increase in game crashes during testing
- [ ] Documentation added explaining exception handling strategy

### Deliverable
- Pull Request: `refactor/critical-exception-handling` → `master`

---

## Testing Strategy

### For Each Phase

1. **Compilation Test**: Code compiles without errors or new warnings
2. **Smoke Test**: Game loads, mod initializes, menu appears
3. **Functional Test**: Each affected module works as expected
4. **Regression Test**: Other modules unaffected

### Phase-Specific Tests

| Phase | Specific Tests |
|-------|----------------|
| Phase 1 | Grappling hooks feel identical, physics unchanged |
| Phase 2 | Memory profiler shows reduced allocations, no object leaks |
| Phase 3 | Grappling hooks work identically, code is more testable |
| Phase 4 | Error scenarios logged properly, no silent failures |

---

## Risk Assessment

| Phase | Risk Level | Mitigation |
|-------|------------|------------|
| Phase 1 | **Low** | Pure refactor, no behavior change |
| Phase 2 | **Medium** | Object lifecycle bugs possible; thorough testing |
| Phase 3 | **Medium** | Interaction between components could break; incremental refactor |
| Phase 4 | **High** | Could expose hidden bugs; extensive testing, staged rollout |

---

## Execution Order Summary

This plan does not include time estimates per the guidelines. The recommended order is:

1. **Phase 1: Architecture Refactor** (`refactor/medium-bananagun-srp`)
   - Refactor BananaGun into smaller components first
   - Makes subsequent phases easier by isolating concerns

2. **Phase 2: Magic Numbers** (`refactor/low-extract-magic-numbers`)
   - Extract constants from now-cleaner codebase
   - Quick win that improves readability

3. **Phase 3: Object Pooling** (`refactor/high-implement-object-pooling`)
   - Apply pooling to well-structured components
   - User-facing performance improvement

4. **Phase 4: Exception Handling** (`refactor/critical-exception-handling`)
   - Largest undertaking, benefits from improved code structure
   - Tackle last with cleaner, more maintainable codebase

---

## Appendix: File Reference

### Files Modified Per Phase

| Phase | Files |
|-------|-------|
| 1 | `Modules/Movement/GrapplingHooks.cs`, optionally `Rockets.cs`, `Potions.cs` |
| 2 | `Tools/ObjectPool.cs` (new), `Tools/PoolManager.cs` (new), `GrapplingHooks.cs`, `Rockets.cs`, `Potions.cs` |
| 3 | `Modules/Movement/GrapplingHooks.cs` (major refactor) |
| 4 | 43 files containing exception handling |
