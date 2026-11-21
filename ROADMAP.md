# Hybrid Bosses: Roadmap

A Wizard of Legend mod (BepInEx + HarmonyX) that adds elemental mini-bosses, rival-wizard fights, chaos stages and new final bosses.
It reuses only assets already in the game; the mod ships code, not art.

Status key: ✅ done · 🧪 built, needs in-game testing · 🔜 next · 📋 planned · ❓ needs research first

---

## 1. Elemental mini-bosses for every stage 🧪

Giant enemies (the `Super*` mini-bosses) take the element of the stage they spawn on, with that element's colours and attacks.

| Stage | Game element | Giant becomes |
|---|---|---|
| Forest, Earth | Earth | Earth giant |
| Ice | Water | Water/Frost giant |
| Fire | Fire | Fire giant |
| Air | Air | Wind giant |
| Lightning | Lightning | Thunder giant |
| Castle / final stage | Neutral | random element (configurable chance) or gray |

- ✅ Find the giants (`SuperKnight/Mage/Rogue/Archer/Lancer/Summoner/Coffin`, all `MiniBoss`) and why they're always gray (`MiniBoss.Start` forces palette 3).
- ✅ Read the stage element (`Level.element`, set by `NextLevelLoader.SetLevelInfo`).
- ✅ Recolour the body and the extra parts (arms, bow, lance, coffin) after each giant's own `Start`. Confirmed in game: every giant on the Wind stage went Wind.
- ✅ Element→palette table (F9): Neutral 0, Water 4, Fire 5, Earth 6, Air 7, Lightning 8 (1–2 enhanced, 3 mini-boss gray).
- 🧪 Give giants the matching elemental enemy skills. All of them work on normal difficulty.
- 🧪 Giant Coffin casts 3 player arcana of its element (see Enemy Arcana below).
- 🧪 Giant Coffin colour: its sprite sheet has no elemental palettes (rows 4–8 render pink), so it keeps palette 3 and gets an element tint and outline.
- 🧪 Giants' summon skill calls the elemental version of their minions (a Wind giant Knight summons Wind Knights). The Coffin still summons plain Coffins.
- 🔜 Confirm in game that borrowed skills animate and hit, and that there are no errors in the log.
- 📋 Fill the skill gaps. Skills that can be borrowed per giant right now:

  | Giant | Fire | Water | Earth | Wind | Thunder |
  |---|---|---|---|---|---|
  | Knight | 3 | 1 | 2 | **0** | **0** |
  | Mage / Summoner | 1 | 1 | 2 | 2 | 2 |
  | Rogue | 3 | 2 | 3 | 3 | 2 |
  | Archer | **0** | 1 | 2 | 1 | **0** |
  | Lancer | **0** | 2 | 2 | 2 | 2 |
  | Coffin | 3 arcana | 3 arcana | 3 arcana | 3 arcana | 3 arcana |

  Skills like WindKnight's and ThunderKnight's only work on their own enemy class. Options: write small adapter states (the way `FinalBoss` adapts other bosses' attacks), or borrow from another class (for example, Rogue's Thunder skills for the Knight).
- 📋 Elemental basic attack (`attackSkillID`), an element-coloured aura/outline, and an element particle burst before attacks.
- 📋 Elemental Coffin stomp: trigger an element burst on landing (`SuperCoffin.StompEffect`) and on its coffin-block bounce.
- 📋 Elemental versions of the other big enemies (`SuperBlob`, `SuperMovingStatue`, `GhoulLarge`, `MimicEnemyLarge`).

## 2. Rival-wizard events 🧪

The red portal sometimes leads to an arena with three teleporters: **Easy / Medium / Hard**. Each one starts an elemental fight against a rival wizard.

- Before the fight, the camera focuses on the rival for about 4 seconds and shows a preview of their arcana.
- Their AI should behave like Sura's (the final boss's).
- Harder portals mean more arcana, faster attack patterns, more HP and better rewards.

