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
