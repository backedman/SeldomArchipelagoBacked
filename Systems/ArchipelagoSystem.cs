using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using Newtonsoft.Json.Linq;
using SeldomDespArchipelago.Players;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Social;
using Terraria.WorldBuilding;
using SeldomDespArchipelago.FlagItem;
using System.Linq;
using SeldomDespArchipelago.NPCs;
using System.Formats.Tar;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System.Diagnostics.Metrics;
using Terraria.GameContent.UI.States;
using Archipelago.MultiClient.Net.MessageLog.Parts;
using Terraria.ModLoader.Config;
using System.Text;
using Archipelago.MultiClient.Net.Helpers;
using System.Data;
using System.Security.Cryptography.X509Certificates;

namespace SeldomDespArchipelago.Systems
{
    class ArchipelagoSystem : ModSystem
    {
        private static readonly Dictionary<string, int> SpecialMappings = new Dictionary<string, int>
        {
            { "Aerial Bane", ItemID.DD2BetsyBow },
            { "Amber Horseshoe Balloon", ItemID.BalloonHorseshoeHoney },
            { "Apprentice's Scarf", ItemID.ApprenticeScarf },
            //{ "Barrel Launcher", ItemID.DeadCellsBarrelLauncher },
            { "Betsy's Wings", ItemID.BetsyWings },
            { "Betsy's Wrath", ItemID.ApprenticeStaffT3 },
            { "Blade Staff", ItemID.Smolstar },
            { "Brand of the Inferno", ItemID.DD2SquireDemonSword },
            { "Celebration Mk2", ItemID.Celeb2 },
            { "Crystal Assassin Hood", ItemID.CrystalNinjaHelmet },
            { "Crystal Assassin Pants", ItemID.CrystalNinjaLeggings },
            { "Crystal Assassin Shirt", ItemID.CrystalNinjaChestplate },
            { "Dark Harvest", ItemID.ScytheWhip },
            { "Durendal", ItemID.SwordWhip },
            //{ "Electric Eel", ItemID.EelWhip },
            { "Empress Wings", ItemID.RainbowWings },
            { "Eventide", ItemID.FairyQueenRangedItem },
            { "Firecracker", ItemID.FireWhip },
            //{ "Flairon", ItemID.FlaironFlail },
            { "Flying Dragon", ItemID.DD2SquireBetsySword },
            { "Ghastly Glaive", ItemID.MonkStaffT2 },
            { "Hallowed Jousting Lance", ItemID.HallowJoustingLance },
            { "Kaleidoscope", ItemID.RainbowWhip },
            { "Luminite", ItemID.LunarOre },
            { "Lunar Flare", ItemID.LunarFlareBook },
            { "Lunar Portal Staff", ItemID.MoonlordTurretStaff },
            { "Monk's Belt", ItemID.MonkBelt },
            //{ "Mud Ball", ItemID.MudBallPlayer },
            //{ "Mushroom Staff", ItemID.MushroomBoi },
            { "Nebula Mantle", ItemID.WingsNebula },
            { "Nightglow", ItemID.FairyQueenMagicItem },
            { "Obsidian Longcoat", ItemID.ObsidianShirt },
            { "Obsidian Outlaw Hat", ItemID.ObsidianHelm },
            { "Pew-matic Horn", ItemID.PewMaticHorn },
            { "Phantom Phoenix", ItemID.DD2PhoenixBow },
            //{ "Possession", ItemID.MoonLordWhip },
            { "Sky Dragon's Fury", ItemID.MonkStaffT3 },
            { "Sleepy Octopod", ItemID.MonkStaffT1 },
            { "Solar Wings", ItemID.WingsSolar },
            { "Spectre Wings", ItemID.GhostWings },
            { "Stardust Plate", ItemID.StardustBreastplate },
            { "Stardust Wings", ItemID.WingsStardust },
            { "Stellar Tune", ItemID.SparkleGuitar },
            { "Sticky Dirt Bomb", ItemID.DirtStickyBomb },
            { "Super Star Shooter", ItemID.SuperStarCannon },
            //{ "Templar's Sling", ItemID.TemplarSling },
            { "Terraprisma", ItemID.EmpressBlade },
            { "The Bee's Knees", ItemID.BeesKnees },
            { "Tome of Infinite Wisdom", ItemID.BookStaff },
            { "Vortex Booster", ItemID.WingsVortex }
            //{ "Vulgar Display of Flower", ItemID.FlowerWhip }
        };

        public readonly Version APversion = new Version(0, 6, 200);
        public const string APWorldName = "Terraria Beta";

        // Data that's reset between worlds
        public class WorldState : TagSerializable
        {
            public static readonly Func<TagCompound, WorldState> DESERIALIZER = LoadFromTagCompound;

            // The slot & multiworld seed the world initialized under.
            // We keep a copy here and in SessionState to prevent unwanted cross-pollination between different saves.
            public string slotName = null;
            public string seed = null;

            // Achievements can be completed while loading into the world, but those complete before
            // `ArchipelagoPlayer::OnEnterWorld`, where achievements are reset, is run. So, this
            // keeps track of which achievements have been completed since `OnWorldLoad` was run, so
            // `ArchipelagoPlayer` knows not to clear them.
            public List<string> achieved = new List<string>();

            // Stores locations that were collected before Archipelago is started so they can be
            // queued once it's started
            public List<string> locationBacklog = new List<string>();

            // Number of items the player has collected in this world
            public int collectedItems;

            // List of rewards received in this world, so they don't get reapplied. Saved in the
            // Terraria world instead of Archipelago data in case the player is, for example,
            // playing Hardcore and wants to receive all the rewards again when making a new player/
            // world.
            public List<int> receivedRewards = new List<int>();

            // List of flags that have been received but not triggered
            public HashSet<string> suspendedFlags = new HashSet<string>();

            // All NPCs that have been randomized.
            public ImmutableHashSet<int> randomizedNPCs = null;

            // Set of town NPC items received in this world. Since this is saved to the world and
            // modded NPC IDs are not stable, the type will need to change if Calamity NPC support
            // is added.
            public HashSet<int> receivedNPCs = new();

            // Contains all ghosts that are available to spawn.
            public Queue<int> ghostNPCqueue = new();

            // Dict of loc npc ids to item npc ids, if a player's npc item happens to be placed in one of their npc locations.
            // If this is the case, we can transform the ghost/bound npc into the item npc as soon as it is activated, for both expediency and cuteness.
            public Dictionary<int, int> npcLocTypeToNpcItemType = null;

            // What the current state of the world is based on the Bosses marked defeated
            public int world_state;

            public bool NPCRandoActive() =>
                !ModContent.GetInstance<Config.Config>().forceOffNPC &&
                randomizedNPCs is not null;

            public TagCompound SerializeData()
            {
                var tag = new TagCompound
                {
                    [nameof(slotName)] = slotName,
                    [nameof(seed)] = seed,
                    [nameof(locationBacklog)] = locationBacklog,
                    [nameof(collectedItems)] = collectedItems,
                    [nameof(receivedRewards)] = receivedRewards,
                    [nameof(suspendedFlags)] = suspendedFlags.ToList(),
                };

                if (NPCRandoActive())
                {
                    tag[nameof(randomizedNPCs)] = randomizedNPCs.ToList();
                    tag[nameof(receivedNPCs)] = receivedNPCs.ToList();
                    tag[nameof(npcLocTypeToNpcItemType) + "Keys"] = npcLocTypeToNpcItemType.Keys.ToList();
                    tag[nameof(npcLocTypeToNpcItemType) + "Values"] = npcLocTypeToNpcItemType.Values.ToList();
                }

                return tag;
            }

            public static WorldState LoadFromTagCompound(TagCompound tag)
            {
                var world = new WorldState();

                world.slotName = tag.GetString(nameof(slotName));
                world.seed = tag.GetString(nameof(seed));
                world.locationBacklog = tag.Get<List<string>>(nameof(locationBacklog));
                world.collectedItems = tag.GetInt(nameof(collectedItems));
                world.receivedRewards = tag.Get<List<int>>(nameof(receivedRewards));
                world.suspendedFlags = tag.Get<List<string>>(nameof(suspendedFlags)).ToHashSet();

                if (tag.TryGet(nameof(randomizedNPCs), out List<int> ranNPC))
                {
                    world.randomizedNPCs = ranNPC.ToImmutableHashSet();
                    world.receivedNPCs = tag.Get<List<int>>(nameof(receivedNPCs)).ToHashSet();

                    var npcKeys = tag.Get<List<int>>(
                        nameof(npcLocTypeToNpcItemType) + "Keys"
                    );

                    var npcValues = tag.Get<List<int>>(
                        nameof(npcLocTypeToNpcItemType) + "Values"
                    );

                    world.npcLocTypeToNpcItemType = npcKeys
                        .Zip(
                            npcValues,
                            (k, v) => new { Key = k, Value = v }
                        )
                        .ToDictionary(x => x.Key, x => x.Value);
                }

                return world;
            }
        }

        // Data that's reset between Archipelago sessions
        public class SessionState
        {
            // The slot & multiworld seed of the currently connected session.
            public string slotName = null;
            public string seed = null;

            public bool calamity = false;
            public bool fargo = false;

            // List of locations that are currently being sent
            public List<Task<Dictionary<long, ScoutedItemInfo>>> locationQueue =
                new List<Task<Dictionary<long, ScoutedItemInfo>>>();

            public ArchipelagoSession session;
            public DeathLinkService deathlink;