What the DLL shows:
- Each stage has a misc-room portal (`PortalManager.miscColor`) leading to a side room inside the stage, a `Room` with `RoomType.Misc` whose NPC is one of its interactables (Banker, Doctor, Collector, Slots, Tailor, Shuffler, Piñata). Its arrival point is `Level.miscRoomPortalInPosition`, set in `LevelGenerator.SetExitAndStorePositions`. I'm assuming this is the "red portal"; still to be confirmed in game.
- `Contestant` (the PvP wizard) is built as an **ally**: in `Start` it renames its hurtbox to `AllyHurtBox`, moves itself onto the Player layer, targets enemies and joins `GameController.allies`. Its AI already has chase, dash, pit-dashing and its own Tornado / Fireball / Sword Throw. It also seems to check for nearby player attacks before dashing (not confirmed). And it calls `HandleExtraSkills`, so it uses extra skills such as our arcana.

Plan:
1. 🧪 Misc room → arena. After stage generation, with config `RivalWizards.Chance` (0.3), the misc room's NPC and interactables are hidden and replaced by a `RivalArena`.
2. 🧪 Three `Portal.Prefab` portals: Easy (green), Medium (yellow), Hard (red). Stepping in teleports you to the arena centre and locks in the tier.
3. 🧪 The rival is the Contestant, turned hostile after its `Start` (hurtbox and layers back to enemy, targets players, rejoins the enemy list, enemy skill category, drops loot). Its own arcana are re-owned through a `SetAttackInfo` patch so they hit you.
4. 🧪 Tiers:

   | Tier | HP | Arcana | Cooldowns |
   |---|---|---|---|
   | Easy | 500 (+50 per difficulty) | 1 | ×1.4 |
   | Medium | 900 (+90 per difficulty) | 2 | ×1.0 |
   | Hard | 1400 (+125 per difficulty) | 3 | ×0.7 |

   Element: the stage's element, or random on neutral stages.
5. 🧪 Intro: rival frozen and invulnerable, the camera moves to it (`CameraController.OverrideAndMoveCameraToLocation`) for `IntroSeconds` (4), and a banner shows "<Element> Rival Wizard (<Tier>)" plus its arcana names. Then the fight starts.
6. 🧪 **Spell pacing** (fix for "casts everything at once"). The Contestant's own spells were ally-tuned: 5–8s cooldowns, all ready at spawn, almost no windup. Now every spell, its own and borrowed, shares a cast gap (Easy 3s, Medium 2s, Hard 1.3s). Its own cooldowns are scaled by tier, there's a 1.5s delay after the intro, damage is scaled by tier (Easy 50%, Medium 75%, Hard 100%), and Easy windups are longer.
7. 🧪 **Intro reveal:** title and tier, then icons appear left to right one by one, each with its name: borrowed arcana, the rival's signature Fireball / Sword Throw / Tornado, its robe and its relic. The intro lasts as long as the reveal needs (at least `IntroSeconds`). Elemental giants get the same reveal, without freezing, the first time you get close.
8. 🧪 **Robe:** a random player robe. Its colour row goes on the rival's sprite (`_PaletteIndex = outfitColorIndex`), and its stat mods (Health, Damage, Cooldown, Speed, Evade, Armor, Crit) become rival bonuses.
9. 🧪 **Relic:** a random relic from a list of 29 stat-type relic classes (DamageUp, HealthUp, ArmorUp, EvadeUp, CritChanceUp, MoveSpeedUp, ReduceCooldown, GlassCannon, the auras…). It shows the real relic icon and name, with its effect translated to a rival stat (relic code itself is written for the player).
10. 🧪 v0.1.1 fixes from testing:
    - **Blizzard:** the Contestant secretly registers the Frost Mage's Blizzard (`FrostMageSummonBlizzard`) as an instant extra skill, and its chase state calls `HandleExtraSkills` every frame before anything else. Now every rival extra skill waits for the shared gap. Blizzard stays only on Water rivals, and is shown in their intro.
    - **Variety:** after each skill, the rival's skill priorities are reshuffled (the one just used drops to 0), so it rotates through its kit. The catalog grows from 3 to 6–9 arcana per element. Rivals get 2 / 3 / 4 arcana (Easy / Medium / Hard).
    - **Safety net:** an arcana that throws when cast is disabled for the session and never picked again.
    - **Reward chest:** rivals drop the mini-boss reward chest (`dropLootChest`, table `TreasureChestMiniBoss`).
