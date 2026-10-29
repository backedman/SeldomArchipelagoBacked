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

namespace SeldomDespArchipelago.Systems
{
    public class GhostItemSystem : ModSystem
    {
        // Active location check IDs assigned for this Archipelago slot
        public static HashSet<int> GhostItemIDs = new HashSet<int>();

        // Item IDs whose checks have already been sent/consumed.
        public static HashSet<int> ConsumedGhostIDs = new HashSet<int>();

        public override void OnWorldUnload()
        {
            Mod.Logger.Warn("We are unloading the world");
            GhostItemIDs.Clear();
            ConsumedGhostIDs.Clear();
        }

        public static void ClearIDs()
        {
            GhostItemIDs.Clear();
            ConsumedGhostIDs.Clear();
        }

        public static void SetGhostItems(Dictionary<string, int> components, bool deleteOnFound = false, bool isReceivable = false)
        {
            foreach (KeyValuePair<string, int> pair in components)
            {
                int id = pair.Value;
                if (id <= 0) continue;

                GhostItemIDs.Add(id);
            }

            RefreshGhostRecipeResults();
        }

        public static void SetConsumedItems(IEnumerable<int[]> itemIds)
        {
            ConsumedGhostIDs.UnionWith(itemIds.SelectMany(ids => ids));
            ModContent.GetInstance<SeldomArchipelago>().Logger.Info($"Set {itemIds.ToList().Count}");
            ModContent.GetInstance<SeldomArchipelago>().Logger.Info($"GhostPool=[{string.Join(", ", GhostItemSystem.GhostItemIDs)}]");
            
            RefreshGhostRecipeResults();
        }

        public static void RefreshGhostRecipeResults()
        {
            if (Main.recipe == null)
                return;

            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];

                if (recipe == null)
                    continue;

                int resultType = recipe.createItem.type;

