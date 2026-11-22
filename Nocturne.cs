using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.0: Nocturne the Cardist's shuffle, expanded. She's free (config Nocturne.Free; the game charges 10 gems) and,
    // when you've signed up for a shuffled run, a preset menu opens:
    //   Full shuffle    robe, relic and arcana (the game's own shuffle)
    //   Arcana only     keep your robe and relic
    //   Robe and relic  keep your arcana
    //   Element build   every arcana from one random element (the game's own one-element shuffle, as Seven Flush does)
    //   Alchemist       full shuffle, and your arcana land on infused / dual versions you've unlocked whenever they can
    // The game's flow stays: talking to her adds the "RandomRun" run modifier, and RandRunMod.Activate calls
    // Player.RandomizeBuild(robe unless you wear Pride, relic, arcana) when the run starts. The preset changes those three
    // switches; Element build forces the element UpdateRandSkillsDict builds the pool from (Neutral = every element).
    internal static class NocturneShuffle
    {
        internal static readonly string[] Names = { "Full shuffle", "Arcana only", "Robe and relic", "Element build", "Alchemist" };
        internal static readonly string[] Descriptions =
        {
            "Robe, relic and arcana",
            "Keep your robe and relic",
            "Keep your arcana",
            "Every arcana from one random element",
            "Full shuffle, landing on your unlocked infused and dual versions",
        };
        internal static readonly ElementType[] Elements = { ElementType.Fire, ElementType.Water, ElementType.Air, ElementType.Lightning, ElementType.Earth };

        internal static bool Shuffling;          // inside RandRunMod.Activate
        internal static ElementType? ForcedElement;
        internal static bool PreferInfused;

        internal static int Preset { get { return Mathf.Clamp(Plugin.NocturnePreset.Value, 0, Names.Length - 1); } }
    }

    // Free shuffles, and the preset menu once you've signed up.
    [HarmonyPatch(typeof(RandomBuildNpc), "HandleConditionalInteraction")]
    static class NocturneInteractPatch
    {
        static float lastOpen = -10f;

        static void Prefix(RandomBuildNpc __instance)
        {
            try { if (Plugin.NocturneFree.Value && __instance != null) Traverse.Create(__instance).Field("cost").SetValue(0); } catch (Exception) { }
        }

        static void Postfix()
        {
            try
            {
                if (GameController.runModifier == null || !GameController.runModifier.HasMod("RandomRun")) return;
                if (Time.unscaledTime - lastOpen < 1.5f) return;
                lastOpen = Time.unscaledTime;
                NocturneMenu.Open();
            }
            catch (Exception ex) { Plugin.Log.LogWarning("NOCTURNE menu failed: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(RandRunMod), "Activate")]
    static class NocturneActivatePatch
    {
        static void Prefix()
        {
            NocturneShuffle.Shuffling = true;
            int p = NocturneShuffle.Preset;
            NocturneShuffle.ForcedElement = p == 3 ? (ElementType?)NocturneShuffle.Elements[UnityEngine.Random.Range(0, NocturneShuffle.Elements.Length)] : null;
            NocturneShuffle.PreferInfused = p == 4;
            Plugin.Log.LogInfo("NOCTURNE shuffling: " + NocturneShuffle.Names[p] + (NocturneShuffle.ForcedElement.HasValue ? " (" + Infusion.Name(NocturneShuffle.ForcedElement.Value) + ")" : ""));
        }

        static void Postfix()
        {
            if (NocturneShuffle.Shuffling && NocturneShuffle.ForcedElement.HasValue)
                IntroUI.Show("Element build: " + Infusion.Name(NocturneShuffle.ForcedElement.Value), "Nocturne dealt you one element", Infusion.ColorOf(NocturneShuffle.ForcedElement.Value), null, 3f);
            NocturneShuffle.Shuffling = false;
            NocturneShuffle.ForcedElement = null;
            NocturneShuffle.PreferInfused = false;
        }

        static Exception Finalizer(Exception __exception)
        {
            NocturneShuffle.Shuffling = false;
            NocturneShuffle.ForcedElement = null;
            NocturneShuffle.PreferInfused = false;
            return __exception;
        }
    }

    // The preset's three switches (robe, relic, arcana).
    [HarmonyPatch(typeof(Player), "RandomizeBuild")]
    [HarmonyPriority(Priority.First)]
    static class NocturneSwitchesPatch
    {
        static void Prefix(ref bool __0, ref bool __1, ref bool __2)
        {
            if (!NocturneShuffle.Shuffling) return;
            switch (NocturneShuffle.Preset)
            {
                case 1: __0 = false; __1 = false; break; // arcana only
                case 2: __2 = false; break;              // robe and relic
            }
        }
    }

    // Element build: the arcana pool from one element. If that element can't fill every slot, the full pool is used.
    [HarmonyPatch(typeof(Player), "UpdateRandSkillsDict")]
    static class NocturneElementPatch
    {
        static bool retrying;

        static void Prefix(ref ElementType __0)
        {
            if (retrying || !NocturneShuffle.Shuffling || !NocturneShuffle.ForcedElement.HasValue) return;
            if (__0 == ElementType.Neutral) __0 = NocturneShuffle.ForcedElement.Value;
        }

        static void Postfix(Player __instance, ElementType __0, ref bool __result)
        {
            if (retrying || __result || !NocturneShuffle.Shuffling || !NocturneShuffle.ForcedElement.HasValue || __0 == ElementType.Neutral) return;
            Plugin.Log.LogInfo("NOCTURNE: not enough " + Infusion.Name(__0) + " arcana unlocked for every slot, using the full pool");
            retrying = true;
            try { __result = (bool)AccessTools.Method(typeof(Player), "UpdateRandSkillsDict").Invoke(__instance, new object[] { ElementType.Neutral }); }
            catch (Exception) { }
            finally { retrying = false; }
            NocturneShuffle.ForcedElement = null;
        }
    }

    // The preset menu (Hub). Same controls as the modifiers menu.
    internal class NocturneMenu : MonoBehaviour
    {
        static NocturneMenu instance;
        internal static bool IsOpen { get { return instance != null && instance.open; } }
        bool open;
        int index;
        float openedAt;
        static GUIStyle titleStyle, rowStyle, small, desc;
        static Font styleFont;

        internal static void Open()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("HybridBosses_NocturneMenu");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<NocturneMenu>();
            }
            instance.open = true;
            instance.index = NocturneShuffle.Preset;
            instance.openedAt = Time.unscaledTime;
        }

        static bool Pad(ChaosInputDevice dev, string action)
        {
            if (dev == null) return false;
            try { return dev.GetButtonDown(action); } catch (Exception) { return false; }
        }

        void Update()
        {
            if (!open) return;
            if (TomiInfusion.InRun) { open = false; return; }
            if (Time.unscaledTime - openedAt < 0.2f) return; // the press that opened it
            Player p = Elites.Player0;
            ChaosInputDevice dev = p != null ? p.inputDevice : null;
            int n = NocturneShuffle.Names.Length;
            if (Input.GetKeyDown(KeyCode.Escape) || Pad(dev, "Cancel")) { open = false; return; }
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Pad(dev, "Skill1")) index = (index - 1 + n) % n;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Pad(dev, "Skill2")) index = (index + 1) % n;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Pad(dev, "Skill3"))
            {
                Plugin.NocturnePreset.Value = index;
                open = false;
                IntroUI.Show("Nocturne will deal: " + NocturneShuffle.Names[index], NocturneShuffle.Descriptions[index], new Color(0.85f, 0.7f, 1f), null, 3f);
                Plugin.Log.LogInfo("NOCTURNE preset: " + NocturneShuffle.Names[index]);
            }
        }

        void OnGUI()
        {
            if (!open) return;
            if (rowStyle == null || GameFont.Changed(ref styleFont))
            {
                titleStyle = GameFont.Style(GUI.skin.label, 22, TextAnchor.MiddleCenter, true);
                rowStyle = GameFont.Style(GUI.skin.label, 17, TextAnchor.MiddleLeft);
                small = GameFont.Style(GUI.skin.label, 12, TextAnchor.MiddleCenter);
                desc = GameFont.Style(GUI.skin.label, 13, TextAnchor.MiddleLeft);
            }
            int n = NocturneShuffle.Names.Length;
            float w = Mathf.Min(640f, Screen.width - 40f), rowH = 44f, h = 100f + rowH * n;
            Rect box = new Rect((Screen.width - w) / 2f, Mathf.Max(20f, (Screen.height - h) / 2f), w, h);
            Color old = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.07f, 0.92f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.8f, 0.45f, 0.9f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(box.x, box.yMax - 2f, box.width, 2f), Texture2D.whiteTexture);
            GUI.color = old;
            GameFont.Shadowed(new Rect(box.x, box.y + 10f, box.width, 30f), "Nocturne's shuffle" + (Plugin.NocturneFree.Value ? "  ·  free" : ""), titleStyle, new Color(1f, 0.9f, 0.6f));
            for (int i = 0; i < n; i++)
            {
                Rect r = new Rect(box.x + 28f, box.y + 50f + i * rowH, box.width - 56f, rowH);
                bool sel = i == index;
                if (sel)
                {
                    GUI.color = new Color(1f, 0.85f, 0.4f, 0.18f);
                    GUI.DrawTexture(new Rect(r.x - 10f, r.y + 2f, r.width + 20f, r.height - 4f), Texture2D.whiteTexture);
                    GUI.color = old;
                }
                GameFont.Shadowed(new Rect(r.x, r.y + 2f, r.width, 22f), (sel ? "> " : "  ") + NocturneShuffle.Names[i] + (i == NocturneShuffle.Preset ? "  (current)" : ""), rowStyle, sel ? new Color(1f, 0.92f, 0.65f) : new Color(0.88f, 0.86f, 0.82f));
                GameFont.Shadowed(new Rect(r.x + 18f, r.y + 22f, r.width, 18f), NocturneShuffle.Descriptions[i], desc, new Color(0.75f, 0.72f, 0.68f));
            }
            GameFont.Shadowed(new Rect(box.x, box.yMax - 32f, box.width, 22f), "Up / Down: choose  ·  Enter: deal  ·  Esc: keep current  ·  pad: Skill1 / Skill2, Skill3 deals, Cancel keeps", small, new Color(0.75f, 0.72f, 0.68f));
        }
    }
}
