using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SeldomDespArchipelago.Systems;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace SeldomDespArchipelago
{
    // 1. System to track active ghost pools, consumed checks, and handle world purges & recipe blocking
    public class GhostItemSystem : ModSystem
    {
        // Active ghost check IDs assigned for this Archipelago seed/slot
        public static HashSet<int> GhostItemIDs = new HashSet<int>();

        // Item IDs whose checks have already been sent/consumed (blocks crafting & drops)
        public static HashSet<int> ConsumedGhostIDs = new HashSet<int>();

        public override void OnWorldLoad()
        {
            GhostItemIDs.Clear();
            ConsumedGhostIDs.Clear();
        }

        public override void OnWorldUnload()
        {
            GhostItemIDs.Clear();
            ConsumedGhostIDs.Clear();
        }

        /// <summary>
        /// Call this whenever rarity pools or slot data are loaded in ArchipelagoSystem.
        /// </summary>
        public static void SetGhostItems(IEnumerable<int> itemIds)
        {
            GhostItemIDs = new HashSet<int>(itemIds);
        }

        /// <summary>
        /// Unlocks a real version of an item once another player or location check finds it.
        /// </summary>
        public static void UnlockRealItem(int itemId)
        {
            ConsumedGhostIDs.Remove(itemId);
            GhostItemIDs.Remove(itemId);
        }

        /// <summary>
        /// Dynamically attaches conditions to recipes so that consumed check items cannot be crafted.
        /// </summary>
        public override void PostAddRecipes()
        {
            LocalizedText conditionText = Language.GetOrRegister("Mods.SeldomDespArchipelago.Conditions.LocationCheckSent", () => "Location Check Already Sent");

            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];
                int resultType = recipe.createItem.type;

                // Add a dynamic condition to every recipe producing a potential ghost item
                recipe.AddCondition(
                    conditionText,
                    () => !ConsumedGhostIDs.Contains(resultType)
                );
            }
        }

        /// <summary>
        /// Explicitly deletes the trigger item and purges all ghost-tagged instances of the specified item ID from player inventory, banks, chests, and world.
        /// </summary>
        public static void PurgeGhostItemsFromWorld(Item triggerItem, int itemId)
        {
            Player player = Main.LocalPlayer;

            // 1. Immediately delete the consumed trigger item itself
            triggerItem.TurnToAir();

            void PurgeContainer(Item[] container)
            {
                if (container == null) return;
                for (int i = 0; i < container.Length; i++)
                {
                    if (container[i].type == itemId && container[i].TryGetGlobalItem<GhostItemGlobal>(out var g) && g.IsActiveGhost(container[i]))
                    {
                        container[i].TurnToAir();
                    }
                }
            }

            // 2. Clear player inventory & personal banks
            PurgeContainer(player.inventory);
            PurgeContainer(player.bank.item);
            PurgeContainer(player.bank2.item);
            PurgeContainer(player.bank3.item);
            PurgeContainer(player.bank4.item);

            // 3. Clear open chest if interacting with one
            if (player.chest >= 0 && Main.chest[player.chest] != null)
            {
                PurgeContainer(Main.chest[player.chest].item);
            }

            // 4. Clear dropped item entities on the ground
            for (int i = 0; i < Main.maxItems; i++)
            {
                Item groundItem = Main.item[i];
                if (groundItem.active && groundItem.type == itemId && groundItem.TryGetGlobalItem<GhostItemGlobal>(out var g) && g.IsActiveGhost(groundItem))
                {
                    groundItem.TurnToAir();
                }
            }

            Main.NewText($"[Archipelago] Check sent! All remaining ghost instances were consumed.", Color.LightSkyBlue);
        }

        // --- Persistence Across World Saves ---
        public override void SaveWorldData(TagCompound tag)
        {
            tag["GhostItemIDs"] = GhostItemIDs.ToList();
            tag["ConsumedGhostIDs"] = ConsumedGhostIDs.ToList();
        }

        public override void LoadWorldData(TagCompound tag)
        {
            if (tag.TryGet("GhostItemIDs", out List<int> ghostList))
            {
                GhostItemIDs = new HashSet<int>(ghostList);
            }

            if (tag.TryGet("ConsumedGhostIDs", out List<int> consumedList))
            {
                ConsumedGhostIDs = new HashSet<int>(consumedList);
            }
        }
    }

    // 2. Intercept item properties, creation, tooltips, usage, equipment lockout, and rendering
    public class GhostItemGlobal : GlobalItem
    {
        // Ensures each item instance tracks its own ghost flag independently
        public override bool InstancePerEntity => true;

        public bool isGhostCheck = false;

        // Dynamically determine if an item is an active ghost check
        public bool IsActiveGhost(Item item)
        {
            if (GhostItemSystem.ConsumedGhostIDs.Contains(item.type)) return false;
            return isGhostCheck || GhostItemSystem.GhostItemIDs.Contains(item.type);
        }

        public override GlobalItem Clone(Item item, Item itemClone)
        {
            var myClone = (GhostItemGlobal)base.Clone(item, itemClone);
            myClone.isGhostCheck = this.isGhostCheck;
            return myClone;
        }

        // --- Strip Equipment Slots & Stats ---
        public override void SetDefaults(Item item)
        {
            if (IsActiveGhost(item))
            {
                item.accessory = false;
                item.headSlot = -1;
                item.bodySlot = -1;
                item.legSlot = -1;
                item.wingSlot = 0;
            }
        }

        public override bool CanEquipAccessory(Item item, Player player, int slot, bool modded)
        {
            if (IsActiveGhost(item))
            {
                return false;
            }
            return base.CanEquipAccessory(item, player, slot, modded);
        }

        // --- Block Natural Drops After Check Sent ---
        public override void OnSpawn(Item item, IEntitySource source)
        {
            if (GhostItemSystem.ConsumedGhostIDs.Contains(item.type))
            {
                item.TurnToAir();
                return;
            }

            if (GhostItemSystem.GhostItemIDs.Contains(item.type))
            {
                isGhostCheck = true;
            }
        }

        // --- Usage & Tooltip Rules ---
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (IsActiveGhost(item))
            {
                var line = new TooltipLine(Mod, "ArchipelagoGhostCheck", "[Archipelago Location Check]\nUse / Right-click to send this check and consume all ghost copies.")
                {
                    OverrideColor = Color.SkyBlue
                };
                tooltips.Add(line);
            }
        }

        public override bool CanUseItem(Item item, Player player)
        {
            if (IsActiveGhost(item))
            {
                int itemId = item.type;

                var apSystem = ModContent.GetInstance<ArchipelagoSystem>();
                if (apSystem != null && apSystem.session != null)
                {
                    // Trigger Archipelago location check sending logic here
                    Mod.Logger.Info($"[Archipelago] Triggered location check for Item ID: {itemId}");
                }

                // Mark check as completed to block subsequent crafts and natural drops
                GhostItemSystem.ConsumedGhostIDs.Add(itemId);

                // Delete the triggering item directly and purge all matching ghosts
                GhostItemSystem.PurgeGhostItemsFromWorld(item, itemId);

                return true;
            }

            return base.CanUseItem(item, player);
        }

        public override bool? UseItem(Item item, Player player)
        {
            if (IsActiveGhost(item))
            {
                return true;
            }
            return base.UseItem(item, player);
        }

        public override void HoldItem(Item item, Player player)
        {
            if (IsActiveGhost(item))
            {
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
        }

        // --- Save & Load Data ---
        public override void SaveData(Item item, TagCompound tag)
        {
            if (isGhostCheck)
            {
                tag["isGhostCheck"] = true;
            }
        }

        public override void LoadData(Item item, TagCompound tag)
        {
            isGhostCheck = tag.ContainsKey("isGhostCheck") && tag.GetBool("isGhostCheck");
        }

        // --- Custom Ghost Drawing (Inventory, Crafting Menu, & World) ---
        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            if (IsActiveGhost(item))
            {
                Main.GetItemDrawFrame(item.type, out Texture2D texture, out Rectangle rect);

                Color ghostColor = new Color(100, 180, 255, 128);

                // Check if drawn in a disabled/unmet crafting slot (darkened drawColor)
                if (drawColor.R < 50 && drawColor.G < 50 && drawColor.B < 50)
                {
                    ghostColor = new Color(50, 90, 130, 100);
                }

                spriteBatch.Draw(
                    texture,
                    position,
                    rect,
                    ghostColor,
                    0f,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f
                );

                return false;
            }

            return base.PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }

        public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            if (IsActiveGhost(item))
            {
                Main.GetItemDrawFrame(item.type, out Texture2D texture, out Rectangle rect);

                Vector2 position = item.position - Main.screenPosition + new Vector2(item.width / 2, item.height - rect.Height / 2);
                Vector2 origin = rect.Size() / 2f;

                Color ghostColor = new Color(100, 180, 255, 128);

                spriteBatch.Draw(
                    texture,
                    position,
                    rect,
                    ghostColor,
                    rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f
                );

                return false;
            }

            return base.PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
        }
    }
}