            // Like `collectedItems`, but unique to this Archipelago session, and doesn't save, so
            // it starts at 0 each session. While less than `collectedItems`, it discards items
            // instead of collecting them. This is needed bc AP just gives us a list of items that
            // we have, and it's up to us to keep track of which ones we've already applied.
            public int currentItem;

            public List<string> goals = new List<string>();

            public bool victory;
            public int slot;

            // AP slot data used by the Loot Item system.
            //
            // These intentionally belong to SessionState rather than WorldState because they are
            // generated by Archipelago for the current slot/session.
            //
            // rolls:
            //   Predetermined random rolls used to select rarity pools.
            //
            // rarityPools:
            //   The selected Terraria items grouped by rarity. GiveLootItem() consumes these lists
            //   as rewards are received.
            public List<double> rolls = new List<double>();
            public Dictionary<string, List<int>> rarityPools =
                new Dictionary<string, List<int>>();

            public Dictionary<string, int> ItemNameToIdMap = new Dictionary<string, int>();
            public Dictionary<int, string> ItemIdToNameMap = new Dictionary<int, string>();

            
        }

        public WorldState world = new();
        public SessionState session;

        public ConnectStatus status = ConnectStatus.Unset;

        public enum ConnectStatus
        {
            Unset,
            Valid,
            SlotOrSeedMismatch,
            CalamityNeeded,
            NoCalamityNeeded,
            FargoNeeded,
            NoFargoNeeded,
            WrongSlot,
            WrongPass,
            WrongGame,
            ClientOlder,
            ClientNewer,
        }

        // Keeps track of the last APworld version the game tried to connect to for player convenience
        public int[] desiredAPversion = null;

        // Contains ghosts that require special housing conditions to spawn.
        public readonly static ImmutableHashSet<int> specialSpawnGhosts =
        [
            NPCID.Truffle
        ];

        public override void OnWorldLoad()
        {
            // Needed for achievements to work right
            typeof(SocialAPI)
                .GetField("_mode", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, SocialMode.None);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            var config = ModContent.GetInstance<Config.Config>();

            LoginResult result;
            ArchipelagoSession newSession;

            try
            {
                newSession = ArchipelagoSessionFactory.CreateSession(
                    config.address,
                    config.port
                );

                result = newSession.TryConnectAndLogin(
                    APWorldName,
                    config.name,
                    ItemsHandlingFlags.AllItems,
                    APversion,
                    null,
                    null,
                    config.password == "" ? null : config.password
                );

                if (result is LoginFailure failure)
                {
                    var error = failure.ErrorCodes.First();

                    status = error switch
                    {
                        ConnectionRefusedError.InvalidSlot => ConnectStatus.WrongSlot,
                        ConnectionRefusedError.InvalidGame => ConnectStatus.WrongGame,
                        ConnectionRefusedError.IncompatibleVersion => ConnectStatus.ClientOlder,
                        ConnectionRefusedError.InvalidPassword => ConnectStatus.WrongPass,
                        _ => ConnectStatus.Unset,
                    };

                    return;
                }
            }
            catch
            {
                return;
            }

            session = new();
            session.session = newSession;

            var success = (LoginSuccessful)result;

            bool versionSlotData = success.SlotData.TryGetValue(
                "version",
                out var versionObj
            );

            bool newerVersion = false;

            if (versionSlotData)
            {
                desiredAPversion = ((JArray)versionObj).ToObject<int[]>();

                newerVersion =
                    desiredAPversion[0] != APversion.Major ||
                    desiredAPversion[1] != APversion.Minor ||
                    desiredAPversion[2] != APversion.Build;
            }

            if (!versionSlotData || newerVersion)
            {
                status = ConnectStatus.ClientNewer;
                Reset();
                return;
            }

            session.goals = new List<string>(
                ((JArray)success.SlotData["goal"]).ToObject<string[]>()
            );

            // ------------------------------------------------------------
            // LOAD PREDETERMINED LOOT ROLLS
            // ------------------------------------------------------------

            if (success.SlotData.TryGetValue("rolls", out object rollsObj) &&
                rollsObj != null)
            {
                // Safely converts JArray, List<object>, double[], or List<double>
                // into List<double>.
                session.rolls = JArray
                    .FromObject(rollsObj)
                    .ToObject<List<double>>();

                if (session.rolls == null)
                {
                    session.rolls = new List<double>();
                    Mod.Logger.Warn(
                        "[Archipelago] 'rolls' converted to null. Initialized empty list."
                    );
                }

                Mod.Logger.Info(
                    $"[Archipelago] Loaded {session.rolls.Count} rolls successfully."
                );
            }
            else
            {
                session.rolls = new List<double>();

                Mod.Logger.Warn(
                    "[Archipelago] 'rolls' key missing or null in SlotData. " +
                    "Initialized empty list."
                );
            }

            // ------------------------------------------------------------
            // LOAD RARITY POOLS
            // ------------------------------------------------------------

            var rarityPoolNames = ((JObject)success.SlotData["rarity_pools"])
                .ToObject<Dictionary<string, List<string>>>();

            // Thread-safe dictionary to map item names to resolved integer IDs for clientSend / AP sync
            session.ItemNameToIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            session.rarityPools = rarityPoolNames.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(name =>
                {
                    string searchName = name;
                    int resolvedId = 0;

                    // 1. Check if an explicit internal ID exception exists for this item name
                    if (SpecialMappings.TryGetValue(name, out int itemid))
                    {
                        resolvedId = itemid;
                    }
                    // 2. Check if exact name exists in ItemID.Search
                    else if (ItemID.Search.ContainsName(searchName))
                    {
                        resolvedId = ItemID.Search.GetId(searchName);
                    }
                    else
                    {
                        // 3. Fallback: Strip spaces and apostrophes
                        searchName = name.Replace(" ", "").Replace("'", "");
                        if (ItemID.Search.ContainsName(searchName))
                        {
                            resolvedId = ItemID.Search.GetId(searchName);
                        }
                    }

                    // 4. Fallback for Modded Items (via ModLoader)
                    if (resolvedId == 0)
                    {
                        foreach (Mod mod in ModLoader.Mods)
                        {
                            if (mod.TryFind<ModItem>(name, out ModItem modItem) || 
                                mod.TryFind<ModItem>(searchName, out modItem))
                            {
                                resolvedId = modItem.Type;
                                break;
                            }
                        }
                    }

                    // Validate result
                    if (resolvedId <= 0)
                    {
                        Mod.Logger.Warn(
                            $"[Archipelago] FAILED to load item: '{name}' (searched as: '{searchName}')"
                        );
                        return 0;
                    }

                    // Map the item name to its resolved ID for clientSend/session lookups
                    session.ItemNameToIdMap[name] = resolvedId;

                    return resolvedId;
                })
                .Where(id => id > 0) // Exclude failed resolutions
                .ToList()
            );

            // Populate Reverse Lookup Map (ID -> Name) if clientSend requires lookups by ID
            session.ItemIdToNameMap = session.ItemNameToIdMap
                .GroupBy(kvp => kvp.Value)
                .ToDictionary(g => g.Key, g => g.First().Key);
                       // Register every rarity-pool item as a ghost item.
            GhostItemSystem.SetGhostItems(
                session.rarityPools.Values.SelectMany(ids => ids)
            );

            Mod.Logger.Info(
                $"[Archipelago] GhostItemSystem loaded " +
                $"{GhostItemSystem.GhostItemIDs.Count} ghost item IDs!"
            );

            foreach (int id in GhostItemSystem.GhostItemIDs)
            {
                Mod.Logger.Info(
                    $"[Archipelago] Registered Ghost Item ID: " +
                    $"{id} ({ItemID.Search.GetName(id)})"
                );
            }

            session.session.MessageLog.OnMessageReceived += ApMessageToChat;

            if ((bool)success.SlotData["deathlink"])
            {
                session.deathlink = session.session.CreateDeathLinkService();
                session.deathlink.EnableDeathLink();

                session.deathlink.OnDeathLinkReceived += ReceiveDeathlink;
            }

            session.calamity = (long)success.SlotData["calamity"] == 1;
            session.fargo = (long)success.SlotData["fargo"] == 1;

            bool randomizedNPCs =
                (long)success.SlotData["npc_rando"] == 1;

            string[] randomizedNPCnames =
                ((JArray)success.SlotData["randomize_npcs"])
                .ToObject<string[]>();

            if (randomizedNPCs)
            {
                world.randomizedNPCs =
                    (
                        from name in randomizedNPCnames
                        select npcNameToID[name]
                    ).ToImmutableHashSet();

                string[] allNPCnames = npcNameToID.Keys.ToArray();

                var locIDtoNPCname = new Dictionary<long, string>();

                foreach (string loc in allNPCnames)
                {
                    locIDtoNPCname[
                        session.session.Locations.GetLocationIdFromName(
                            APWorldName,
                            loc
                        )
                    ] = loc;
                }

                if (locIDtoNPCname.ContainsKey(-1))
                {
                    throw new Exception(
                        "Some retrieved NPC locations turned up -1 ids."
                    );
                }

                var task = session.session.Locations.ScoutLocationsAsync(
                    locIDtoNPCname.Keys.ToArray()
                );

                if (task.Wait(1000))
                {
                    world.npcLocTypeToNpcItemType = new();

                    int playerID = success.Slot;
                    var npcLocDict = task.Result;

                    foreach (long key in npcLocDict.Keys)
                    {
                        ItemInfo itemInfo = npcLocDict[key];

                        if (
                            itemInfo.Player.Slot == playerID &&
                            allNPCnames.Contains(itemInfo.ItemName)
                        )
                        {
                            int npcType =
                                npcNameToID[locIDtoNPCname[key]];

                            world.npcLocTypeToNpcItemType[npcType] =
                                npcNameToID[itemInfo.ItemName];
                        }
                    }
                }
            }

