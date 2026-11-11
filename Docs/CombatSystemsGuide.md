**Your space shooter's combat systems — a step-by-step learning guide**

The game now has five increasingly large waves, four regular enemy types, weighted pickups, two rare player weapons, and a final boss. The existing asteroid starts the encounter. Defeating the boss finishes the level; pressing R then restarts it.

**1. Understand which script owns which job.**

| Script | Responsibility |
|---|---|
| `SpawnManager.cs` | Counts waves, creates enemies and pickups, starts the boss encounter, recognizes victory. |
| `Enemy.cs` | Moves, shoots, dodges, rams, shields, and destroys regular enemies. |
| `BossEnemy.cs` | Moves the boss to the center, runs its attacks, tracks its health, handles defeat. |
| `EnemyLaser.cs` | Moves hostile projectiles, optionally steers them, damages the player or destroys pickups. |
| `Player.cs` | Controls movement, health, ammo, temporary weapons, negative effects, and the C-key magnet. |
| `Bullet.cs` | Moves player projectiles and prevents a single projectile from dealing damage more than once. |
| `HomingProjectile.cs` | Steers a player projectile toward the nearest valid regular enemy or boss. |
| `RadialShot.cs` | Creates one volley of eight outward-moving plasma bolts. |
| `TripleShotPowerup.cs` | Shared pickup behavior, despite the historical name. Its ID chooses the effect. |
| `UIManager.cs` | Updates the Canvas, including wave status, ammo, boss health, game over, victory, and restart. |
| `CameraShake.cs` | Applies brief render-time camera movement when the player loses health. |

Keeping these jobs separate lets you change a projectile's behavior without rewriting the spawn system, or change the HUD without changing the boss's health calculation.

**2. How the enemy waves work.**

`StartSpawning()` starts two coroutines: one for enemies and one for pickups. A coroutine is a method that can pause at a `yield` instruction and continue later without blocking the game. Calling `StartSpawning()` again while it is already running does nothing, so destroying the asteroid cannot accidentally start duplicate waves.

The enemy count is calculated with:

```csharp
_firstWaveCount + (CurrentWave - 1) * _extraEnemiesPerWave
```

The starting count is 3 and the increase is 2. Therefore waves 1–5 contain 3, 5, 7, 9, and 11 regular enemies. Each wave waits until all pending enemies have spawned and every tracked enemy has either been destroyed or escaped below the screen. Escaping enemies give no score but do allow the wave to finish. A four-second intermission separates waves.

The manager uses a `HashSet<Enemy>` to track the living enemies. A set stores each enemy once. When an enemy is finally destroyed, `OnDestroy()` calls `EnemyRemoved(this)`. The manager removes that particular enemy and updates the remaining count. The UI includes pending spawns as well as living enemies so that it never reports a cleared wave while more enemies are still queued.

The gap between spawns becomes shorter as the wave increases, with a minimum of 0.5 seconds. Movement speed also increases, capped at a 1.35 multiplier. These caps keep later numbers from becoming unlimited.

**3. The regular enemy types and movement.**

| Type | Movement | Special behavior | Points |
|---|---|---|---:|
| Raider | Smooth wave | Shoots and can ram nearby players | 10 |
| Interceptor | Zigzag | Detects incoming player shots and dodges sideways; can ram | 15 |
| Hunter | Smooth wave | Fires slower seeking projectiles | 20 |
| Rear Gunner | Diagonal | Fires back toward the player after passing below them | 20 |

Wave 1 uses Raiders. Wave 2 adds Interceptors to the random selection, wave 3 adds Hunters, and wave 4 adds Rear Gunners. This unlocks variety gradually; it does not guarantee that every unlocked type appears in every wave.

Wave movement uses `Mathf.Sin(age * frequency)` for horizontal velocity. Sine changes smoothly between -1 and +1, so the enemy turns smoothly. Zigzag uses the sign of that sine value: horizontal velocity switches directly between left and right. Diagonal movement keeps a lateral direction while descending. Enemies reflect their horizontal movement at the camera's edges.

