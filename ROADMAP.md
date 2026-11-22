# Hybrid Bosses: Roadmap

A Wizard of Legend mod (BepInEx + HarmonyX) that adds elemental mini-bosses, rival-wizard fights, chaos stages and new final bosses.
It mostly reuses the game's own assets. Custom art is possible, but so far every idea has been covered by clever asset reuse.

Status key: ✅ done · 🧪 built, needs in-game testing · 🔜 next · 📋 planned · ❓ needs research first · 💬 dev note (idea to consider)

Current version: **0.6.0**. Technical details (game internals, file layout, pitfalls) live in `CLAUDE.md`.

---

## 1. Elemental giants (mini-bosses) ✅ / 🧪

Giant enemies (the `Super*` mini-bosses: Knight, Mage, Rogue, Archer, Lancer, Summoner, Coffin) take the element of the stage they spawn on.

| Stage | Giant becomes |
|---|---|
| Forest, Earth | Earth giant |
| Ice | Water giant |
| Fire | Fire giant |
| Air | Wind giant |
| Lightning | Thunder giant |
| Castle / neutral stages | random element (config chance) or gray |

Built:
- ✅ Element palette on the body and extra parts (arms, bow, lance). Confirmed in game.
- ✅ Borrowed elemental enemy skills where they fit, and summons swapped to elemental minions (a Wind giant Knight summons Wind Knights).
- 🧪 Giant Coffin: no elemental palettes exist for it, so it gets an element tint and outline, and casts 3 player arcana of its element.
- 🧪 Intro reveal the first time you get close: its skills and arcana appear one by one, without freezing anything.
- 🧪 Enhanced arcana: giants that cast arcana have a 20% chance (`EnemyArcana.EnhancedChance`) per cast to use the enhanced version. **No signature meter: signatures are for the player, rivals and the Council only.**

Next:
- 🧪 **Skill gaps filled (v0.4.10):** every elemental giant now has at least 2 element moves (`ElementalGiants.MinElementMoves`). Giants with nothing or little to borrow (Wind/Thunder Knight, Fire/Thunder Archer and Fire Lancer have nothing; several have only 1) get player arcana of their element that suit their style: knights get close-range blasts and slams, archers and mages get projectiles.
- 📋 Element-coloured aura/outline and a burst before attacks; an elemental Coffin stomp.
- 📋 The other big enemies that aren't mini-bosses: giant Blob, Moving Statue, large Ghoul, large Mimic.

## 2. Rival wizards 🧪

A rival is the game's PvP wizard (`Contestant`), turned hostile and given player arcana.