            session.slot = success.Slot;

            string theSlotName =
                session.session.Players.GetPlayerName(session.slot);

            string theSeed =
                session.session.RoomState.Seed;

            session.slotName = theSlotName;
            world.slotName = theSlotName;

            session.seed = theSeed;
            world.seed = theSeed;

            foreach (var location in world.locationBacklog)
                QueueLocation(location);

            world.locationBacklog.Clear();
        }

        public override void LoadWorldData(TagCompound tag)
        {
            if (
                tag.TryGet<WorldState>("ApWorldData", out var worldData) &&
                worldData.seed != ""
            )
            {
                world = worldData;
            }
        }

        public override void PostWorldLoad()
        {
            if (session is null)
                return;

            if (
                session.slotName != world.slotName ||
                session.seed != world.seed
            )
            {
                status = ConnectStatus.SlotOrSeedMismatch;
                Reset();
                return;
            }

            bool calamityActive = ModLoader.HasMod("CalamityMod");

            if (calamityActive != session.calamity)
            {
                if (calamityActive)
                    status = ConnectStatus.NoCalamityNeeded;
                else
                    status = ConnectStatus.CalamityNeeded;

                Reset();
                return;
            }

            bool fargoActive =
                ModLoader.HasMod("FargowiltasSouls");

            if (fargoActive != session.fargo)
            {
                if (fargoActive)
                    status = ConnectStatus.NoFargoNeeded;
                else
                    status = ConnectStatus.FargoNeeded;

                Reset();
                return;
            }

            status = ConnectStatus.Valid;

            // Refresh suspended flags
            HashSet<string> collectedItems =
                (
                    from item in session.session.Items.AllItemsReceived
                    select item.ItemName
                ).ToHashSet();

            world.suspendedFlags =
                (
                    from flag in flags
                    where collectedItems.Contains(flag) && !CheckFlag(flag)
                    select flag
                ).ToHashSet();

            // Change Guide to Ghost
            bool worldHasGuide =
                !world.NPCRandoActive() ||
                world.receivedNPCs.Contains(NPCID.Guide);

            bool sessHasGuide =
                session is not null &&
                session.session.Items.AllItemsReceived.Any(
                    i => i.ItemName == "Guide"
                );

            if (!worldHasGuide && !sessHasGuide)
            {
                int guideIndex =
                    NPC.FindFirstNPC(NPCID.Guide);

                if (guideIndex != -1)
                {
                    Main.npc[guideIndex]
                        .Transform(ModContent.GetInstance<GhostNPC>().Type);

                    GhostNPC ghost =
                        Main.npc[guideIndex].ModNPC as GhostNPC;

                    ghost.SetGhostType(NPCID.Guide);
                }
            }
        }

        public void ApMessageToChat(LogMessage message)
        {
            var config =
                ModContent.GetInstance<Config.Config>();

            string normalMsg() =>
                string.Concat(
                    from part in message.Parts
                    select part.Text
                );

            string colorMsg()
            {
                if (!config.colorText)
                    return normalMsg();

                StringBuilder builder = new StringBuilder();

                foreach (var part in message.Parts)
                {
                    string msg = part.Text;
                    var color = part.Color;

                    string colorHex =
                        color.R.ToString("X2") +
                        color.G.ToString("X2") +
                        color.B.ToString("X2");

                    builder.Append(
                        $"[c/{colorHex}:{msg}]"
                    );
                }

                return builder.ToString();
            }

            if (config.chatSettings == Config.ChatSetting.Disable)
                return;

            bool thisSlotMentioned = false;
            bool playerPart = false;

            foreach (var part in message.Parts)
            {
                if (part.Type == MessagePartType.Player)
                {
                    playerPart = true;

                    if (part.Text == session.slotName)
                        thisSlotMentioned = true;
                }
            }

            if (playerPart && !thisSlotMentioned)
            {
                switch (config.chatSettings)
                {
                    case Config.ChatSetting.All:
                        Chat(colorMsg());
                        break;

                    case Config.ChatSetting.Grey:
                        Chat(normalMsg(), Color.Gray);
                        break;

                    case Config.ChatSetting.Filter:
                        break;

                    default:
                        throw new Exception(
                            "Unhandled chat configuration"
                        );
                }
            }
            else
            {
                Chat(colorMsg());
            }
        }

        public static string[] flags =
        {
            "Post-Trojan Squirrel",
            "Post-King Slime",
            "Post-Desert Scourge",
            "Post-Giant Clam",
            "Post-Eye of Cthulhu",
            "Post-Cursed Coffin",
            "Post-Acid Rain Tier 1",
            "Post-Crabulon",
            "Post-Evil Boss",
            "Post-Old One's Army Tier 1",
            "Post-Goblin Army",
            "Post-Queen Bee",
            "Post-The Hive Mind",
            "Post-The Perforators",
            "Post-Skeletron",
            "Post-Deerclops",
            "Post-The Slime God",
            "Post-Deviantt",
            "Hardmode",
            "Post-Dreadnautilus",
            "Post-Hardmode Giant Clam",
            "Post-Pirate Invasion",
            "Post-Queen Slime",
            "Post-Banished Baron",
            "Post-Aquatic Scourge",
            "Post-Cragmaw Mire",
            "Post-Acid Rain Tier 2",
            "Post-The Twins",
            "Post-Old One's Army Tier 2",
            "Post-Brimstone Elemental",
            "Post-The Destroyer",
            "Post-Cryogen",
            "Post-Skeletron Prime",
            "Post-Lifelight",
            "Post-Calamitas Clone",
            "Post-Plantera",
            "Post-Great Sand Shark",
            "Post-Leviathan and Anahita",
            "Post-Astrum Aureus",
            "Post-Golem",
            "Post-Old One's Army Tier 3",
            "Post-Martian Madness",
            "Post-The Plaguebringer Goliath",
            "Post-Duke Fishron",
            "Post-Mourning Wood",
            "Post-Pumpking",
            "Post-Everscream",
            "Post-Santa-NK1",
            "Post-Ice Queen",
            "Post-Frost Legion",
            "Post-Ravager",
            "Post-Empress of Light",
            "Post-Betsy",
            "Post-Lunatic Cultist",
            "Post-Astrum Deus",
            "Post-Lunar Events",
            "Post-Moon Lord",
            "Post-Profaned Guardians",
            "Post-Dragonfolly",
            "Post-Providence, the Profaned Goddess",
            "Post-Champion of Timber",
            "Post-Champion of Terra",
            "Post-Champion of Earth",
            "Post-Champion of Nature",
            "Post-Champion of Life",
            "Post-Champion of Death",
            "Post-Champion of Spirit",
            "Post-Champion of Will",
            "Post-Storm Weaver",
            "Post-Ceaseless Void",
            "Post-Signus, Envoy of the Devourer",
            "Post-Polterghast",
            "Post-Mauler",
            "Post-Nuclear Terror",
            "Post-The Old Duke",
            "Post-The Devourer of Gods",
            "Post-Eridanus, Champion of Cosmos",
            "Post-Yharon, Dragon of Rebirth",
            "Post-Abominationn",
            "Post-Exo Mechs",
            "Post-Supreme Witch, Calamitas",
            "Post-Primordial Wyrm",
            "Post-Mutant",
            "Post-Boss Rush"
        };

        public static bool FindFlag(string flag, out string fuzzy)
        {
            fuzzy = null;

            if (flags.Contains(flag))
            {
                return true;
            }
            else
            {
                string lowerItem = flag.ToLower();

                int assumedItemIndex =
                    Array.FindIndex(
                        flags,
                        x => x.ToLower().Contains(lowerItem)
                    );

                if (assumedItemIndex > -1)
                {
                    fuzzy = flags[assumedItemIndex];
                    return true;
                }

                return false;
            }
        }

        public int getRarity()
        {
            int rarity = 0;

            // Rarity 1:
            // #King Slime | #Eye of Cthulhu
            if (NPC.downedSlimeKing || NPC.downedBoss1)
            {
                rarity = 1;
            }

            // Rarity 2:
            // Rarity 1 & #Evil Boss
            if (rarity >= 1 && NPC.downedBoss2)
            {
                rarity = 2;
            }

            // Rarity 3:
            // Rarity 2 & (#Skeletron | #Queen Bee)
            if (
                rarity >= 2 &&
                (NPC.downedBoss3 || NPC.downedQueenBee)
            )
            {
                rarity = 3;
            }

            // Rarity 4:
            // Rarity 3 & #Wall of Flesh
            if (rarity >= 3 && Main.hardMode)
            {
                rarity = 4;
            }

            // Rarity 5:
            // Rarity 4 & (@mech_boss(1) | #Queen Slime)
            if (
                rarity >= 4 &&
                (NPC.downedMechBossAny || NPC.downedQueenSlime)
            )
            {
                rarity = 5;
            }

            // Rarity 7:
            // Rarity 5 & #Plantera
            if (rarity >= 5 && NPC.downedPlantBoss)
            {
                rarity = 7;
            }

            // Rarity 8:
            // Rarity 7 & (#Duke Fishron | #Empress of Light)
            if (
                rarity >= 7 &&
                (NPC.downedFishron || NPC.downedEmpressOfLight)
            )
            {
                rarity = 8;
            }

            // Rarity 10:
            // Rarity 8 & #Moon Lord
            if (rarity >= 8 && NPC.downedMoonlord)
            {
                rarity = 10;
            }

            return rarity;
        }