The later combat audit corrected two diagonal-movement details. `Instantiate` calls `Awake` before the spawn manager assigns the final random position. Choosing left/right only in `Awake` therefore used the wrong position. `Configure` now chooses the entry direction again after placement: enemies on the left head right, and enemies on the right head left. At a screen boundary the controller also saves the new direction in `_diagonalSign`, so it keeps moving inward instead of trying to turn back outward on the next physics step.

An Interceptor searches for player bullets within 2.5 world units. It checks whether their velocity points toward the enemy, rather than dodging every nearby bullet. A dodge lasts 0.45 seconds and has a cooldown. Ramming has a 2.2-unit trigger range, lasts 0.65 seconds at speed 5, and has a two-second cooldown. The ram direction is captured when the charge begins, giving you a chance to move out of its path.

A Rear Gunner compares its Y position with the player's. When it crosses below the player, it can fire upward immediately. The other regular types do not turn ordinary attacks upward at the player.

**4. Enemy shields and pickup attacks.**

From wave 2 onward, each regular enemy has a 25% chance to spawn shielded. The cyan ring visual reflects that state. The first player projectile is consumed and removes the shield; it does not destroy the enemy or award points. The next hit destroys the enemy normally. The early `return` in the shield branch is what prevents the same hit from both breaking the shield and killing the enemy.

Regular enemies also search for a pickup ahead of them: below the enemy, within six units, and within 1.2 units horizontally. If there is one, it takes priority over shooting the player. A hostile projectile that collides with a pickup calls `DestroyByEnemy()`. That removes the pickup without granting its effect or playing the collection sound. A seeking shot keeps the pickup as its target instead of switching to the player.

**5. Balanced pickup spawning.**

Pickups have a separate timer, so supplies keep appearing during intermissions and the boss encounter. After the initial delay, a spawn attempt happens every 2.5–4 seconds, with at most four active pickups. The weighted table is:

| Pickup | Weight / probability with this table |
|---|---:|
| Ammo | 50 / 50% |
| Shield | 12 / 12% |
| Triple shot | 10 / 10% |
| Speed | 8 / 8% |
| Health | 5 / 5% |
| Slow hazard | 5 / 5% |
| Radial weapon | 5 / 5% |
| Homing weapon | 5 / 5% |

These weights total 100, so they currently match percentages. If you change them, the general probability is `weight / sum of valid weights`. A weight of zero prevents that entry from being selected. Random selection does not guarantee a health pickup after exactly twenty attempts.

When the player's ammo is zero, the next available pickup slot is guaranteed to be ammo. It still obeys the pickup cap and spawn timer. This gives the player a recovery route without making health as common as ammunition.

**6. The harmful pickup and C-key magnet.**

The red slow pickup uses ID 6. Collecting it halves the player's effective movement speed for five seconds. The code applies the multiplier after calculating the current movement speed, so sprinting and a speed boost are affected consistently. Collecting another slow pickup refreshes the five-second deadline instead of multiplying the slowdown again. The Canvas displays `SLOWED` with the time remaining.

Pressing C searches for available pickups within twelve world units. Each eligible pickup receives a reference to the player. Its movement then changes from downward travel to travel toward the player at fourteen units per second. Once collected, it uses the same collection logic as a normally falling pickup. The magnet attracts harmful pickups too; the HUD explicitly says this.

**7. The rare radial and homing player weapons.**

The purple radial pickup, ID 5, activates an eight-direction plasma volley for five seconds. `RadialShot` divides 360 degrees by eight, giving a 45-degree separation. For each direction it instantiates a bolt and calls `Bullet.Launch(direction)`. The temporary volley object destroys itself, while the individual bolts retain their own lifetimes.

The cyan homing pickup, ID 7, activates one guided projectile per shot for five seconds. Every 0.15 seconds, the projectile searches for the closest valid target. It compares squared distances, which give the same nearest-target ordering without needing square roots. A destroyed or otherwise invalid target triggers a new search immediately. With no target, the projectile continues in its last direction until its normal lifetime expires.

