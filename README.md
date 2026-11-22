# WOL Alchemical Fusion

A run-changing content mod for Wizard of Legend: elemental bosses, rival wizards, Chaos stages, elites, new mini-boss fights and an arcana infusion system, all built from the game's own enemies, arcana and art.

## Features

### Bosses and enemies
- **Elemental giants.** Mini-bosses take the element of the stage they're on: its colours, borrowed elemental skills and real player arcana, with an intro card and a boss health bar.
- **Special mini-bosses.** Sometimes a mini-boss is replaced by one of eleven fights: the **Giant Cyclops**, the **Mimic King**, the **Cyclops Eye Court**, the **Elemental Conclave**, the **Hive Mother**, the **Gravewarden**, the **Golem Gauntlet**, the **Turret Fortress**, the **Summoner Coven**, the **Lancer Phalanx** and the **Archer Volley**. They have armour that breaks into stun phases, and they match the stage's element.
- **Elemental enemies.** Coffin Knights, Cyclops, blobs and ghouls get elemental versions on elemental stages, with a burst of their element when they hit you.
- **Elites.** Twelve traits that change how you fight an enemy: Shielded, Hasted, Splitting, Draining, Blinking, Volatile, Vengeful, Haunting, Anchored, Overcharged, Infused, Thief. Riskier elites drop more gold.
- **Final champions.** The final room sometimes holds a primordial Archon, the Chaos Sovereign or **the Primordial Alchemist** instead of Sura: an entrance of its own, the whole castle in its colours, spoken lines, a second phase and its own ending.

### Rival wizards
- A **rival portal** can open in a stage's starting room. Inside is a wizard with its own robe, relics, arcana and signature, who talks back.
- The **Echo of your last run**: sometimes the rival is your previous run's build, infusions and all.
- **Rematches**: a beaten rival may return later in the run, tougher, with infused arcana and a chance to drop one.
- **Tournaments and team matches**: some portals hold three rounds in a row, or a 2-vs-2 with an allied wizard.
- **Rival League**: each element's rival is a named wizard who remembers your record. Wins earn league points and ranks, and the top rank unlocks the **Grandmaster's Robe** (+30% damage to wizards).

### Stages
- **Chaos stages** recolour the stage, convert its enemies and giants, raise rewards and corrupt that section's Council member.
- With **WoL Arcanos**, stages can also be **Radiant** or **Prism**.

### Infusion
- **Tomix, the Mixing Tome** (Tomi's gold cousin) comes to the arcana merchant during runs. In his spellbook, keep one arcana and sacrifice another (or buy a card off the shelf to sacrifice). The kept arcana takes the sacrificed one's element: damage type, status effect, +10% damage, and its animation, icon and card in that element's colours.
- **Dual infusion**: later in a run, an infused arcana can take a second element. Its casts alternate between the two.
- **Unlocks**: every infused version you make stays unlocked. In the Hub, Tomix floats across from Tomi on the matching planter: his book lets you choose each arcana's version: the original, a single infused version, or a dual pair (the arcana's own element counts).
- **Random versions**: runs sometimes roll a dual version for you, and **Nocturne's shuffle** (free, with presets: full, arcana only, robe and relic, one element, alchemist) can roll any version you've unlocked.
- **Infusion trials**: three waves with all your arcana forced into one element, for a pre-infused reward.
- **The arcana merchant** often sells arcana you've unlocked infused versions of as one of those versions.

### Robes (in the wardrobe, through LegendAPI)
- **White Mantle**: beat the Primordial Alchemist. Infused and dual arcana hit harder and cool down faster.
- **Primordial robes**: beat each element's Archon. Its look and aura, and that element's arcana hit harder, cool down faster and inflict their status more often. Own them all and the Primordial Alchemist shows its **true form**.
- Rivals and champions notice what you wear.
- **Grandmaster's Robe**: reach the league's top rank.

### Run extras
- **Bounties**: one per run, announced in the Hub and at the start of the run, with a gold reward.
- **Rival League board**: walk up to the big wizard statue in the Hub and press Interact (or F2) for your rank, your record against each rival, your robes and the next bounty.
- **Tomix's journal**: with his book open, press J (pad: Interact) for your infused versions and dual pairs, one page per element.
- **Modifiers menu**: press **F1** in the Hub (controller: **Skill4** while Tomi's spellbook is open) to switch systems on or off and tune their chances. No config file editing needed, which makes it handy on the Steam Deck.

## Installation
Install with **r2modman** or the **Thunderstore Mod Manager**. Manual install: put `WolHybridBosses.dll` in `BepInEx/plugins`.

Requires **BepInExPack of Legend** and **LegendAPI** (installed automatically). **WoL Arcanos** is recommended: with it installed, Radiant and Prism content turns on automatically, and it respects Arcanos' own unlock rules.

## Controls
- **Infusing at Tomix**: press **I** on the keyboard. On a controller, press any of the **Skill1 / Skill2 / Skill3** arcana buttons (changeable in the config, `Infusion.Buttons`).
- **Modifiers menu**: F1 in the Hub, or Skill4 while Tomi's spellbook is open.
- **Rival League board**: Interact at the big wizard statue in the Hub, or F2.
- **Tomix's journal**: J, or Interact on a pad, while his book is open.
- **Champion conversations**: Confirm / Interact (pad) or Space / Enter to go to the next line.
- **Test hotkeys** are off by default. Turn on `Debug.Hotkeys` in the config to use them; the list is in the project's ROADMAP.md.

## Files it creates
All are in `BepInEx/config`:
- **`osi.wol.hybridbosses.infusions.txt`**: your unlocked infused versions and Hub choices.
- **`osi.wol.hybridbosses.echo.txt`**: your last run, for the Echo.
- **`osi.wol.hybridbosses.progress.txt`**: league records and rank, Alchemist wins, robe unlocks.

Delete one to reset it. With the test hotkeys on, nothing is saved to them.

## Credits
- Built on the game's own enemies, arcana, relics and art (nothing from the game is redistributed).
- Recommended: integration with **WoL Arcanos** by Nahuelx32: Radiant and Prism elements, its icons and effects, and its unlocks.

## Feedback
Bugs and ideas: open an issue on the GitHub page. The BepInEx log (`BepInEx/LogOutput.log`) helps a lot. The mod's lines are tagged RIVAL, ELITE, MINIBOSS, INFUSION, BOUNTY, REMATCH and so on.
