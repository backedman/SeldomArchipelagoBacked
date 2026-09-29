using Terraria;
using Terraria.DataStructures;

public class ArchipelagoEntitySource : EntitySource_Gift
{
    // Pass the entity to the base class and set a custom context string for tagging
    public ArchipelagoEntitySource(Entity entity, string context = "Archipelago") 
        : base(entity, context)
    {
        
    }
}