        public bool CheckFlag(string flag) => flag switch
        {
            "Post-King Slime" => NPC.downedSlimeKing,
            "Post-Eye of Cthulhu" => NPC.downedBoss1,
            "Post-Evil Boss" => NPC.downedBoss2,
            "Post-Old One's Army Tier 1" => DD2Event.DownedInvasionT1,
            "Post-Goblin Army" => NPC.downedGoblins,
            "Post-Queen Bee" => NPC.downedQueenBee,
            "Post-Skeletron" => NPC.downedBoss3,
            "Post-Deerclops" => NPC.downedDeerclops,
            "Hardmode" => Main.hardMode,
            "Post-Pirate Invasion" => NPC.downedPirates,
            "Post-Queen Slime" => NPC.downedQueenSlime,
            "Post-The Twins" => NPC.downedMechBoss2,
            "Post-Old One's Army Tier 2" => DD2Event.DownedInvasionT2,
            "Post-The Destroyer" => NPC.downedMechBoss1,
            "Post-Skeletron Prime" => NPC.downedMechBoss3,
            "Post-Plantera" => NPC.downedPlantBoss,
            "Post-Golem" => NPC.downedGolemBoss,
            "Post-Old One's Army Tier 3" => DD2Event.DownedInvasionT3,
            "Post-Martian Madness" => NPC.downedMartians,
            "Post-Duke Fishron" => NPC.downedFishron,
            "Post-Mourning Wood" => NPC.downedHalloweenTree,
            "Post-Pumpking" => NPC.downedHalloweenKing,
            "Post-Everscream" => NPC.downedChristmasTree,
            "Post-Santa-NK1" => NPC.downedChristmasSantank,
            "Post-Ice Queen" => NPC.downedChristmasIceQueen,
            "Post-Frost Legion" => NPC.downedFrost,
            "Post-Empress of Light" => NPC.downedEmpressOfLight,
            "Post-Lunatic Cultist" => NPC.downedAncientCultist,
            "Post-Lunar Events" => NPC.downedTowerNebula,
            "Post-Moon Lord" => NPC.downedMoonlord,
            _ => CheckModFlag(flag),
        };

        private bool CheckModFlag(string flag)
        {
            bool? result = null;

            if (ModLoader.HasMod("CalamityMod"))
                result = CalamitySystem.CheckCalamityFlag(flag);

            if (
                result is null &&
                ModLoader.HasMod("FargowiltasSouls")
            )
            {
                result = FargoSystem.CheckFargoFlag(flag);
            }

            if (result is null)
            {
                Chat($"Unknown flag: {flag}");
                return false;
            }

            return result.Value;
        }

        public static Dictionary<string, int> npcNameToID = new()
        {
            {"Guide", NPCID.Guide },
            {"Merchant", NPCID.Merchant },
            {"Nurse", NPCID.Nurse },
            {"Demolitionist", NPCID.Demolitionist },
            {"Dye Trader", NPCID.DyeTrader },
            {"Angler", NPCID.Angler },
            {"Zoologist", NPCID.BestiaryGirl },
            {"Dryad", NPCID.Dryad },
            {"Painter", NPCID.Painter },
            {"Golfer", NPCID.Golfer },
            {"Arms Dealer", NPCID.ArmsDealer },
            {"Tavernkeep", NPCID.DD2Bartender },
            {"Stylist", NPCID.Stylist },
            {"Goblin Tinkerer", NPCID.GoblinTinkerer },
            {"Witch Doctor", NPCID.WitchDoctor },
            {"Clothier", NPCID.Clothier },
            {"Mechanic", NPCID.Mechanic },
            {"Party Girl", NPCID.PartyGirl },
            {"Wizard", NPCID.Wizard },
            {"Tax Collector", NPCID.TaxCollector },
            {"Truffle", NPCID.Truffle },
            {"Pirate", NPCID.Pirate },
            {"Steampunker", NPCID.Steampunker },
            {"Cyborg", NPCID.Cyborg },
            {"Santa Claus", NPCID.SantaClaus },
            {"Princess", NPCID.Princess },
        };

        public static Dictionary<int, string> npcIDtoName =
            npcNameToID.ToDictionary(x => x.Value, x => x.Key);

