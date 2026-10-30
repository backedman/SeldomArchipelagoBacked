using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SeldomDespArchipelago.Systems
{
    public class GhostItemSystem : ModSystem
    {
        // SINGLE SOURCE OF TRUTH FOR ALIASES
        // Category Name -> List of concrete Terraria Item Names
        public static readonly Dictionary<string, string[]> CategoryAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Evil Sword", new[] { "Light's Bane", "Blood Butcherer" } },
            // Add any future categories here (e.g. { "Phaseblade", new[] { "Red Phaseblade", "Blue Phaseblade", ... } })
        };

        // Reverse lookup: Item Name -> Category Name (Built dynamically)
        public static readonly Dictionary<string, string> ItemToCategoryMap = BuildReverseAliasMap();

        private static Dictionary<string, string> BuildReverseAliasMap()
        {
            // Use OrdinalIgnoreCase and store cleaned item names as keys
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var (category, items) in CategoryAliases)
            {
                foreach (var item in items)
                {
                    string cleaned = CleanItemName(item);
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        map[cleaned] = category;
                    }
                }
            }
            return map;
        }

        /// <summary>
        /// Cleans up and normalizes an item name string for Terraria ID lookup.
        /// </summary>
        public static string CleanItemName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Trim whitespace
            string cleaned = name.Trim();

            cleaned = new string(cleaned.Where(char.IsLetterOrDigit).ToArray());

            // Optional: Handle common punctuation or formatting variations if needed.
            // Terraria's ItemID.Search handles standard apostrophes and casing well 
            // as long as typos or trailing/leading spaces are removed.
            return cleaned;
        }

        public static bool TryGetCategory(string itemName, out string categoryName)
        {
            string cleanedName = CleanItemName(itemName);
            return ItemToCategoryMap.TryGetValue(cleanedName, out categoryName);
        }

        /// <summary>
        /// Resolves a component or category name into all valid Terraria item IDs.
        /// </summary>
        public static List<int> ResolveItemIds(string componentOrCategory)
        {
            var ids = new List<int>();
            string cleanedCategory = CleanItemName(componentOrCategory);

            if (CategoryAliases.TryGetValue(componentOrCategory, out string[] aliases))
            {
                foreach (string name in aliases)
                {
                    string cleanedName = CleanItemName(name);
                    if (ItemID.Search.TryGetId(cleanedName, out int id))
                        ids.Add(id);
                }
            }
            else 
            {
                // Try direct lookup with cleaned name
                if (ItemID.Search.TryGetId(cleanedCategory, out int id))
                {
                    ids.Add(id);
                }
                else
                {
                    // Fallback attempt: strip out punctuation or try alternate casing if necessary
                    // (e.g., removing smart quotes if Python sent unicode apostrophes like ’ instead of ')
                    string normalized = cleanedCategory.Replace('’', '\'');
                    if (ItemID.Search.TryGetId(normalized, out id))
                    {
                        ids.Add(id);
                    }
                }
            }

            return ids;
        }

        // Active component ID groups
        public static List<ImmutableArray<int>> ComponentGroups = new();
        public static HashSet<int> GhostItemIDs = new();
        public static HashSet<int> ConsumedGhostIDs = new();

        public override void OnWorldUnload()
        {
            ClearIDs();
        }

        public static void ClearIDs()
        {
            ComponentGroups.Clear();
            GhostItemIDs.Clear();
            ConsumedGhostIDs.Clear();
        }

        public static void SetGhostItems(ImmutableArray<ImmutableArray<int>> componentGroups)
        {
            ClearIDs();



            if (componentGroups.IsDefaultOrEmpty)
                return;

            foreach (var group in componentGroups)
            {
                if (group.IsDefaultOrEmpty) continue;

                ComponentGroups.Add(group);
                foreach (int id in group)
                {
                    if (id > 0)
                        GhostItemIDs.Add(id);
                }
            }

            ModContent.GetInstance<SeldomArchipelago>().Logger.Info(
                $"[Archipelago] GhostItemIDs initialized with {GhostItemIDs.Count} item IDs across {ComponentGroups.Count} groups."
            );

            RefreshGhostRecipeResults();
        }

        public static void MarkConsumedByItemId(int itemId)
        {
            foreach (var group in ComponentGroups)
            {
                if (group.Contains(itemId))
                {
                    ConsumedGhostIDs.UnionWith(group);
                }
            }

            RefreshGhostRecipeResults();
        }

        public static void SetConsumedItems(IEnumerable<int> itemIds)
        {
            foreach (int id in itemIds)
            {
                MarkConsumedByItemId(id);
            }

            RefreshGhostRecipeResults();
        }

        public static void RefreshGhostRecipeResults()
        {
            if (Main.recipe == null) return;

            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];
                if (recipe != null && GhostItemIDs.Contains(recipe.createItem.type))
                {
                    if (recipe.createItem.TryGetGlobalItem<GhostItemGlobal>(out var ghost))
                    {
                        ghost.isGhostCheck = true;
                    }
                }
            }
        }
    }

    public class GhostItemGlobal : GlobalItem
    {
        public override bool InstancePerEntity => true;
        public bool isGhostCheck = false;

        public bool IsUnconsumedGhost(Item item)
        {
            return isGhostCheck 
                && !GhostItemSystem.ConsumedGhostIDs.Contains(item.type) 
                && GhostItemSystem.GhostItemIDs.Contains(item.type);
        }

        public override GlobalItem Clone(Item item, Item itemClone)
        {
            var myClone = (GhostItemGlobal)base.Clone(item, itemClone);
            myClone.isGhostCheck = this.isGhostCheck;
            return myClone;
        }

        public override void OnCreated(Item item, ItemCreationContext context)
        {
            isGhostCheck = GhostItemSystem.GhostItemIDs.Contains(item.type) 
                        && !GhostItemSystem.ConsumedGhostIDs.Contains(item.type);
        }

        public override bool CanEquipAccessory(Item item, Player player, int slot, bool modded)
        {
            if (GhostItemSystem.GhostItemIDs.Contains(item.type))
                return !isGhostCheck;

            return base.CanEquipAccessory(item, player, slot, modded);
        }

        public override void OnSpawn(Item item, IEntitySource source)
        {
            if (!GhostItemSystem.GhostItemIDs.Contains(item.type))
                return;

            int itemId = item.type;
            if (GhostItemSystem.ConsumedGhostIDs.Contains(itemId))
            {
                isGhostCheck = false;
                return;
            }

            isGhostCheck = true;
            if (item.useStyle == ItemUseStyleID.None)
            {
                item.useStyle = ItemUseStyleID.HoldUp;
                item.useTime = 30;
                item.useAnimation = 30;
                item.consumable = true;
            }

            item.accessory = false;
            item.headSlot = -1;
            item.bodySlot = -1;
            item.legSlot = -1;
            item.wingSlot = 0;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (IsUnconsumedGhost(item))
            {
                tooltips.Add(new TooltipLine(
                    Mod,
                    "ArchipelagoGhostCheck",
                    "[Archipelago Location Check]\nUse / Left-click to send this check"
                ) { OverrideColor = Color.SkyBlue });
            }
        }

        public override bool CanUseItem(Item item, Player player)
        {
            if (IsUnconsumedGhost(item))
            {
                int itemId = item.type;
                var apSystem = ModContent.GetInstance<ArchipelagoSystem>();

                if (apSystem != null)
                {
                    Mod.Logger.Info($"[Archipelago] Triggered location check for Item ID: {itemId}");
                    apSystem.QueueLocationClient(itemId);
                }

                GhostItemSystem.MarkConsumedByItemId(itemId);
                isGhostCheck = false;

                Main.NewText("[Archipelago] Check sent! Item unghosted.", Color.LightSkyBlue);
                return true;
            }

            return base.CanUseItem(item, player);
        }

        public override bool? UseItem(Item item, Player player) => IsUnconsumedGhost(item) ? true : base.UseItem(item, player);

        public override void HoldItem(Item item, Player player)
        {
            if (IsUnconsumedGhost(item) && item.useStyle == ItemUseStyleID.None)
            {
                item.useStyle = ItemUseStyleID.HoldUp;
                item.useTime = 30;
                item.useAnimation = 30;
                item.consumable = true;
            }
        }

        public override void SaveData(Item item, TagCompound tag)
        {
            if (isGhostCheck) tag["isGhostCheck"] = true;
        }

        public override void LoadData(Item item, TagCompound tag)
        {
            isGhostCheck = tag.ContainsKey("isGhostCheck") && tag.GetBool("isGhostCheck");
        }

        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            if (IsUnconsumedGhost(item))
            {
                Main.GetItemDrawFrame(item.type, out Texture2D texture, out Rectangle rect);
                Color ghostColor = (drawColor.R < 50 && drawColor.G < 50 && drawColor.B < 50)
                    ? new Color(50, 90, 130, 100)
                    : new Color(100, 180, 255, 128);

                spriteBatch.Draw(texture, position, rect, ghostColor, 0f, origin, scale, SpriteEffects.None, 0f);
                return false;
            }

            return base.PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }

        public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            if (IsUnconsumedGhost(item))
            {
                Main.GetItemDrawFrame(item.type, out Texture2D texture, out Rectangle rect);
                Vector2 position = item.position - Main.screenPosition + new Vector2(item.width / 2, item.height - rect.Height / 2);
                Vector2 origin = rect.Size() / 2f;

                spriteBatch.Draw(texture, position, rect, new Color(100, 180, 255, 128), rotation, origin, scale, SpriteEffects.None, 0f);
                return false;
            }

            return base.PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
        }
    }
}