11. 🧪 v0.1.2 fixes, after "he hurt himself with Tornado and Shock Dragon and never attacked me":
    - An IL scan of every catalog prefab found 5 that home in on *enemies* themselves (Shock Dragon, Lightning Storm, Volt Splinter, Water Bounce, Air Sentry). Another 14 spawn follow-up hits that load the player's stats later (Wind Falcon, Shock Boomerang, Flame Buster, Meteor, Flame Trap, Water Ball, Current Burst, Earth Boomerang, Poison Bolas, Exploding Boulder, Aero Ring, Thunder Fan, Ball Lightning, Shock Nova). Both groups are off by default (config `EnemyArcana.IncludeRisky`). That leaves 24 clean arcana: Fire 5, Water 5, Earth 6, Wind 5, Thunder 3.
    - Everything an enemy casts is tagged with `ArcanaOwned`. Hits under it that set their stats later are re-owned too.
    - Friendly-fire guard: anything an enemy caster or rival casts can never damage it or any other enemy.
    - The rival's own Fireball / Tornado / Sword Throw are attributed to it whenever their states run, not only during its Update.
    - Robe stats were misread (Vigor showed "HP −40%"). Robe mods are a magnitude plus an `isIncrease` flag, and are read that way now.
    - 📋 Bring the risky 19 back one by one by handling their follow-up hits, and aim the homing ones at players.
12. 📋 Later: a boss health bar, better chest tables for Hard, dropping the rival's relic, a secondary element on Hard, Sura-style attack patterns (`FinalBoss` `standardPatternList`/`superPatternList`), more arcana per element, and relics with special effects (Resurrect, Vampire…) implemented for real.
7. 📋 Arena layout tuning once seen in game (portal and rival offsets are guesses).

## 3. Chaos stages 🧪 (v0.4.2)

Scope agreed for now: a regular stage can have a **Chaos version**.
- 🧪 **Look:** purple-tinted tiles (recoloured atlas), decorations and lights.
- 🧪 **Enemies:** they incorporate **Chaos-element arcana** (regular enemies 1, giants 2; elemental giants keep their stage element on top).
- 🧪 **Rival:** the stage's rival challenge (misc-room arena) features a **Chaos rival** casting Chaos arcana. Rivals never appear outside their arenas.
- 🧪 **Rewards:** gold and item drops ×1.5 while on the stage (`ChaosStages.RewardMultiplier`).
- 🧪 **Shown:** "Chaos <stage>" stage title, and the stage's marker is tinted purple on the loading-screen board.
- Which stages: `ChaosStages.StageChance` (0.15) per regular floor; boss floors, the Hub and the final room stay normal. The roll comes from the run seed + tier + stage, so the board and the stage always agree. **Shift+F11** toggles Chaos on the current stage for testing.
- 📋 Later: the Council fight corrupted on Chaos runs (their attacks tinted dark purple like Chaos arcana, sprites tinted, a dialogue change that makes them seem a little corrupted). Also Chaos boss floors.

### Earlier research notes (prototype v0.4.1)

What the DLL shows:
- Each stage is a `TileSet` class (`FireTileSet`, `IceTileSet`… on top of `CastleBaseTileSet`) with **one atlas `material`**. `MeshGenerator.GenerateMeshes` builds the tile meshes under a "Meshes" container with plain white vertex colours, then static-batches them. Tiles have no palette index like enemies.
- Decorations and props are sprites under `Level.levelContainer`. There's also stage lighting (lights in the scene, `GameController.stageLight`).

Prototype (code only, no new art):
- **Tiles:** each atlas is copied through a RenderTexture (works on the game's non-readable textures), every colour is remapped by the variant (hue / saturation / value, with a per-colour cache so it's fast), and a cloned material replaces the original.
- **Decorations:** tinted. **Lights:** blended toward the variant colour.
- 3 variants: **Void** (purple), **Ember** (inverted warm), **Blight** (sickly green).
- **Shift+F11** cycles Void → Ember → Blight → normal on the stage you're on. `ChaosStages.Chance` (0 for now) rolls a variant per stage. The log line shows the counts and milliseconds.

Next, once the look is approved:
- tune or add variants (maybe one per stage element);
- a "Chaos <Stage>" title;
- chaos rules: mixed-element enemies, more giants and rivals, Chaos rivals more likely, maybe `Level.doubleEnemies`;
- better rewards;
- show chaos stages on the loading-screen board.