        public void Collect(
            string item,
            bool bypassStarterConfigCheck = false
        )
        {
            if (npcNameToID.ContainsKey(item))
            {
                world.receivedNPCs.Add(npcNameToID[item]);
                return;
            }

            if (
                !bypassStarterConfigCheck &&
                ModContent.GetInstance<Config.Config>()
                    .manualFlags.Contains(item)
            )
            {
                GiveItem(
                    null,
                    (Player player) =>
                    {
                        Item flagStarter =
                            new Item(
                                ModContent.ItemType<FlagStarter>()
                            );

                        FlagStarter flagStarterMod =
                            flagStarter.ModItem as FlagStarter;

                        flagStarterMod.FlagName = item;

                        player.QuickSpawnItem(
                            player.GetSource_GiftOrReward(),
                            flagStarter,
                            1
                        );

                        Chat(
                            $"Flag Starter for {item} received! " +
                            "If you ever lose a flagstarter item, use " +
                            "'/apflagstart' and '/apflagstart list'."
                        );
                    }
                );

                world.suspendedFlags.Add(item);
                return;
            }
            else
            {
                world.suspendedFlags.Remove(item);
            }

            switch (item)
            {
                case "Reward: Torch God's Favor":
                    GiveItem(ItemID.TorchGodsFavor);
                    break;

                case "Post-King Slime":
                    BossFlag(ref NPC.downedSlimeKing, NPCID.KingSlime);
                    break;

                case "Post-Eye of Cthulhu":
                    BossFlag(ref NPC.downedBoss1, NPCID.EyeofCthulhu);
                    break;

                case "Post-Evil Boss":
                    BossFlag(ref NPC.downedBoss2, NPCID.EaterofWorldsHead);
                    break;

                case "Post-Old One's Army Tier 1":
                    DD2Event.DownedInvasionT1 = true;
                    break;

                case "Post-Goblin Army":
                    NPC.downedGoblins = true;
                    break;

                case "Post-Queen Bee":
                    BossFlag(ref NPC.downedQueenBee, NPCID.QueenBee);
                    break;

                case "Post-Skeletron":
                    BossFlag(ref NPC.downedBoss3, NPCID.SkeletronHead);
                    break;

                case "Post-Deerclops":
                    BossFlag(ref NPC.downedDeerclops, NPCID.Deerclops);
                    break;

                case "Hardmode":
                    ActivateHardmode();
                    break;

                case "Post-Pirate Invasion":
                    NPC.downedPirates = true;
                    break;

                case "Post-Queen Slime":
                    BossFlag(ref NPC.downedQueenSlime, NPCID.QueenSlimeBoss);
                    break;

                case "Post-The Twins":
                    Action set = () =>
                        NPC.downedMechBoss2 =
                        NPC.downedMechBossAny = true;

                    if (NPC.AnyNPCs(NPCID.Retinazer))
                    {
                        if (NPC.AnyNPCs(NPCID.Spazmatism))
                        {
                            // If the player is fighting The Twins, it would mess
                            // with the CalamityGlobalNPC.OnKill logic, so we have
                            // a fallback.
                            if (ModLoader.HasMod("CalamityMod"))
                                CalamitySystem.SpawnMechOres();

                            NPC.downedMechBoss2 =
                                NPC.downedMechBossAny = true;
                        }
                        else
                        {
                            BossFlag(set, NPCID.Retinazer);
                        }
                    }
                    else
                    {
                        BossFlag(set, NPCID.Spazmatism);
                    }

                    break;

                case "Post-Old One's Army Tier 2":
                    DD2Event.DownedInvasionT2 = true;
                    break;

                case "Post-The Destroyer":
                    BossFlag(
                        () => NPC.downedMechBoss1 =
                            NPC.downedMechBossAny = true,
                        NPCID.TheDestroyer
                    );
                    break;

                case "Post-Skeletron Prime":
                    BossFlag(
                        () => NPC.downedMechBoss3 =
                            NPC.downedMechBossAny = true,
                        NPCID.SkeletronPrime
                    );
                    break;

                case "Post-Plantera":
                    BossFlag(ref NPC.downedPlantBoss, NPCID.Plantera);
                    break;

                case "Post-Golem":
                    BossFlag(ref NPC.downedGolemBoss, NPCID.Golem);
                    break;

                case "Post-Old One's Army Tier 3":
                    DD2Event.DownedInvasionT3 = true;
                    break;

                case "Post-Martian Madness":
                    NPC.downedMartians = true;
                    break;

                case "Post-Duke Fishron":
                    BossFlag(ref NPC.downedFishron, NPCID.DukeFishron);
                    break;

                case "Post-Mourning Wood":
                    BossFlag(ref NPC.downedHalloweenTree, NPCID.MourningWood);
                    break;

                case "Post-Pumpking":
                    BossFlag(ref NPC.downedHalloweenKing, NPCID.Pumpking);
                    break;

                case "Post-Everscream":
                    BossFlag(ref NPC.downedChristmasTree, NPCID.Everscream);
                    break;

                case "Post-Santa-NK1":
                    BossFlag(ref NPC.downedChristmasSantank, NPCID.SantaNK1);
                    break;

                case "Post-Ice Queen":
                    BossFlag(ref NPC.downedChristmasIceQueen, NPCID.IceQueen);
                    break;

                case "Post-Frost Legion":
                    NPC.downedFrost = true;
                    break;

                case "Post-Empress of Light":
                    BossFlag(ref NPC.downedEmpressOfLight, NPCID.HallowBoss);
                    break;

                case "Post-Lunatic Cultist":
                    BossFlag(ref NPC.downedAncientCultist, NPCID.CultistBoss);
                    break;

                case "Post-Lunar Events":
                    NPC.downedTowerNebula =
                        NPC.downedTowerSolar =
                        NPC.downedTowerStardust =
                        NPC.downedTowerVortex = true;
                    break;

                case "Post-Moon Lord":
                    BossFlag(ref NPC.downedMoonlord, NPCID.MoonLordCore);
                    break;

                case "Post-Desert Scourge":
                    CalamitySystem.CalamityOnKillDesertScourge();
                    break;

                case "Post-Giant Clam":
                    CalamitySystem.CalamityOnKillGiantClam(false);
                    break;

                case "Post-Acid Rain Tier 1":
                    CalamitySystem.CalamityAcidRainTier1Downed();
                    break;

                case "Post-Crabulon":
                    CalamitySystem.CalamityOnKillCrabulon();
                    break;

                case "Post-The Hive Mind":
                    CalamitySystem.CalamityOnKillTheHiveMind();
                    break;

                case "Post-The Perforators":
                    CalamitySystem.CalamityOnKillThePerforators();
                    break;

                case "Post-The Slime God":
                    CalamitySystem.CalamityOnKillTheSlimeGod();
                    break;

                case "Post-Dreadnautilus":
                    CalamitySystem.CalamityDreadnautilusDowned();
                    break;

                case "Post-Hardmode Giant Clam":
                    CalamitySystem.CalamityOnKillGiantClam(true);
                    break;

                case "Post-Aquatic Scourge":
                    CalamitySystem.CalamityOnKillAquaticScourge();
                    break;

                case "Post-Cragmaw Mire":
                    CalamitySystem.CalamityOnKillCragmawMire();
                    break;

                case "Post-Acid Rain Tier 2":
                    CalamitySystem.CalamityAcidRainTier2Downed();
                    break;

                case "Post-Brimstone Elemental":
                    CalamitySystem.CalamityOnKillBrimstoneElemental();
                    break;

                case "Post-Cryogen":
                    CalamitySystem.CalamityOnKillCryogen();
                    break;

                case "Post-Calamitas Clone":
                    CalamitySystem.CalamityOnKillCalamitasClone();
                    break;

                case "Post-Great Sand Shark":
                    CalamitySystem.CalamityOnKillGreatSandShark();
                    break;

                case "Post-Leviathan and Anahita":
                    CalamitySystem.CalamityOnKillLeviathanAndAnahita();
                    break;

                case "Post-Astrum Aureus":
                    CalamitySystem.CalamityOnKillAstrumAureus();
                    break;

                case "Post-The Plaguebringer Goliath":
                    CalamitySystem.CalamityOnKillThePlaguebringerGoliath();
                    break;

                case "Post-Ravager":
                    CalamitySystem.CalamityOnKillRavager();
                    break;

                case "Post-Astrum Deus":
                    CalamitySystem.CalamityOnKillAstrumDeus();
                    break;

                case "Post-Profaned Guardians":
                    CalamitySystem.CalamityOnKillProfanedGuardians();
                    break;

                case "Post-Dragonfolly":
                    CalamitySystem.CalamityOnKillTheDragonfolly();
                    break;

                case "Post-Providence, the Profaned Goddess":
                    CalamitySystem.CalamityOnKillProvidenceTheProfanedGoddess();
                    break;

                case "Post-Storm Weaver":
                    CalamitySystem.CalamityOnKillStormWeaver();
                    break;

                case "Post-Ceaseless Void":
                    CalamitySystem.CalamityOnKillCeaselessVoid();
                    break;

                case "Post-Signus, Envoy of the Devourer":
                    CalamitySystem.CalamityOnKillSignusEnvoyOfTheDevourer();
                    break;

                case "Post-Polterghast":
                    CalamitySystem.CalamityOnKillPolterghast();
                    break;

                case "Post-Mauler":
                    CalamitySystem.CalamityOnKillMauler();
                    break;

                case "Post-Nuclear Terror":
                    CalamitySystem.CalamityOnKillNuclearTerror();
                    break;

                case "Post-The Old Duke":
                    CalamitySystem.CalamityOnKillTheOldDuke();
                    break;

                case "Post-The Devourer of Gods":
                    CalamitySystem.CalamityOnKillTheDevourerOfGods();
                    break;

                case "Post-Yharon, Dragon of Rebirth":
                    CalamitySystem.CalamityOnKillYharonDragonOfRebirth();
                    break;

                case "Post-Exo Mechs":
                    CalamitySystem.CalamityOnKillExoMechs();
                    break;

                case "Post-Supreme Witch, Calamitas":
                    CalamitySystem.CalamityOnKillSupremeWitchCalamitas();
                    break;

                case "Post-Primordial Wyrm":
                    CalamitySystem.CalamityPrimordialWyrmDowned();
                    break;

                case "Post-Boss Rush":
                    break;

                case "Post-Trojan Squirrel":
                    FargoSystem.FargoOnKillTrojanSquirrel();
                    break;

                case "Post-Cursed Coffin":
                    FargoSystem.FargoOnKillCursedCoffin();
                    break;

                case "Post-Deviantt":
                    FargoSystem.FargoOnKillDeviantt();
                    break;

                case "Post-Banished Baron":
                    FargoSystem.FargoOnKillBanishedBaron();
                    break;

                case "Post-Lifelight":
                    FargoSystem.FargoOnKillLifelight();
                    break;

                case "Post-Betsy":
                    FargoSystem.FargoOnKillBetsy();
                    Chat(
                        "Note: Boss Checklist Mod won't show that you have the Post-Betsy flag."
                    );
                    break;

                case "Post-Champion of Timber":
                    FargoSystem.FargoOnKillTimberChampion();
                    break;

                case "Post-Champion of Terra":
                    FargoSystem.FargoOnKillTerraChampion();
                    break;

                case "Post-Champion of Earth":
                    FargoSystem.FargoOnKillEarthChampion();
                    break;

                case "Post-Champion of Nature":
                    FargoSystem.FargoOnKillNatureChampion();
                    break;

                case "Post-Champion of Life":
                    FargoSystem.FargoOnKillLifeChampion();
                    break;

                case "Post-Champion of Death":
                    FargoSystem.FargoOnKillDeathChampion();
                    break;

                case "Post-Champion of Spirit":
                    FargoSystem.FargoOnKillSpiritChampion();
                    break;

                case "Post-Champion of Will":
                    FargoSystem.FargoOnKillWillChampion();
                    break;

                case "Post-Eridanus, Champion of Cosmos":
                    FargoSystem.FargoOnKillCosmosChampion();
                    break;

                case "Post-Abominationn":
                    FargoSystem.FargoOnKillAbominationn();
                    break;

                case "Post-Mutant":
                    FargoSystem.FargoOnKillMutant();
                    break;

                case "Reward: Hermes Boots":
                    GiveItem(ItemID.HermesBoots);
                    break;

                case "Reward: Magic Mirror":
                    GiveItem(ItemID.MagicMirror);
                    break;

                case "Reward: Demon Conch":
                    GiveItem(ItemID.DemonConch);
                    break;

                case "Reward: Magic Conch":
                    GiveItem(ItemID.MagicConch);
                    break;

                case "Reward: Grappling Hook":
                    GiveItem(ItemID.GrapplingHook);
                    break;

                case "Reward: Cloud in a Bottle":
                    GiveItem(ItemID.CloudinaBottle);
                    break;

                case "Reward: Climbing Claws":
                    GiveItem(ItemID.ClimbingClaws);
                    break;

                case "Reward: Ancient Chisel":
                    GiveItem(ItemID.AncientChisel);
                    break;

                case "Reward: Fledgling Wings":
                    GiveItem(ItemID.CreativeWings);
                    break;

                case "Reward: Rod of Discord":
                    GiveItem(ItemID.RodofDiscord);
                    break;

                case "Reward: Aglet":
                    GiveItem(ItemID.Aglet);
                    break;

                case "Reward: Anklet of the Wind":
                    GiveItem(ItemID.AnkletoftheWind);
                    break;

                case "Reward: Ice Skates":
                    GiveItem(ItemID.IceSkates);
                    break;

                case "Reward: Lava Charm":
                    GiveItem(ItemID.LavaCharm);
                    break;

                case "Reward: Water Walking Boots":
                    GiveItem(ItemID.WaterWalkingBoots);
                    break;

                case "Reward: Flipper":
                    GiveItem(ItemID.Flipper);
                    break;

                case "Reward: Frog Leg":
                    GiveItem(ItemID.FrogLeg);
                    break;

                case "Reward: Shoe Spikes":
                    GiveItem(ItemID.ShoeSpikes);
                    break;

                case "Reward: Tabi":
                    GiveItem(ItemID.Tabi);
                    break;

                case "Reward: Black Belt":
                    GiveItem(ItemID.BlackBelt);
                    break;

                case "Reward: Flying Carpet":
                    GiveItem(ItemID.FlyingCarpet);
                    break;

                case "Reward: Moon Charm":
                    GiveItem(ItemID.MoonCharm);
                    break;

                case "Reward: Neptune's Shell":
                    GiveItem(ItemID.NeptunesShell);
                    break;

                case "Reward: Compass":
                    GiveItem(ItemID.Compass);
                    break;

                case "Reward: Depth Meter":
                    GiveItem(ItemID.DepthMeter);
                    break;

                case "Reward: Radar":
                    GiveItem(ItemID.Radar);
                    break;

                case "Reward: Tally Counter":
                    GiveItem(ItemID.TallyCounter);
                    break;

                case "Reward: Lifeform Analyzer":
                    GiveItem(ItemID.LifeformAnalyzer);
                    break;

                case "Reward: DPS Meter":
                    GiveItem(ItemID.DPSMeter);
                    break;

                case "Reward: Stopwatch":
                    GiveItem(ItemID.Stopwatch);
                    break;

                case "Reward: Metal Detector":
                    GiveItem(ItemID.MetalDetector);
                    break;

                case "Reward: Fisherman's Pocket Guide":
                    GiveItem(ItemID.FishermansGuide);
                    break;

                case "Reward: Weather Radio":
                    GiveItem(ItemID.WeatherRadio);
                    break;

                case "Reward: Sextant":
                    GiveItem(ItemID.Sextant);
                    break;

                case "Reward: Band of Regeneration":
                    GiveItem(ItemID.BandofRegeneration);
                    break;

                case "Reward: Celestial Magnet":
                    GiveItem(ItemID.CelestialMagnet);
                    break;

                case "Reward: Nature's Gift":
                    GiveItem(ItemID.NaturesGift);
                    break;

                case "Reward: Philosopher's Stone":
                    GiveItem(ItemID.PhilosophersStone);
                    break;

                case "Reward: Cobalt Shield":
                    GiveItem(ItemID.CobaltShield);
                    break;

                case "Reward: Armor Polish":
                    GiveItem(ItemID.ArmorPolish);
                    break;

                case "Reward: Vitamins":
                    GiveItem(ItemID.Vitamins);
                    break;

                case "Reward: Bezoar":
                    GiveItem(ItemID.Bezoar);
                    break;

                case "Reward: Adhesive Bandage":
                    GiveItem(ItemID.AdhesiveBandage);
                    break;

                case "Reward: Megaphone":
                    GiveItem(ItemID.Megaphone);
                    break;

                case "Reward: Nazar":
                    GiveItem(ItemID.Nazar);
                    break;

                case "Reward: Fast Clock":
                    GiveItem(ItemID.FastClock);
                    break;

                case "Reward: Trifold Map":
                    GiveItem(ItemID.TrifoldMap);
                    break;

                case "Reward: Blindfold":
                    GiveItem(ItemID.Blindfold);
                    break;

                case "Reward: Pocket Mirror":
                    GiveItem(ItemID.PocketMirror);
                    break;

                case "Reward: Paladin's Shield":
                    GiveItem(ItemID.PaladinsShield);
                    break;

                case "Reward: Frozen Turtle Shell":
                    GiveItem(ItemID.FrozenTurtleShell);
                    break;

                case "Reward: Flesh Knuckles":
                    GiveItem(ItemID.FleshKnuckles);
                    break;

                case "Reward: Putrid Scent":
                    GiveItem(ItemID.PutridScent);
                    break;

                case "Reward: Feral Claws":
                    GiveItem(ItemID.FeralClaws);
                    break;

                case "Reward: Cross Necklace":
                    GiveItem(ItemID.CrossNecklace);
                    break;

                case "Reward: Star Cloak":
                    GiveItem(ItemID.StarCloak);
                    break;

                case "Reward: Titan Glove":
                    GiveItem(ItemID.TitanGlove);
                    break;

                case "Reward: Obsidian Rose":
                    GiveItem(ItemID.ObsidianRose);
                    break;

                case "Reward: Magma Stone":
                    GiveItem(ItemID.MagmaStone);
                    break;

                case "Reward: Shark Tooth Necklace":
                    GiveItem(ItemID.SharkToothNecklace);
                    break;

                case "Reward: Magic Quiver":
                    GiveItem(ItemID.MagicQuiver);
                    break;

                case "Reward: Rifle Scope":
                    GiveItem(ItemID.RifleScope);
                    break;

                case "Reward: Brick Layer":
                    GiveItem(ItemID.BrickLayer);
                    break;

                case "Reward: Extendo Grip":
                    GiveItem(ItemID.ExtendoGrip);
                    break;

                case "Reward: Paint Sprayer":
                    GiveItem(ItemID.PaintSprayer);
                    break;

                case "Reward: Portable Cement Mixer":
                    GiveItem(ItemID.PortableCementMixer);
                    break;

                case "Reward: Treasure Magnet":
                    GiveItem(ItemID.TreasureMagnet);
                    break;

                case "Reward: Step Stool":
                    GiveItem(ItemID.PortableStool);
                    break;

                case "Reward: Discount Card":
                    GiveItem(ItemID.DiscountCard);
                    break;

                case "Reward: Gold Ring":
                    GiveItem(ItemID.GoldRing);
                    break;

                case "Reward: Lucky Coin":
                    GiveItem(ItemID.LuckyCoin);
                    break;

                case "Reward: High Test Fishing Line":
                    GiveItem(ItemID.HighTestFishingLine);
                    break;

                case "Reward: Angler Earring":
                    GiveItem(ItemID.AnglerEarring);
                    break;

                case "Reward: Tackle Box":
                    GiveItem(ItemID.TackleBox);
                    break;

                case "Reward: Lavaproof Fishing Hook":
                    GiveItem(ItemID.LavaFishingHook);
                    break;

                case "Reward: Red Counterweight":
                    GiveItem(ItemID.RedCounterweight);
                    break;

                case "Reward: Yoyo Glove":
                    GiveItem(ItemID.YoYoGlove);
                    break;

                case "Reward: Coins":
                    GiveCoins();
                    break;

                case "Reward: Diving Helmet":
                    GiveItem(ItemID.DivingHelmet);
                    break;

                case "Reward: Jellyfish Necklace":
                    GiveItem(ItemID.JellyfishNecklace);
                    break;

                case "Reward: Life Crystal":
                    GiveItem(ItemID.LifeCrystal);
                    break;

                case "Reward: Enchanted Sword":
                    GiveItem(ItemID.EnchantedSword);
                    break;

                case "Reward: Starfury":
                    GiveItem(ItemID.Starfury);
                    break;

                case "Reward: Defender Medal":
                    GiveItem(ItemID.DefenderMedal);
                    break;

                case "Loot Item":
                    GiveLootItem();
                    break;

                case null:
                    break;

                default:
                {
                    bool handled = false;

                    if (ModLoader.HasMod("CalamityMod"))
                        handled = CalamitySystem.GiveItem(item);

                    if (
                        !handled &&
                        ModLoader.HasMod("FargowiltasSouls")
                    )
                    {
                        handled = FargoSystem.GiveItem(item);
                    }

                    if (!handled)
                        Chat($"Received unknown item: {item}");

                    break;
                }
            }
        }