The homing code now searches both `Enemy` and `BossEnemy`. Its turn rate is limited to 360 degrees per second, so it steers rather than instantly teleporting its heading.

Triple, radial, and homing pickups replace one another. A new pickup cancels the previous special weapon. The radial and homing effects store an expiry time, such as `Time.time + 5f`. This avoids an old coroutine unexpectedly switching off a newly refreshed power-up. Once the deadline passes, normal shooting resumes. Every firing action costs one ammo, including a triple or eight-bolt radial volley. At zero ammo, no projectile is spawned and no firing sound plays.

**8. Subtle damage feedback.**

`Player.Damage()` first checks the shield. A shielded hit removes one shield charge and returns before changing health. Only actual life loss calls `CameraShake.Shake()`, including the fatal hit.

The shake defaults are a 0.18-second duration, 0.045-unit maximum strength, and frequency 40. Perlin noise generates smooth variation and the offset fades down to zero. A new hit refreshes the duration without stacking ever-larger offsets.

The camera position changes only around rendering: its original position is saved before that camera renders and restored immediately afterward. This matters because player wrapping, enemy boundaries, and pickup spawning also read the camera. Those gameplay systems continue to see a stable camera. Disabling the component also restores the original position and removes its rendering callbacks.

**9. The final boss wave.**

The `Final Wave` field on `Spawn_manager` is 5. After its eleven regular enemies have been cleared, the manager displays `FINAL WAVE 5 | BOSS APPROACHING`, waits for the intermission, then spawns exactly one boss. The normal wave coroutine ends there. It cannot accidentally start wave 6 while you fight the boss. The pickup coroutine continues until the player loses or wins.

The boss prefab has its own `BossEnemy` component. It reuses the existing ship sprite and explosion animation at a larger scale and with a purple tint. Its ordinary `Enemy` component is removed, so regular movement, ramming, single-hit death, or random shooting cannot fight the boss controller for control of the same object.

The spawn manager computes the center using viewport coordinates `(0.5, 0.5)`, converted into a world position at the gameplay plane. It creates the boss above the top of the camera at that center's X coordinate. This remains centered when the screen aspect ratio changes; it does not assume world `(0, 0)` is the camera's center.

**10. Moving down, then holding position.**

`BossEnemy.FixedUpdate()` moves its Rigidbody2D toward the stored center using:

```csharp
Vector2.MoveTowards(currentPosition, center, entranceSpeed * Time.fixedDeltaTime)
```

The entrance speed is two world units per second. Multiplying by the physics time step makes the distance proportional to elapsed time. `MoveTowards` stops at the destination without overshooting. The boss uses a kinematic Rigidbody2D so its script controls its position. After arrival it continues holding that same point.

The arrival flag checks the completed physics position, not merely the next scheduled movement. Only after arriving does the attack coroutine start. The boss is vulnerable during its entrance, but it does not shoot on the way down.

**11. The two boss attacks.**

The boss repeats this sequence:

1. Show `FAN INCOMING` and an orange charge visual for 0.65 seconds.
2. Fire five shots in a fan. The angles relative to the player's direction are -24, -12, 0, 12, and 24 degrees.
3. Wait 0.4 seconds, aim at the player's new position, and fire a second five-shot fan.
4. Recover for 2.2 seconds.
5. Show `SEEKERS INCOMING` and a purple charge visual for 0.9 seconds.
6. Launch two seekers from opposite sides of the ship, initially angled outward by 35 degrees.
7. Recover for three seconds, then repeat.

The fan bolts travel straight at speed 4.2; they do not follow you after firing. The seekers travel at speed 2.8 and can turn at 55 degrees per second. They disappear after five seconds. Limited speed, turning, lifetime, warnings, and recovery time give the player room to dodge.

The boss has separate `BossFanBolt` and `BossSeeker` prefabs. Changing those does not change the existing Raider or Hunter projectiles. The boss plays the shot sound once per volley, rather than once for every bullet in a five-shot burst.