## 3 (original plan). Chaos stages

Recoloured versions of the existing stages that act as "chaos" variants.

- ❓ Find how tilesets are coloured. Enemies use the `Chaos/Palette Swap` shader; check whether tiles and decorations use it too, or plain sprites (`Level.tileSetName` picks the set, for example `FireTileSet`).
- 📋 If palettes are available: swap the palette index on stage load. If not: tint materials, or recolour by shader swap at runtime.
- 📋 Chaos stage rules: mixed-element enemies, Chaos-element giants, maybe a modifier (double enemies via `Level.doubleEnemies`, more giants).
- 📋 A chance per stage to roll the chaos variant (configurable).

## 4. Arcanos mod compatibility (Radiant variants) 📋 ❓

Let enemies and stages roll **Radiant** variants when the Arcanos mod is installed.

- ❓ Get the Arcanos mod and check how it adds the Radiance element (probably through LegendAPI's element support). Note what it exposes: element ID, colours, arcana.
- 📋 Soft dependency (`[BepInDependency(..., SoftDependency)]`): detect Arcanos at runtime, so this mod works without it.
- 📋 Radiant giants: a custom palette or tint, plus Radiant arcana as their skills.
- 📋 Radiant stages, built on the chaos-stage recolour system from part 3.

## 5. Final-stage boss pool 🧪 (v0.4.0)

Built:
- 🧪 Each run has a `FinalBoss.Chance` (0.35) that the final boss is a **champion** instead of Sura:
  - an elemental **Archon** (Fire / Water / Earth / Wind / Thunder Archon), or
  - the **Chaos Sovereign** (`ChaosSovereignChance` 0.3), with arcana from every element.
  It's the rival-wizard kit at a new **Final** tier: 2600 HP (+200 per difficulty), 5 arcana, a dash arcana, a signature meter, fast spell pacing, a robe and a relic, and the full intro reveal.
- 🧪 **Loading-screen icon (v0.4.1).** The run decision is made when the run starts. On the loading screen's progress board (the three stage bosses, then Sura's Chaos icon), the final circle shows the champion instead: the ring tinted in its element colour, the Chaos symbol hidden, one of the element's arcana icons in its place. It's placeholder art, and it greys out under the board's dimmer like the others.
- The old corner badge in the final room is now optional (`FinalBoss.RoomBadge`, off).
- 🧪 **Normal ending kept.** Sura stays the room's official boss. When the fight starts she's hidden and frozen and the champion appears where she stands. When the champion dies, Sura is put into her *final* death (`chaosMode` on, both form intros marked done), and the room's own code runs the victory, `BossOutro` (**Chaos arcana unlock** + achievement) and the ending, exactly as after beating her.
- F12 test key: in the final room before the fight, it turns this fight into a champion. Elsewhere, it forces the next final room and spawns a champion beside you to try the fight.
- 📋 Next: a big boss health bar for champions, phases (element swap at 50% for Archons), real badge art, Radiance in the Sovereign pool once Arcanos is supported, and possibly champions replacing Council members.

## 5 (original plan). Final-stage boss pool

The final stage can roll strong elemental wizard bosses instead of Sura, or a new boss.

- 📋 Elemental champions: one element each, built on the rival-wizard class (part 2) at full strength.
- 📋 **The All-Element boss:** every element plus Chaos (and Radiance when Arcanos is installed). Phases cycle elements, with a palette/particle change as the tell. `FinalBoss` already borrows each elemental boss's signature attack, which is the template here.
- 📋 Hook into `FinalBossRoomEventHandler` to pick the boss (weights in config; Sura stays in the pool).
- ❓ Check how `EndingRoomEventHandler` spawns all five element bosses, to reuse for multi-boss or phase fights.

## Shared system: Enemy Arcana v2: the whole arcana pool 🧪 (v0.2.0)

Goal: rivals and giants can use **every player arcana that fits their element**, from vanilla and from other mods. A Chaos "jack of all trades" rival mixes every element. No more hand-picked lists.

