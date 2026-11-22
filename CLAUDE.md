# Wizard of Legend 1 mod: Hybrid Bosses and Elemental Giants

Handoff from a chat session to code mode. Read this first, then `reference/`.

## Goal (the user's words, condensed)
- Make the **giant/large versions of regular enemies** have **elemental variants**. Currently, on normal difficulty they are always gray and non-elemental. The elemental variants should appear **on normal difficulty too**, not only on higher difficulty or in elemental zones. Variety is the point.
- Add **rival-wizard bosses** that reuse the player's assets and arcana, the way the final boss does. Sprinkle them through a run as events, and maybe add new final bosses.
- Everything uses assets already in the game ("clever re-use", asset flipping). The mod ships code only, no copied assets.
- User is new to modding and wants either working code or very specific steps. Casual, direct tone; push back honestly.

## Environment (confirmed)
- Windows. Game: Steam, `C:\Program Files (x86)\Steam\steamapps\common\Wizard of Legend` (not yet confirmed by a successful build, but the decompile header showed `...\Wizard of Legend\WizardOfLegend_Data\Managed\Assembly-CSharp.dll`).
- Mod manager: **r2modman**. BepInEx profile: `C:\Users\migne\AppData\Roaming\r2modmanPlus-local\WizardOfLegend\profiles\Default`. Log: `<profile>\BepInEx\LogOutput.log`.
- BepInEx 5.4.21, Unity 5.6.7, Mono CLR 2.0, so the plugin targets **net35**. HarmonyX is available (`0Harmony.dll` in `BepInEx\core`). LegendAPI 2.2.1 is installed (items, outfits, skills/arcana, elements, music; **nothing for enemies or bosses**).
- Project: git repo `Desktop\WOLHybridBosses\WOL-Hybrid_Bosses\` (GitHub `3dmindscapper/WOL-Hybrid_Bosses`) with `WolHybridBosses.csproj`, `Plugin.cs`, `ROADMAP.md`. `dotnet build` there copies the DLL into the profile's `BepInEx\plugins`. Builds confirmed working. The parent `Desktop\WOLHybridBosses\` only holds old bin/obj. v0.0.6 compiled cleanly; not yet run in game.
- `ROADMAP.md` is the user's feature plan (elemental mini-bosses, rival-wizard portal events, chaos stages, Arcanos/Radiance compat, final-boss pool, modded arcana). Keep it updated.
- Useful log filter (PowerShell): `Get-Content "$env:APPDATA\r2modmanPlus-local\WizardOfLegend\profiles\Default\BepInEx\LogOutput.log" | Select-String "Hybrid Bosses" | Out-File "$HOME\Desktop\hb.txt"` (output is UTF-16; convert before reading).

## Findings so far

### Player-clone approach is a dead end (for now)
Cloning the player GameObject and driving it from script failed:
- `Player.Start()` claims the HUD (`GameUI.ClaimLowerHUD` -> `StockSkillUI.RefreshUI` NRE).
- After spawning a clone, **the real player's** `TransitionToSkill` threw NREs (arcana charged but did nothing); `lowerHUD` and `inputDevice` were null on the real player. Cause not fully pinned down.
- Player arcana are nested `Player.SkillState` classes (204 of them, `Player+UseFireBeam` etc.) held in `skillIDs`, `skillsDict`, `skillStates`, `assignedSkills[6]`, fired via `Player.TransitionToSkill(int skillNo, bool checkButtonHeld)`. They are tightly coupled to player input and HUD.
- Decision: **build bosses on the enemy side** instead.

### The game already has the hybrid-boss pattern (from the decompile)
- Class chain: `Entity` -> `Enemy` -> `Boss` / `MiniBoss`. Elemental bosses: `FireBoss`, `IceBoss`, `EarthBoss`, `AirBoss`, `LightningBoss`; `FinalBoss : Boss`.
- `FinalBoss` defines states that **subclass other bosses' attack states**, e.g. `FinalBossFireBossState : FireBoss.FireBossDragonLineState`, `FinalBossWaterBossState : IceBoss.IceBossAquaBeamState`, `FinalBossEarthBossState : EarthBoss.EarthBossRockThrowState`, `FinalBossAirBossState : AirBoss.AirBossGaleBlastState`, plus a custom lightning state. They override timing (`StdWindupTime`, `StdExitTime`) and animation names (`animOption0 = "Forehand"`, `slideAnimName = "SlideRight"`) so the borrowed attacks fit the final boss's animator.
- `FinalBoss.InitFSM()` registers all states with `fsm.AddState(...)`, and builds dictionaries: `skillRangeDict` (state -> range), `skillEleDict` (state -> `ElementType`), `bossSkillDict`. Before each attack it calls `EmitElement(skillEleDict[name])` for an element-coloured particle burst. `TransitionToNextState()` picks the next attack name from `standardPatternList`/`superPatternList` and routes through a dash state.
- `Boss`: `attackPatternList` (List<List<BossSkillState>>), `attackPatternIndex/Index`, `attackIndexCurrentMax`, `TransitionToNextState()` does `fsm.ChangeState(attackPatternList[i][j].name, allowSelfTransition: true)`. HP is set in `InitComponents` via a `NumVarStatMod("BossHP", 1500 + 125 * difficulty, ...)`. Uses `EnemyHealthBar`, `bossDisplayName`.
- `MiniBoss : Enemy` (HP 750 + 75*difficulty) sets `_PaletteIndex = miniBossPaletteIndex (3)` on its sprite material.
- Risk: borrowed attacks need each source boss's animations, so hybrids will need animation-name adaptation like `FinalBoss` does.

### Enemy recolouring is a shader palette (confirmed in-game)
- Regular enemies use shader `Chaos/Palette Swap` with `_PaletteIndex` / `_PaletteCount` = **9**. Pressing F6 cycled Archer and Lancer through 0..8: the user saw their enhanced palette and the elemental palettes.
- `Enemy` has `Dictionary elementToPalette` (6 entries) and `_enemyElementCount = 6`. **F9 (dump element->palette mapping) has not been run yet.** Do this first to map element -> index.
- Some enemies don't use palette swap: Dove and ThunderZoneEnemy use `Chaos/Sprite Diffuse Outline` (no palette); EnemyTurret uses `Sprites/Default`.
- A live `ThunderArcher` (element Lightning) had `paletteIndex 8`, which suggests **index 8 = lightning**. Unconfirmed.

### Enemy prefab keys (74, from `Enemy.enemyPrefabPaths`, spawnable via `Enemy.Spawn(key, ...)`)
- Base: Blob, BlobMini, BlobSpitter, BlobRoller, Ghoul, **GhoulLarge**, Coffin, MovingStatue, EnemyTurret, PoisonPlant, LaserOrb, Cyclops, MimicEnemy, **MimicEnemyLarge**, Spider, SpikeSpider, Archer, Knight, SkeletonMage, Rogue, Lancer, Summoner, Orb*Familiar.
- Elemental sets (Archer/Knight/Mage/Rogue/Lancer/Summoner) for **Frost, Earth, Flame, Wind, Thunder**, plus `*ZoneEnemy` for each.
- **Super\***: SuperKnight, SuperMage, SuperRogue, SuperArcher, SuperLancer, SuperSummoner, SuperCoffin, SuperBlob, SuperMovingStatue. These are probably the "giant" enemies the user means. **Unverified.** Which keys are the gray giants is the first thing to settle (F8 spawns candidates).
- Other: Contestant (PvP), Dove, Rat, Eagle.
- Elemental variants are separate prefabs and classes (e.g. `ThunderArcher`), not a field flip.

### PvP code exists (unread)
`BossRoomEventHandler+<StartPvP>`, `PvpEnemySpawner`, `Contestant`. Possible foundation for rival-wizard encounters. Read before designing rivals.

### Other types seen in the F10 dump (not yet decompiled)
`ZoneEnemy` (+ Flame/Frost/Earth/Wind/Thunder subclasses), `EnemyFactory` (spawns via `Enemy.Spawn(prefabKey, pos, parent, dropLoot, alerted, spawnCard, autoAlert, disableCountDeath)`), `ElementalPrefabSpawner`, `SpawnRandomPrefab`, `MiniBossSummonState`, `EndingRoomEventHandler` (spawns all five element bosses), `BossRoomEventHandler`, `FinalBossRoomEventHandler`.

### Settled by reading the DLL's IL (session 2, no decompiler needed)
- **Giants = `Super*` classes, all `: MiniBoss`** (SuperKnight/Mage/Rogue/Archer/Lancer/Summoner/Coffin). SuperBlob : Blob, SuperMovingStatue : MovingStatue, GhoulLarge : Enemy are not MiniBoss.
- `Enemy.Spawn(string key, Vector3 pos, Transform parent, bool dropLoot, bool alerted, bool spawnCard, bool survivalMode, bool overrideCountDeath)` (static; also an `EName` enum overload).
- Palette: `Enemy.Start -> InitElement -> SetPaletteIndex`: index = `elementToPalette[element]`; Neutral enemies on difficulty > 0 get `Clamp(difficulty, 0, 2)` (enhanced looks). **`MiniBoss.Start` then always overwrites with `miniBossPaletteIndex` (3) — that's why giants are always gray.** `Entity.Start` sets `element = Level.element` if `elementFromTileSet`.
- Elemental regular enemies (e.g. FlameKnight : Knight) differ only in `InitFSM`: different `attackSkillID` plus extra nested `SkillState`s. `Enemy.SkillState` ctor `(name, fsm, ent, reqDifficulty, priority, minRadius, maxRadius, cooldown[, auto])` calls `RegisterSkill` -> `availableEnemySkillsDict`; `HandleExtraSkills` picks by difficulty >= req, range, LOS, IsReady, highest priority. Then `fsm.AddState(IState)`.
- ~40 elemental skills take a plain `Enemy` in their ctor, so they can be transplanted onto giants. Ones typed to their own class (FrostKnight IceShield/IceBash, all WindKnight, ThunderKnight, Archer arrow attacks, FlameLancer etc.) can't. Table is in `Plugin.cs` (`ElementalGiants.Skills`). Some rows had computed args (e.g. `attackRadius * x`), so I hand-fixed min/max and Install() sanity-checks ranges.
- IL tooling (scratch, may be gone): a PowerShell `ReflectionOnlyLoadFrom` + IL opcode walker. Easy to recreate; or install ILSpy.

- Stage element: `NextLevelLoader.SetLevelInfo(name)` sets `Level.element`/`Level.tileSetName`: Forest->Earth, Earth->Earth, Ice->**Water**, Fire->Fire, Air->Air, Lightning->Lightning, PlayerRoom->Neutral (Home), else Neutral (CastleBase). ElementType order: Fire, Water, Air, Lightning, Earth, Neutral, Chaos, Ice, Plant, Poison.
- Giant `Start` overrides: SuperMage/Archer/Lancer/Summoner/Coffin call `MiniBoss.Start` then do more. **SuperLancer resets paletteIndex to 3 and recolours lance/attack sprites; SuperCoffin recolours `coffinRenderer` to 3; SuperArcher copies paletteIndex onto LeftArm/Bow/Arm.** So the element must be applied after the most-derived Start, on all child palette-swap renderers.

## In-game results
- v0.0.6 tested by user: stage matching works (Wind stage: every giant Wind). Element->palette confirmed: Neutral 0, Water 4, Fire 5, Earth 6, Air 7, Lightning 8. No mod errors in the log.
- SuperCoffin moveset: JumpAttack (stomp), BlockBounce (`CreateCoffinBlock`, the "block"), Summon "Coffin", Ready/Hurt/Dead. There's no elemental coffin, so v0.0.7 gives it hand-picked "Coffin" SkillDef rows with `CastAnim = "Ready"`.
- Non-MiniBoss giants (not handled yet): SuperMovingStatue : MovingStatue (Start calls Enemy.Start, no palette code), SuperBlob : Blob (Start forces miniBossPaletteIndex itself), GhoulLarge : Enemy, MimicEnemyLarge : MimicEnemy.

- v0.0.7 tested: summon swap works (logged EarthKnight, FlameLancer etc.). SuperCoffin goes **pink** on palettes 4–8, so its sheet has no elemental rows: keep palette 3 and tint or outline it (MiniBoss uses `_OutlineColor`/`_OutlineColor2`). The user wants the coffin (and rivals) to cast **player arcana** instead.
- Arcana research: `Contestant : Enemy` (PvP wizard) reimplements Tornado/SwordThrow/Fireball as its own states (ctor typed to Contestant). Its FireballState instantiates the prefab and calls `Attack.SetAttackInfo("Player", "ShootFireball", level, isUltimate)`, the same call player arcana make (e.g. `UseFireBeam.CreateFireBeam`: `FireBeam.Prefab` + `ChaosInst` + `SetAttackInfo`). There are 100 `Player+Use*` arcana. Plan: our own data-driven `ArcanaState : Enemy.SkillState` (skillID, prefab, delivery type). See ROADMAP "Enemy Arcana".

## v0.5.15: board / stage Chaos plan agree and stay stable (built, not yet run in game)
- **GameController.stagesPerTier is 1.** NextLevelLoader goes to the boss once stageCount > stagesPerTier, so a tier has stagesPerTier + 1 = 2 regular stages, with stageCount 1 and 2.
  - Before, the board used regular = 1, giving every square its own tier, and IsChaos rejected each tier's 2nd stage.
  - Now ChaosRun.RegularStages (= stagesPerTier + 1) is used everywhere: IsChaos, board, IsCorrupted, JumpToCouncil. The board cross-checks against curMaxStagesPerTier - 1.
- **Seed:** FinalRun.Tick rolls the run once on arriving in the Hub. It used to roll on leaving, after the first board had already drawn the previous run's plan. ChaosStageRunner.Update calls Tick every frame.
- **Ctrl+F11 fallback** now uses the game's rule (stageCount > stagesPerTier → boss).

## v0.5.14: Chaos stages actually apply; all humanoids turn Chaos (built, not yet run in game)
- **Planned but never tinted:** the log had `CHAOS stage planned` but no `CHAOS stage on`. ChaosStageRunner required Level.levelContainer and gave up silently.
  - It now applies as soon as Planned, and keeps checking for 5s.
  - It logs `CHAOS stage was planned for X but this stage is Y` if the stage key never matches.
  - Apply falls back to all scene sprites when levelContainer is null.
- **Enemies:** ChaosEnemies.ArmRegular no longer rolls, so every Knight/Rogue/Archer/Mage/Summoner/Lancer turns Chaos. The ChaosStages.EnemyChance config is now unused.
- **Attacks:** ChaosStageAttackPatch (SetAttackInfo postfix, Chaos stage only, not during a corrupted Council fight) chaosifies and tints enemy attacks that carry an element.
  - It uses CouncilCorruption.TintAttack(atk, stage: true). RestoreTints is now internal, and Revert calls it.
- **Ctrl+F11:** finds an inactive NextLevelLoader via Resources.FindObjectsOfTypeAll. Otherwise it loads directly: the boss floor after the tier's last stage, else the tier scene again.

## v0.5.13: Ctrl+F11 skips to the next stage (built, not yet run in game)
- Debug.SkipToNextStage finds the floor's NextLevelLoader (the exit portal) and invokes LoadNextLevel. That picks the next regular stage, the Council floor after a tier's last stage, or the next tier, then calls GameController.LoadLevel.
- It does nothing (logs `SKIP: no exit`) on a floor without a NextLevelLoader.

## v0.5.12: Council board tint fixed (built, not yet run in game)
- The lavender Air tier (squares, icons, ring, portrait) was ours. ChaosBoardPatch tinted every Image under a corrupted Council circle, and on the wrong tier: the Earth tier had the Chaos stage, but the Air circle got the tint.
- **Fix:** Council circles are counted in board order, skipping start, final and null entries, using the same tier numbering as the squares. Only "Portrait" images are tinted (fallback: non-circle, non-dimmer images that aren't inside a stage square).
- Logs `CHAOS board plan: squares ..C... | council <circle>=corrupted(<images>)`.
- GameController.tierCount is 0-based during stages (NextLevelLoader: tier 3 after a boss → FinalBossLevel), so CouncilRoomStartPatch's IsCorrupted(tierCount) was already right.

## v0.5.11: rival room props, spacing, arrival spots; darker Chaos squares (built, not yet run in game)
- v0.5.10 in game: the rooms work, the portals spread partly, and the rival fight works.
- **Props:** rooms keep their props and run the breakable pass (randomInteractables = true; Room.Generate → Decorator.DecorateByTileSet). Clearing interactables had left black holes on obstacle tiles.
  - After Generate, any new scene-root objects are adopted under the room before it moves.
  - Chests, portals, enemies and shop/NPC/pedestal/relic components are removed.
  - Logged as `RIVAL room ...: N breakables, ...`.
- **Spot checks:**
  - The mask is limited to Wall/Pit/Obstacle/Destructible/Environment.
  - Blocked uses a real LinecastAll that ignores colliders containing the start point.
  - Clearance() measures distance to the nearest wall. Roomy() picks the most open reachable floor within 6 units, inside the room's mesh bounds minus 2.
- **Placement:**
  - Roomy() picks the selector/arena arrival points and the stage entrance, after 2 FixedUpdates.
  - Portal offsets are ±5, with 4 units minimum spacing and clearance ≥ 1; the leave portal is at (0,-5).
- **Board:** Chaos squares are almost black purple (0.17,0.06,0.26). The Air tier's own squares are lavender, which the user mistook for Chaos.

## v0.5.10: open-spot mask from the player's collision matrix (built, not yet run in game)
- The v0.5.9 log showed every linecast hitting "LowerWalls": the arrival checkpoint sits inside one, so that layer is walkable.
- WallMask is now built from Physics2D.GetIgnoreLayerCollision for the layers of the player's solid colliders, minus the player and Enemy layers. It is logged as `RIVAL open-spot check blocks on layers: ...`.
- The path check walks the line with OverlapPointAll from 1 unit out, since a linecast starting inside a collider counts it.
- Run() moves any player still more than 40 units from the arena into it. A portal spawned on the player registered the choice without teleporting them.

## v0.5.9: open-spot check uses wall layers only (built, not yet run in game)
- v0.5.8 confirmed in game: the rooms render in place, with matching collision.
- But RivalEvent.Blocked counted every solid room collider and rejected every spot. All portals stacked on the arrival point, and the rival fell back into a wall.
- Now only colliders on the "Wall"/"LowerWall" layers (WallGenerator) and BottomlessPit-tagged ones count, and linecasts use that mask.
- On failure it logs `RIVAL no open spot ... (last block: ...)`. The rival's checkpoint fallback also skips blocked spots.

## v0.5.8: rival rooms generated at the origin, then moved (built, not yet run in game)
- v0.5.7 rendered, but the art sat near (0,0) over the real stage while the room's own colliders were far off-map.
  The game's generators create parts with `new GameObject()` at the world origin and `SetParent(room)` keeping that
  spot, and `MeshGenerator.GenerateMeshes` static-batches the meshes in place.
- RivalEvent.BuildRoom now generates with the room at (0,0,0) (`RivalEvent.Building` flag), then moves it to its spot.
- RivalRoomMeshPatch (GenerateMeshes prefix, only while Building) runs the same steps without StaticBatchingUtility.Combine, so the meshes follow the root.

## v0.5.7: real room instances (built, not yet run in game)
- **Root cause of the black rival rooms:** `Room.Load(name, instantiate, parent, pos)`: with `false` it returns the PREFAB ASSET's Room (`ChaosBundle.GetWithComponent`), only `true` does `ChaosInst` + SetParent + position. 0.5.4-0.5.6 passed false, so Generate ran on the prefab asset in memory: nothing in the scene rendered, SetActive/position "didn't stick" (0.5.6 log: root active False, meshes still at asset coordinates). Now `true`, plus a `scene.IsValid()` guard. Earlier builds polluted those prefab assets for that game session (generated children added to them): restart the game after updating.

## v0.5.6: rival rooms visible
- 0.5.5 diagnostics: both built rooms sat at **(0,0,0)** (the Room.Load position didn't stick) and every generated MeshRenderer was **inactive** ("OFF": Wall 720 verts FireTileSet, layer Default/0), so the selector/arena was a black void with working colliders. Now: `transform.position = at` before and again after `Generate`, then `Activate` sets every GameObject in the room active (except EnemiesTrans/InteractablesTrans) and enables MeshRenderers (sprites keep their own state). Diagnose also logs root active state, parent and how many meshes are on.

## v0.5.5: fixes from the 0.5.4 test
- **Giant boss bar had two names:** `MiniBoss.InitComponents` already creates and claims an `EnemyHealthBar` (`MiniBoss.healthBar`, name from `bossDisplayName`). `BossBar.Attach` now borrows it for MiniBoss (renames it, sets `bossDisplayName`, never shows/destroys it) instead of stacking a second bar.
- **Chaos stages / champion changed between stages:** `FinalRun`'s run key included `GameController.levelNameList`, which the game reshuffles during a run, so it re-rolled the seed mid-run. Key = `loopCount` only; a new run is still detected by passing through Hub/Tutorial.
- **Board marks off by one:** `stagesInOrder` starts with the Start circle (square layout log: `StartCircle[...root]`). `startCircle` is now skipped along with `bossesInOrder` and `finalBossCircle`.
- **Spawned outside the start room / blue portal off-centre:** the selector was built from a **Start** room prefab, which brings the start setup with it. Start/Exit rooms are never used now, and every `Portal` inside a built room is destroyed after Generate.
- **Rival rooms (sky + invisible walls, stacked portals):** 0.5.4 rooms had walls (colliders) but no visible floor, and only 4 checkpoints, so snapping put every portal on the same spot. Now: the arena prefers Survival/RandomSurvival, then Challenge rooms (the game's fight rooms), else the largest Standard; the selector uses the smallest Standard (>= 120 tiles). `MatchStageSorting` copies the stage floor meshes' sorting layer/order onto ours (fix attempt) and `Diagnose` logs mesh count/bounds/verts/material/sorting layer, colliders and checkpoints per room (`RIVAL room ...`). Portals and the rival use `OpenSpot`: arrival + offset, rotated/shortened until no BottomlessPit-tagged or room collider is within 0.7 and no linecast hit from the arrival, one portal per 2 units; placed 2 FixedUpdates after building so colliders are registered.

## v0.5.4: own rival portal + rooms, board marks that stick
- **Rival event no longer uses the misc room** (user request). `RivalEvent` (RivalEvent.cs): at the end of stage generation (`LevelGenPatch` adds a one-shot handler to the static `LevelGenerator.levelGeneratedEventHandlers`, invoked at the end of `Generate` after SetStartPosition/SetExitAndStorePositions), with `RivalWizards.Chance`, a Portal opens on a random stage `Checkpoint` 15-70 units from `Level.startPosition` (any checkpoint >= 6 if none), with an "A rival's portal has opened" card. It leads to a **selector room** (3 tier portals tagged `RivalPortal.Event` + a white portal back) and then the **arena**; 3s after the rival dies a white portal leads back next to the entrance. The entrance and selector portals are destroyed once a tier is picked (one fight per portal). F11 = the same event beside the player (element cycle kept).
- **Rooms:** `Level.LoadedRoomDict` (name -> Room template of this stage's tileset). `Room.Load(name, false, null, pos)` instantiates `Room.prefabPath + name + ".prefab"` from the bundle; `Room.Generate()` builds the room's own floor ("Meshes", then StaticBatchingUtility.Combine), walls ("Colliders"), pits, decorations and checkpoints under its own transform (`Globals.FindOrCreateGameObject(name, parent)` = parent.Find, so nothing global is touched; `CheckpointGenerator.DestroyOldCheckpoints` is local too). Arena = largest Standard room (>= 120 tiles), selector = a Start room, else the smallest Standard. Before Generate: randomEnemies/randomInteractables/randomPits off, generateCheckpoints on, enemy/factory/interactable lists cleared, EnemiesTrans/InteractablesTrans children destroyed. Placed at start + (0,-420) and (0,-780). The camera isn't clamped to the level (only `Level.AvgTileGridPosition` reads min/max). Checkpoints = safe floor spots: arrival = checkpoint nearest their centroid; portals and rival snapped to checkpoints.
- The misc-room hook (`TryReplaceMiscRoom`, `RivalArena`) and the Ctrl+F11 `ArenaLab` experiment are removed.
- **Board marks that stick:** the board recolours reached squares after Enable, so `BoardMarks` (component on the board) re-applies marks every LateUpdate (keeping the board's alpha) and restores originals on the next Enable. A Chaos square = root Image dark purple (0.32,0.12,0.5) + its element icon swapped for `finalBossCircle/ChaosIcon`'s sprite, tinted (0.85,0.6,1). Corrupted Council portrait images lerped 0.6 toward purple. `ChaosBoardPatch` has `[HarmonyPriority(Priority.First)]` (it clears the marks; the champion patch adds Sura's portrait mark after it). Logs the square layout once.
- **Empty final circle on champion runs:** the game keeps Sura's `BossPortrait` disabled (the circle shows the Chaos symbol). Champion runs now enable it (kept enabled through a `show` mark) and tint it in the element colour.

## v0.5.3: Chaos stage fix, Sura board tint, friendlier voicelines, arena experiment
- **Chaos stages never triggered naturally (bug since 0.4.2):** `GameController.stagesPerTier` = 2 regular stages (the boss floor is its own "<X>BossLevel" scene, loaded when `stageCount >= stagesPerTier`), and `stageCount` is 1-based during a stage (`LoadLevel` adds 1 when a tier scene loads). `IsChaos` rejected `stage >= per - 1`, i.e. every in-game stage. Now canonical = 0-based regular index: `IsChaos(tier, idx)` valid for 0 <= idx < stagesPerTier; in game idx = stageCount - 1 (no Chaos on BossLevel scenes / boss rooms). The board walks `stagesInOrder` skipping `bossesInOrder` + `finalBossCircle`: k-th square = tier k / stagesPerTier, idx k % stagesPerTier (`curMaxStagesPerTier` is 3 because it counts the boss). Council markers go on `bossesInOrder[t]`. The user's screenshot showed every tier's 2nd square purple: the old Council marker (i % per == per - 1 with forcing stuck on).
- **Ctrl+F12 = one fight only** (`ForceOnce`, consumed by the next Council room); Shift+F12 stays the lasting toggle.
- **Board icon for champions:** no more overlay arcana icon. Sura's `BossPortrait` multiplied toward the element colour (lerp 0.7), `BossCircleInner` element darkened, the ring multiplied, `ChaosIcon` kept only for the Chaos Sovereign. Originals cached per Image and restored on Sura runs.
- **Voicelines:** rewritten in the game's upbeat mentor tone (each member dabbled in Chaos and wants to drop their element; when beaten they come back to it). No glitching any more (`Corrupt()` unused; whispers dropped from dialog).
- **Arena experiment (Ctrl+F11, `ArenaLab`):** copies every renderer/collider within 14 units of `Level.miscRoomPortalInPosition` (skipping bodies/UI/objects bigger than 60 units) to 400 units below, opens a Portal beside the player to it (`createReturnPortal`), logs scene roots, the level container's children, what was copied and what was skipped as whole-level. Purpose: decide whether a dedicated rival portal can lead to our own copied arena room in the same scene (stage progress untouched) instead of borrowing the misc room.

## v0.5.2: Corrupted Council polish
- **Dialog fix:** the 0.5.0/0.5.1 glitching never showed. `DialogManager.GhostWrite` types `DialogMessage.message` directly (ParseMessage's result isn't what's typed), and the first Council line has both portraits active (`left FireBoss (True), right Player0 (True)`), so the one-speaker check skipped it. Game data (`core` asset, dialog JSON): Council dialogs `<Boss>-Intro` / `<Boss>-Win` are 2 messages, all spoken by the member (left), player portrait silent; later messages inherit speakers. Now `CouncilDialogPatch` (Activate prefix) rewrites `__0.message` with a custom line (`CouncilCorruption.Lines[FireBoss|IceBoss|EarthBoss|AirBoss|LightningBoss][intro=0|win=1][index]`, index from `dialogDict[id].messages`), lightly glitched; originals kept in `rewritten` and restored in `End()`. Postfix tints the left portrait and the text. The ParseMessage patch is gone.
- **Echo aim fix:** Chaos Beam (AtTarget) landed beside the Flame Empress: bosses' `closestTargetInfo` can be empty, and `ArcanaCaster.Cast` fell back to `origin + dir*4`. `ArcanaCaster.TargetOf(caster)` = target info if its `hurtBoxTransform` is set, else the nearest living player; used by Cast, Chain re-aim, the finisher's late re-aim and the echoes.
- **Room tint:** `ChaosStages.Apply(visualOnly: true)` 1.6s into the corrupted room (after ChaosStageRunner's scene-change revert); no rewards/arming; boss floors with no "Meshes" container take every non-body mesh, and every scene sprite (not Entity/Canvas) as decoration when there's no `Level.levelContainer`. Reverted in `End()`.
- **Darker Chaos tint:** attacks multiply (0.46,0.2,0.72); particles/trails/lines remapped by brightness onto a ramp from near-black (0.16,0.03,0.28) to violet (0.72,0.32,1) (`ChaosRamp`); lights (0.45,0.15,0.8); boss body (0.62,0.42,0.85). Effects without an Attack: baseline of all renderers/particles when the boss is found, then every 0.2s anything new that isn't UI, an Entity body, or under a root carrying a player Attack gets `TintTree`. All originals recorded and restored in `End()` (pooled effects outlive the room). PoolManager.GetPoolItem is generic, so it isn't patched.

## v0.5.1: Corrupted Council gameplay + jump key
- **Reward = a Chaos arcana.** The Council chest: `BossRoomEventHandler.ActivateExitPortal` instantiates `TreasureChest` with `lootTableName = BossRoomEventHandler.bossTreasureChestID` (static). `TreasureChest.Break` -> `LootManager.DropLoot(table, pos)` -> `DropSkill(pos, amount, skillID, lockedRate, sigRate, elementSet, bool, bool)` (an explicit skillID is used as is). `CouncilChestPatch` (Break prefix/finalizer) sets `CouncilChestOpening` for the corrupted room's chest; `CouncilChaosArcanaDropPatch` (DropSkill prefix) swaps `__2` for a `LootManager.chaosSkillList` entry not in the private `currentPlayerSkills` (amount at least 1), once.
- **Chaos damage:** `Chaosify(AttackInfo)` on every non-player attack in the fight (SetAttackInfo postfix + the 0.2s scan): `elementType = Chaos`; the strongest of burn/slow/poison/shock/freeze chance becomes `chaosChance` (level kept, min 1), the elemental ones zeroed.
- **Chaos echoes** (instead of replacing a move: boss patterns are `Boss` states in `attackPatternList`, driven by `Boss.TransitionToNextState`): 2 Chaos-element arcana (no melee/dash/summons/wards) picked when the boss is found. `CouncilEchoPatch` (TransitionToNextState postfix): 45% per attack end, 5s cooldown (first after 6s), outline flash white -> purple over 0.6s, then `ArcanaCaster.CastAll(boss, def, aim)` (can roll enhanced). Intro card once `bossSpawned` and the dialog is closed: "Corrupted <name>", echo icons, "Chaos echoes · Chaos damage · drops a Chaos arcana".
- **Ctrl+F12** `JumpToCouncil`: on a regular stage of a run, `Forced = true`, `stageCount = stagesPerTier`, `GameController.LoadLevel(bossLevelNameDict[Application.loadedLevelName])` (the scene NextLevelLoader loads after a tier). Shift+F12 turns forcing off again.

## v0.5.0: Corrupted Council
- Rule: a tier's Council member is corrupted when any regular stage of that tier is Chaos (`CouncilCorruption.IsCorrupted(tier)` over `ChaosRun.IsChaos`, same seed, so board and fight agree). Config `ChaosStages.CorruptedCouncil`. Test: Shift+F12 toggles `Forced` (every Council fight; applies to the current room too; all boss circles purple on the board while on).
- Council fights = `BossRoomEventHandler` (fields boss, bossIntroDialogID, bossVictoryDialogID, playBossDialog...; `BossIntro` coroutine instantiates `bossPrefab`, `DialogManager.HandleDialog(bossIntroDialogID)`, `Boss.ExitIntro`; `BossOutro` -> victory dialog). **`FinalBossRoomEventHandler` derives from it**: the Start postfix skips it.
- `CouncilRoomStartPatch` (Start postfix) -> `Begin` + `CouncilCorruptionRunner`: polls `boss`, then `CorruptBoss` (SpriteRenderer colour x (0.78,0.55,1), purple `_OutlineColor`, boss bar `SetName("Corrupted " + BossDisplayName)`), and every 0.2s tints all active non-player `Attack`s (pooled boss attacks get their info before we see them). `CouncilAttackTintPatch` (SetAttackInfo postfix) tints new ones at once. `TintAttack`: visual root = parent Projectile, else the attack's parent if it's not an Entity and has <= 6 children, else the attack; sprites x purple, ParticleSystem `main.startColor`, Lights, Trail/LineRenderers; once per root.
- Dialog: `DialogManager.ParseMessage` postfix corrupts the text when `currentDialogID` is the room's intro/victory ID and the active speaker isn't a player (`*Player*` speakerID; logs the speaker IDs once): plain-text glitches (`#%&*`, stutters), tags left intact, 50% a whisper suffix, seeded by the line. `Activate` postfix: boss portrait tinted, message text lerped to purple while the boss speaks (restored otherwise). No custom rich-text tags (lines are ghost-written char by char).
- `ChaosBoardPatch` also tints the boss circle (`i % per == per - 1`) of corrupted tiers.
- Visual/dialog only; moves and stats unchanged.