**12. Boss health, defeat, and restarting.**

The boss starts with thirty health. Every individual player projectile removes one health. `Bullet.TryConsume()` returns false if that bullet was already used; that prevents duplicate damage from multiple callbacks for the same projectile. Triple shot can therefore deal up to three damage if all three bolts actually hit.

The Canvas has a new `BossHUD`, hidden during ordinary waves. It shows health numerically, a filled bar, and the attack warning. Its fill amount is `currentHealth / (float)maximumHealth`. The `(float)` conversion is necessary: integer division would make most fractions become zero. The UI is updated when damage happens; it does not decide how much health the boss has.

At zero health, the boss marks itself defeated before doing anything else. It stops attacking, disables its collider and physics simulation, clears hostile projectiles, awards 250 points once, and plays the existing explosion animation and sound. After the animation, it notifies the spawn manager. Victory is accepted only for the tracked boss while the encounter is still running and the player has not already lost.

Victory stops both spawn loops, hides the gameplay HUD, displays `VICTORY!`, and shows the existing R restart prompt. `Time.timeScale = 0f` freezes remaining gameplay objects. Unity still calls `Update`, allowing the UI to detect R. Player input and damage also check the round-over state, so shooting cannot continue on the victory screen.

Pressing R restores `Time.timeScale = 1f` before reloading the current scene. That creates fresh player, wave, and boss state. R still works after the usual game-over screen too.

**13. Tuning the game yourself.**

Select `Spawn_manager` in the Hierarchy to change `Final Wave`, enemy counts, spawn/intermission timing, shield chance, pickup intervals, cap, or weights. The boss prefab reference is assigned there.

Select `Assets/prefabs/BossEnemy.prefab` to change health, reward points, entrance speed, fan count/spacing, warning duration, burst spacing, and recovery durations. Select `BossFanBolt.prefab` or `BossSeeker.prefab` to tune projectile speed, lifetime, and seeker turning.

`[SerializeField]` exposes a private field in Unity's Inspector and saves its value in the scene or prefab. A private field remains controlled by its own class in C# while still being editable by you in the Editor. `[Min]`, `[Range]`, and `[Header]` make Inspector editing clearer; they do not replace good runtime logic.

Edit prefab assets or scene settings outside Play mode when you want changes to persist. Runtime test changes normally disappear when you leave Play mode. The current numerical values are a playable starting point; difficulty and pickup balance should be tuned by playing several complete runs.

**14. What was verified in the running Editor.**

The boss checks exercised all five wave counts, duplicate-start protection, its vertical entrance and exact center hold, both attack patterns, the direction and spread of fan shots, seeker turning, real projectile collisions against the player and boss, player homing acquisition, health-bar updates, the final hit, the 250-point reward, hostile-projectile cleanup, victory, and the actual R-key restart path. A separate loss check confirmed that player death during the boss entrance stops attacks and spawning without awarding victory. The final victory text fits on one line without overflow.

Earlier combat checks covered regular-enemy shields, movement, evasion, ramming, rear firing, shooting pickups, all eight pickup weights, guaranteed ammo recovery, the C-key magnet, weapon replacement and expiry, and camera-shake position restoration. Normal movement measured approximately 3.5 units per second, while the slow hazard reduced it to 1.75. Magnet movement measured 14 units per second. These are functional tests; the final difficulty still benefits from complete human playthroughs.

The test-only accelerated wave timings and forced conditions were not saved to the game scene. The saved scene uses a 1.2-second base enemy interval, four-second intermissions, a 25% shield chance from wave 2, final wave 5, and a thirty-health boss. Unity was left in Edit mode with no compile or runtime console errors.

**15. Your requested checklist, re-checked on 10 November 2026.**

All the items below are implemented and connected to the saved game scene. A fresh audit passed 52 checks with no failures. Detailed results are saved in `Docs/CombatRecheckResults.json`.

