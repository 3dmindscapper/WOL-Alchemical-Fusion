# Hybrid Bosses: Roadmap

A Wizard of Legend mod (BepInEx + HarmonyX) that adds elemental mini-bosses, rival-wizard fights, chaos stages and new final bosses.
It mostly reuses the game's own assets. Custom art is possible, but so far every idea has been covered by clever asset reuse.

Status key: ✅ done · 🧪 built, needs in-game testing · 🔜 next · 📋 planned · ❓ needs research first · 💬 dev note (idea to consider)

Current version: **0.5.1**. Technical details (game internals, file layout, pitfalls) live in `CLAUDE.md`.

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
- 🧪 **Event:** the misc-room portal (assumed to be the red one) sometimes (`RivalWizards.Chance` 0.3) leads to an arena with three portals: Easy (green), Medium (yellow), Hard (red).
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
- 🔜 Arena layout tuning (portal and rival positions are guesses).
- 💬 **Dedicated rival portal instead of the red portal room:** a new portal that can appear on each 2nd stage of a tier (the X-2 stages, just before the Council member fights). WoL Arcanos or Tournament Edition have custom portals, so this should be possible. If adopted, the event above moves to that portal.
- 💬 **Fight in the real PvP arena:** if the game has a PvP arena scene, teleport the player there for the rival fight and back, without losing the stage's state.
- 💬 **Evil echo of your last run:** remember the previous lost run (robe, arcana, relics) and bring it back as an evil rival.
- ✅ **Boss health bar (v0.4.9):** the game's own boss bar (the Council's and Sura's) with the rival's name, shown when the intro ends. Champions and elemental giants get it too (`BossBar.Enabled`, `BossBar.Giants`).
- 📋 Dropping the rival's relic, a secondary element on Hard, Sura-style attack patterns, and special relics (Resurrect, Vampire…) done for real.

## 3. Stage variants

### 3a. Chaos stages 🧪

A regular stage can have a **Chaos version** (`ChaosStages.StageChance` 0.15 per regular floor; boss floors, the Hub and the final room stay normal).

Built:
- 🧪 **Look:** purple-tinted tiles, decorations and lights.
- 🧪 **Enemies (v0.4.5):** the types that have elemental versions on other stages (**Knight, Rogue (thief), Archer, Mage (wizard), Summoner, Lancer**, including their elemental versions) each have a 50% chance (`ChaosStages.EnemyChance`) to be a Chaos version: **1 Chaos arcana and their purple palette row**. That's like elemental stages, where not all of them are elemental. Other regular enemies keep their normal kit. **Giants become Chaos giants** instead of taking the stage element: Chaos element, 2 Chaos arcana, no borrowed elemental skills. They wear their own purple palette row, found automatically by reading each giant's palette texture (`ChaosStages.GiantPalette` = -1). A giant with no purple row (the Coffin) gets a purple tint instead.
- 🧪 **Rival:** the stage's rival arena features a Chaos rival casting Chaos arcana. Rivals never appear outside their arenas.
- 🧪 **Rewards:** gold and item drops ×1.5 (`ChaosStages.RewardMultiplier`).
- 🧪 **Shown:** "Chaos <stage>" title, and a purple marker on the loading-screen board. The board and the stage always agree.
- Shift+F11 toggles Chaos on the current stage (test key).

Next:
- 🔜 Check in game that the automatic purple row looks right on each giant (the log shows `CHAOS palette for <giant>: row N`). If it's wrong, set `ChaosStages.GiantPalette`; F6 cycles rows on the nearest enemy to find the right one.
- 🧪 **Corrupted Council (v0.5.0):** if Chaos reached a tier (any of its regular stages is a Chaos stage), that tier's Council member is corrupted. Their sprites get a dark purple tint and outline, and all their attacks are tinted like Chaos arcana. The boss bar reads "Corrupted <name>". Their intro and victory lines come out glitched, with Chaos whispers, a purple-tinted portrait and purple text. Their circle on the loading-screen board is purple. Moves and stats are unchanged for now. `ChaosStages.CorruptedCouncil`; Shift+F12 forces it for testing.
  - 🧪 **v0.5.1:** their chest drops a **Chaos arcana** instead of an element arcana. Their attacks deal **Chaos damage** (their elemental status effect becomes Chaos). They get 2 **Chaos echoes**: between their own attacks they sometimes flash and cast a Chaos arcana at you, so their whole moveset (and fight identity) stays intact. A card at fight start shows the echoes.
  - 📋 Maybe: a corrupted phase at low health.
- 📋 Chaos boss floors.

### 3b. Radiant and Prismatic stages 📋 💬

Same approach as Chaos, once Chaos is nailed down:
- **Radiant:** everything tinted toward pearl white; giants and elemental enemies cast Radiant spells; Radiant rivals.
- **Prismatic:** WoL Arcanos has Prismatic arcana that mix two elements. Prismatic stages mix elements, with rivals using dual-element kits and Prismatic arcana.
- Both use the same pieces as Chaos (title, board marker, rewards, rival variant), and both need WoL Arcanos (see 5).

## 4. Final-stage boss pool 🧪

Built:
- 🧪 Each run has a 35% chance (`FinalBoss.Chance`) that the final boss is a **champion** instead of Sura: an elemental **Archon**, or the **Chaos Sovereign** (30% of champions) with arcana from every element. It's the rival kit at a Final tier: 2600 HP, 5 arcana, dash arcana, signature, robe and relic.
- 🧪 **Loading-screen icon:** on champion runs, the board's final circle (where Sura's Chaos icon sits) shows the champion instead: element-coloured ring with one of its arcana icons (placeholder art).
- 🧪 **Normal ending kept:** beating the champion ends the run exactly like beating Sura, including the Chaos arcana unlock.

Next:
- 🧪 Boss health bar (v0.4.9, shared with rivals).
- ✅ **Second phase (v0.4.12):** at half health the champion freezes in a burst, then an "awakens" card shows its new arcana. An **Archon becomes a hybrid**: it keeps its 2 strongest arcana, gains 3 from a second element (and that element's own spells), and casts faster. The boss bar becomes e.g. "Fire–Water Archon". The **Chaos Sovereign** becomes "Unbound": 2 more arcana and faster casts. (`FinalBoss.Phases`)
- 📋 real icon art, and Radiant/Prismatic arcana in the Sovereign's pool when WoL Arcanos is installed.

## 5. WoL Arcanos compatibility 🔜

WoL Arcanos (v0.57.0) is now installed: 192 arcana, mod robes, and Radiant/Prismatic elements.
- 🔜 Check the startup log for `ARCANA found N arcana from other mods`. Our catalog already looks for other mods' arcana at startup, but hasn't been verified with Arcanos.
- 📋 Map Arcanos' Radiant and Prismatic elements into our pools (for 3b and the Sovereign).
- 📋 Keep it a **soft dependency**: everything must work without Arcanos.
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
| F11 | Open a rival arena around you (cycles Fire → Water → Earth → Wind → Thunder → Chaos) |
| Shift+F11 | Toggle the Chaos version of the current stage |
| F12 | Final champion test (this final room, or spawn one here and make this run's final boss a champion) |
| Shift+F12 | Force Corrupted Council on/off (every Council fight, including the current room) |
| Ctrl+F12 | Jump straight to this tier's Council floor, corrupted (from any regular stage of a run) |

Config: `BepInEx\config\osi.wol.hybridbosses.cfg` (created on first launch).
