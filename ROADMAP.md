# WOL Alchemical Fusion: Roadmap

Released: **1.0.0** on Thunderstore (developed as "Hybrid Bosses"). In development: **1.1.0** (built, ready to test). Technical details (game internals, file layout, pitfalls, per-version notes) live in `CLAUDE.md`.

Legend: ✅ tested in game · 🧪 built, needs more play · 💡 idea · ⏸ set aside · size **S** (an evening) / **M** (a few sessions) / **L** (a big update)

---

## 1. Visual identity (locked)

These are the looks the mod already uses in game. Treat them as fixed: new features, key art, screenshots and store pages should match them, so everything reads as one mod.

### Principles
- **Re-use the game, never redistribute it.** Bodies, attacks, rooms and UI come from Wizard of Legend's own assets at runtime. New looks are recolours (the recolour engine), never new sprite art shipped in the package. The only original art is the mod icon and the key art.
- **One colour per element, everywhere.** An element looks the same on an infused arcana, an elemental enemy, a Council member, a card frame and a UI title.
- **Recolour by brightness.** Every element look is a gradient map: dark pixels take the element's shadow tone, bright pixels its highlight. That keeps the game's shading and pixel detail intact.
- **Duals show both.** Two-element things split diagonally: the first element bottom-left, the second top-right, blended softly across the middle.
- **Chaos is dark.** A near-black, deep purple. Never a light lavender.
- **The game's voice.** UI text uses the game's own pixel font, with upbeat, friendly lines in the game's tone.

### Element palette
Ramp = the recolour gradient, from shadow to highlight. Identity = the colour used for titles, outlines, frames and portals.

| Element | Ramp (shadow → highlight) | Identity colour | Character |
|---|---|---|---|
| Fire | `#590A05` `#D9380A` `#FF8C1A` `#FFD959` `#FFFAD9` | `#FF731A` orange | hot-headed |
| Water | `#0D144D` `#144DBF` `#4099FF` `#A6E0FF` `#EBFAFF` | `#4099FF` blue | cool, composed |
| Ice | `#406699` `#80B8E6` `#BFE6FF` `#F2FAFF` `#FFFFFF` | `#D4EEFF` white / light blue | crisp |
| Earth | `#2E1A0A` `#613819` `#946633` `#C79E61` `#EDD9A8` | `#A87C45` brown | steady |
| Thunder | `#403808` `#99850D` `#FAD926` `#FFF58C` `#FFFFEB` | `#FFE633` yellow | loud |
| Wind | `#0D4038` `#338C73` `#80D9B3` `#CCFAE0` `#F7FFF7` | `#99FFD9` mint | playful |
| Chaos | `#140524` `#4D147A` `#9E59F2` `#D9B8FF` | `#7A33C7` deep purple (tiles: near-black purple) | gleeful, unhinged |
| Radiant (Arcanos) | `#8C611F` `#F2B840` `#FFEDA6` `#FFFFF5` | `#FFE68C` pale gold | noble |
| Prism (Arcanos) | cycles Fire → Water → Earth → Wind → Thunder | `#FF8CE6` pink / rainbow | whimsical |

### UI kit
- **Bands and boxes:** a dark band `#0A0812` at about 85% opacity, a thin gold edge `#F2CC73`, cream text `#FFF7E6` with a dark drop shadow, in the game's font.
- **Accent colours:**
  - titles: pale gold `#FFE699`
  - gold and bounty text: `#FFD973`
  - the Echo: lavender `#B8A8D9`
  - armour: pale blue outline `#BFD9FF`
- **Cards:** an infused card's frame takes a 55% tint of its element's identity colour; a dual's frame takes the mix of both.
- **Outlines:**
  - elements and elites: a coloured outline that pulses gently
  - armoured bosses: the pale blue outline, back to their element outline while stunned
- **Dialog box:** a dark box at the bottom with the speaker's name in their element colour, a coloured left edge, and the line typed out.
- **Announcement cards:** a centred title in the element colour, a subtitle with " · " separators, and icons revealed left to right.

### Logo and icon
- The icon is a ring of six element arcs (Fire, Water, Earth, Wind, Thunder, Chaos, clockwise from the top) around a dark centre, with a cream "AF" monogram (`#FFE8AA`). The background is a dark purple gradient (`#1E0E30` → `#08050E`), with "ALCHEMICAL FUSION" in gold underneath.
- Keep the ring and the monogram as the logo. The ring is the mod's symbol: six elements fused into one.

