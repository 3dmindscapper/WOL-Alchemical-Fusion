# WOL Alchemical Fusion

A run-changing content mod for Wizard of Legend: elemental bosses, rival wizards, Chaos stages, elites, new mini-boss fights and an arcana infusion system, all built from the game's own enemies, arcana and art.

## Features

### Bosses and enemies
- **Elemental giants.** Mini-bosses take the element of the stage they're on: its colours, borrowed elemental skills and real player arcana, with an intro card and a boss health bar.
- **Special mini-bosses.** Sometimes a mini-boss is replaced by one of six new fights: the **Giant Cyclops**, the **Mimic King**, the **Cyclops Eye Court**, the **Elemental Conclave**, the **Hive Mother** and the **Gravewarden**. They have armour that breaks into stun phases, and they match the stage's element.
- **Elemental enemies.** Coffin Knights, Cyclops, blobs and ghouls get elemental versions on elemental stages.
- **Elites.** Twelve traits that change how you fight an enemy: Shielded, Hasted, Splitting, Draining, Blinking, Volatile, Vengeful, Haunting, Anchored, Overcharged, Infused, Thief. Riskier elites drop more gold.
- **Final champions.** The final room sometimes holds an elemental Archon or the Chaos Sovereign instead of Sura.

### Rival wizards
- A **rival portal** can open in a stage's starting room. Inside is a wizard with its own robe, relics, arcana and signature, who talks back.
- The **Echo of your last run**: sometimes the rival is your previous run's build, infusions and all.
- **Rematches**: a beaten rival may return later in the run, tougher, with infused arcana and a chance to drop one.

### Stages
- **Chaos stages** recolour the stage, convert its enemies and giants, raise rewards and corrupt that section's Council member.
- With **WoL Arcanos**, stages can also be **Radiant** or **Prism**.

### Infusion
- **Tomi comes to the arcana merchant** during runs. In his spellbook, keep one arcana and sacrifice another (or buy a card off the shelf to sacrifice). The kept arcana takes the sacrificed one's element: damage type, status effect, +10% damage, and its animation, icon and card in that element's colours.
- **Dual infusion**: later in a run, an infused arcana can take a second element. Its casts alternate between the two.
- **Unlocks**: every infused version you make stays unlocked. In the Hub, Tomi lets you choose each arcana's version: the original, a single infused version, or a dual pair (the arcana's own element counts).
- **Random versions**: runs sometimes roll a dual version for you, and **Nocturne's shuffle** can roll any version you've unlocked.
- **Infusion trials**: three waves with all your arcana forced into one element, for a pre-infused reward.

### Run extras
- **Bounties**: one per run, shown in the Hub, with a gold reward.
- **Modifiers menu**: press **F1** in the Hub (controller: **Skill4** while Tomi's spellbook is open) to switch systems on or off and tune their chances. No config file editing needed, which makes it handy on the Steam Deck.

## Installation
Install with **r2modman** or the **Thunderstore Mod Manager**. Manual install: put `WolHybridBosses.dll` in `BepInEx/plugins`.

Requires **BepInExPack of Legend**. **WoL Arcanos** is optional: with it installed, Radiant and Prism content turns on automatically, and it respects Arcanos' own unlock rules.

## Controls
- **Infusing at Tomi**: press **I** on the keyboard. On a controller, press any of the **Skill1 / Skill2 / Skill3** arcana buttons (changeable in the config, `Infusion.Buttons`).
- **Modifiers menu**: F1 in the Hub, or Skill4 while Tomi's spellbook is open.
- **Test hotkeys** are off by default. Turn on `Debug.Hotkeys` in the config to use them; the list is in the project's ROADMAP.md.

## Files it creates
Both files are in `BepInEx/config`:
- **`osi.wol.hybridbosses.infusions.txt`**: your unlocked infused versions and Hub choices.
- **`osi.wol.hybridbosses.echo.txt`**: your last run, for the Echo.

Delete either one to reset it.

## Credits
- Built on the game's own enemies, arcana, relics and art (nothing from the game is redistributed).
- Optional integration with **WoL Arcanos** by Nahuelx32: Radiant and Prism elements, its icons and effects, and its unlocks.

## Feedback
Bugs and ideas: open an issue on the GitHub page. The BepInEx log (`BepInEx/LogOutput.log`) helps a lot. The mod's lines are tagged RIVAL, ELITE, MINIBOSS, INFUSION, BOUNTY, REMATCH and so on.
