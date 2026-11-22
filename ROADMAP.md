# WOL Alchemical Fusion: Roadmap

Current version: **1.0.0** (first public release, as WOL Alchemical Fusion; developed as "Hybrid Bosses"). Technical details (game internals, file layout, pitfalls, per-version notes) live in `CLAUDE.md`.

Legend: ✅ tested in game · 🧪 built, needs more play · 💡 idea · ⏸ set aside

## Status at 1.0.0

Everything below is built. Tested in game so far: elemental giants, rival arenas and fights, Chaos stages, the recolour engine on arcana, Tomi infusion menu and card recolours, the Tomi run list. The rest is built and has been through code review, but needs real runs to confirm and tune.

## What's in the mod

### Enemies
- ✅ **Elemental giants.** Every mini-boss on an elemental stage takes that element: its palette (or a recoloured sprite sheet for the Coffin), borrowed elemental skills, player arcana, an intro card and the boss bar. Neutral stages: a random element now and then.
- 🧪 **Elemental Coffin Knights, Cyclops, blobs and ghouls.** On elemental stages they can take the stage's element (recoloured body, elemental damage and status; Coffins and Cyclops also borrow one arcana).
- 🧪 **Elites.** Twelve traits: Shielded, Hasted, Splitting, Draining, Blinking, Volatile, Vengeful, Haunting, Anchored, Overcharged, Infused, Thief. Extra gold scaled by how risky the trait is.
- 🧪 **Special mini-bosses** (replace a giant 30% of the time, in its own room): Giant Cyclops, Mimic King, Cyclops Eye Court, Elemental Conclave, Hive Mother, Gravewarden. Armour that breaks into stun phases; they match the stage's element or variant.
- 🧪 **Enemy arcana.** Giants, rivals, champions and Council members cast real player arcana (and, with WoL Arcanos, enemy versions of its light and prism arcana).

### Stages
- 🧪 **Chaos stages** (and, with WoL Arcanos, **Radiant** and **Prism** stages): tinted tiles, converted enemies and giants, better rewards, and the tier's Council member corrupted to match.

### Rivals
- ✅ **Rival wizards.** A portal in the starting room leads to an arena with a rival wizard of the stage's element: robe, relics, arcana, signature, intro card. Difficulty rolls Easy / Medium / Hard, with Hard more likely deeper in the run.
- 🧪 **Echo of your last run.** Sometimes the portal holds your previous run's loadout (arcana with infusions, robe, relics). Once per run.
- 🧪 **Rematches.** A beaten rival may come back later in the run, a tier harder with some arcana infused; it can drop one of its arcana.
- 🧪 **Archons.** Hard rivals are sometimes final champions with a second phase; they and Hard rivals can cast dual-infused arcana.
- 🧪 **Relics.** Rivals use any relic, cursed ones included. Holder-only relics run for real on them; the rest become stat bonuses.
- 🧪 **Voice lines.** Each element has a personality; rivals talk at the start, at half health and on defeat (the Echo and Archons too).

### Final boss
- 🧪 **Champion pool.** The final room sometimes holds an elemental Archon or the Chaos Sovereign instead of Sura, with a half-health phase.
- 🧪 **Council.** Corrupted (Chaos / Radiant / Prism) or infused Council members with recoloured bodies and attacks, and rewritten dialog lines.

### Infusion (player)
- 🧪 **Tomi at the arcana merchant.** Keep one arcana, sacrifice another (or a card off the merchant's shelf); the kept one takes the sacrificed one's element: damage type, status, +10% damage, its colours, card art and icon.
- 🧪 **Dual infusion.** From the third section on, an infused arcana can take a second element; casts alternate between the two.
- 🧪 **Unlocks.** Every infused version unlocks for good. At Tomi in the Hub you choose an arcana's version: the original, any single version, or any dual pair. The arcana's own element counts toward pairs; Prism stays single.
- 🧪 **Random versions.** Arcana with pairs sometimes get a random dual version for the run; Nocturne's paid shuffle can roll any unlocked version.
- 🧪 **Pre-infused drops and stock**, rare. **Infusion trials:** a portal in the starting room forces all your arcana into one element for three waves; clearing it gives a pre-infused arcana.

### Run extras
- 🧪 **Bounties.** One per run, shown in the Hub; completing it drops gold.
- 🧪 **Modifiers menu.** F1 in the Hub (or Skill4 while Tomi's spellbook is open): toggle and tune every system without the config file.

### WoL Arcanos (optional)
- 🧪 Radiant and Prism stages, Council members and rivals; enemy versions of its arcana; its unlock rules respected (nothing new unlocks from our drops); its icons and effects reused. Its element values never go on enemies (they break the game's damage code). Its ailments (Soaked, Magnetized...) aren't applied by us, but our elemental hits should benefit from them.

## To confirm in play (open since the last review)
- Special mini-bosses count for clearing the stage (especially the Conclave's hidden Archon).
- The Eye Court's orbs circle smoothly; the Large Mimic behaves outside chest rooms.
- Gold economy: elite gold, thieves, specials, bounties and trials on top of normal gold vs infusion costs.
- Anchored's pull feels fair; rival voice lines' timing; dual icons read well at small sizes.
- Real relics on rivals all activate cleanly (watch the log for "could not wear").

## Ideas for later 💡
- Element reactions (Fire on Frozen shatters, Thunder on Wet chains).
- Elite pairs at high loops (two traits).
- Council members summoning a mini version of a special you beat this run.
- A champion that mirrors the element you infused most.
- Fight records in the Hub (a collection of everything beaten).
- A run summary card (infusions, elites, specials, next Echo).
- ⏸ Variant stage hazards (stages are built for their own hazards).

## Test hotkeys (config Debug.Hotkeys = true)

| Key | What it does |
|---|---|
| F1 (Hub) | Modifiers menu (always available, not a debug key) |
| F5 | List enemies (log) |
| Ctrl+F5 | Spawn the next special mini-boss beside you (cycles all six) |
| Alt+F5 | Rematch rival portal beside you |
| Ctrl+F6 | Trial portal beside you (elements in turn) |
| Alt+F6 | One step of progress on this run's bounty |
| Ctrl+F7 | Make the nearest enemy an elite (traits in turn) |
| Alt+F7 | +500 gold |
| F8 | Spawn the next giant beside you (ends with the Giant Cyclops) |
| Ctrl+F8 | Tomi beside you |
| Alt+F8 | Infusion test pack: unlock a random version of each equipped arcana, then roll Nocturne's shuffle |
| F10 | Re-roll the nearest giant's element |
| Ctrl+F10 | Infusion look preview on your own arcana (cycles elements) |
| Alt+F10 | Echo of your last run, beside you |
| F11 | Rival portal beside you (cycles elements) |
| Shift+F11 | Toggle the variant on the current stage |
| Ctrl+F11 | Skip to the next stage |
| Ctrl+Shift+F11 | Record your current loadout as the Echo |
| F12 | Final champion test |
| Ctrl+F12 | Jump to this tier's Council fight |
| Shift+F12 | Cycle the Council mode (normal / variant / infused) |
| Ctrl+Shift+F12 | Jump to the final fight |
| F6 / F7 / F9 / Ctrl+F9 | Developer dumps: palette cycle, nearest enemy, element palettes, arcana visuals |
