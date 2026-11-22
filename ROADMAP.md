# Hybrid Bosses: Roadmap

A Wizard of Legend mod (BepInEx + HarmonyX) that adds elemental mini-bosses, rival-wizard fights, chaos stages and new final bosses.
It mostly reuses the game's own assets. Custom art is possible, but so far every idea has been covered by clever asset reuse.

Status key: ✅ done · 🧪 built, needs in-game testing · 🔜 next · 📋 planned · ❓ needs research first · 💬 dev note (idea to consider)

Current version: **0.7.4**. Technical details (game internals, file layout, pitfalls) live in `CLAUDE.md`.

**Status:** sections 1–6 are built and in the **polish phase**: in-game testing and fixes, no new scope.
**Next new features: section 7** (elites, infusion trials, stage hazards, infused Council).

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
- 🧪 **Radiant/Prism look (v0.6.1–0.6.2):** a gentler gold wash on Radiant stages, and strong gold-to-pearl infusion on Radiant attacks. Infused base arcana show as "Radiant X" / "Prismatic X" in intros. Arcanos' element values are never put on enemy bodies or enemy attacks (a Prism giant couldn't be damaged).

### 5a. Arcanos' Radiant and Prism arcana on enemies 🧪 (v0.6.3)

Arcanos writes its Radiant/Prism arcana for the player only: mouse aim, inventory, HUD and the player's skill events. So they can't run on an enemy, and **rivals can't use the player versions** either (a rival is an enemy, not a player). Instead, what makes them what they are was studied and rebuilt for enemies, using the installed mod's own pieces (nothing of Arcanos is shipped):
- **Look and sound:** Arcanos' own sprites (light lance, light whip, flash, holy halo, light feathers, mote of light, prism arcs per element) and its own effects (light beams, sparkles, splashes, debris, gusts, lightning impacts).
- **Hits:** the game's generic circle hitbox with the arcana's real stat damage, owned by the enemy. It's the same recipe Arcanos uses for its attacking training dummy.

Rebuilt so far:

| Arcana | Variant | What the enemy version does |
|---|---|---|
| Light Lance | Radiant | a spear of light flies straight and bursts into sparkles |
| Flash | Radiant | a mote of light gathers on your spot, then a blinding burst (2 Flashes) |
| Holy Halo | Radiant | a halo over the caster pulses holy light around it 3 times |
| Light Whip | Radiant | a lash of light across the front |
| Wings of Light | Radiant | a fan of light feathers |
| Elemental Prism | Prism | five bursts around you, each the next element |
| Triad | Prism | three prism arcs, each a different element |

The ailments, in controlled form:
- **Radiant (Flash → Dazzled):** Arcanos' Radiant hits stack Flashes on enemies until they're blinded. On the player, Radiant hits stack Flashes, and at 3 you're **Dazzled** for 2.5s: a bright glare washes over the screen and fades. Controls are never taken away.
- **Prism (element cycle):** like Arcanos' Prism, each hit is the next of the five base elements, with that element's status (burn, freeze, poison, slow, shock) and colour. This now applies to **every** Prism enemy attack, not just the rebuilt arcana.
- **Arcanos' own ailments on our enemies** (Blind, its pulse stun, Heat, Soaked, Cracked, Whirl, Magnetized, Curse) apply through Arcanos itself. Our side now respects them: blinded or stunned enemies don't cast arcana or force signatures, rivals don't "unstick" out of them, and the giant watchdog never breaks them.