        public override void PostUpdateWorld()
        {
            if (session == null)
                return;

            if (!session.session.Socket.Connected)
            {
                Chat(
                    "Disconnected from Archipelago. Reload the world to reconnect."
                );

                session = null;
                return;
            }

            var unqueue = new List<int>();

            for (var i = 0; i < session.locationQueue.Count; i++)
            {
                var status = session.locationQueue[i].Status;

                if (
                    status switch
                    {
                        TaskStatus.RanToCompletion or
                        TaskStatus.Canceled or
                        TaskStatus.Faulted => true,

                        _ => false,
                    }
                )
                {
                    if (status == TaskStatus.RanToCompletion)
                    {
                        foreach (
                            var item in session.locationQueue[i].Result.Values
                        )
                        {
                            Chat(
                                $"Sent {item.ItemName} to " +
                                $"{session.session.Players.GetPlayerAlias(item.Player)}!"
                            );
                        }
                    }
                    else
                    {
                        Chat(
                            "Sent an item to a player...but failed to get info about it!"
                        );
                    }

                    unqueue.Add(i);
                }
            }

            unqueue.Reverse();

            foreach (var i in unqueue)
                session.locationQueue.RemoveAt(i);

            while (session.session.Items.Any())
            {
                var itemName =
                    session.session.Items.DequeueItem().ItemName;

                if (session.currentItem++ < world.collectedItems)
                    continue;

                Collect(itemName);

                world.collectedItems++;
            }

            if (ModLoader.HasMod("CalamityMod"))
                CalamitySystem.CalamityPostUpdateWorld();

            if (ModLoader.HasMod("FargowiltasSouls"))
                FargoSystem.FargoPostUpdateWorld();

            if (session.victory)
                return;

            foreach (var goal in session.goals)
            {
                if (
                    !session.session.Locations.AllLocationsChecked.Contains(
                        session.session.Locations.GetLocationIdFromName(
                            APWorldName,
                            goal
                        )
                    )
                )
                {
                    return;
                }
            }

            var victoryPacket = new StatusUpdatePacket()
            {
                Status = ArchipelagoClientState.ClientGoal,
            };

            session.session.Socket.SendPacket(victoryPacket);

            session.victory = true;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["ApWorldData"] = world;
        }