                if (GhostItemIDs.Contains(resultType))
                {
                    if (recipe.createItem.TryGetGlobalItem<GhostItemGlobal>(
                    out var ghost))
                    {
                        ghost.isGhostCheck = true;
                    }
                }
            }
        }
    }

    // Ghost item properties, creation, tooltips, usage, and rendering
    public class GhostItemGlobal : GlobalItem
    {
        public override bool InstancePerEntity => true;

        public bool isGhostCheck = false;

        public bool IsUnconsumedGhost(Item item)
        {
            if (!isGhostCheck)
                return false;

            if (GhostItemSystem.ConsumedGhostIDs.Contains(item.type))
                return false;

            return GhostItemSystem.GhostItemIDs.Contains(item.type);
        }

        public override GlobalItem Clone(
            Item item,
            Item itemClone)
        {
            var myClone =
                (GhostItemGlobal)base.Clone(item, itemClone);

            myClone.isGhostCheck = this.isGhostCheck;

            return myClone;
        }

        public override void OnCreated(
            Item item,
            ItemCreationContext context)
        {
            if (!GhostItemSystem.GhostItemIDs.Contains(item.type) || GhostItemSystem.ConsumedGhostIDs.Contains(item.type))
            {
                isGhostCheck = false;
            }
            else
            {
                isGhostCheck = true;
            }
        }

        public override bool CanEquipAccessory(
            Item item,
            Player player,
            int slot,
            bool modded)
        {
            if (GhostItemSystem.GhostItemIDs.Contains(item.type))
            {
                return !isGhostCheck;
            }

            return base.CanEquipAccessory(
                item,
                player,
                slot,
                modded
            );
        }

        public override void OnSpawn(
            Item item,
            IEntitySource source)
        {
            if (!GhostItemSystem.GhostItemIDs.Contains(item.type))
            {
                return;
            }

            int itemId = item.type;

            if (source is ArchipelagoEntitySource)
            {
                isGhostCheck = false;
                return;
            }

            if (GhostItemSystem.ConsumedGhostIDs.Contains(itemId))
            {
                isGhostCheck = false;
                return;
            }

            if (GhostItemSystem.GhostItemIDs.Contains(itemId))
            {
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
        }

        public override void ModifyTooltips(
            Item item,
            List<TooltipLine> tooltips)
        {
            if (IsUnconsumedGhost(item))
            {
                var line = new TooltipLine(
                    Mod,
                    "ArchipelagoGhostCheck",
                    "[Archipelago Location Check]\n" +
                    "Use / Left-click to send this check"
                )
                {
                    OverrideColor = Color.SkyBlue
                };

                tooltips.Add(line);
            }
        }

        public override bool CanUseItem(
            Item item,
            Player player)
        {
            if (IsUnconsumedGhost(item))
            {
                int itemId = item.type;

                var apSystem =
                    ModContent.GetInstance<ArchipelagoSystem>();

                if (apSystem != null &&
                    apSystem.session != null)
                {
                    Mod.Logger.Info(
                        $"[Archipelago] Triggered location check for Item ID: {itemId}"
                    );
                    apSystem.QueueLocationClient(itemId);
                }

                // Mark as consumed globally, which unghosts this item instance and all others like it
                GhostItemSystem.ConsumedGhostIDs.Add(itemId);
                isGhostCheck = false;

                // Reapply standard stats onto the used item so it functions normally immediately
                item.accessory = true; // or whatever base value, but lets it be usable
                
                Main.NewText(
                    "[Archipelago] Check sent! Item unghosted.",
                    Color.LightSkyBlue
                );

                return true;
            }

            return base.CanUseItem(item, player);
        }

        public override bool? UseItem(
            Item item,
            Player player)
        {
            if (IsUnconsumedGhost(item))
            {
                return true;
            }

            return base.UseItem(item, player);
        }

        public override void HoldItem(
            Item item,
            Player player)
        {
            if (IsUnconsumedGhost(item))
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

        public override void SaveData(
            Item item,
            TagCompound tag)
        {
            if (isGhostCheck)
            {
                tag["isGhostCheck"] = true;
            }
        }

        public override void LoadData(
            Item item,
            TagCompound tag)
        {
            isGhostCheck =
                tag.ContainsKey("isGhostCheck") &&
                tag.GetBool("isGhostCheck");
        }

        public override bool PreDrawInInventory(
            Item item,
            SpriteBatch spriteBatch,
            Vector2 position,
            Rectangle frame,
            Color drawColor,
            Color itemColor,
            Vector2 origin,
            float scale)
        {
            if (IsUnconsumedGhost(item))
            {
                Main.GetItemDrawFrame(
                    item.type,
                    out Texture2D texture,
                    out Rectangle rect
                );

                Color ghostColor =
                    new Color(100, 180, 255, 128);

                if (drawColor.R < 50 &&
                    drawColor.G < 50 &&
                    drawColor.B < 50)
                {
                    ghostColor =
                        new Color(50, 90, 130, 100);
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

            return base.PreDrawInInventory(
                item,
                spriteBatch,
                position,
                frame,
                drawColor,
                itemColor,
                origin,
                scale
            );
        }

        public override bool PreDrawInWorld(
            Item item,
            SpriteBatch spriteBatch,
            Color lightColor,
            Color alphaColor,
            ref float rotation,
            ref float scale,
            int whoAmI)
        {
            if (IsUnconsumedGhost(item))
            {
                Main.GetItemDrawFrame(
                    item.type,
                    out Texture2D texture,
                    out Rectangle rect
                );

                Vector2 position =
                    item.position -
                    Main.screenPosition +
                    new Vector2(
                        item.width / 2,
                        item.height -
                        rect.Height / 2
                    );

                Vector2 origin =
                    rect.Size() / 2f;

                Color ghostColor =
                    new Color(100, 180, 255, 128);

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

            return base.PreDrawInWorld(
                item,
                spriteBatch,
                lightColor,
                alphaColor,
                ref rotation,
                ref scale,
                whoAmI
            );
        }
    }
}