| Requested task | Implementation and where to look |
|---|---|
| Homing projectile seeks closest target | `HomingProjectile.FixedUpdate` searches regular enemies and bosses, compares squared distances, and steers toward the nearest valid target. |
| Rare power-up activates homing | `HomingPowerup.prefab`, pickup ID 7, has weight 5 out of 100. `Player.EnableHomingShot` activates it for five seconds. |
| Enemy avoids player shots | `EnemyInterceptor.prefab` uses `DetectIncomingShot`; range 2.5, dodge speed 3.5, duration 0.45 seconds, cooldown 0.8 seconds. |
| C attracts pickups quickly | `Player.Update` reads the C-key press; `AttractPickups` selects pickups within 12 units; `TripleShotPowerup.FixedUpdate` moves them at speed 14. |
| Enemy shoots pickups ahead | `FindPickupAhead` chooses a valid pickup below the enemy and within its forward corridor; `EnemyLaser` destroys it on collision. |
| Enemy fires backward after passing player | `EnemyRearGunner.prefab` compares Y positions and fires upward when it crosses behind the player. |
| Enemy aggression and ramming | Raiders and Interceptors charge when within 2.2 units; the ram is a committed direction with a cooldown. |
| Enemy shield absorbs one free hit | From wave 2, regular enemies have a 25% shield chance; the first projectile removes the shield and returns before kill logic. |
| Balanced pickups, common ammo, rare health | Independent spawn timers, a four-pickup cap, ammo weight 50, health weight 5, and guaranteed ammo recovery when empty. |
| Enemy types can shoot and damage player | All four regular types use `EnemyLaser`; actual collision calls `Player.Damage`. |
| Enemy with unique projectile | Hunter uses the seeking `EnemySeeker.prefab`; its turning is limited so it can be evaded. |
| Unique enemy movement | Raider/Hunter use waves, Interceptor zigzags, and Rear Gunner enters diagonally. |
| Negative pickup | `SlowPowerup.prefab`, ID 6, halves effective movement speed for five seconds and displays the remaining time. |
| Increasing enemy waves | `SpawnManager.WaveRoutine` produces 3, 5, 7, 9, and 11 regular enemies, followed by the final boss. |

The audit included actual physics collisions, an actual virtual C-key press through Unity's Input System, timed expiry, measured player movement, weighted prefab creation, actual asteroid-triggered spawning, shield distribution, pickup replenishment, and wave counts. It also checked that an Interceptor physically moved out of a laser's path, not merely that its dodge flag changed.

**16. C# concepts used by these systems.**

An **enum** gives readable names to a set of choices, such as `EnemyRole.Interceptor`. A **prefab** stores a configured GameObject so each spawned instance receives the same components and Inspector values. The four regular enemy prefabs share one `Enemy` class but use different role, movement, and projectile settings.

A **reference** is a variable pointing to an existing object, such as the pickup's `_magnetTarget` pointing to the player. A **null check** handles the case where the referenced object is missing or has been destroyed. `IsTargetable` prevents homing weapons from pursuing an enemy that is already exploding.

A **Vector2** stores an X and Y value. Subtracting positions gives a vector pointing from one object to another. `.normalized` keeps its direction and makes its length one, allowing a separate speed value to control movement. `sqrMagnitude` gives squared length, which is sufficient for comparing distances.

`Vector2.Dot` measures how closely two directions align. For the dodge check, one direction is the bullet's movement and the other points from the bullet toward the enemy. A positive value near one means the bullet is heading toward the enemy; a negative value means it is heading away. The current threshold is 0.6, which is a broad threat cone rather than a guarantee of collision.

A **cooldown** stores the earliest allowed time for another action. An **expiry time** stores when an effect ends. For example, `Time.time + 5f` means five game seconds from now. These comparisons let the game continue running while abilities wait; there is no blocking loop.

`FixedUpdate` is used for physics movement. `Update` reads key presses and manages ordinary frame-by-frame logic. Coroutines use `yield return new WaitForSeconds(...)` to sequence waves and attacks while the rest of the game continues.