How it works:
1. **Catalog** (`ArcanaCatalog.Generated.cs`, from `tools/gencatalog.ps1`): all 233 player skill states read from the DLL, each with its skill ID, family (Projectile / Spell / Dash / Jump / Melee / Excluded = buffs, minions, wards, heal), main prefab and prefab type (Projectile / Wave / Other). Re-run the script after a game update.
2. **Element from the game's data:** at the start of a run, each arcana's element is read from its own player stat data (Fire → Fire; Water/Ice → Water; Earth/Plant/Poison → Earth; Air → Wind; Lightning → Thunder; Chaos and Neutral → Chaos rivals only). So a Thunder rival can only ever get Lightning arcana.
3. **Other mods' arcana** (LegendAPI skills and so on): found at startup by reflection (`Player.SkillState` subclasses with a `staticID`), with their prefabs found by a small IL scan, then put through the same pipeline.
4. **Delivery by family:** projectiles fly at you (fans spread 3 shots, chain arcana fire 3 in a row and re-aim), waves travel toward you, ground spells land on you, self spells go off around the caster, basics hit in front of it. Hand-tuned timings override the defaults where they exist (`EnemyArcana.Tuned`).
5. **Generic ownership** (`OwnerContext`), the piece that makes the "risky" arcana usable. Every arcana prefab class gets Harmony patches on its Unity callbacks and coroutines. While an enemy-owned object's code runs:
   - `GameController.enemies` and `players` are swapped, so arcana that hunt "the nearest enemy" (Shock Dragon, Lightning Storm, Volt Splinter…) hunt **you**;
   - anything it spawns (follow-up hits, strikes, bursts) is tagged with the same owner;
   - every stat load (`SetAttackInfo`, including the copy-from-parent version) is re-owned to the caster.
   The friendly-fire guard stays as a last safety net, and any arcana that throws an error is switched off for the session.
6. **Fields the player's state normally sets** on prefabs (`skillCategory`, `skillID`, `parentEntity`, `parentObject`, `moveVector`, `origin`, `targetGroup`, wave skill fields) are filled by name when present.
7. **Loadout picking:** rivals spread their arcana over different cast types (Chaos rivals: over different elements). The rival's own Fireball / Tornado / Blizzard only appear on Fire / Wind / Water rivals (Sword Throw is neutral). Chaos rivals keep all of them.
8. Config: `EnemyArcana.Families` (turn families off), `RivalWizards.ChaosChance` (0.15). F11 cycles Fire → Water → Earth → Wind → Thunder → Chaos.

Next: per-arcana fixes as testing finds them (an override row in `Tuned`, or a small init hook). Charge/hold arcana scaling, ultimates on Hard, and minion arcana (currently excluded).

