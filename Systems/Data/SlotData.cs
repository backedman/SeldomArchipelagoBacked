using Archipelago.MultiClient.Net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ID;
using System.Linq;
using System.Data;
using static SeldomDespArchipelago.Systems.ArchipelagoSystem;
using System.IO;
using System.Configuration;
using Terraria;
using System.ComponentModel;

namespace SeldomDespArchipelago.Systems.Data
{
    /// <summary>
    /// Contains static information about an Archipelago slot. 
    /// </summary>
    public readonly struct SlotData : TagSerializable
    {
        public int Slot { get; init; }
        public string Name { get; init; }
        public string Seed { get; init; }
        public ImmutableArray<string> Goals { get; init; }
        public bool Deathlink { get; init; }
        public bool NpcRando { get; init; }
        public ImmutableHashSet<int> RandomizedNPCs { get; init; }
        // Dict of loc npc ids to item npc ids, if a player's npc item happens to be placed in one of their npc locations.
        // If this is the case, we can transform the ghost/bound npc into the item npc as soon as it is activated, for both expediency and cuteness.
        public ImmutableDictionary<int, int> ItemsByNPC { get; init; }
        public ImmutableArray<ImmutableArray<int>> ComponentIDs { get; init; }
        public bool DepthRando { get; init; }
        public bool ComponentsRando { get; init; }
        public bool Calamity { get; init; }
        public bool Fargo { get; init; }
        public SlotData() {}
        public SlotData(ArchipelagoSession session, LoginSuccessful login)
        {
            Slot = login.Slot;
            Name = session.Players.GetPlayerName(Slot);
            Seed = session.RoomState.Seed;
            Goals = ((JArray)login.SlotData["goal"]).ToObject<string[]>().ToImmutableArray();
            bool isEnabled(string key) => (long)login.SlotData[key] > 0;
            Deathlink = (bool)login.SlotData["deathlink"];
            NpcRando = isEnabled("npc_rando");
            DepthRando = isEnabled("depth_rando");
            
            JArray componentsArray = login.SlotData.TryGetValue("components", out var compObj) ? compObj as JArray : null;
            ComponentsRando = componentsArray != null && componentsArray.Count > 0;

            Calamity = isEnabled("calamity");
            Fargo = isEnabled("fargo");
            RandomizedNPCs = null;
            var itemsByNPC = new Dictionary<int, int>();
            var componentIDLists = new List<ImmutableArray<int>>();

            if (NpcRando)
            {
                string[] randomizedNPCnames = ((JArray)login.SlotData["randomize_npcs"]).ToObject<string[]>();
                RandomizedNPCs = (from name in randomizedNPCnames select npcNameToID[name]).ToImmutableHashSet();
                string[] allNPCnames = npcNameToID.Keys.ToArray();
                var locNamesByID = new Dictionary<long, string>();
                foreach (string loc in allNPCnames)
                {
                    locNamesByID[session.Locations.GetLocationIdFromName(SessionState.APWorldName, loc)] = loc;
                }
                if (locNamesByID.ContainsKey(-1))
                {
                    throw new Exception($"Some retrieved NPC locations turned up -1 ids.");
                }
                var task = session.Locations.ScoutLocationsAsync(locNamesByID.Keys.ToArray());
                if (task.Wait(1000))
                {
                    var locByID = task.Result;
                    foreach (long key in locByID.Keys)
                    {
                        var itemInfo = locByID[key];
                        if (itemInfo.Player.Slot == Slot && allNPCnames.Contains(itemInfo.ItemName))
                        {
                            int npcType = npcNameToID[locNamesByID[key]];
                            itemsByNPC[npcType] = npcNameToID[itemInfo.ItemName];
                        }
                    }
                }
                else  // TODO: Have better backup plan
                {
                    ModContent.GetInstance<ArchipelagoSystem>().Mod.Logger.Info("Failed to properly initialize " + nameof(ItemsByNPC));
                }
            }

            if (ComponentsRando)
            {
                string[] componentNames = componentsArray.ToObject<string[]>();

                foreach (string component in componentNames)
                {
                    List<int> resolvedIds = GhostItemSystem.ResolveItemIds(component);

                    if (resolvedIds.Count > 0)
                    {
                        componentIDLists.Add(resolvedIds.ToImmutableArray());
                        ModLoader.GetMod(nameof(SeldomDespArchipelago)).Logger.Info($"[Archipelago] Successfully resolved component '{component}' to item ID(s): [{string.Join(", ", resolvedIds)}]");
                    }
                    else
                    {
                        ModLoader.GetMod(nameof(SeldomDespArchipelago)).Logger.Warn($"[Archipelago] Error: Could not resolve item ID(s) for component '{component}'. Report this to the devs.");
                    }
                }
            }
            ItemsByNPC = NpcRando ? itemsByNPC.ToImmutableDictionary() : null;
            ComponentIDs = ComponentsRando ? componentIDLists.ToImmutableArray() : default;
        }

        public TagCompound SerializeData()
        {
            var tag = new TagCompound
            {
                [nameof(Slot)] = Slot,
                [nameof(Name)] = Name,
                [nameof(Seed)] = Seed,
                [nameof(Goals)] = Goals.ToList(),
                [nameof(Deathlink)] = Deathlink,
                [nameof(NpcRando)] = NpcRando,
                [nameof(DepthRando)] = DepthRando,
                [nameof(ComponentsRando)] = ComponentsRando,
                [nameof(Calamity)] = Calamity,
                [nameof(Fargo)] = Fargo,
            };
            if (NpcRando)
            {
                tag[nameof(RandomizedNPCs)] = RandomizedNPCs.ToList();
                tag[nameof(ItemsByNPC) + "Keys"] = ItemsByNPC.Keys.ToList();
                tag[nameof(ItemsByNPC) + "Values"] = ItemsByNPC.Values.ToList();
            }
            if (ComponentsRando)
            {
                var compTags = new List<TagCompound>();
                foreach (var inner in ComponentIDs)
                {
                    compTags.Add(new TagCompound { ["Ids"] = inner.ToList() });
                }
                tag[nameof(ComponentIDs)] = compTags;
            }
            return tag;
        }

        public static readonly Func<TagCompound, SlotData> DESERIALIZER = (tag) =>
        {
            bool npcRando = tag.GetBool(nameof(NpcRando));
            var npcs = npcRando ? tag.Get<List<int>>(nameof(ItemsByNPC) + "Keys") : null;
            var items = npcRando ? tag.Get<List<int>>(nameof(ItemsByNPC) + "Values") : null;
            
            bool componentsRando = tag.GetBool(nameof(ComponentsRando));
            var compTags = componentsRando ? tag.GetList<TagCompound>(nameof(ComponentIDs)) : null;
            
            ImmutableArray<ImmutableArray<int>> componentIDs = default;
            if (componentsRando && compTags != null)
            {
                var builder = ImmutableArray.CreateBuilder<ImmutableArray<int>>(compTags.Count);
                foreach (var t in compTags)
                {
                    builder.Add(t.GetList<int>("Ids").ToImmutableArray());
                }
                componentIDs = builder.ToImmutable();
            }

            return new SlotData()
            {
                Slot = tag.GetInt(nameof(Slot)),
                Name = tag.GetString(nameof(Name)),
                Seed = tag.GetString(nameof(Seed)),
                Goals = tag.GetList<string>(nameof(Goals)).ToImmutableArray(),
                Deathlink = tag.GetBool(nameof(Deathlink)),
                NpcRando = npcRando,
                RandomizedNPCs = npcRando ? tag.GetList<int>(nameof(RandomizedNPCs)).ToImmutableHashSet() : null,
                ItemsByNPC = npcRando ? npcs.Zip(items, (k, v) => new { Key = k, Value = v }).ToImmutableDictionary(x => x.Key, x => x.Value) : null,
                DepthRando = tag.GetBool(nameof(DepthRando)),
                ComponentsRando = componentsRando,
                ComponentIDs = componentIDs,
                Calamity = tag.GetBool(nameof(Calamity)),
                Fargo = tag.GetBool(nameof(Fargo)),
            };
        };

        public void Write(BinaryWriter writer)
        {
            writer.Write(Slot);
            writer.Write(Name);
            writer.Write(Seed);
            writer.Write(string.Join(';', Goals));
            writer.Write(Deathlink);
            writer.Write(NpcRando);
            writer.Write(string.Join(';', RandomizedNPCs));
            writer.Write(string.Join(';', ItemsByNPC.Keys));
            writer.Write(string.Join(';', ItemsByNPC.Values));
            writer.Write(DepthRando);
            writer.Write(ComponentsRando);
            if (ComponentsRando)
            {
                writer.Write(ComponentIDs.Length);
                foreach (var inner in ComponentIDs)
                {
                    writer.Write(string.Join(',', inner));
                }
            }
            writer.Write(Calamity);
            writer.Write(Fargo);
        }

        public static SlotData? ReadSlotData(BinaryReader reader)
        {
            try
            {
                int slot = reader.ReadInt32();
                string name = reader.ReadString();
                string seed = reader.ReadString();
                ImmutableArray<string> goals = reader.ReadString().Split(';').ToImmutableArray();
                bool deathlink = reader.ReadBoolean();
                bool npcRando = reader.ReadBoolean();
                ImmutableHashSet<int> randomizedNPCs = (from i in reader.ReadString().Split(';') select int.Parse(i)).ToImmutableHashSet();
                var itemsByNPCKeys = from i in reader.ReadString().Split(';') select int.Parse(i);
                var itemsByNPCValues = from i in reader.ReadString().Split(';') select int.Parse(i);
                bool depthRando = reader.ReadBoolean();
                bool componentsRando = reader.ReadBoolean();
                
                ImmutableArray<ImmutableArray<int>> componentIDs = default;
                if (componentsRando)
                {
                    int count = reader.ReadInt32();
                    var builder = ImmutableArray.CreateBuilder<ImmutableArray<int>>(count);
                    for (int i = 0; i < count; i++)
                    {
                        string line = reader.ReadString();
                        var inner = string.IsNullOrEmpty(line) 
                            ? ImmutableArray<int>.Empty 
                            : line.Split(',').Select(int.Parse).ToImmutableArray();
                        builder.Add(inner);
                    }
                    componentIDs = builder.ToImmutable();
                }

                bool calamity = reader.ReadBoolean();
                bool fargo = reader.ReadBoolean();
                
                return new SlotData
                {
                    Slot = slot,
                    Name = name,
                    Seed = seed,
                    Goals = goals,
                    Deathlink = deathlink,
                    NpcRando = npcRando,
                    RandomizedNPCs = randomizedNPCs,
                    ItemsByNPC = itemsByNPCKeys.Zip(itemsByNPCValues, (k, v) => KeyValuePair.Create(k, v)).ToImmutableDictionary(),
                    DepthRando = depthRando,
                    ComponentsRando = componentsRando,
                    ComponentIDs = componentIDs,
                    Calamity = calamity,
                    Fargo = fargo,
                };
            }
            catch (Exception e)
            {
                ModContent.GetInstance<SeldomArchipelago>().Logger.Info($"Failed to receive slotdata: {e}");
                return null;
            }
        }
    }
}