### Key art brief (for you to make)
- **Mood:** the alchemy of elements. Two elements colliding into a new one, framed by the six-element ring.
- **Strong subjects** (all already in the mod):
  - a wizard at Tomi with a card splitting into two element colours
  - a rival wizard and your Echo facing off
  - the Elemental Conclave's five Zoners around the Archon
  - a Chaos-corrupted Council member
- **Style:** match the game's pixel art and the palette above. Dark backgrounds, so the element colours glow.
- **Sizes worth having:**
  - Thunderstore icon: 256×256 (done)
  - README banner: 1280×400 or 1920×600
  - GitHub social preview: 1280×640
  - a few in-game screenshots or GIFs for the README

---

## 2. Expansion roadmap

Expansions are themed updates built on the systems already in place. Each section lists its ideas with a size.

### 1.1.0 (built, ready to test, folded into 1.1.1) 🧪
- **Nocturne overhaul.** Her shuffle is free (config `Nocturne.Free`), and talking to her opens a preset menu:
  - Full shuffle
  - Arcana only
  - Robe and relic
  - Element build: every arcana from one random element
  - Alchemist: lands on your unlocked infused and dual versions
- **The Primordial Alchemist** (named the White Alchemist until 1.1.1). A godlike manifestation of the essence every element flows from. A new final champion: a pure-white wizard with a permanent aura, every arcana dual-infused, and a "Transmuted" second phase.
  - Beating it always teaches one new infused version of an arcana you carry.
  - The first win gives the **White Mantle**, a new robe in the wardrobe (through LegendAPI, which most mods use for robes): your wizard turns white with the aura, and infused and dual arcana hit 15% harder and cool down 15% faster. Without LegendAPI installed it's worn from the modifiers menu instead.
- **Rival League.** Each element's rival is a named wizard who remembers your record (shown on the intro, and in their greeting) and always wears the same robe.
  - Wins give league points and a rank (Novice → Adept → Duelist → Champion → Grandmaster), shown in the Hub.
  - Higher ranks make Hard rivals likelier and pay a gold bonus per win.
  - The rivals: Ember Vex (Fire), Mira Frostwhisper (Water), Gravel Thorne (Earth), Zephy Quill (Wind), Volta Brash (Thunder), Nyx Havoc (Chaos), Sol Aurelian (Radiant), Iris Spectra (Prism).
- **Tournaments.** A rival portal sometimes holds three rounds against three elements, one difficulty step apart, with a breather and some health between rounds. The prize is a gold chest, and half the time a pre-infused arcana.
- **Team matches.** You and an allied wizard (another league rival, on your side) against two rivals.
- **Bestiary** (in the special mini-boss pool, now 11 fights):
  - **Golem Gauntlet:** the Crush Colossus shakes Golems loose.
  - **Turret Fortress:** a great turret with a ring of four that rebuilds.
  - **Summoner Coven:** coven summoners shield their leader.
  - **Lancer Phalanx:** each armour break sends the phalanx charging.
  - **Archer Volley:** Blinking archers keep repositioning.

### 1.1.1 (built, ready to test) 🧪
- **The Primordial Alchemist** (renamed): introduces itself before the fight with one comment on your arcana (a dual infusion, else an infusion, else your main element or a nudge toward fusion), and speaks at half health and in defeat.
- **Final room on champion runs:** Sura never appears. The torches light, the champion arrives on the empty throne and speaks (Archons as their element), and the whole castle (walk-up, throne room, cosmic arena, lights) takes the champion's colours. NPCs and their wares keep theirs.
- **Archons** look primordial: the whole body in their element's colours, with a permanent aura. The loading board shows a matching recoloured Sura (white for the Alchemist).
- **Team matches:** the biggest arena, a real ally (fights the rivals, the rivals fight it), and all four wizards introduced before anyone moves.
- **Rival League board** at the Hub's wizard statue (Interact, or F2).
- **Bounties** as centred cards; **portal labels** under every mod portal.
- **Nocturne** skips her gem pitch when free. **The merchant** stocks your unlocked infused versions.
- **LegendAPI** is required; WoL Arcanos recommended.
- **Testing:** Alt+1-6 keys (tournament, team, Alchemist final, Archon final, Archon portal, instakill); with the hotkeys on, nothing unlocks for good.
- **Fixes:** the final victory after a champion (softlock), Tomi's book in your house, the bounty on the title screen, robe icons in loadouts, red boxes on elite ghosts and after-images.

