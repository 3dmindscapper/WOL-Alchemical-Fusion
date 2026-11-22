using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.16: an in-game modifiers menu (Hub only) for the mod's main switches and chances, so nothing needs the config
    // file (handy on the Steam Deck). Changes are saved to the config right away and apply from the next roll
    // (the next stage, spawn or run).
    //   Open:   F1 anywhere in the Hub, or the Modifiers button (config Modifiers.Button, default Skill4) while Tomi's
    //           spellbook is open there.
    //   Keys:   Up / Down (W / S), Left / Right or Enter / Space to change, F1 / Esc to close.
    //   Pad:    Skill1 up, Skill2 down, Skill3 change, the Modifiers button (or Cancel) closes.
    internal class ModifiersMenu : MonoBehaviour
    {
        sealed class Item
        {
            public string Label;
            public Func<string> Value;
            public Action<int> Change; // +1 next, -1 previous
        }

        internal static bool IsOpen;
        static List<Item> items;
        int index;
        static GUIStyle titleStyle, rowStyle;
        static Font styleFont;

        static Item Toggle(string label, ConfigEntry<bool> e)
        {
            return new Item { Label = label, Value = () => e.Value ? "On" : "Off", Change = d => e.Value = !e.Value };
        }

        // A chance / multiplier cycled through a few presets (0 = off).
        static Item Steps(string label, ConfigEntry<float> e, float[] steps, bool percent)
        {
            return new Item
            {
                Label = label,
                Value = () => e.Value <= 0f ? "Off" : percent ? Mathf.RoundToInt(e.Value * 100f) + "%" : "x" + e.Value.ToString("0.##"),
                Change = d =>
                {
                    int i = 0;
                    float best = float.MaxValue;
                    for (int k = 0; k < steps.Length; k++) { float diff = Mathf.Abs(steps[k] - e.Value); if (diff < best) { best = diff; i = k; } }
                    i = (i + d + steps.Length) % steps.Length;
                    e.Value = steps[i];
                }
            };
        }

        static List<Item> Items
        {
            get
            {
                if (items != null) return items;
                items = new List<Item>
                {
                    Steps("Chaos / variant stages", Plugin.ChaosStageChance, new[] { 0f, 0.1f, 0.15f, 0.25f, 0.4f }, true),
                    Toggle("Elemental giants", Plugin.ElementalGiants),
                    Steps("Elemental coffins, cyclops, blobs, ghouls", Plugin.ElementalNeutralChance, new[] { 0f, 0.3f, 0.6f, 1f }, true),
                    Steps("Special mini-bosses", Plugin.MinibossFightChance, new[] { 0f, 0.15f, 0.3f, 0.5f, 1f }, true),
                    Steps("Elites", Plugin.EliteChanceMultiplier, new[] { 0f, 0.5f, 1f, 1.5f, 2f }, false),
                    Steps("Rival portals", Plugin.RivalEventChance, new[] { 0f, 0.15f, 0.3f, 0.5f }, true),
                    Steps("Echo of your last run", Plugin.EchoChance, new[] { 0f, 0.2f, 0.35f, 0.6f }, true),
                    Steps("Rival rematches", Plugin.GrudgeChance, new[] { 0f, 0.15f, 0.3f, 0.5f }, true),
                    Steps("Infusion trials", Plugin.TrialChance, new[] { 0f, 0.08f, 0.12f, 0.25f }, true),
                    Toggle("Infusion at Tomi", Plugin.InfusionEnabled),
                    Toggle("Corrupted Council", Plugin.CorruptedCouncil),
                    Toggle("Infused Council", Plugin.InfusedCouncil),
                    Toggle("Final champions", Plugin.FinalChampions),
                    Toggle("Bounties", Plugin.BountiesEnabled),
                };
                return items;
            }
        }

        void Update()
        {
            bool inHub = !TomiInfusion.InRun;
            if (!inHub) { IsOpen = false; return; }
            ChaosInputDevice dev = TomiInfusion.Device;
            if (!IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.F1) || (TomiInfusion.MenuOpen && Pad(dev, Plugin.ModifiersButton.Value))) { IsOpen = true; index = 0; }
                return;
            }
            if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.Escape) || Pad(dev, Plugin.ModifiersButton.Value) || Pad(dev, "Cancel")) { IsOpen = false; return; }
            int n = Items.Count;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Pad(dev, "Skill1")) index = (index - 1 + n) % n;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Pad(dev, "Skill2")) index = (index + 1) % n;
            int change = 0;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Pad(dev, "Skill3")) change = 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) change = -1;
            if (change != 0)
            {
                try { Items[index].Change(change); Plugin.Log.LogInfo("MODIFIERS " + Items[index].Label + " -> " + Items[index].Value()); } catch (Exception) { }
            }
        }

        static bool Pad(ChaosInputDevice dev, string action)
        {
            if (dev == null || string.IsNullOrEmpty(action)) return false;
            try { return dev.GetButtonDown(action.Trim()); } catch (Exception) { return false; }
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            if (rowStyle == null || GameFont.Changed(ref styleFont))
            {
                titleStyle = GameFont.Style(GUI.skin.label, 22, TextAnchor.MiddleCenter, true);
                rowStyle = GameFont.Style(GUI.skin.label, 17, TextAnchor.MiddleLeft);
            }
            List<Item> list = Items;
            float w = Mathf.Min(620f, Screen.width - 40f), rowH = 26f, h = 96f + rowH * list.Count;
            Rect box = new Rect((Screen.width - w) / 2f, Mathf.Max(20f, (Screen.height - h) / 2f), w, h);
            Color old = GUI.color;
            GUI.color = new Color(0.04f, 0.03f, 0.07f, 0.92f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.8f, 0.45f, 0.9f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(box.x, box.yMax - 2f, box.width, 2f), Texture2D.whiteTexture);
            GUI.color = old;
            GameFont.Shadowed(new Rect(box.x, box.y + 10f, box.width, 30f), "Hybrid Bosses  ·  Modifiers", titleStyle, new Color(1f, 0.9f, 0.6f));
            for (int i = 0; i < list.Count; i++)
            {
                Rect r = new Rect(box.x + 28f, box.y + 50f + i * rowH, box.width - 56f, rowH);
                bool sel = i == index;
                if (sel)
                {
                    GUI.color = new Color(1f, 0.85f, 0.4f, 0.18f);
                    GUI.DrawTexture(new Rect(r.x - 10f, r.y, r.width + 20f, r.height), Texture2D.whiteTexture);
                    GUI.color = old;
                }
                Color c = sel ? new Color(1f, 0.92f, 0.65f) : new Color(0.88f, 0.86f, 0.82f);
                GameFont.Shadowed(r, (sel ? "> " : "  ") + list[i].Label, rowStyle, c);
                string v = list[i].Value();
                GameFont.Shadowed(new Rect(r.x, r.y, r.width, r.height), "<color=#" + (v == "Off" ? "8a8580" : "ffd27a") + ">" + v + "</color>", RightStyle(), c);
            }
            GameFont.Shadowed(new Rect(box.x, box.yMax - 34f, box.width, 24f), "Up / Down: choose  ·  Left / Right, Enter: change  ·  F1 / Esc: close  ·  pad: Skill1 / Skill2 / Skill3, " + Plugin.ModifiersButton.Value + " closes",
                CentreSmall(), new Color(0.75f, 0.72f, 0.68f));
        }

        static GUIStyle right, small;
        static GUIStyle RightStyle() { if (right == null || right.font != rowStyle.font) right = new GUIStyle(rowStyle) { alignment = TextAnchor.MiddleRight }; return right; }
        static GUIStyle CentreSmall() { if (small == null || small.font != rowStyle.font) small = new GUIStyle(rowStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 12 }; return small; }
    }
}
