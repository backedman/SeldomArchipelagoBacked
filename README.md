# Usage
To play using this branch, you must generate and host a game **using the packaged `.apworld` found on the releases page**, and subscribe to (or manually install) the corresponding tModLoader mods.
**The workshop mod will NOT work with a YAML generated from the website.**
## Generating a Game
This process is identical to that of most custom games:
1. Download the .apworld and place it in the `custom_worlds` directory of your Archipelago installation.
2. Launch `ArchipelagoLauncher.exe` and click `Generate Template YAMLs` in the menu.
- This should open the `Players/Templates` folder that should contain a new `Terraria Beta.yaml` file for configuration.
3. Configure this file, save a copy, and place it in the `Players` directory, along with whichever `.yaml` files you want to generate with.
4. Navigate back to `ArchipelagoLauncher.exe` and click `Generate`.
5. Find the resulting `.zip` file in the `output` directory. From there, you can host the game easily on `archipelago.gg`.
## Installing the Mod
The simplest way to install the mod is to subscribe to it [here](https://steamcommunity.com/sharedfiles/filedetails/?id=3676602360).
Alternatively, you can download the `SeldomDespArchipelago.tmod` file in the corresponding release and downpatch as specified in the instructions below.
## Connecting to a Game
1. Launch tModLoader, navigate to Workshop > Manage Mods, and check to see if Archipelago Randomizer (BETA) is properly installed.
2. Configure the server address, port, slot/player name, password as necessary, and the rest to your liking.
3. Launch a world (creating a new one is recommended). If the chat displays "Archipelago is Active," you are connected successfully!
## Downpatching Content Mods
Sometimes content mods receive updates that impact randomizer logic overnight and are thus no longer compatible with a multiworld. If this happens in the midst of a playthrough, you will need to downpatch.
### Via modpack
**Currently recommended.**
1. Download the modpack attached to the corresponding release and extract the contents to `C:/Users/YourPCName/Documents/My Games/Terraria/tModLoader/Mods/ModPacks`.
    - You can also manually locate this folder by opening tModLoader, navigating to Workshop > Mod Packs, and clicking 'Open Mod Pack Folder'.
2. Load tModLoader and navigate to your Mod Packs. Locate the pack you just installed and click 'Import Pack (Local)'.
3. Navigate to Workshop > Manage Mods to ensure the mods are being loaded from the pack instance. If it has a light blue highlight, that means it worked.
### Via .tmod file
1. Unsubscribe from the mod you want to downpatch on the Workshop.
2. Download the .tmod for the content mod version you need (pins in Archipelago discord's #terraria channel) and place it in `C:/Users/YourPCName/Documents/My Games/Terraria/tModLoader/Mods`.
    - You can also manually locate this folder by opening tModLoader, navigating to Workshop > Manage Mods, and clicking 'Open Mods Folder'. *This will open two folders; ignore the one that shows a bunch of numbered folders.*
3. Load tModLoader. If you are prompted to redownload lost mods or reinstall dependencies, click 'Continue Anyway.'
4. Navigate to Workshop > Manage Mods to ensure the mods are being loaded locally. If it has a purple highlight, that means it worked.

# About This Fork

This is a public beta for some features that may one day be included in Terraria's core Archipelago implementation.

Built off of the [official Archipelago branch](https://github.com/Seldom-SE/archipelago_terraria_client), developed and managed by Seldom.
If you're unclear about what Archipelago is, check out that page's README.

## Currently Implemented
For in-depth information on what each feature does, scroll down to the **Feature Explanation** section.
### Major Features
- Compatibility with latest version of Calamity (2.2.4)
- Compatibility with latest version of Fargo Souls (1.7.3.9)
  - Includes new Fargo-themed goals
- NPC randomization (Vanilla Town NPCs only, no pets)
- Wall of Flesh + Princess goals
  - Additional configuration for randomizing checks after the set goal
- Additional achievement classifications
- Shimmer logic toggle
- Health logic
- YAML-less UT Support
- Spoiler log compression
- Advanced configuration for manual flag activation (building off of Hardmode Starter)
- Advanced configuration for AP chat
### Minor Features
- Fixed early activation of "Begone, Evil!"
- Mute achievements on world load
# Feature Explanation
## Content Mod Integration
You can choose to enable certain community-made content mods in your `.yaml` prior to generating a multiworld.
Currently supported are:
- Calamity
- Fargo's Souls
### Calamity
This integration is mostly identical to core:
- Randomizes Calamity boss/event flags
- Adds Calamity items and accessories to the reward pool
- Features several goals:
  - Calamitas Clone
  - Astrum Deus
  - Providence
  - Devourer of Gods
  - Yharon
  - Final Bosses (Supreme Calamitas, Exo Mechs)
  - Boss Rush
In addition to these, **Health Logic** has been added as an option in the `.yaml` (see below).
#### Workshop Link
https://steamcommunity.com/sharedfiles/filedetails/?id=2824688072
### Fargo's Souls
- Randomizes Fargo boss/event flags
- Adds Fargo items and accessories to the reward pool
- Features several goals:
  - Eridanus
  - Abominationn
  - Mutant
  - Soul of Eternity
  - Deviant
    - NOTICE: This goal is achievable early and intended only for use in very short syncs.

Incompatible with the getfixedboi option.
#### Workshop Link
https://steamcommunity.com/sharedfiles/filedetails/?id=2815540735
### Calamity + Fargo
Both the Calamity and Fargo implementation can be enabled simultaneously.
This requires usage of the Calamity - Fargo's Soul DLC.
#### Workshop Link
https://steamcommunity.com/sharedfiles/filedetails/?id=3044249615
## NPC Randomization
NPC Randomization is a setting you can enable in your `.yaml` prior to generating a multiworld.
If enabled: 
- The ability for each vanilla town NPC to move in becomes an item.
- Finding and talking to a rescuable NPC (Sleeping Angler, Bound Goblin, etc.) is a location.
- Meeting the conditions for an NPC to move in (barring receiving them as an AP item) and speaking to the resultant Ghost NPC is a location.
As of now, these only apply to vanilla Town NPCs that are not pets or wandering merchants.
### Notes
For a ghost or rescuable NPC to spawn, you need to meet the conditions that it would normally spawn under in an Archipelago randomizer.
For example, the Clothier ghost will move in after receiving the Post-Skeletron item, *not* after defeating Skeletron.
The Bound Tinkerer will appear after receiving Post-Goblin Army, *not* after defeating it in-game.

A real NPC will only move in if the corresponding item is received (you need the "Merchant" item for the Merchant to move in, etc.)

Both the real Truffle and the ghost Truffle will only spawn in a glowing mushroom field.

Incompatible with the getfixedboi option.
## New goals
These are additional goals you can enable in your `.yaml` prior to generating a multiworld.
- Wall of Flesh
- Princess
### Wall of Flesh
Victory is earned once the Wall of Flesh is summoned and defeated.
Note that this is intended for use with NPC randomization. It is strongly recommended you do not enable this goal with NPC randomization disabled, as this will result in an immediately beatable game.
### Princess
Victory is earned once the requirements for the Princess is met (all Vanilla town NPCs move in besides Santa Claus & Town Pets).
This goal randomizes everything up to and including Plantera. While created with NPC Randomization in mind, it is also perfectly playable otherwise.

Incompatible with the getfixedboi option.
## "Shuffle To" Configuration
This option is present in your `.yaml` and allows you to shuffle checks *past* your set goal.
For example, setting it to 'Moon Lord' will randomize all checks up to Moon Lord, even if an earlier boss is chosen for the goal.
### Notes
- Certain configurations may result in 'post-game' checks, or checks that are only accessible after the goal condition is reached.
- This option can *not* be set to a boss earlier than the chosen goal. If it is, the generator will simply ignore this option.
## Additional Achievement Classifications
The following achievement classifications are added and can be toggled in the `.yaml`:
- Rare: Achievements involving rare enemies/drops
- Time: Achievements based on random time-based events
- Crafting: Achievements dedicated to crafting complex items
### Notes
Some achievements may fall into multiple categories.
- For example, 'The Great Slime Mitosis' will only be shuffled if both 'Rare' and 'Time' are enabled, as it requires both the spawn of a rare NPC (mystic frog for mystic slime) and the occurance of a random event (natural party for cool slime).

Additionally, the logic for some achievements may change depending on which categories are enabled.
- For example, 'Kill the Sun' requires access to a solar tablet *unless* 'Time' is enabled, in which case it only requires the conditions for a naturally spawning eclipse to be met.
## Shimmer Logic Toggle
Sequence breaks involving shimmer transmutations can now be disabled or enabled in the `.yaml`.
If disabled, all transmutations or decrafting using Shimmer are disregarded in logic.
### Notes
This option only covers optional transmutations via Shimmer. The player may still be expected to utilize the Shimmer when mandatory, i.e. transmuting a sparkle slime balloon into the diva slime.
## Health Logic
The randomizer considers a maximum health requirement for each boss, invasion, and event, requiring access to prior health upgrades depending on their intended stage of progression in vanilla.
### Notes
- This option is intended for use with Calamity, and guarantees that early bosses like Leviathan & Anahita and Primordial Wyrm will not have to be fought with extremely inadequate gear.
  - It is ON by default.
- All amounts are based off [the walkthrough from the official Calamity wiki](https://calamitymod.wiki.gg/wiki/Guide:Mod_progression).
- Currently, each boss's health requirement cannot be fine-tuned. However, there is an additional handicap setting that allows you to decrease how many different types of max HP upgrades are needed for each boss.
  - For example, with Calamity enabled, setting the handicap to -2 would remove both the Sanguine Tangerine and Life Fruit requirement for Plantera, while Yharon would only require Life Crystals, Fruits, and one Calamity health upgrade (chosen between Sanguine Tangerine, Miracle Fruit, or Tainted Cloudberry)
- If this option is enabled, UT will show a boss/event as glitched logic if they could otherwise be fought barring the health requirement.
## YAML-less UT Support
No YAML is required to connect the Universal Tracker to a Terraria Beta slot.
## Spoiler log compression
The YAML option 'Compressed Playthrough', when enabled, hides events from the multiworld spoiler log's playthrough.
## Advanced Manual Flag Configuration
Accessible through the client-side mod's config.
This expands upon the "Hardmode Starter" option in stable by allowing all vanilla & Calamity flags to be set for manual activation.
Setting a flag for manual activation means that once you receive the item, you gain the ability to trigger the corresponding item instead of it activating manually.
### Notes
**This option allows you to opt out of receiving flags like Hardmode too early and making early-game difficult. Please make sure to configure it before entering a world.**
Some good examples of flags to set as manual are Hardmode, Post-Plantera (for the dungeon changes especially) and Post-Moon Lord (which, in Calamity, adds incredibly tough enemies to the Hallow and Underworld).
## Advanced AP Chat Configuration
Accessible through the client-side mod's config.
Provides the ability to toggle the color and visibility of AP messages unrelated to your world.
# Credits
## Seldom
- Original mod creator, responsible for base compatibility + all features not mentioned
## Desperandos
- Calamity compatibility (2.2.0 - Present)
- NPC Randomization
- WoF + Princess goals
- 'Shuffle Up To' config
- Achievement classifications
- Shimmer + Health logic
- Manual Flag configuration
- AP Chat configuration
## @zuils
- Fargo's Souls integration
- Cross-mod compatibility with Calamity and Fargo's
- Enhanced compatibility with UT
## @Crackminer
- Calamity compatibility (2.1.2)
## @GMFelixfex
- Major bugfix for NPC Randomization
# License

Archipelago Terraria Client is licensed under MIT. The purple and white Archipelago logo was created by this repo's owner (Desperandos). The icon and collection button image used by this
mod are modified versions of the Archipelago logo, made to fit Terraria's style.