## v0.4.13: rival rewards, arena Archons, stun-safe watchdog
- **Rewards (user spec):** Easy 1 standard chest, Medium 2 standard, Hard or arena Archon 1 gold, the champion replacing Sura none. `e.dropLootChest = false` (the game's drop is always gold `TreasureChestMiniBoss`); `RivalRewards` spawns `Globals.ChaosInst<TreasureChest>(TreasureChest.Prefab, null, pos, null)` 1s after death, side by side via `Entity.GetAvailableNearbyLocation`, `preventMimic = true`. Standard = the prefab's own loot table; gold = `lootTableName = "TreasureChestMiniBoss"` (`TreasureChest.SetChestType`: "MiniBoss" -> MiniBoss, "Boss" -> Boss, "TreasureChestSmall" -> Mini, "TreasureChestElemental" -> Elemental, else Standard).
- **Arena Archons:** `RivalArena.Run`, Hard portal: `RivalWizards.ArchonChance` (0.25) -> `Rivals.Spawn(..., RivalTier.Final, arenaArchon: true)` ("Fire Archon" / "Chaos Sovereign" on Chaos stages), full champion kit incl. phase 2; `mk.ArenaArchon` -> gold chest.
- **Watchdog vs stun:** giant stun = "Hurt" (MiniBossHurtState: armour break, each combo hit re-enters Hurt and `HandleDurationIncrease` extends it, combo counter/achievement on exit), then "Ready" (MiniBossReadyState, 0.5-0.75s, `AllowHurtState`). Free list now also has Ready/Regen/Stealth, any name containing Hurt/Frozen, any state whose class derives from *HurtState; the timer resets while free. Recovery goes through "Ready" (the giant's own recovery), Chase only if there's none.

## v0.4.12: champion phase 2
- `ChampionPhase` (ChampionPhase.cs), attached at the end of `Rivals.Intro` when tier == Final and `FinalBoss.Phases`. Once, at `health.HealthPercentage <= 0.5`: enabled=false for 2.2s, invulnerable, ShakeCamera(2), `OverdriveEffects` burst (temporary instance, destroyed after 3s).
  - Archon: second element = random other prefix. Keeps its 2 highest-priority arcana; the rest are removed from `availableEnemySkillsDict` (FSM keeps the states); +3 `PickVaried` of the second element's pool via `EnemyArcana.Give` (tier cooldown/windup). `mk.SecondPrefix` set (SignatureAllowed then also allows that element's own Contestant spells; Blizzard stays removed since MakeHostile already dropped it). `mk.GapMultiplier` 0.85 (used in `Rivals.MarkCast`). Title "Fire–Water Archon".
  - Sovereign: +2 arcana from any element, gap x0.7, title "Chaos Sovereign, Unbound".
  - New signature arcana among the gained ones if the old one was dropped (`SignatureMeter.SkillID` updated, hits kept). BossBar renamed, IntroUI "... awakens" card with the gained arcana. Log `FINAL phase 2: ...`.

## v0.4.11: giant intro icons + giant watchdog
- "?" icons: `IconManager.GetSkillIcon` returns a placeholder for unknown IDs (borrowed enemy skills like EarthKnightPoisonBolas have none), so the intro's null-icon tint fallback never kicked in. `IntroUI.SkillIcon` now compares against the placeholder (`GetSkillIcon("HybridBossesNoSuchSkill")`) and returns null. `IntroUI.EnemySkillIcon`: hand map `EnemyIconMap` (FlameShield->FlameShieldDash, IceBeam->UseAquaBeam, DrillCharge->UseEarthDrill, ...), else the shortest catalog SkillID ending with the skill's core name (PoisonBolas->UsePoisonBolas, FireWall->SummonFireWall...), else an element-coloured tile.
- Crash (giant) Knight stuck in its charge on an Earth giant, cause unknown. Suspects: borrowed `EarthKnightExplosionShieldState` (plays "ChargeAlt" then "Charge", sets invulnerable + floorCollider trigger + ignore knockback until OnExit; IsReady needs HP < 62.5%), or the giant's own Ready -> Dash charge. `GiantWatchdog` (all giants): same non-free state > 6s -> EndMovement + ChangeState("Chase") (runs OnExit), logs `GIANT <class> stuck in '<state>'`. Use that log line to find the real cause.

## v0.4.10: giant skill gaps filled
- `ElementalGiants.Apply`: when a giant isn't an arcana giant and installed fewer than `ElementalGiants.MinElementMoves` (2) borrowed SkillDefs, `GapArcana(kind, prefix, missing)` gives player arcana of its element. Families by style (`GapFamilies`): Knight Spell/Jump/Ward, Archer Projectile/Spell, Mage Projectile/Spell, Rogue Projectile/Jump/Spell, Lancer Spell/Projectile; one per family first, then any non-melee/non-summon arcana of the element. Uses the giant cast gap (`EnemyArcana.CastGap`) and enhanced rolls; no dash arcana, no signature.
- Counts from the Skills table: 0 borrowed = Wind/Thunder Knight, Fire/Thunder Archer; 1 = Fire Mage, Water Knight/Mage/Archer, Wind Archer, Lancers have 2 (Fire Lancer 0). The log line `GIANT ... skills added N, arcana M` shows the top-up.

## v0.4.9: boss health bars
- The game's boss bar is `EnemyHealthBar` (`Assets/Prefabs/UI/EnemyHealthBar.prefab`, static `Prefab`). `Boss.InitComponents`: `Globals.ChaosInst<EnemyHealthBar>(Prefab, null, null, null)` (no parent: the prefab has its own canvas), `Claim(this)`, `SetActive(false)`, `SetName(bossDisplayName)`; `BossIntroState.OnEnter` -> `FadeIn()`. It updates health/shield itself; its `Release` is private.
- `BossBar` (BossBar.cs) does the same for our enemies: `Attach(e, title)` hidden at the start of `Rivals.Intro` (rivals + champions), `Show()` when the intro ends; `ShowFor` for giants when `GiantIntro` shows. One shown at a time (newest wins), destroyed 1.5s after death. Fallback parent = first root screen-space Canvas if the prefab has none (logs "BOSSBAR parented to canvas ..."). Config `BossBar.Enabled`, `BossBar.Giants` (both true).

## v0.4.8: player-style signature, axe/water-arc fixes
- **Signature rework (user rule: only a signature arcana can spend it, and it fires that arcana's different signature version).** From `Player.HandleOverdrive` IL: meter full -> overdriveReady (OverdriveEffects aura `ToggleAura(true)` + `Burst(10,true,false,true)` = "PlayerOverdrive" sounds, outline, the signature skill's `cooldownRef.Reset()`), decays and expires ("PlayerOverdriveExpire"). Casting the signature skill while ready: `isUltimate = true`, `TimeScaleController.StandardFreezeIntoEaseOutTimeScale()`, invulnerability, `Burst(40,false,true,true)`, ShakeCamera(1). Signature-capable arcana = states whose ctor sets `hasSignatureVariant` (list in `Signature.SignatureStates`, from `tools/fieldwriters.ps1 "Player+SkillState" hasSignatureVariant`; matched to catalog rows by `GenArcana.ClassName`).
  - Rivals: `RivalMarker.Signature` = highest-priority capable arcana in its loadout, else the last arcana is swapped for a capable one of its pool. Intro shows "(signature)". `SignatureMeter`: hits -> Ready (aura via `OverdriveEffects.Prefab` + `Initialize(color*1.6, transform, sprite material)`), Ready for `TierSignatureSeconds` (now 8/10/12/14) then expires; while Ready it forces the signature ArcanaState out of Chase/Idle every 0.5s if `IsReady()` (gap bypassed in `ArcanaState.IsReady`). `Signature.Consume` in `ArcanaState.ExecuteSkill` fires the cue and returns true -> `CastAll(..., signature: true)`.
  - The old timed "everything empowered for N seconds" window (the random glow the user saw) is gone, and so is the cast-gap halving.
  - Enhanced vs signature split: `ArcanaCaster.SignatureCast` (static, set in SafeCast) -> `SetAttackInfo(..., isUltimate: SignatureCast)` and `isUltimate` field only on signature casts; `empowered` (enhanced: isEmpowered, extra shots, +30% dmg) can now roll for rivals too (`EnhancedArcanaChance`). Log tags ", SIGNATURE" vs ", enhanced". Finisher int args use the signature level (+1).
- Earth Axe basic floated: `EarthAxeBasic.InitThrow(Vector2, bool)` (spins out, starts `StartTimer`) added to the finisher. `EnsureLifetime`: any `MeleeAttackObject` with neither `useAnimTime` nor `useLifeTime` after launch gets a life timer (lifeTime or 1.2s).
- Water Arc NRE: `Projectile.CreateProjectile` needs the Projectile on the prefab root; WaterArc keeps it on a child -> instantiate the prefab and wire the child. Finisher `Defaults`: WaterArcProjectile arcRange 5 / flyTime 0.25 (from `CreateWaterArc`). `InitPrefabFields` now also fills parentTrans/followTrans/orbitTrans (only if empty), targetVector/atkVector/attackVec/attackVector, spawnPos.

## v0.4.7: arcana finisher
- User: a Chaos Sovereign's Hammer Throw left the hammer floating. We never run the player's skill state, only spawn its object, and many states keep driving that object: `Player+UseHammerThrow` creates the `EarthHammer`, spins it, then `ThrowHammer` -> `InitThrow(isUltimate ? 5 : 4)` (sets `updateVelocity`, starts the hit bursts) + FireProjectile. Without that it never moves.
- `tools/driven.ps1` lists, for every `Player+*` state, the calls/field writes on its prefab classes. `ArcanaFinisher.Finish` (called at the end of `ArcanaCaster.Cast`) runs a table of launch steps by prefab class (Init/Initialize/InitStrike/InitBounce/InitJavelin/Activate..., args filled by type: int level, bool empowered, Vector2 dir, Transform/GameObject/Entity caster). "Late" steps (InitThrow, ThrowAxe, ReleaseBolas, FrostWing Release, EarthCascade.SetReleaseVars, AquaBlitzBall.Activate) run 1 frame + 0.1s later, re-aimed at the target, then `updateVelocity = true` + `RefreshFlightTime`. Charge shots (any `released` bool + currentCharge/maxCharge) are released at full charge, again after Start. Runs inside OwnerContext. Logs `ARCANA finisher <Class.Method>: ran/failed/not found` once each (DebugKeys).
- `EnemyArcana.Set` uses a quiet field lookup (`ArcanaFinisher.Field`); AccessTools.Field logged a warning per missing field per cast.

## v0.4.6: rival boomerang + walking in place
- The "cyclone" every rival had (user: "Cyclone_Boomerang") is the Contestant's own `SwordThrowState` (`Assets/Prefabs/Projectiles/SwordThrow.prefab`, a spinning boomerang). `SignatureAllowed` treated it as neutral; now SwordThrow = Wind (like Tornado), unknown own spells = false, Chaos keeps all.
- `RivalAttackRedirectPatch`: an off-theme own spell (Fireball/Tornado/SwordThrow) is rewritten to "Chase" (`ref string __0`) instead of cancelled, so a finished state is never left running; skipped only when already in Chase.
- Walking in place, root cause from `ContestantChaseState.Update` IL: `MoveToTarget` returns true in attack radius -> ChangeState("Attack"); we skip it while the basic cools down, so the rival stood in Chase with Run looping (Chase replays Run only on facing change). `RivalUnstuck.FixAnimation`: in Chase, still > 0.15s -> play "Idle"+facing; moves again -> "Run"+facing.
- Also: `ArcanaState.PlayCastAnimation` ("Cast"/"Attack"+facing, logs `ARCANA <class> <id> animation: ...` once with DebugKeys), basic cooldown 0.5.
- Contestant FSM states (InitFSM): Idle, GoBack, Chase, Attack (ContestantWindSlashBasic), Hurt, Dead, Fall, Frozen, Dash, Tornado, SwordThrow, Fireball, FrostMageSummonBlizzard.

## v0.4.5: rival fixes + chaos enemy chance
- Chaos: Lancer added to `ChaosEnemies.ChaosTypes`; per-enemy roll `ChaosStages.EnemyChance` (0.5), rolled once (ChaosArmed added either way).
- Rival "walking in place": `Enemy+AttackState.OnEnter` does `attack.SetAttackInfo(skillCategory, attackSkillID="ContestantWindSlashBasic")`; with our `skillCategory = globalEnemySkillCategory` that data doesn't exist, so the basic swung for nothing in a loop. Now each rival gets `mk.Basic` (a Melee-family arcana of its element, `ArcanaDef.Basic`: windup 0.3, cd 0.9, range 0-4, registered with auto=false, exempt from the cast gap). `RivalAttackRedirectPatch` (prefix on `FSM.ChangeState(string,bool)`): for a rival FSM (`RivalBasics` map), "Attack" -> its basic state (looked up in `FSM.states`) if ready, else skip; the built-in basic never runs. Melee arcana are removed from the regular loadout. `RivalUnstuck`: in Chase, moved < 0.2 for 1s, player > 3.5 away -> `CalculateDashVector(player)` + ChangeState("Dash"), 2.5s cooldown.
- Signature rework (`RivalKit.cs`): `SignatureMeter` activates when full -> outline, `CameraController.ShakeCamera(1.5, true)`, DelayCasts(0), a window of `TierSignatureSeconds` (4/5/6/7) where `Signature.Active` = every arcana empowered and `MarkCast` gap ×0.5. It forces its highest-priority non-basic, non-dash ArcanaState via `Transition(false)` once it's in Chase/Idle. OnGUI: draining bar + "SIGNATURE!" burst. `Signature.Consume/Ready` were removed.
- Audit logs: `RIVAL kit (everything it may cast): ...` after MakeHostile; `RIVAL own spell: <state>` for the Contestant's Fireball/Tornado/SwordThrow.

## v0.4.4: chaos versions of the elemental enemy types
- User: Knight, Rogue (thief), Archer, Mage (wizard), Summoner have elemental versions on other stages, so on Chaos stages they get 1 chaos arcana + their purple palette row. `ChaosEnemies.IsChaosType` walks the class chain (covers FlameKnight etc.); not MiniBoss or rivals. `ChaosRegularEnemyPatch` (Enemy.Start postfix) arms one frame later via coroutine (`ArmRegular`: PurpleRow / tint, 1 arcana at ×1.6 cooldown). Lancers were not asked for.

## v0.4.3: chaos stage corrections (user dev notes)
- Regular enemies on Chaos stages get NO chaos arcana. Giants (MiniBoss) become Chaos giants: `ElementalGiants.TryMakeElemental` -> `ChaosGiants.Apply` when `ChaosStages.Planned` (no stage element, no SkillDefs, no summon swap). element = ElementType.Chaos, 2 arcana from `ChaosEnemies.Pool()` (PoolExact "Chaos", minus Minion/Seeker/Ward), GiantIntro with prefix "Chaos".
- Purple palette: `ChaosGiants.PurpleRow` reads the material's `_Palette` texture (RenderTexture readback), one row per `_PaletteCount` (rows along Y if height == count, else columns). Row order (direct or flipped) is chosen so the Water row is bluish and the Fire row reddish. Picks the most violet (hue ~0.78) non-element row; score < 0.25 -> tint fallback. Cached per texture. Config `ChaosStages.GiantPalette` (-1 auto) overrides.
- The Enemy.Start chaos hook was removed: it ran inside MiniBoss.Start before the palette-3 reset. Late giants go through GiantStartPatch. `ChaosStages.Apply` still calls `ChaosEnemies.ArmAll` (giants only) for the Shift+F11 toggle.
- ROADMAP.md was tidied by the user's request; their dev notes are tagged 💬 there (dedicated rival portal on X-2 stages, PvP arena, evil echo of the last run, Radiant/Prismatic stages, Arcanos safekeeping copy = ask the author).

## v0.4.2: CRASH FIX + chaos stages (built, NOT yet run in game)
- **Bug in 0.4.0/0.4.1 ("borked the game", Compass.LateUpdate NRE spam, WoL Arcanos patch failures):** `PatchAll` ran in Awake. Harmony makes Mono compile each original; Mono runs static constructors of classes the method touches. `FinalBossRoomEventHandler.Start` reads `LootManager.chaosSkillList`, so patching it ran `LootManager..cctor` before the asset bundles were loaded. That cctor builds items (`DamageUpAtLowHealth.get_Prefab` -> `ChaosBundle.Get`) -> NRE -> TypeInitializationException -> LootManager dead for the session (no loot, broken level setup, Compass NREs). Our Awake also aborted, so no "loaded" line.
- **Rule from now on: never patch in Awake.** `Plugin.PatchWhenGameReady` coroutine waits for `ChaosBundle.bundleLoaded` (+1 frame), then PatchAll, DiscoverModded, ownership patches and runners. ChaosBundle's own cctor is harmless (dictionary + fields). WoL Arcanos (installed, v0.57.0, 192 arcana, Spanish logs) defers its patches the same way.
- Chaos stages, scoped by the user: purple tiles/lights + enemies with Chaos arcana; the rival arena on that stage = Chaos rival; better rewards; "Chaos" title; board marker. Council corruption (tinted attacks, sprites, dialogue) is future work.
  - `ChaosRun.IsChaos(tier, stage)` = hash(FinalRun.Seed, tier, stage) < `ChaosStages.StageChance` (0.15, new key); never the last stage of a tier, Hub/Tutorial/PvP/boss rush/final room.
  - `ChaosStages.PlanCurrentStage` runs in the LevelGenerator.SetExitAndStorePositions postfix, before TryReplaceMiscRoom (StagePrefixOrRandom -> "Chaos"; MakeHostile uses `ArcanaCatalog.PoolExact("Chaos")` first). `Planned` is only valid for the stage key it was made for. The runner applies 1.5s after load.
  - Apply: atlas recolour (single "Chaos" variant), decoration tint, lights, `LootManager.globalGoldModifier` / `itemDropModifier` ×RewardMultiplier (mods HB_ChaosGold / HB_ChaosDrops, removed on Revert), `ChaosEnemies.ArmAll` + `Enemy.Start` postfix (1 Chaos arcana for regular enemies at ×1.6 cooldown, 2 for MiniBoss; skips Boss, rivals, critters).
  - `get_StageTitle` postfix -> "Chaos " + title. `ChaosBoardPatch` (GameProgressBoard.Enable postfix): stagesInOrder[i] with IsChaos(i / perTier, i % perTier) tinted purple (dimmers skipped).

## v0.4.1: loading-screen champion icon + chaos stage prototype (broken by the Awake patching bug; fixed in 0.4.2)
- User clarified: the placeholder icon belongs on the **loading screen**'s progress board (3 stage bosses, then Sura's Chaos icon), not in the room.
- `GameProgressBoard` (LoadingScreen.gameProgressBoard): per-element `*BossCircle`/`*Dimmer` fields plus `finalBossCircle` (RectTransform) and `finalBossDimmer`; `Enable()` -> InitializeTierOrders, SetPlayerColors, ToggleFade, SetPlayerPiece.
- `FinalRun` (FinalBossPool.cs): per-run decision. Reroll when in Hub/Tutorial (flag), or when the key `GameController.loopCount|levelNameList` changes. `FinalChampion.Roll` reads it. `ProgressBoardPatch` (postfix on Enable): tints the circle's own Image, disables its child Images (except the dimmer), adds child "HB_ChampionIcon" Image (sibling index at the dimmer). Logs the circle layout once (`FINAL board circle layout: ...`) for tuning. Config `FinalBoss.RoomBadge` (off).
- Chaos stages: `TileSet.material` = one atlas; `MeshGenerator.GenerateMeshes` builds the meshes under "Meshes", then `StaticBatchingUtility.Combine`. `ChaosStages.cs`: RenderTexture readback -> HSV remap (per-colour cache) -> cloned Material on every MeshRenderer under "Meshes"; SpriteRenderers under `Level.levelContainer` without an Entity parent get tinted; all Lights blended. `ChaosStageRunner` (DontDestroyOnLoad) resets on stage change and rolls `ChaosStages.Chance` (default 0) 1.5s after load. Shift+F11 cycles.
- Check: whether tile shaders sample `mainTexture` (if no visible change, find the shader's texture property), gamma vs linear on readback (washed-out colours = use the Linear readwrite), cost in ms, the board layout log.

## v0.4.0: final-stage boss pool (built, NOT yet run in game)
- User wants: champions sometimes instead of Sura, a placeholder icon showing "you won't face Sura", and the normal ending plus Chaos arcana unlock when the champion is beaten.
- Game flow: Sura is a scene object "FinalBoss"; `FinalBossRoomEventHandler.Start` sets `boss` = it. `Update`: when boss fsm "Dead" and not all players dead and !bossDefeated: if finalBoss.chaosMode && !secondFormStarted -> ChaosIntro; if superModeAvailable && superMode && !thirdFormStarted -> SuperIntro; else bossDefeated -> AnnounceVictory -> `BossOutro` (`LootManager.GetLockedChaosArcana` -> `Player.HandleSkillUnlock`, achievement) -> `ActivateExitPortal` (RunData timestamp, RunHistoryEntry.Save, DeathSummaryUI, LevelSummary "Ending"). `FinalBossDeadState.OnEnter` with chaosMode=true takes the final ("DeadHoodless") branch.
- `FinalBossPool.cs`: postfix on `FinalBossRoomEventHandler.Start` -> `FinalChampion.Roll` (config FinalBoss.Enabled/Chance 0.35/ChaosSovereignChance 0.3). Component polls room `bossSpawned` -> HideSura (enabled=false, invulnerable, renderers and colliders off, every EnemyHealthBar hidden) -> `Rivals.Spawn(pos, prefix, RivalTier.Final)` + `Rivals.Intro` (shared intro, refactored out of RivalArena). On champion down -> Sura chaosMode=true, superMode=false, room secondFormStarted=thirdFormStarted=true, `sura.fsm.ChangeState("Dead", false)`.
- `RivalTier.Final` (TierHP 2600+200*diff, 5 arcana, gap 1.1, no mini-boss chest). `FinalChampionBadge` OnGUI top-right placeholder.
- F12 debug. Encoding note: PowerShell `Get-Content -Raw` without `-Encoding utf8` mangles UTF-8 ("·" -> "Â·"); use the Edit tool or pass -Encoding utf8.
- Risks to check: Sura's lamps/orbs or BossIntro leftovers during the champion fight; whether her death state's time-freeze or "DeadHoodless" anim is visible (she's hidden); badge timing (room Start may run at floor load).

## v0.3.2: rival fall protection
- User: v0.3.x "working great". Asked for player-like fall protection on rivals.
- Player: `Fall.Update` keeps `preFallPos` (last ground position); `Player+PlayerFallState` on finishedFalling -> `TakeFallDamage` (AttackInfo "Pit", `Player.FallDamage` stat) -> after fallDelay 1s respawn at `Entity.GetAvailableNearbyLocation(preFallPos, false, false)`. `ContestantFallState.Update` calls base `FallState.Update`, then teleports to `myOrigin` and goes to Chase.
- `RivalFallPatch` (RivalKit.cs): postfix on `Entity+FallState.Update`; for rivals, once per fall: myOrigin = safe spot (restored next frame), HP -= max(player FallDamage, 5% max HP) (never below 1), 1s invulnerability.

## v0.3.1
- User rule: **signatures only for the player, rivals and the Council members** (the Council has its own signatures in its movesets; leave them alone). Everyone else can use *enhanced* arcana. Giants no longer get a SignatureMeter. `ArcanaState.ExecuteSkill`: with a meter -> `Signature.Consume`; without -> roll `EnemyArcana.EnhancedChance` (0.2) for an enhanced cast (same empowered path).

## v0.3.0: dash arcana, signature, summons, wards (built, NOT yet run in game)
- User asks: rivals dash and charge a signature from hits like the player (so dash arcana work), and summon arcana + wards must be in ("the Summoner already summons elemental agents").
- Game recipe found: `Summoner+SummonerMinionState.SummonEnemy` (ChaosInst minionPrefab, name += skillCategory, `parentTrans`, scale 1.25, `isEnemy = true`, `SetSkillInfo(cat, "<El>SummonerMinion", 1, false)`, useSummonDuration false, useBurst if diff>0, startingHealth 35) and `SummonerSeekerState` (SetTransforms(parent, parent, null), SetThrowPosition, isEnemy, `attack.SetAttackInfo(cat, "<El>SummonerSeeker")`, health 5). All 5 elements have both stat IDs. Ctors need `Summoner`, so the recipe is reimplemented in `Summons` (ArcanaCaster.cs). Summons run WITHOUT OwnerContext (that would invert their native enemy targeting).
- Catalog: `tools/gencatalog.ps1` now emits families Minion / Seeker / Ward and kinds Minion / Seeker / Ward (Excluded = buffs + heal only). `ArcanaDef.EnemyStatID` and `Castable` (summons need enemy stats). `ArcanaCatalog.Pool` excludes Dash; `DashPool` holds them.
- New files: `ArcanaCaster.cs` (`ArcanaCaster.CastAll/SafeCast/Cast`, `Summons`, `SummonLeash`, `ArcanaState` moved here), `RivalKit.cs` (`DashArcana` + patch on Contestant+DashState / Enemy+DashState OnEnter/OnExit; `Signature`/`SignatureMeter` with OnGUI bar and outline; `Attack.ExecuteAttack` postfix counts hits on Player colliders).
- Ward: Delivery.Ward = AtSelf + `MakeHostileObject` (Ally* names -> Enemy*, layers) + Ward.spawnPosition.
- Empowered: `SetAttackInfo(..., isUltimate: true)`, isEmpowered/isUltimate bool fields set on prefab MBs, shots up, scale 1.2, damage ×1.3, windup ×1.4.
- Config: `EnemyArcana.ExcludeFamilies` (new key, so existing cfg files don't block new families).
- Check in game: `RIVAL <el> pool: N arcana, dash <id> (of N), signature after N hits`, `ARCANA ... summoned ...`, `SIGNATURE ... ready/unleashed`, whether minions/seekers attack the player, ward hurtbox breakable, dash effects placement.

## v0.2.0: Enemy Arcana v2 (built, NOT yet run in game)
- User goal: rivals and giants get ALL thematically relevant player arcana (no limiting variety). No off-element arcana (no fire on a thunder rival), but keep a "jack of all trades" rival. Also pull in modded arcana.
- Files: `ArcanaCatalog.Generated.cs` (generated, 233 rows; regenerate with `tools/gencatalog.ps1 -OutFile ArcanaCatalog.Generated.cs`), `ArcanaCatalog.cs` (runtime build: element via `StatManager.GetSkillData("Player", id)` + `AttackInfo.GetInfoFromData(player...)` `.elementType`; family -> Delivery defaults; `EnemyArcana.Tuned` overrides; modded discovery + `IlScan`), `OwnerContext.cs` (dynamic Harmony prefix+finalizer on Awake/Start/OnEnable/Update/FixedUpdate/LateUpdate/OnTrigger*/OnCollision* and iterator MoveNext of every prefab class in the catalog, plus Projectile/Wave/ThrownItem; swaps `GameController.enemies` and `players` while owned code runs; tags spawned objects; `Attack.Awake` tagging; `SetAttackInfo(AttackInfo,int)` handling).
- `EnemyArcana.cs` rewritten: Delivery Projectile/Wave/AtTarget/AtSelf/Melee; Shots/Spread/ShotDelay; `InitPrefabFields` by name; each shot runs inside `OwnerContext.Enter(parent, true)`. The `Risky` list is gone (ownership replaces it). Config `EnemyArcana.Families`.
- Rivals: `ArcanaCatalog.Pool(prefix)` ("Chaos" = all elements) + `PickVaried` (spread over Delivery, or Element for Chaos). `SignatureAllowed`: Fireball only Flame, Tornado only Wind, Blizzard only Frost, Sword Throw all; Chaos keeps all (enforced in the HandleCooldowns postfix and Blizzard removal). Config `RivalWizards.ChaosChance` 0.15. F11 cycles 6 pools.
- Log lines to check: `ARCANA catalog: N castable arcana (Flame x, ...)`, `ARCANA ownership patches: N methods on M prefab classes (ms)`, `RIVAL <el> pool: N arcana`, `ARCANA ... cast <id> [element, family/delivery xN, attacks k]`, `disabled for this session`.
- Known risks: swapping enemies/players inside owned code may confuse arcana that summon allies; Melee basics spawned unparented may look odd (remove "Melee" from Families); Dash family spawns at self only (no dash movement); ownership patching cost at startup (logged ms).

## v0.1.2 (not tested; superseded by v0.2.0)
- User test of 0.1.1 (Thunder Medium): the rival "used wind cyclone and thunder dragon, hurt himself, never tried to kill me". Log: SummonLightningStorm `attacks 0` (spawns strikes later via its own skillCategory/skillID fields + validTargets = enemies). ShockDragon targets `GameController.enemies` via GetClosestTarget. Robe Vigor logged "HP -40%".
- Only one attack layer exists (`ChaosCollisions` "Attack"), so targeting is purely attack info (targetNames / IsAllyCheck / IsSkillOriginator), not physics.
- Scratch tool `risk.ps1` (IL scan per class: SPAWNS / SETINFO / TARGETS / PLAYERREF) produced `EnemyArcana.Risky` (19 skill IDs, off unless `EnemyArcana.IncludeRisky`).
- `ArcanaOwned` component on every spawned arcana root. The SetAttackInfo postfix re-owns "Player" calls under an ArcanaOwned parent. `Attack.CheckCollision` prefix: if the attack is ArcanaOwned, or atkInfo.entity is a rival, and the target collider's Entity is an Enemy -> false.
- `ContestantSpellStates`: prefix/postfix on Fireball/Tornado/SwordThrow `OnEnter` + `Update` set `Rivals.CurrentCaster` (saved and restored).
- Robe fix: `isIncrease` is private (FieldRef). Factor: |v|<=1 -> 1±v, <=3 -> v, else flat/500. Each robe mod is logged (`RIVAL robe mod ...`) for tuning. `ARCANA <type> targets: ...` is logged once per caster.

## v0.1.1 (tested: Blizzard gated, but self-hits; see v0.1.2)
- User test of 0.1.0: robes look great. Problems: arcana "static" (log: Wind rivals always rolled from the same 3; the Hard rival never cast any; Medium cast once) and an unannounced Blizzard at fight start. Relic pool logged only 8 stat relics.
- Cause: `Contestant.InitFSM` registers `BlizzardState` "FrostMageSummonBlizzard" (windup 0, cd 10, extra skill). `ContestantChaseState.Update` calls `HandleExtraSkills(true,false,false)` before its own spell logic, so Blizzard bypassed the gap and won on priority.
- Fix: `Enemy.HandleExtraSkills` prefix (rivals: skip while the gap runs) and postfix (on true: `Rivals.OnSkillUsed` -> MarkCast + reshuffle every skill's `priorityLevel`, the used one to 0). Blizzard is removed from `availableEnemySkillsDict` unless Frost. Intro shows "SummonBlizzard" for Frost.
- Catalog grew to ~40 (see EnemyArcana.Catalog). `EnemyArcana.Broken` blacklists any arcana whose Cast throws. Tier arcana counts 2/3/4.
- Chest: `EnemyDeadState.EnemyDropLoot` instantiates `TreasureChest.Prefab` and calls `LootManager.DropLoot(chestLootTableID...)` when `dropLootChest`. Contestant's chestLootTableID is already "TreasureChestMiniBoss", so MakeHostile sets `dropLootChest = true`.

## v0.1.0: pacing, intro reveal, robes and relics (tested: robes look great)
- User test of 0.0.9: rivals work (F11 arena, tiers, MakeHostile) but "insta-cast everything". Cause: Contestant ctor cooldowns (tornado 6, swordThrow 5, fireball 8, dash 1.2 with 4 charges), ready flags true at spawn, casts after AnimPlayed(0.2). Plus full player damage. The Thunder rival logged no ArcanaState casts at all (unknown why; check).
- Pacing: `Rivals.CanCast/MarkCast/DelayCasts` (per-enemy next-cast time). `ArcanaState.IsReady` also checks CanCast, and OnEnter does MarkCast. `Contestant.HandleCooldowns` postfix forces tornado/sword/fireball ready=false during the gap; OnEnter postfix on its 3 spell states does MarkCast. Tier tables in `Rivals` (TierCastGap, TierDamage, TierWindup, TierCooldown). Giants use config `EnemyArcana.CastGap` (2.5).
- `IntroUI.cs`: OnGUI title, subtitle and icons revealed left to right (0.55s apart, pop-in); returns its duration. Sprites drawn via `GUI.DrawTextureWithTexCoords` on `sprite.textureRect`. Lookups: `IconManager.GetSkillIcon/GetItemIcon/GetWizardSprite`, `TextManager.GetSkillName/GetItemName/GetOutfitName` (all fail soft).
- `RivalLoadout.cs`: `Outfit.OutfitList` -> random robe; `outfitColorIndex` -> `_PaletteIndex` on the rival sprite (Contestant.InitElement uses 24, i.e. the player sheet; unverified it's the same sheet). `OutfitModStat` (modType + add/multiModifier.modValue) -> `RivalBonus`. Relics: `LootManager.availableItemIDList` (private, via Traverse) -> `Item.CreateItem(id)` class name -> `RelicEffects` table (29 classes). Bonus: Hp/Damage/Cooldown/Speed multipliers, Armor/Evade via stat AddMod, Crit via ReOwn. Config `RivalWizards.Robes/Relics`.
- `GiantIntro.cs`: attached in `ElementalGiants.Apply`; shows the giant's installed SkillDefs and arcana within 9 units, once. Config `ElementalGiants.Intro`.
- Check in game: robe colour (pink = wrong sheet), relic pool size in log (`RIVAL relic pool: N`), icons null for enemy skills (they fall back to coloured squares), outfit stat magnitudes (log line `bonuses:`).

## v0.0.9: Rival wizards (tested by user: works, needed pacing)
- `RivalWizards.cs`. Postfix on `LevelGenerator.SetExitAndStorePositions` -> `Rivals.TryReplaceMiscRoom`: find the `RoomType.Misc` room via `Level.GridRoomTupleList` (Tuple `.Second` = Room component), hide `Interactables/*` except `Portal`, then `RivalArena.Create(Level.miscRoomPortalInPosition, stage prefix or random)`.
- RivalArena: 3 `Portal.Prefab` instances (fields set via Traverse: targetLocation, targetSpawnOffset, color, createReturnPortal) tagged with `RivalPortal`. A `Portal.OnTriggerEnter2D` prefix calls `Choose(tier)`. Coroutine: wait 1.2s, `Enemy.Spawn("Contestant", ...)` + `RivalMarker` (added before Start), wait 2 frames, `enabled=false`, invulnerable, camera override, OnGUI banner, wait IntroSeconds, enabled=true.
- `Contestant.Start` postfix -> `Rivals.MakeHostile`: rename AllyHurtBox/AllyFloorContact back, layers Player->Enemy and AllyFloorContact->EnemyFloorContact, `skillCategory = Enemy.globalEnemySkillCategory`, allies->enemies lists, `targetGroup = GameController.players`, HP via `NumVarStatMod("RivalHP", hp, 10, Override, true)` on `health.healthStat`, arcana via `EnemyArcana.Give(e, defs, cdMult)`.
- Contestant's own states use "Player" stats: a prefix/postfix on `Contestant.Update` sets `Rivals.CurrentCaster`; an `Attack.SetAttackInfo(string,string,int,bool)` postfix calls `EnemyArcana.ReOwn` while it's set. `ReOwn` is idempotent (the damage multiplier is applied once per AttackInfo). The target-list template falls back to `NeutralKnightAttack` in `globalEnemySkillCategory`.
- Config `RivalWizards.Enabled/Chance(0.3)/IntroSeconds(4)`. F11 opens an arena at the player.
- Risks to check: misc-room NPCs might spawn after generation (then still visible); portal colours applied in Awake before our fields are set; portal/rival offsets vs room size; rival palette; Contestant states that SetAttackInfo outside its Update (coroutines) won't be re-owned; whether the dodge logic still makes sense when hostile.

## v0.0.8: Enemy Arcana (built, NOT yet run in game)
- New file `EnemyArcana.cs`: `ArcanaDef` (element, player skillID = arcana `staticID`, prefab class with static `Prefab`, Delivery Projectile/AtTarget/AtSelf, windup/cd/range/prio) and `ArcanaState : Enemy.SkillState`. OnEnter: base + `movement.EndMovement` + `windupStopwatchID = ChaosStopwatch.Begin(windup,...)`. ExecuteSkill: base (queues exit, `exitTime` 0.4) + spawn. Projectiles go through `Projectile.CreateProjectile(parent, prefab, pos, rot, null)`, then set parentObject/parentEntity/moveVector; others use `Instantiate`. Every child `Attack` goes through `MakeEnemyAttack`.
- **Team fix:** `Attack.SetAttackInfo("Player", id)` gives real stats, but `GenerateAttackInfo` credits a "Player" category to `GameController.playerScripts[0]` and uses the player's `targetNames`. So we overwrite skillCategory, entity, gameObject and targetNames (from `StatManager.GetSkillData(enemy.skillCategory, enemy.attackSkillID)` + `AttackInfo.GetInfoFromData`). `IsAllyCheck` only skips when the category starts with "Player". `ExecuteAttack` uses atkInfo as-is (no regeneration).
- Arcana catalog dump: all `Player+*` skill states with staticID and prefab classes; regenerate with an IL scan of `get_Prefab` calls and `.cctor` strings. ~200 entries (including Basic/Dash variants).
- SuperCoffin: `GiantKind` "" (no borrowed enemy skills), `UsesArcana` and `NoElementPalettes`, so it keeps palette 3 and gets `Tint` (sr.color, _Color, _OutlineColor/_OutlineColor2). Config `EnemyArcana.DamageMultiplier` (1.0) and `EnemyArcana.AllGiants` (false).
- Things to check in game: projectiles hit the player and not enemies; AtTarget prefabs (BoulderDrop, Tornado, WaterBlast, LightningStrike) may need init that the player state normally does; some prefabs may follow the player or a cursor; tint/outline visibility; whether MiniBoss armor flashes clear the outline.

## v0.0.7 (coffin skills + summon swap)
- Coffin skills as above. `SwapSummonMinions`: the giant's "Summon" SkillState `enemyPrefabKey` is swapped to `<Prefix><Key>` if that prefab exists (SkeletonMage -> Mage). F10 rerolls add skills cumulatively (debug only).

## Current plugin (v0.0.6 base)
- Feature: Harmony postfix on `MiniBoss.Start` plus every giant's own `Start`; applies only from the most-derived one. On an elemental stage the giant always takes the stage element (`MatchStageElement`, default on). On neutral stages `NeutralStageChance` (0.5) picks a random element. Sets `element`, palette on all child palette-swap sprites, and adds that element's matching enemy skills (Knight->Knight set, Summoner->Mage set, Coffin colour only). Config in `BepInEx\config\osi.wol.hybridbosses.cfg`.
- Hotkeys: F5 grouped enemy list + giant skill lists, F6 cycle palette, F7 field dump, F8 spawn next Super* giant beside player, F9 dump element->palette, F10 cycle nearest giant through the five elements.

## Next steps
1. In a run: F8 to spawn each giant, F5 to see `GIANT ... -> element, palette, skills added N` and `GIANTSKILLS`, F9 for the palette table, F10 to try every element. Watch the log for `Failed to add` / exceptions while giants use borrowed skills (likely failure: missing animation names on the giant's animator, or a skill casting `ent` to its own class).
2. Fix whatever breaks; consider giving giants the element's `attackSkillID` damage element, an element-coloured aura/outline, and covering Wind/Thunder Knight (no transplantable skills yet) by borrowing Rogue/Lancer skills.
3. **Hybrid mini-boss/boss:** subclass `MiniBoss` or `Boss`, register states in `InitFSM` the way `FinalBoss` does, borrowing attack states from `FireBoss`/`IceBoss`/`EarthBoss`/`AirBoss`, with a phase list (element, states, palette) and an element particle/palette change as the telegraph. Read `FireBoss` (simplest) first.
4. Rival wizards and extra final bosses: after studying the PvP classes and `FinalBossRoomEventHandler`.

## Files
- `Plugin.cs`, `WolHybridBosses.csproj`: current plugin and build
- `reference/decompiled_FinalBoss_Boss_MiniBoss_EnemyFactory.cs.txt`: **missing from the folder** (never saved, as was the earlier v0.0.5 draft). Re-export from ILSpy if needed. Was: ILSpy output (FinalBoss ~lines 1-3200, Boss ~3210, MiniBoss ~4103, EnemyFactory ~4502). Strip CRLF before grepping.
- Decompiling: ILSpy -> expand `Assembly-CSharp` -> `-` namespace -> right-click class -> Save Code.