### Also in 1.1.1 🧪
- **Primordial robes.** Beating an Archon (in the final room or behind a Hard portal) unlocks its element's robe in the wardrobe. Wearing one gives the Archon's look and aura, and that element's arcana (infused or dual halves count) get stronger:

  | Robe | Arcana | Damage | Cooldown | Status chance |
  |---|---|---|---|---|
  | Primordial Flame Robe | Fire | +20% | −20% | burn +15% |
  | Primordial Tide Robe | Water and Ice | +15% | −20% | slow / freeze +20% |
  | Primordial Stone Robe | Earth | +25% | −15% | poison +15% |
  | Primordial Gale Robe | Wind | +15% | −25% | slow +10% |
  | Primordial Storm Robe | Thunder | +25% | −15% | shock +15% |
  | Primordial Chaos Robe | Chaos | +20% | −20% | chaos +15% |
  | Primordial Dawn Robe | Radiant (Arcanos) | +20% | −20% | |
  | Primordial Prism Robe | Prism (Arcanos) | +20% | −20% | |
- **Grandmaster's Robe.** Reaching the league's top rank (Grandmaster) unlocks it: +30% damage to wizards (rivals, the Echo, Archons, the Primordial Alchemist, the Council and Sura), with a gold aura.
- **Alternate endings.** Sura keeps the standard ending. After an Archon or the Primordial Alchemist, its last words play in the recoloured castle, followed by an ending card of its own ("The First Fire Returns", "The Primordial Return"...). Then the exit portal and the game's ending scene as usual.

- **Primordial true form.** With every Primordial robe (the six base elements, plus Radiant with Arcanos), the Alchemist gets a third phase at 25% health: it heals to 60% and cycles through each Archon's colours, aura and two of its arcana, one form every 9 seconds.
- **Tomix, the Mixing Tome** (1.1.2). Tomi's gold cousin, a recoloured spellbook with his own lines: beside Tomi in the Hub (versions, and his journal with one page per element: infused versions and dual pairs) and at the merchant in runs (infusing). Tomi's book is the game's again.
- **Skippable conversations** (1.1.2). Champions stay put, you're paused, Confirm / Interact or Space / Enter advances.
- **Gear reactions.** Rivals (after their greeting), Archons and the Alchemist (in their intro) comment on your Primordial robe, the White Mantle, the Grandmaster's Robe or your Grandmaster rank.
- **Hit flair.** Elemental ghouls, blobs and golems (and elemental Coffin Knights / Cyclops) throw a small burst of their element when they hit you.
- **Council portraits** on the loading board are recoloured onto their element's or variant's ramp.

### To be discussed 💬
Future ideas, none scheduled. Sizes are rough (S / M / L).
- **Fused signatures (M).** Firing a dual arcana's signature triggers both elements together for one big effect. (Everyday element combos are already covered by WoL Arcanos' ailments.)
- **Rival League extras (M–L):**
  - a standalone league / tournament mode in the game's PvP arena (today the league ranks up from every rival fight inside runs: duels, tournaments, team matches)
  - rival duos that fuse attacks
  - Echo upgrades (inherits your most-killed elite trait)
- **Council Reforged (L):**
  - Council members summoning a special you beat this run
  - fused Council phases
  - Sura cycling the elements you infused most
- **Bestiary extras (M):**
  - elite pairs at high loops
  - Zoners, Golems and Turrets taking stage variants
- **Records and presentation (M):**
  - fight records in the Hub
  - a run summary card
  - modifier presets
  - support for translations
- **Relic fusion at Tomi (L)** and an **alchemist's journal (S)**.

### Set aside ⏸
- Variant stage hazards (stages are built around their own hazards).
- An ailment bridge to WoL Arcanos (its ailments are tied to specific arcana, not elements).

---