        public void Reset()
        {
            typeof(SocialAPI)
                .GetField("_mode", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, SocialMode.Steam);

            if (session != null)
            {
                session.session.MessageLog.OnMessageReceived -=
                    ApMessageToChat;

                session.session.Socket.DisconnectAsync();
            }

            session = null;
        }

        public override void OnWorldUnload()
        {
            world = new();
            status = ConnectStatus.Unset;
            desiredAPversion = null;

            Reset();
        }

        public string[] Status()
        {
            if (status == ConnectStatus.Valid)
            {
                List<string> msg =
                    ["Archipelago is active!"];

                if (ModLoader.HasMod("CalamityMod"))
                {
                    msg.Add(
                        "Calamity Archipelago detected. If you beat a Calamity boss " +
                        "and it doesn't give you a check, restart your game and beat " +
                        "it again. It is a rare, unsolved bug."
                    );
                }

                if (
                    ModContent.GetInstance<Config.Config>()
                        .forceOffNPC
                )
                {
                    msg.Add(
                        "[c/FF5757:NOTICE:] You have forced off NPC Ghosting. " +
                        "Please verify that your slot has NPC Randomization disabled."
                    );
                }

                return msg.ToArray();
            }

            return status switch
            {
                ConnectStatus.Unset => new[]
                {
                    "The world is not connected to Archipelago! Reload the world to try again.",
                    "If you are the host, check your config in the main menu at Workshop > Manage Mods > Config",
                },

                ConnectStatus.WrongSlot => new[]
                {
                    $"Could not find a slot named \"{ModContent.GetInstance<Config.Config>().name}\" registered in the multiworld.",
                    "If you are the host, check your config in the main menu at Workshop > Manage Mods > Config, then reload the world."
                },

                ConnectStatus.WrongPass => new[]
                {
                    "The password for the Archipelago server is incorrect.",
                    "If you are the host, check your config in the main menu at Workshop > Manage Mods > Config, then reload the world."
                },

                ConnectStatus.WrongGame => new[]
                {
                    $"The slot \"{ModContent.GetInstance<Config.Config>().name}\" is set to a different game on the server, not \"{APWorldName}\".",
                    "If this is the correct slot, make sure that you did not use the website to generate your YAML.",
                    "See this page for more information: https://github.com/desperandos101/SeldomArchipelago/tree/release",
                    "You have been disconnected from the server.",
                },

                ConnectStatus.SlotOrSeedMismatch => new[]
                {
                    "This world has save data for a different multiworld/slot.",
                    $"SAVE DATA MULTIWORLD SLOT: {world.slotName}, SEED {world.seed}",
                    "You have been disconnected from the server. Please load a different world."
                },

                ConnectStatus.CalamityNeeded => new[]
                {
                    "The multiworld slot you connected to has Calamity integration enabled, but you do not have the mod enabled in your modlist.",
                    "You have been disconnected from the server. Please enable Calamity, then reload this world."
                },

                ConnectStatus.NoCalamityNeeded => new[]
                {
                    "The multiworld slot you connected to has Calamity integration disabled, but you have the mod enabled in your modlist.",
                    "You have been disconnected from the server. Please disable Calamity, then reload this world."
                },

                ConnectStatus.FargoNeeded => new[]
                {
                    "The multiworld slot you connected to has Fargo Souls integration enabled, but you do not have the mod enabled in your modlist.",
                    "You have been disconnected from the server. Please enable Fargo, then reload this world."
                },

                ConnectStatus.NoFargoNeeded => new[]
                {
                    "The multiworld slot you connected to has Fargo Souls integration disabled, but you have the mod enabled in your modlist.",
                    "You have been disconnected from the server. Please disable Fargo Souls, then reload this world."
                },

                ConnectStatus.ClientOlder => new[]
                {
                    "The multiworld slot you connected to requires a newer version of the client.",
                    "You have been disconnected from the server. Please upgrade your client, then reload the world."
                },

                ConnectStatus.ClientNewer => new[]
                {
                    "The multiworld slot you connected to requires an older version of the client.",
                    $"Look on the releases page for the latest client compatible with APWorld version {(desiredAPversion is null ? "0.6.61, then if that fails to connect, 0.6.62." : $"{desiredAPversion[0]}.{desiredAPversion[1]}.{desiredAPversion[2]}.")}",
                    "You have been disconnected from the server. Please downpatch your client, then reload the world."
                },
            };
        }

        public bool SendCommand(string command)
        {
            if (session == null)
                return false;

            var packet = new SayPacket()
            {
                Text = command,
            };

            session.session.Socket.SendPacket(packet);

            return true;
        }

        public string[] DebugInfo()
        {
            var info = new List<string>();

            if (world == null)
            {
                info.Add(
                    "The mod thinks you're not in a world, which should never happen"
                );
            }
            else
            {
                info.Add("You are in a world");

                if (world.locationBacklog.Count > 0)
                {
                    info.Add(
                        "You have locations in the backlog, which should only be the case if Archipelago is inactive"
                    );

                    info.Add(
                        $"Location backlog: [{string.Join("; ", world.locationBacklog)}]"
                    );
                }
                else
                {
                    info.Add(
                        "No locations in the backlog, which is usually normal"
                    );
                }

                info.Add(
                    $"You've collected {world.collectedItems} items"
                );

                info.Add(
                    $"NPC randomization is {(world.NPCRandoActive() ? "en" : "dis")}abled"
                );

                info.Add(
                    $"NPCs randomized: [{(world.randomizedNPCs is not null ? string.Join(", ", from npc in world.randomizedNPCs select npcIDtoName[npc]) : "None")}]"
                );

                info.Add(
                    $"Received NPC IDs: [{string.Join(", ", from npc in world.receivedNPCs select npcIDtoName[npc])}]"
                );
            }

            if (session == null)
            {
                info.Add(
                    "You're not connected to Archipelago"
                );
            }
            else
            {
                if (session.session.Socket.Connected)
                {
                    info.Add(
                        "You're connected to Archipelago"
                    );
                }
                else
                {
                    info.Add(
                        "You're not connected to Archipelago, but the mod thinks you are"
                    );
                }

                if (session.locationQueue.Count > 0)
                {
                    info.Add(
                        "You have locations queued for sending. In normal circumstances, these locations will be sent ASAP."
                    );

                    var statuses = new List<string>();

                    foreach (var location in session.locationQueue)
                    {
                        statuses.Add(
                            location.Status switch
                            {
                                TaskStatus.Created => "Created",
                                TaskStatus.WaitingForActivation => "Waiting for activation",
                                TaskStatus.WaitingToRun => "Waiting to run",
                                TaskStatus.Running => "Running",
                                TaskStatus.WaitingForChildrenToComplete => "Waiting for children to complete",
                                TaskStatus.RanToCompletion => "Completed",
                                TaskStatus.Canceled => "Canceled",
                                TaskStatus.Faulted => "Faulted",
                                _ => "Has a status that was added to C# after this code was written",
                            }
                        );
                    }

                    info.Add(
                        $"Location queue statuses: [{string.Join("; ", statuses)}]"
                    );
                }
                else
                {
                    info.Add(
                        "No locations in the queue, which is usually normal"
                    );
                }

                info.Add(
                    $"DeathLink is {(session.deathlink == null ? "dis" : "en")}abled"
                );

                info.Add(
                    $"{session.currentItem} items have been applied"
                );

                info.Add(
                    $"Goals: [{string.Join("; ", session.goals)}]"
                );

                info.Add(
                    $"You have {session.rolls?.Count ?? 0} rarity rolls remaining"
                );

                info.Add(
                    $"Rarity pools: {session.rarityPools?.Count ?? 0}"
                );

                info.Add(
                    $"Victory has {(session.victory ? "been achieved! Hooray!" : "not been achieved. Alas.")}"
                );

                info.Add(
                    $"You are slot {session.slot}"
                );
            }

            return info.ToArray();
        }

        public void Chat(
            string message,
            Color color,
            int player = -1
        )
        {
            if (player == -1)
            {
                if (Main.netMode == NetmodeID.Server)
                {
                    ChatHelper.BroadcastChatMessage(
                        NetworkText.FromLiteral(message),
                        color
                    );

                    Console.WriteLine(message);
                }
                else
                {
                    Main.NewText(message, color);
                }
            }
            else
            {
                ChatHelper.SendChatMessageToClient(
                    NetworkText.FromLiteral(message),
                    color,
                    player
                );
            }
        }

        public void Chat(
            string[] messages,
            int player = -1
        )
        {
            foreach (var message in messages)
                Chat(message, player);
        }

        public void Chat(
            string message,
            int player = -1
        ) =>
            Chat(message, Color.White, player);

        public void QueueLocation(string locationName)
        {
            if (session == null)
            {
                world.locationBacklog.Add(locationName);
                return;
            }

            var location =
                session.session.Locations.GetLocationIdFromName(
                    APWorldName,
                    locationName
                );

            if (location == -1)
                return;

            if (
                session.session.Locations.AllLocationsChecked.Contains(
                    location
                )
            )
            {
                Mod.Logger.Info(
                    $"[AP] Location {locationName} already collected."
                );

                return;
            }

            session.locationQueue.Add(
                session.session.Locations.ScoutLocationsAsync(
                    new[] { location }
                )
            );

            session.session.Locations.CompleteLocationChecks(
                new[] { location }
            );
        }

