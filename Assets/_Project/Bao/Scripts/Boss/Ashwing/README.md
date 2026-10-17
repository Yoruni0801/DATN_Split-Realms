# Ashwing — Cú Tro Tàn

The boss prefab `Assets/_Project/Bao/Boss/Ashwing.prefab` opts into `AshwingBossCombat` and `AshwingBossVFX`. The placed instance in Map 6 is configured as well. Ordinary Ashwing enemies continue using their existing AI.

## Combat

| Attack | Readable cue | Response |
| --- | --- | --- |
| Cinder Razor | One charged feather and a narrow aim line; final 0.2 seconds lock aim | Dodge the committed line or reflect the feather |
| Feather Fan | Three charged feathers and three diverging aim lines | Move through the gaps; phase 2 adds two delayed outer feathers |
| Hunting Dash | Folded wings and two rails marking the complete damage lane | Jump or parry; a successful parry deals 16 damage and staggers for 1.35 seconds |
| Ashfall | Four committed ground markers around a central safe gap | Stay in the gap; descending feathers can be parried; cinder patches expire after 1.8 seconds |

At 50% health the boss clears existing ordnance, transitions once, then introduces Ashfall. Rain has a dedicated cooldown and finishes before the next attack can begin. Timing uses game time, including during pauses and slow motion. Dash sweeps the player and scenery instead of relying on a point overlap.

Health, loot and existing death/Timeline events remain owned by `StandaloneFlyingShooterEnemy`. The intro's `TimelineEnemyAggroController` still enables aggro. Do not attach the ordinary enemy's `AshwingVFXController` to this boss; it requires a separate enemy AI.

## Presentation and tuning

- Enlarged presentation: projectile prefab scale 0.60 (previously 0.36), effects scale 1.5, and base projectile speed 18 units/s (previously 12). Fan and phase multipliers still apply. Ashfall feathers use scale 0.72, descend in 0.30s, and mark the same 0.9-unit radius used by their ground damage; warning durations are unchanged.
- Ivory facial detail is preserved by the sprite shader; red feathers receive moving cinder veins, warm edges, hit flashes and death dissolve.
- The existing SpriteSkin rig receives additive wing/root poses. Four wingtip trails follow the animated silhouette.
- Reusable presentation pools contain 56 ribbons and 112 feather motes. Gameplay projectiles and rain lanes have explicit lifetimes and are cleared on cancellation, death and disable.
- Audio references use clips already present in the project, with separate charge and release sources. Camera feedback uses the existing `AdvancedCameraController2D`.
- A separate `AshwingBoss.controller` provides stable Fly/Attack playback without modifying the ordinary enemy's animator.

Default boss health is 240. Other primary tuning values on `AshwingBossCombat` are snipe windup 0.62s, fan windup 0.82s, dash windup 0.85s, projectile damage 10, dash damage 18, and reflected projectile damage 20. The arena is centered 8 units left of the initial boss position with a half-width of 21 units, matching this hallway encounter. Revisit the arena parameters when placing the prefab in another map.

`Tools > Ashwing > Install Cinder Sovereign upgrade` recreates missing presentation assets and wires the boss prefab plus matching instances in the active scene. It explicitly resets the install's health, arena and asset-reference defaults; routine balancing should be done directly in the Inspector instead.

## Verification performed in Unity

- Unity compilation: no C# errors; the plumage shader reported no shader errors.
- Boss and projectile prefabs: no missing scripts.
- Map 6 runtime: observed Cinder Razor, Feather Fan, Hunting Dash, phase transition and Ashfall; confirmed four rain lanes with active cinder patches.
- Dash parry callback during the live dash: success, 16 HP deducted, Stagger state entered.
- Game-time pause during windup: charge remained unchanged.
- Live projectile parry adapter: velocity reversed and damage changed from 10 to 20 immediately.
- Projectile collision-handler integration with PlayerHealth: 10 HP deducted; test health restored afterward.
- Isolated prefab clone death: Dead state entered, owned ordnance disabled and loot detached/activated. Clone death callbacks were isolated from scene progression.
- Console inspection after the runtime checks: no errors.

### Companion collider regression

Map 6's following fairies have solid colliders on the Wall layer. Previously, feathers treated them as terrain and disappeared before reaching the player; ground queries could also land Ashfall on a fairy. `IsArenaSurface` now excludes `FairyPlayerCompanionController` colliders for projectile impacts, rain placement and dash terrain queries.

Validated in Map 6 Play Mode with controlled positions and real Physics2D simulation: feathers at 18, 16.2 and 20.34 units/s passed through an active fairy collider and each dealt 10 HP; a solid Wall collider still stopped the shot without player damage. Ashfall's actual update queries selected Crystal ground beneath a fairy, dealt 12 HP on descent, avoided a second landing hit, and dealt 3 HP on a burn tick. Moving 2.7 units outside the patch caused no damage. Runtime test objects were removed and player health restored before leaving Play Mode.

Verification used Play Mode and targeted runtime probes, not a standalone build or a full difficulty/long-duration performance benchmark. Temporary runtime positioning, camera framing and invincibility used for inspection were discarded on leaving Play Mode.