Next:
- 🔜 Test: each port's look, size and damage; the Dazzle glare; whether Prism statuses feel fair.
- 📋 More ports if these work: Radiant Aegis (a reflecting shield), Light Deer (a summon), Supernova, Refraction.
- ❓ Purified (Arcanos' second Radiant stage: armour broken, extra damage taken) as a short "takes +15% damage" on the player, if Dazzled alone feels too light.
## 6. Arcana infusion (player feature) 🧪 v0.7.4

**Idea:** pick an arcana to keep and an arcana to sacrifice. The kept arcana takes the sacrificed arcana's element. For example, Ice Spear plus a sacrificed Fire arcana gives a "Fire-infused Ice Spear": the same move, now dealing fire damage, applying burn and looking fiery.

Why it fits: the enemy side already exists. Variant stages turn arcana into another element's damage and colours ("Radiant Tele Strike"), so infusion hands that engine to the player.

**Decided:**
- **The player picks which arcana keeps its move** and which one gives its element (the sacrificed one).
- **Run only:** infusions last for the current run.
- **Re-infusing is allowed:** it replaces the old infusion.
- **+10% damage while infused,** so it's worth considering more often. Re-infusing doesn't stack it: always +10%.
- **Same-element merging: no, for now.** It would turn a flexible idea into an "always merge for a power boost" outcome.
- **Infused arcana can drop** (rarely) and **be sold by the arcana merchant** (rarely).
- **The arcana's card sprite and icon are recoloured** to the new element.

**Where and cost (v0.7.1):** **Tomi** teleports in beside the arcana merchant on every shop visit. In a run, his spellbook only shows **your own arcana**, and infusing happens right in that menu:
- Point at the arcana to **keep** and press **[I]**, then point at the arcana to **sacrifice** and press **[I]**, then **[I]** again to infuse. Cost: the sacrificed arcana plus **80 + 40 gold per tier**. A hint bar at the bottom shows each step.
- You can't sacrifice your signature, an arcana that already has the kept one's element, or your equipped basic or dash.
- Infused arcana get the element's look on their **actual attacks** (every animation frame recoloured, particles, trails, lights) and on every icon. They also take its damage type, status and +10%.
- Rarely, a dropped arcana (4%) or one in the merchant's stock (8%, 25% pricier) is already infused; its card shows it.
- **Every infused version you get stays unlocked.** Back in the Hub, point at an arcana in Tomi's spellbook and press **[I]** to switch between its original and its unlocked infused versions. The chosen version is what you cast in runs, until you infuse it differently during a run.
- Test: **Ctrl+F8** brings Tomi beside you.

Still to do:
- 📋 "Fire-infused X" names in the game's own spellbook / HUD text (the hint bar already says it).
- 🧪 Controller (v0.7.3): the arcana buttons act as [I] in Tomi's spellbook (config `Infusion.Buttons`), so it works on a Steam Deck.
- 🧪 The mod's on-screen cards and hints use the game's own font (v0.7.3).
- 📋 Element accents for infused player attacks (enemy variants have them).
### 6a. Research: how vanilla sells an element's look (v0.6.4)

What the game does (code scan of the arcana classes):
- **Attacks are hand-coloured animated sprite sheets.** Almost every arcana class drives an `Animator` that swaps sprites each frame (Fireball, Ice Lance, Boulder, Water Blast, Lightning Strike, Flame/Tremor Lance...). The element is painted into the art itself.
- **Particle systems are accents, not the base.** Only some arcana carry them in code: Fire Blast (`pEmitters`), Flame Slash (`firePartSystems`), Twister (3 systems), Shock Line, Boulder Drop (`explosionEmitter`), Chaos Beam (`lightningEmitter`).
- **The palette shader** (`_Palette` / `_PaletteIndex`: one sprite, many colour rows) is used for **cards, enemies, outfits** and a few enemy projectiles. For example, `EnemyFireball.SetColor` recolours an enemy mage's fireball per element, and `FlameLance`/`TremorLance` pick a palette row. Player arcana sprites aren't palette sprites.
- **Cards:** the card frame/back palette row comes from rarity. The element identity on a card is the arcana's own icon art. So recolouring the icon is the way to show an infused card.
- Ctrl+F9 logs every arcana prefab's makeup (sprites and shaders, animators, particles with their colours and textures, trails, lights) as `VISUALS …` lines, so we can confirm per arcana on a real install.

Why a plain hue shift won't do:
- Ice and white highlights have almost no saturation, so a hue shift leaves them unchanged.
- Earth's brown needs darker values and less saturation, and Chaos's near-black purple needs strong darkening. A hue shift keeps brightness.
- A colour multiply over the sprites (today's variant tint) can only darken, so it reads weakly on bright effects.

**Recommended approach: a gradient map per element.** Each pixel's brightness is mapped onto a 3–5 stop ramp in the element's colours. Shadows, mid-tones and highlights each land where that element puts them, and the animation, shapes and detail are kept.

| Element | Ramp (dark → mid → highlight) |
|---|---|
| Fire | deep red → orange → yellow-white |
| Water | navy → blue → cyan-white |
| Ice | steel blue → light blue → white |
| Earth | dark brown → brown / ochre → sand |
| Thunder | olive → yellow → white |
| Wind | teal → mint → white |
| Chaos | near-black purple → dark violet → lilac |
| Radiant / Prism (Arcanos) | gold → pearl / the Prism cycle colours |

How to apply it:
1. **Animated sprites:** recolour each sprite atlas once per element (cached). Then, in LateUpdate, swap whatever sprite the Animator just set for its recoloured twin. Exact colours, no shader needed, cheap after the first time. This is the core of infusion.
2. **Particles, trails, lines, lights:** start colours / gradients / light colour mapped onto the same ramp (already done for variants).
3. **Element accents** sell it further: a few element particles on the cast or the hit, such as embers for fire, droplets for water, frost for ice, pebbles and dust for earth, sparks for thunder, gusts for wind, wisps for chaos. Use the game's own element effects where they can be reused, and Arcanos' effects when installed (EstallidoDeLlamas, Salpicon, Escarcha, Escombros, ImpactoRayo, Rafaga, ChispasCaos).
4. **Damage:** the element and its status move over (burn / freeze / slow / poison / shock / Chaos), +10% damage.
5. **Card sprite and icon:** the same gradient map on the arcana's icon wherever it's shown (pickup card, merchant, spellbook, HUD).

Shared win: the same recolour engine would replace today's variant tint on enemies, giving Chaos/Radiant/Prism enemies proper element looks too.

Pieces to build:
1. 🧪 **Recolour engine (v0.6.5):** gradient maps per element, recoloured twins of every animated frame, particles/trails/lines/lights on the same ramp, and element accents (Arcanos effects). It's live on Chaos/Radiant/Prism enemies' attacks. **Ctrl+F10 previews each element's look on your own arcana** (look only).
2. 🧪 Infusion state per run, +10% damage, the element/status conversion on the player's attacks (v0.7.0).
3. 🧪 The infusing interaction: in Tomi's spellbook, Tomi beside the arcana merchant (v0.7.1); infused versions stay unlocked and are chosen at Tomi in the Hub.
4. 🧪 Recoloured card sprite and icon in every UI (v0.7.0); 📋 "Fire-infused X" names in the game's own text.
5. 🧪 Rare infused drops and merchant stock (v0.7.0).
6. Arcanos: Radiant/Prism infusions only from arcana the player has unlocked.
7. Edge cases: signature versions, basics and dash arcana, co-op, mid-run saves, arcana whose element can't be read.
## 7. Next features 📋

Planned, not started. Each one builds on systems that already exist.

### 7a. Elite enemies 📋
Now and then a regular enemy spawns with **one trait** that changes how you fight it:

| Trait | Effect | Look |
|---|---|---|
| Shielded | a shield that has to be broken before it takes damage | gold outline |
| Hasted | faster movement and attacks | streaky after-images |
| Splitting | splits into two smaller copies on death | pale, swollen tint |
| Draining | heals itself from the damage it deals | red pulse |
| Blinking | teleports short distances between attacks | ghostly, flickering tint |

- Uses: the recolour engine and outlines for the looks, the variant enemy hooks to apply a trait, and the game's own stats for speed, health and shields.
- A small "Elite <enemy>" name above it. Rewards: more gold, a better chance at an infused arcana drop (fits the "rare infused drops" rule).
- Tuning: rare on the first tier, more common later. Never stacked with a variant giant.

### 7b. Infusion trials 📋
A portal (like the rival portal) that sometimes opens on a stage and leads to a **trial room**:
- All your arcana are temporarily forced into one element, chosen by the trial ("Trial of Frost"). You fight a few waves with that element.
- Clear it: **one free infusion** at Tomi (no gold, no sacrifice), or a pre-infused arcana of that element.
- Leave or fail: back through the way-back portal, nothing lost but the time.
- Reuses: the rival event's room building and portals, infusion's attack conversion (a run-wide "forced element" mode), and the recolour engine so your spells visibly change.

### 7c. Variant stage hazards 📋
Each variant stage plays differently, not just looks different:
- **Radiant:** light pillars that flare on a telegraphed timer. Standing in one dazzles you (the existing glare) and also dazzles enemies, so they're a tool too.
- **Prism:** floor patches that cycle through the five elements. Stepping on one applies that element's status, to enemies as well.
- **Chaos:** rifts that sometimes spit out an extra enemy unless you close them by hitting them.
- Built from the tile and room code (hazard spots on open floor, like the rival portal placement), Arcanos effects where installed, and the game's own status effects.

### 7d. Infused Council members 📋
A Council member who has infused their own element with another, for example "Flame Empress, Frost-infused". It's separate from the Chaos/Radiant/Prism variant Council:
- Their whole moveset stays, but their attacks take the infused element's damage, status and look (the recolour engine on their attacks, like player infusion).
- Boss bar "<Element>-infused <name>", a few new voicelines (they've been experimenting with a classmate's element), and a recoloured room.
- Reward: their chest drops an arcana of the infused element, or a pre-infused arcana of their own element.
- When: a chance on later loops, or from a choice on the loading board. It never stacks with a variant Council member.
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
| Ctrl+F9 | Dump every arcana prefab's makeup (sprites, shaders, animators, particles, trails, lights) for infusion research |
| F10 | Cycle the nearest giant through all five elements |
| Ctrl+F8 | Bring Tomi beside you (infusing in his spellbook with [I]) |
| Ctrl+F10 | Infusion look preview: your own arcana take Fire → Water → Ice → Earth → Thunder → Wind → Chaos → Radiant → off (look only) |
| F11 | Open a rival portal beside you: selector room → arena → way back (cycles Fire → Water → Earth → Wind → Thunder → Chaos, then Radiant → Prism with Arcanos) |
| Shift+F11 | Cycle the current stage: normal → Chaos → Radiant → Prism (the last two with Arcanos) |
| Ctrl+F11 | Skip to the next stage, as if through the exit portal (needs the floor's exit; a Council floor only has one after the fight) |
| F12 | Final champion test (this final room, or spawn one here and make this run's final boss a champion) |
| Shift+F12 | Cycle the forced Council variant: off → Chaos → Radiant → Prism (every Council fight, including the current room) |
| Ctrl+Shift+F12 | Jump straight to the final fight (Sura or this run's champion), from any stage of a run |
| Ctrl+F12 | Jump straight to this tier's Council floor, corrupted for that fight (from any regular stage of a run) |

Config: `BepInEx\config\osi.wol.hybridbosses.cfg` (created on first launch).