        public void QueueLocationClient(string locationName)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                QueueLocation(locationName);
                return;
            }

            var packet =
                ModContent.GetInstance<SeldomArchipelago>()
                    .GetPacket();

            packet.Write(locationName);
            packet.Send();
        }

        //map itemid to item name (non internal, but through a dict. Should match name in rarity pools)
        public void QueueLocationClient(int ItemId)
        {
            // 1. Check if an active session exists and contains the ID-to-Name map
            if (session?.ItemIdToNameMap != null && session.ItemIdToNameMap.TryGetValue(ItemId, out string locationName))
            {
                QueueLocationClient(locationName);
                return;
            }

            // 2. Fallback: Check standard Terraria ItemID name if not found in custom map
            if (ItemID.Search.ContainsId(ItemId))
            {
                string searchName = ItemID.Search.GetName(ItemId);
                
                // If ItemID.Search returned a valid name, try queuing with it
                if (!string.IsNullOrEmpty(searchName))
                {
                    QueueLocationClient(searchName);
                    return;
                }
            }

            Mod.Logger.Warn($"[Archipelago] Unable to queue location for Item ID {ItemId}: No mapped name found in session.ItemIdToNameMap.");
        }

        public void Achieved(string achievement)
        {
            world.achieved.Add(achievement);
        }

        public List<string> GetAchieved()
        {
            return world.achieved;
        }

        public void TriggerDeathlink(
            string message,
            int player
        )
        {
            if (session?.deathlink == null)
                return;

            var death =
                new DeathLink(
                    session.session.Players.GetPlayerAlias(session.slot),
                    message
                );

            session.deathlink.SendDeathLink(death);

            ReceiveDeathlink(death);
        }

        public void ReceiveDeathlink(DeathLink death)
        {
            var message =
                $"[DeathLink] " +
                $"{(death.Source == null ? "" : $"{death.Source} died")}" +
                $"{(death.Source != null && death.Cause != null ? ": " : "")}" +
                $"{(death.Cause == null ? "" : $"{death.Cause}")}";

            for (var i = 0; i < Main.maxPlayers; i++)
            {
                var player = Main.player[i];

                if (player.active && !player.dead)
                {
                    player.Hurt(
                        PlayerDeathReason.ByCustomReason(message),
                        999999,
                        1
                    );
                }
            }

            if (Main.netMode == NetmodeID.SinglePlayer)
                return;

            var packet =
                ModContent.GetInstance<SeldomArchipelago>()
                    .GetPacket();

            packet.Write(message);
            packet.Send();
        }

        public static void ActivateHardmode()
        {
            if (Main.hardMode)
                return;

            ArchipelagoSystem.BossFlag(NPCID.WallofFlesh);
            WorldGen.StartHardmode();
        }

        void BossFlag(
            ref bool flag,
            int boss
        )
        {
            BossFlag(boss);
            flag = true;
        }

        void BossFlag(
            Action set,
            int boss
        )
        {
            BossFlag(boss);
            set();
        }

        static void BossFlag(int boss)
        {
            if (ModLoader.HasMod("CalamityMod"))
                CalamitySystem.VanillaBossKilled(boss);

            if (ModLoader.HasMod("FargowiltasSouls"))
                FargoSystem.VanillaBossKilled(boss);
        }

        void GiveItem(
            int? item,
            Action<Player> giveItem
        )
        {
            if (item != null)
                world.receivedRewards.Add(item.Value);

            for (var i = 0; i < Main.maxPlayers; i++)
            {
                var player = Main.player[i];

                if (player.active)
                {
                    giveItem(player);

                    if (item != null)
                    {
                        if (Main.netMode == NetmodeID.Server)
                        {
                            var packet =
                                ModContent.GetInstance<SeldomArchipelago>()
                                    .GetPacket();

                            packet.Write("YouGotAnItem");
                            packet.Write(item.Value);
                            packet.Send(i);
                        }
                        else
                        {
                            player
                                .GetModPlayer<ArchipelagoPlayer>()
                                .ReceivedReward(item.Value);
                        }
                    }
                }
            }
        }

        public void GiveLootItem()
        {
            if (session == null)
            {
                Mod.Logger.Error(
                    "[AP] Cannot give Loot Item: session is null."
                );

                return;
            }

            int rarity = getRarity();

            // Explicitly report whether the list exists and how many rolls remain.
            // This is preferable to $"{session.rolls}", which only tells us the
            // list's type rather than whether it is null or empty.
            Mod.Logger.Error(
                $"[AP] Loot state: " +
                $"rollsNull={session.rolls == null}, " +
                $"rollCount={session.rolls?.Count ?? -1}, " +
                $"rarityPoolsNull={session.rarityPools == null}, " +
                $"rarityPoolCount={session.rarityPools?.Count ?? -1}, " +
                $"worldRarity={rarity}"
            );

            if (session.rolls == null || session.rolls.Count == 0)
            {
                Mod.Logger.Error(
                    "[AP] Cannot give Loot Item: no rarity rolls remain."
                );

                return;
            }

            if (
                session.rarityPools == null ||
                session.rarityPools.Count == 0
            )
            {
                Mod.Logger.Error(
                    "[AP] Cannot give Loot Item: rarity pools are not initialized."
                );

                return;
            }

            // Consume the next predetermined roll.
            double roll = session.rolls[0];
            session.rolls.RemoveAt(0);

            // Pick an initial rarity from 0 through the current world rarity.
            int selectedRarity =
                (int)(roll * (rarity + 1));

            Mod.Logger.Info(
                $"[AP] Loot Roll Consumed: " +
                $"roll={roll:F4}, " +
                $"input rarity={rarity}, " +
                $"calculated selectedRarity={selectedRarity}. " +
                $"Remaining rolls in queue: {session.rolls.Count}"
            );

            // Diagnostic: Log current available rarity pool state
            Mod.Logger.Info(
                $"[AP] Current Rarity Pools State " +
                $"(Total registered keys: {session.rarityPools.Count}):"
            );

            foreach (var kvp in session.rarityPools)
            {
                Mod.Logger.Info(
                    $"  - Key: '{kvp.Key}' | " +
                    $"Count: {kvp.Value?.Count ?? 0}"
                );
            }

            // Search upward, wrapping from the maximum rarity back to 0.
            int currentRarity = selectedRarity;

            for (
                int checkedRarities = 0;
                checkedRarities <= 10;
                checkedRarities++
            )
            {
                string poolName =
                    $"{currentRarity}";

                if (
                    session.rarityPools.TryGetValue(
                        poolName,
                        out List<int> pool
                    )
                )
                {
                    if (pool != null && pool.Count > 0)
                    {
                        int item = pool[0];
                        pool.RemoveAt(0);

                        Mod.Logger.Info(
                            $"[AP] Success! Found item {item} " +
                            $"({ItemID.Search.GetName(item)}) in '{poolName}' " +
                            $"(attempt {checkedRarities + 1}/11). " +
                            $"Remaining in '{poolName}': {pool.Count}."
                        );

                        // Give the unghosted item through the existing GiveItem overload.
                        GiveItem(item);
                        return;
                    }
                    else
                    {
                        Mod.Logger.Debug(
                            $"[AP] Key '{poolName}' found, " +
                            "but the list is empty (0 items)."
                        );
                    }
                }
                else
                {
                    Mod.Logger.Debug(
                        $"[AP] Key '{poolName}' does NOT exist in session.rarityPools."
                    );
                }

                // Pool exhausted. Move to the next rarity.
                currentRarity =
                    (currentRarity + 1) % 11;
            }

            Mod.Logger.Error(
                $"[AP] Cannot give Loot Item: all rarity pools are exhausted! " +
                $"(Started search at 'Rarity {selectedRarity}', checked 11 offsets)."
            );
        }

        public void GiveItem(int item) =>
            GiveItem(
                item,
                player =>
                    player.QuickSpawnItem(
                        player.GetSource_GiftOrReward(),
                        item,
                        1
                    )
            );

        int[] baseCoins =
        {
            15,
            20,
            25,
            30,
            40,
            50,
            70,
            100
        };

        void GiveCoins()
        {
            var flagCount = 0;

            foreach (var flag in flags)
            {
                if (CheckFlag(flag))
                    flagCount++;
            }

            var count =
                baseCoins[flagCount % 8] *
                (int)Math.Pow(10, flagCount / 8);

            var platinum = count / 10000;
            var gold = count % 10000 / 100;
            var silver = count % 100;

            GiveItem(
                null,
                player =>
                {
                    if (platinum > 0)
                    {
                        player.QuickSpawnItem(
                            player.GetSource_GiftOrReward(),
                            ItemID.PlatinumCoin,
                            platinum
                        );
                    }

                    if (gold > 0)
                    {
                        player.QuickSpawnItem(
                            player.GetSource_GiftOrReward(),
                            ItemID.GoldCoin,
                            gold
                        );
                    }

                    if (silver > 0)
                    {
                        player.QuickSpawnItem(
                            player.GetSource_GiftOrReward(),
                            ItemID.SilverCoin,
                            silver
                        );
                    }
                }
            );
        }

        public List<int> ReceivedRewards() =>
            world.receivedRewards;

        public override void ModifyHardmodeTasks(
            List<GenPass> list
        )
        {
            // If all mech boss flags are collected, but not Hardmode,
            // there was no Hallow when hallowed ore was generated, so
            // no ore was generated. So, we generate new ore if this is
            // the case.
            list.Add(
                new PassLegacy(
                    "Hallowed Ore",
                    (progress, config) =>
                    {
                        if (ModLoader.HasMod("CalamityMod"))
                            CalamitySystem.CalamityStartHardmode();
                    }
                )
            );
        }
    }
}