Built:
- ✅ **Event (own portal, v0.5.4–0.5.11; user-tested: rooms and fight work):** on some stages (`RivalWizards.Chance` 0.3) a **rival portal** opens on open floor somewhere on the stage ("A rival's portal has opened"). It leads to a **selector room** with three portals spread around you: Easy (green), Medium (yellow), Hard (red), plus a white one back. After that comes the **arena** (one of the stage's Survival/Challenge rooms), and after the fight a portal back to where you came in. Both rooms are real rooms of the stage's own tileset with their props and breakables, built off-map in the same scene, so stage progress is untouched. The misc room keeps its NPC.
  - How the rooms are built: at the map origin and then moved, so their art and walls line up; you land on the most open floor; portals are at least 4 units apart and never on a wall or out a doorway.
  - 🧪 v0.5.11: breakables in both rooms, wider portal spacing, and no portals in walls or wall nooks (the stage portal too). Waiting on a test.
- 🧪 **Intro:** the rival is frozen and the camera moves to it. Its arcana, dash arcana, signature spells, robe and relic appear one by one, then the fight starts.
- 🧪 **Tiers:**

  | Tier | HP | Arcana | Spell gap | Damage | Signature after | Reward (v0.4.13) |
  |---|---|---|---|---|---|---|
  | Easy | 500 | 2 | 3s | 50% | 12 hits | 1 standard chest |
  | Medium | 900 | 3 | 2s | 75% | 9 hits | 2 standard chests |
  | Hard | 1400 | 4 | 1.3s | 100% | 6 hits | 1 gold chest |
  | Hard: Archon (25%) | 2600 | 5 + phase 2 | 1.1s | 100% | 6 hits | 1 gold chest |

  HP goes up with difficulty. Element: the stage's, or random on neutral stages. 15% chance of a **Chaos** rival (`RivalWizards.ChaosChance`) with arcana from every element.
- 🧪 **Archons in the arena (v0.4.13):** the Hard portal sometimes (`RivalWizards.ArchonChance` 0.25) holds an Archon instead: the full final-champion kit with its half-health hybrid phase (a Chaos Sovereign on Chaos stages). The champion that replaces Sura drops no chest, since the final room has its own rewards.
- ✅ **Kit (user-tested: "working great"):**
  - any player arcana of its element, picked for variety, with priorities reshuffled after each cast;
  - a dash arcana fired from its own dashes;
  - a signature meter that fills on hits (see the v0.4.8 rework below);
  - summons and wards;
  - its own Fireball / Tornado / Sword Throw / Blizzard only when they match its element.
- 🧪 **Fixes (v0.4.5), from testing:**
  - **Walking in place:** the built-in basic attack (a Wind Slash with no stats outside its ally role) swung for nothing over and over. Rivals now get an **elemental basic arcana** (shown in the intro as "(basic)") that replaces it, and an unstuck watchdog makes them dash at you if they "run" without moving.
  - **Only their kit:** the log lists everything a rival may cast (`RIVAL kit …`), and logs its built-in spells too (`RIVAL own spell …`). They match the intro: arcana, basic, dash, and the own spells that fit its element.
  - **Signature like the player's (reworked in v0.4.8):** each rival has one **signature arcana** (marked "(signature)" in the intro; always an arcana that has a signature version in the game). Its hits fill the meter. When full it gets the player's overdrive aura, glow and sound. The next time it casts that arcana it fires the **signature version**, with the player's cue (burst, hit-stop, camera shake). Unused, the ready state runs out after 8 / 10 / 12 / 14 s (Easy / Medium / Hard / Final).
- 🧪 **Fixes (v0.4.6), from testing:**
  - **Cyclone boomerang on every rival:** that's the rival's own Sword Throw, which was wrongly treated as neutral. It's now a Wind spell, so only Wind (and Chaos) rivals throw it.
  - **Still walking in place at times:** next to you, while its basic cooled down, the rival stood still with its run cycle playing. It now shows its idle pose while standing and runs again when it moves, and its arcana casts play a cast animation.
- 🧪 **Fixes (v0.4.7):** arcana that the player "finishes" after casting (the Hammer Throw's spin-and-throw, boomerangs, dragons, axes, bolas, charge shots like Ball Lightning) now get that last step when an enemy casts them, so nothing is left floating in place.
- 🧪 **Fixes (v0.4.8):** the Earth rival's axe basic now spins out instead of floating; the Frost rival's Water Arc basic works (it crashed before); rivals no longer glow at random (that was the old timed signature window).
- ✅ **Robe and relic:** a random player robe (its colours and stat bonuses) and a random stat relic (real icon and name, effect translated to a rival stat).
- 🧪 Player-style fall protection.

Next:
- 🔜 Confirm v0.5.11 in game: breakables, portal spacing, nothing in walls.
- 💬 Limit the portal to the 2nd stage of each tier (just before the Council fight), as in the earlier dev note, if one random stage per run feels better.
- 💬 **Evil echo of your last run:** remember the previous lost run (robe, arcana, relics) and bring it back as an evil rival.
- ✅ **Boss health bar (v0.4.9):** the game's own boss bar (the Council's and Sura's) with the rival's name, shown when the intro ends. Champions and elemental giants get it too (`BossBar.Enabled`, `BossBar.Giants`).
- 📋 Dropping the rival's relic, a secondary element on Hard, Sura-style attack patterns, and special relics (Resurrect, Vampire…) done for real.

## 3. Stage variants

### 3a. Chaos stages 🧪

A regular stage can have a **Chaos version** (`ChaosStages.StageChance` 0.15 per regular stage; a tier has 2 regular stages; boss floors, the Hub and the final room stay normal). The run's plan is rolled when you arrive in the Hub, so the loading board and the stages agree from the first board on and never change mid-run.

Built:
- 🧪 **Look:** dark purple tiles, decorations and lights. **v0.5.14:** this now actually applies as soon as the stage is built (it gave up silently before).
- 🧪 **Enemies (v0.5.14):** every humanoid with elemental versions (**Knight, Rogue, Archer, Mage, Summoner, Lancer**, including the stage's elemental ones) turns Chaos: purple palette row and 1 Chaos arcana. Their own elemental attacks deal **Chaos damage** and are tinted Chaos purple. (`ChaosStages.EnemyChance` no longer used.)
- 🧪 **Giants become Chaos giants:** Chaos element, 2 Chaos arcana, their own purple palette row found from the palette texture (`ChaosStages.GiantPalette` = -1); the Coffin gets a purple tint.
- 🧪 **Rival:** the rival portal on a Chaos stage leads to a Chaos rival. Rivals never appear outside their arenas.
- 🧪 **Rewards:** gold and item drops ×1.5 (`ChaosStages.RewardMultiplier`).
- 🧪 **Shown:** "Chaos <stage>" title. On the loading board the square turns **near-black purple with the Chaos symbol** in place of its element icon (v0.5.11). **v0.5.15:** marks land on the right squares (the board was counting one stage per tier) and stay put between stages.
- 🧪 **Corrupted Council:** if Chaos reached a tier, that tier's Council member is corrupted:
  - dark purple sprites and outline, "Corrupted <name>" boss bar;
  - attacks tinted near-black purple and dealing Chaos damage;
  - 2 **Chaos echoes** cast at you between their own attacks, so their moveset stays intact;
  - custom upbeat voicelines (they've been dabbling in Chaos and want to trade their element for it);
  - their room turns Chaos purple; their chest drops a **Chaos arcana**;
  - on the board, **only their portrait** is tinted purple (v0.5.12: the tint used to spill over the whole tier, on the wrong tier).
- Test keys: Shift+F11 (Chaos on this stage), Ctrl+F11 (next stage), Shift/Ctrl+F12 (Council).

Next:
- 🔜 Confirm v0.5.14–0.5.15 in game: purple tiles, every humanoid Chaos, board marks right and stable.
- 🔜 Check the automatic purple row on each giant (`CHAOS palette for <giant>: row N` in the log; F6 cycles rows).
- 📋 Maybe: a corrupted phase at low health; voicelines in the game's other languages (custom lines are English only).
- 📋 Chaos boss floors.

### 3b. Radiant and Prism stages (with WoL Arcanos) 🧪 (v0.6.0)

Each regular stage still rolls once for a variant (`ChaosStages.StageChance`, 15%), then picks which one:

| Variant | Share of variant stages | Needs |
|---|---|---|
| Chaos | 50% | nothing (base game) |
| Radiant (Arcanos "Fulgor") | 35% | WoL Arcanos |
| Prism (Arcanos "Prisma") | 15% | WoL Arcanos |

Without Arcanos every variant stage is Chaos, exactly as before. Shares are in the `StageVariants` config section. Arcanos stays optional: it's read by reflection, and the mod loads after it when it's there.

Built (🧪, not yet tested in game):
- 🧪 **Look:** Radiant stages turn pearl and gold; Prism stages spread every shade across the rainbow. Chaos tiles are now a real dark purple (the old hue shift turned grass red).
- 🧪 **Enemies and giants:** the same humanoids as on Chaos stages, plus giants, take the variant. Radiant enemies get a pale gold tint; Prism enemies slowly cycle through hues. They cast arcana of that element and their own attacks become that element (Arcanos' own element handling; Prism attacks cycle elements its way). Where Arcanos has few castable arcana of an element, base arcana fill in and turn into it when cast.
- 🧪 **Rewards per variant:** Chaos more gold and item drops; Radiant a bit more gold, and its enemies may leave a health pickup (15%); Prism more item drops and Chaos gems.
- 🧪 **Shown:** "Radiant <stage>" / "Prismatic <stage>" titles; on the board, a gold square or a violet square with Arcanos' Radiant / Prism icon. The log plan line reads C / R / P.
- 🧪 **Radiant / Prismatic Council:** a tier's Council member takes the variant of the tier's first variant stage: "Radiant"/"Prismatic" boss bar, tint (Prism cycles hues), attacks of that element, 2 echoes from that pool, their room recoloured, and **new upbeat voicelines** for each member (dazzled by Radiance / fascinated by every colour at once, and back to their element once beaten).
- 🧪 **Rivals:** a variant stage's rival portal leads to a rival of that variant. The occasional off-element rival on other stages rolls Chaos / Radiant / Prism by the same split.
- 🧪 **Archons:** with Arcanos, 25% of elemental Archons (final champion and arena Archon) are Radiant or Prismatic. Their phase 2 is "Ascendant" / "Refracted" (2 more arcana, faster casts) instead of an elemental hybrid. The Chaos Sovereign and Chaos rivals can now roll Radiant and Prism arcana too.
- 🧪 **Unlocks respected:** Radiant and Prism arcana have their own unlock requirements in Arcanos, so nothing here unlocks them. A Radiant/Prismatic Council chest, or a Radiant/Prism rival's or Archon's chest, drops only an arcana of that element the player has **already unlocked**, never a locked one. With none unlocked, the chest keeps its normal drop. (The Corrupted Council's Chaos arcana now follows the same rule.)
- Test keys: Shift+F11 cycles this stage off → Chaos → Radiant → Prism; Shift+F12 cycles the forced Council variant; F11's rival test cycle includes Radiant and Prism.

Next:
- 🔜 Test in game: `VARIANTS: WoL Arcanos found` at startup, the `ARCANA catalog` line listing Radiant and Prism counts, tiles, enemies, board icons, Council lines and chests.
- 📋 If Arcanos has very few castable Radiant/Prism arcana, tune the fill-in pools.
- 💬 A variant mark for the run on the board's final circle when the champion is a Radiant/Prismatic Archon (its portrait is already tinted in its colour).
## 4. Final-stage boss pool 🧪

Built:
- 🧪 Each run has a 35% chance (`FinalBoss.Chance`) that the final boss is a **champion** instead of Sura: an elemental **Archon**, or the **Chaos Sovereign** (30% of champions) with arcana from every element. It's the rival kit at a Final tier: 2600 HP, 5 arcana, dash arcana, signature, robe and relic.
- 🧪 **Loading-screen icon (v0.5.3):** on champion runs, Sura's own portrait on the board's final circle is tinted in the champion's element colour (ring included). The Chaos symbol stays only for the Chaos Sovereign.
- 🧪 **Normal ending kept:** beating the champion ends the run exactly like beating Sura, including the Chaos arcana unlock.

Next:
- 🧪 Boss health bar (v0.4.9, shared with rivals).
- ✅ **Second phase (v0.4.12):** at half health the champion freezes in a burst, then an "awakens" card shows its new arcana. An **Archon becomes a hybrid**: it keeps its 2 strongest arcana, gains 3 from a second element (and that element's own spells), and casts faster. The boss bar becomes e.g. "Fire–Water Archon". The **Chaos Sovereign** becomes "Unbound": 2 more arcana and faster casts. (`FinalBoss.Phases`)
- 🧪 The run's champion is rolled in the Hub with the Chaos plan (v0.5.15), so the board's final circle is right from the first board.
- 🧪 Radiant and Prismatic Archons and Radiant/Prism arcana in the Sovereign's pool, when WoL Arcanos is installed (v0.6.0, see 3b).

## 5. WoL Arcanos compatibility ✅ / 📋

WoL Arcanos (v0.57.0) is installed: 192 arcana, mod robes, and the Prism ("Prisma") and Radiant ("Fulgor") elements.
- ✅ **Already optional:** no hard dependency. Its arcana are found at startup by scanning loaded mods (log: `ARCANA catalog: 201 castable arcana … 192 from other mods`), and enemies, rivals and giants cast them. Without Arcanos, everything runs on the base game's arcana.
- ✅ Its robes work as rival robes (the palette it logs is the game's own).
- 🧪 Arcanos' Radiant and Prism arcana are tagged in the catalog (v0.6.0).
- 🧪 Soft dependency declared; its element and unlock API is read by reflection (`ArcanosBridge`, v0.6.0).
- 💬 **Safekeeping copy of Arcanos** (use the installed mod if present, otherwise a dormant bundled copy): redistributing another author's mod needs their permission, so **ask the author first**. An alternative is to store only the data we need, such as arcana lists and colours, without the mod itself.

## Shared system: Enemy Arcana 🧪

Lets any enemy cast real player arcana. Used by rivals, champions, arcana giants and chaos enemies.
- ✅ **Catalog of every player arcana** (233 skill states, generated from the game with `tools/gencatalog.ps1`), plus other mods' arcana found at startup. Each arcana's element comes from the game's own stats, so themes are never mixed.
- ✅ **Delivery by kind:** projectiles, waves, ground spells, self spells, basics, dash arcana, minions and seekers (in the game's own enemy mode), wards.
- ✅ **Generic ownership:** homing and follow-up hits from enemy-cast arcana target the player, and enemy casts never hurt enemies.
- Config: `EnemyArcana.ExcludeFamilies` to turn kinds off.
- 📋 Per-arcana fixes as testing finds them; charge/hold scaling; ultimates on Hard.

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
| F11 | Open a rival portal beside you: selector room → arena → way back (cycles Fire → Water → Earth → Wind → Thunder → Chaos, then Radiant → Prism with Arcanos) |
| Shift+F11 | Cycle the current stage: normal → Chaos → Radiant → Prism (the last two with Arcanos) |
| Ctrl+F11 | Skip to the next stage, as if through the exit portal (needs the floor's exit; a Council floor only has one after the fight) |
| F12 | Final champion test (this final room, or spawn one here and make this run's final boss a champion) |
| Shift+F12 | Cycle the forced Council variant: off → Chaos → Radiant → Prism (every Council fight, including the current room) |
| Ctrl+F12 | Jump straight to this tier's Council floor, corrupted for that fight (from any regular stage of a run) |

Config: `BepInEx\config\osi.wol.hybridbosses.cfg` (created on first launch).
