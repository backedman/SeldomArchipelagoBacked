using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SeldomDespArchipelago.Players;
using System.Collections;

namespace SeldomDespArchipelago.Systems
{
    public class ArchipelagoRestrictionGlobalItem : GlobalItem
    {
        public override bool CanUseItem(Item item, Player player)
        {
            var apPlayer = player.GetModPlayer<ArchipelagoPlayer>();

            if (player.whoAmI == Main.myPlayer)
            {
                // Ensure the player is actually holding/selecting THIS specific item
                bool isSelectedItem = player.HeldItem == item || Main.mouseItem == item;
                bool justClicked = Main.mouseLeft && Main.mouseLeftRelease;

                // 1. Health Potions
                if (item.healLife > 0 && !apPlayer.CanUseHealthPotions)
                {
                    if (isSelectedItem && justClicked)
                    {
                        Main.NewText("Health Potions are currently locked!", Microsoft.Xna.Framework.Color.OrangeRed);
                        apPlayer.SendLocationCheckForPotion(item.type); //Edge case for if a health potion is a ghosted item
                    }
                    return false;
                }

                // 2. Mana Potions
                if (item.healMana > 0 && !apPlayer.CanUseManaPotions)
                {
                    if (isSelectedItem && justClicked)
                    {
                        Main.NewText("Mana Potions are currently locked!", Microsoft.Xna.Framework.Color.OrangeRed);
                    }
                    return false;
                }
            }

            // 3. Recall Items (Magic Mirror, Recall Potion, Potion of Return, Cell Phone, Shellphone, Wormhole Potion)
            if (IsRecallItem(item.type) && !apPlayer.CanRecall)
            {
                Main.NewText("Recall abilities are currently locked!", Microsoft.Xna.Framework.Color.OrangeRed);
                return false;
            }

            // 4. Specific Buff Potions Lockdown
            if (item.buffType > 0 && item.healLife <= 0 && item.healMana <= 0)
            {
                if (apPlayer.BannedPotions.Contains(item.type))
                {
                    Main.NewText($"This potion ({item.Name}) has not been unlocked yet!", Microsoft.Xna.Framework.Color.OrangeRed);
                    return false;
                }
            }



            return base.CanUseItem(item, player);
        }
        


        private static bool IsRecallItem(int itemType)
        {
            return itemType == ItemID.MagicMirror ||
                   itemType == ItemID.IceMirror ||
                   itemType == ItemID.RecallPotion ||
                   itemType == ItemID.PotionOfReturn ||
                   itemType == ItemID.CellPhone ||
                   itemType == ItemID.Shellphone ||
                   itemType == ItemID.ShellphoneOcean ||
                   itemType == ItemID.ShellphoneHell ||
                   itemType == ItemID.ShellphoneSpawn ||
                   itemType == ItemID.WormholePotion ||
                   itemType == ItemID.DemonConch ||
                   itemType == ItemID.MagicConch;
        }
    }
}