## 3. To confirm in play
- Special mini-bosses count for clearing the stage (especially the Conclave's hidden Archon); the five new Bestiary fights behave (turrets and golems as bosses, Blinking archers).
- The Eye Court's orbs circle smoothly; the Large Mimic behaves outside chest rooms.
- Gold economy: elite gold, thieves, specials, bounties, trials, league bonuses and tournament prizes, against infusion costs.
- Anchored's pull feels fair; rival voice lines' timing; dual icons read well at small sizes.
- Real relics on rivals all activate cleanly (watch the log for "could not wear").
- 1.1.x checks:
  - **Nocturne:** the menu opens straight away when free; each preset changes the shuffle as described.
  - **Primordial Alchemist and Mantle:** white body and aura; one comment in its intro; the Mantle whitens your wizard and its cooldown bonus takes effect.
  - **Final room:** no Sura or Sura bar at any point; the castle recolour looks right (white floors not too bright) and doesn't hitch (log `ARENA tint ... ms`); the victory and ending play after a champion (log `FINAL victory declared`).
  - **Team match:** the ally attacks and is attacked; the intro shows all four before the fight.
  - **League board:** the prompt shows at the central statue.
  - **Robe icons:** in the robe's colours (log `ROBE icon from ...`).
  - **Elite ghosts / after-images:** no box, and they still fade.
  - **Primordial robes:** unlock after an Archon (log `ROBES unlocked`), appear in the wardrobe, recolour you with an aura; the element's arcana cool down faster and hit harder.
  - **Grandmaster's Robe:** in the wardrobe at Grandmaster; extra damage on rivals, the Council and Sura only.
  - **True form:** at 25% after the Transmuted phase, only with every robe (Alt+7 to test); forms change every 9s with their colours and arcana (log `ALCHEMIST form:`).
  - **Tomix:** appears beside Tomi in the Hub and at the merchant in runs (log `TOMIX is here`), gold, name over his head; his book infuses / switches versions, Tomi's doesn't; the journal opens with J / pad Interact and pages work on a pad.
  - **Conversations:** you're paused, Confirm skips; no attacks during the Alchemist's intro.
  - **Epilogues:** the last words and the ending card after a champion (log `FINAL epilogue`), then the exit portal works.

---
## 4. What shipped in 1.0.0

- **Enemies:**
  - ✅ elemental giants
  - 🧪 elemental Coffin Knights, Cyclops, blobs and ghouls
  - 🧪 twelve elite traits with gold by risk
  - 🧪 six special mini-bosses (Giant Cyclops, Mimic King, Cyclops Eye Court, Elemental Conclave, Hive Mother, Gravewarden) with armour and stun phases, matching the stage
  - 🧪 enemies casting real player arcana
- **Stages:**
  - 🧪 Chaos stages
  - 🧪 Radiant and Prism stages with WoL Arcanos
  - 🧪 the tier's Council member corrupted to match
- **Rivals:**
  - ✅ rival wizards in starting-room portals, with difficulty that scales with the run
  - 🧪 the Echo of your last run
  - 🧪 rematches
  - 🧪 Archons and dual-infused arcana
  - 🧪 every relic, cursed included (holder-only ones run for real)
  - 🧪 voice lines
- **Final boss:**
  - 🧪 elemental Archons and the Chaos Sovereign in place of Sura
  - 🧪 corrupted and infused Council members
- **Infusion:**
  - 🧪 Tomi at the merchant, with shelf sacrifices
  - 🧪 dual infusion
  - 🧪 permanent unlocks with original-element pairs
  - 🧪 random run duals and Nocturne's shuffle
  - 🧪 rare pre-infused drops and stock
  - 🧪 infusion trials
- **Run extras:**
  - 🧪 bounties
  - 🧪 the modifiers menu (F1 in the Hub)
- **WoL Arcanos (optional):** Radiant and Prism content, enemy versions of its arcana, its unlock rules respected, its icons and effects reused.

---

## 5. Test hotkeys (config Debug.Hotkeys = true)

| Key | What it does |
|---|---|
| F1 (Hub) | Modifiers menu (always available, not a debug key) |
| F2 (Hub) | Rival League board (also: Interact at the big wizard statue; not a debug key) |
| F5 | List enemies (log) |
| Ctrl+F5 | Spawn the next special mini-boss beside you (cycles all eleven) |
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
| Alt+F9 | Rival tournament portal beside you |
| Alt+F10 | Echo of your last run, beside you |
| Alt+F11 | Team match portal beside you |
| Alt+F12 | Jump to the final fight against the Primordial Alchemist |
| Alt+1 / Alt+2 | Tournament / team match portal beside you (Alt+F9 is often taken by NVIDIA's recorder) |
| Alt+3 | Jump to the final fight against the Primordial Alchemist |
| Alt+4 | Jump to the final fight against an Archon (or the Chaos Sovereign) |
| Alt+5 | Hard rival portal that always holds an Archon |
| Alt+6 | Instakill on / off (every enemy at 1 HP) |
| Alt+7 | Every Primordial robe for this session, then the Primordial Alchemist final (its true form) |
| (any, hotkeys on) | Test mode: no unlocks are saved this session (mod progress files, the game's arcana unlocks, the final Chaos arcana) |
| F11 | Rival portal beside you (cycles elements) |
| Shift+F11 | Toggle the variant on the current stage |
| Ctrl+F11 | Skip to the next stage |
| Ctrl+Shift+F11 | Record your current loadout as the Echo |
| F12 | Final champion test |
| Ctrl+F12 | Jump to this tier's Council fight |
| Shift+F12 | Cycle the Council mode (normal / variant / infused) |
| Ctrl+Shift+F12 | Jump to the final fight |
| F6 / F7 / F9 / Ctrl+F9 | Developer dumps: palette cycle, nearest enemy, element palettes, arcana visuals |
