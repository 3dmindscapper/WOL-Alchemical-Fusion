using System;
using UnityEngine;

namespace WolHybridBosses
{
    // Rival rewards, by tier: Easy = 1 standard chest, Medium = 2 standard chests, Hard (or an arena Archon) = 1 gold
    // chest. The champion that replaces Sura drops nothing (the final room gives its own rewards).
    // Chests are the game's TreasureChest prefab, as ExitRoomEventHandler.CreateTreasureChest makes them: the prefab's
    // own loot table is the standard chest; "TreasureChestMiniBoss" makes it the gold mini-boss chest
    // (TreasureChest.SetChestType reads the type from the loot table name).
    internal class RivalRewards : MonoBehaviour
    {
        const string GoldTable = "TreasureChestMiniBoss";
        Enemy owner;
        int standard, gold;
        StageVariant variant; // Radiant / Prism rivals: their chests drop only unlocked arcana of that element
        bool given;
        float deadSince = -1f;

        internal static void Attach(Enemy e, RivalMarker mk)
        {
            if (e == null || mk == null || mk.NoChest) return;
            RivalRewards r = e.GetComponent<RivalRewards>() ?? e.gameObject.AddComponent<RivalRewards>();
            r.owner = e;
            r.variant = Variants.FromPrefix(mk.Prefix);
            if (mk.Echo != null) { r.standard = 0; r.gold = 1; return; } // v0.9.0: beating your own Echo is worth a gold chest
            switch (mk.Tier)
            {
                case RivalTier.Easy: r.standard = 1; r.gold = 0; break;
                case RivalTier.Medium: r.standard = 2; r.gold = 0; break;
                case RivalTier.Hard: r.standard = 0; r.gold = 1; break;
                default: r.standard = 0; r.gold = mk.ArenaArchon ? 1 : 0; break; // Final: only an arena Archon
            }
        }

        void Update()
        {
            if (given || owner == null || owner.health == null) return;
            if (owner.health.CurrentHealthValue > 0) return;
            if (deadSince < 0f) { deadSince = Time.time; return; }
            if (Time.time - deadSince < 1f) return; // after its death plays
            given = true;
            Give(owner.transform.position);
        }

        void Give(Vector2 at)
        {
            int total = standard + gold;
            if (total == 0) return;
            for (int i = 0; i < total; i++)
            {
                float x = total == 1 ? 0f : (i - (total - 1) * 0.5f) * 2.2f; // side by side
                Vector2 pos = at + new Vector2(x, 0f);
                try { pos = Entity.GetAvailableNearbyLocation(pos, false, false); } catch (Exception) { }
                Spawn(pos, i >= standard, variant);
            }
            Plugin.Log.LogInfo("RIVAL reward: " + standard + " standard chest(s), " + gold + " gold chest(s)");
        }

        internal static void SpawnChest(Vector2 pos, bool goldChest, StageVariant variant) { Spawn(pos, goldChest, variant); } // v1.1.0: the tournament prize

        static void Spawn(Vector2 pos, bool goldChest, StageVariant variant)
        {
            try
            {
                TreasureChest c = Globals.ChaosInst<TreasureChest>(TreasureChest.Prefab, null, (Vector3)pos, null);
                if (c == null) return;
                if (goldChest) c.lootTableName = GoldTable;
                c.preventMimic = true; // a reward, not a trap
                if (variant == StageVariant.Radiant || variant == StageVariant.Prism) c.gameObject.AddComponent<VariantChest>().Variant = variant;
            }
            catch (Exception ex) { Plugin.Log.LogWarning("RIVAL reward chest failed: " + ex.Message); }
        }
    }
}
