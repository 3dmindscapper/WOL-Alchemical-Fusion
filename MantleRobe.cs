using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.0: the White Mantle as a real robe in the game's wardrobe, through LegendAPI's outfit module (the way other
    // mods add robes; LegendAPI handles the wardrobe, the shop and the save data). It unlocks with the first White
    // Alchemist win; wearing it is what turns the mantle on (white look, white aura, infused arcana +15% damage and
    // 15% faster cooldowns).
    // LegendAPI is optional: without it (or if registering fails) the mantle is worn from the modifiers menu instead.
    // Everything touching LegendAPI's types sits in RegisterWithLegendApi, which only runs when LegendAPI is loaded,
    // so its types are never needed otherwise.
    internal static class MantleRobe
    {
        internal const string LegendApiGuid = "xyz.yekoc.wizardoflegend.LegendAPI";
        internal const string OutfitID = "AlchemicalWhiteMantle";

        internal static bool Registered { get; private set; }
        internal static bool Wearing { get; private set; } // set by the robe's equip callback

        internal static bool LegendApiPresent
        {
            get { try { return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(LegendApiGuid); } catch (Exception) { return false; } }
        }

        // Plugin startup (it only builds data: no patches, no game assets).
        internal static void TryRegister()
        {
            if (!LegendApiPresent) { Plugin.Log.LogInfo("MANTLE: LegendAPI not installed, the White Mantle is worn from the modifiers menu"); return; }
            try { RegisterWithLegendApi(); Registered = true; Plugin.Log.LogInfo("MANTLE registered as a wardrobe robe (" + OutfitID + ")"); }
            catch (Exception ex) { Plugin.Log.LogWarning("MANTLE: robe registration failed, falling back to the modifiers menu: " + ex.Message); }
        }

        static void RegisterWithLegendApi()
        {
            List<OutfitModStat> mods = new List<OutfitModStat>
            {
                new OutfitModStat(LegendAPI.Outfits.CustomModType, 0f, 0f, 0f, false),
            };
            Outfit robe = new Outfit(OutfitID, 0, mods, false, false);
            LegendAPI.OutfitInfo info = new LegendAPI.OutfitInfo
            {
                outfit = robe,
                name = "White Mantle",
                unlockCondition = () => Progress.GetBool("mantle.unlocked"),
                customMod = (player, on, outfitChange, stat) =>
                {
                    Wearing = on;
                    Plugin.Log.LogInfo("MANTLE robe " + (on ? "worn" : "taken off"));
                },
                customDesc = (showNumbers, stat) => showNumbers
                    ? "Infused and dual arcana deal +" + Mathf.RoundToInt((WhiteMantle.DamageBonus - 1f) * 100f) + "% damage and cool down " + Mathf.RoundToInt((1f - WhiteMantle.CooldownFactor) * 100f) + "% faster. You glow white."
                    : "Woven from the Primordial Alchemist's essence. Infused arcana grow stronger.",
            };
            LegendAPI.Outfits.Register(info);
        }
    }
}