### v0.3.0 additions 🧪
- **Dash arcana work like the player's.** Rivals (and arcana-casting giants) get one dash arcana of their element. It fires from their own dashes (`Contestant.DashState` / `Enemy.DashState` OnEnter → OnExit): walls, traps, decoys, mines and waves are left at the dash start along the dash direction, projectiles fire from the dash end at you, and ground spells land on you. The intro shows it as "(dash)".
- **Signatures are for the player, rivals and the Council only** (the Council already has its own signatures in its movesets; this mod doesn't touch them). Giants have no meter. Instead, any giant arcana has a `EnemyArcana.EnhancedChance` (0.2) to be cast as its enhanced version.
- **Signature meter (rivals).** It fills as the rival's hits land on you: Easy 12, Medium 9, Hard 6 hits. When full, the caster glows in its element colour, a small meter under it pulses "SIGNATURE", and its next arcana is empowered: ultimate stats, a longer tell, more shots (fan ×2, single projectile ×3), 20% bigger, +30% damage.
- **Summons**, done the way the game's elemental Summoners do it. Minion arcana (Fire, Water, Earth, Wind and Lightning minions) and seeker arcana (Flame, Ice, Earth, Wind and Thunder seekers) spawn in the game's own enemy mode (`isEnemy`). They use the matching Summoner's enemy stats (`FlameSummonerMinion`, `ThunderSummonerSeeker`…), with 35 HP per minion and 5 per seeker, up to 3 minions and 6 seekers, and vanish when their caster dies. Not yet castable: Chaos minion, Skeleton, Turret (no enemy stats to borrow).
- **Wards** (Fire / Water / Earth / Wind / Lightning / Chaos): placed under the caster, owned (pulses hit you, never enemies), and their hurtbox is made hostile so you can break them.
- **Fall protection for rivals (v0.3.2), like the player's.** A rival that falls into a pit loses the player's pit damage (at least 5% of its max HP, never lethal) and comes back at the nearest free spot to where it last stood on solid ground, invulnerable for 1s. Before, it snapped back to its spawn point for free.
- ✅ User-tested: rivals with dash arcana, signatures, summons and wards are "working great".
- Config `EnemyArcana.ExcludeFamilies` replaces `Families`: list the kinds to turn off (Projectile, Spell, Jump, Melee, Dash, Minion, Seeker, Ward). Empty = everything.

## Shared system: Enemy Arcana (v1, superseded) 

One system that lets any enemy cast player arcana. The giant Coffin uses it first, then rival wizards, then final bosses.

What the DLL shows:
- The game's own enemy wizard (`Contestant`, from PvP) doesn't run player arcana. Its Tornado, Sword Throw and Fireball are rewritten as enemy states, so even the developers took that route. Those states only work on `Contestant`, so we can't reuse them directly.
- But both sides make attacks the same way: spawn the arcana's prefab, then `Attack.SetAttackInfo("Player", "<arcana skill ID>", level, isUltimate)`. Contestant's fireball uses the player's own skill ID (`ShootFireball`), so it does real player-arcana damage and status effects.
- There are about 100 player arcana classes (`Player+Use...`). Each one's aiming and timing code needs a `Player`, but the part that does damage (prefab + attack info) doesn't.

- Team trap: with the "Player" category, the game credits the hit to the player and uses the player's target list, so the arcana would hit enemies. `EnemyArcana.MakeEnemyAttack` loads the real player stat data, then re-owns the attack: enemy skill category, enemy as attacker, and the target list from the enemy's own basic attack.

Plan:
1. 🧪 `ArcanaState : Enemy.SkillState` (in `EnemyArcana.cs`), driven by data (`ArcanaDef`): skill ID, prefab class, delivery type, element, windup, cooldown and range. Uses the game's normal enemy skill flow (windup → cast → exit → cooldown).
2. 🧪 Delivery types: **Projectile**, **AtTarget**, **AtSelf**. 📋 Later: **Beam**, **Wave** (directional, like Thunder Wave and Earth Wave), **Dash**, **Multi-hit/combo**.
3. 🧪 Starter catalog, 3 arcana per element:
   - Fire: Fireball, Flame Slash, Fire Blast
   - Water: Frost Fan, Ice Lance, Water Blast
   - Earth: Boulder Throw, Hammer Throw, Boulder Drop
   - Wind: Twister, Wind Falcon, Tornado
   - Thunder: Shock Line, Shock Boomerang, Lightning Strike
4. 🧪 First host: the giant Coffin (also gets a tint and outline in place of the pink palettes). Config `EnemyArcana.AllGiants` gives arcana to every elemental giant.
5. 📋 Grow the catalog toward all ~100 arcana; reuse for rivals and final bosses, with a preview of their arcana loadout.

## Extra: modded arcana for bosses 📋 ❓

Let rival and final bosses use arcana added by other mods.

- ❓ LegendAPI registers player arcana. Find where it stores them, so we can list modded arcana at runtime.
- ❓ Big catch: player arcana (`Player.SkillState`) are tied to player input and HUD, and enemies can't run them directly. Two options:
  - an adapter that runs a player arcana's projectile/effect code on an enemy (hard, but would work for most mods), or
  - a small registration API: other mods give an enemy-side version of their arcana (easier, but needs their cooperation).
- 📋 Start with option two for our own and Arcanos arcana, and try option one later.

---

## Testing hotkeys (debug build)

| Key | Does |
|---|---|
| F5 | List live enemies grouped by class/element/palette, plus each giant's skills |
| F6 | Cycle the nearest enemy's palette |
| F7 | Dump every field of the nearest enemy |
| F8 | Spawn the next giant beside you |
| F9 | Dump the element → palette table and the current stage element |
| F10 | Cycle the nearest giant through all five elements |
| F11 | Open a rival arena around you (three portals) |
| F12 | Final champion test (this final room, or spawn one here and make this run's final boss a champion) |
| Shift+F11 | Toggle the Chaos version of the current stage |

Config: `BepInEx\config\osi.wol.hybridbosses.cfg` (created on first launch).
