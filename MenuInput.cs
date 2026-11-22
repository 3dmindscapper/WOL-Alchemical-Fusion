using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.3: shared input for the mod's own menus (Nocturne's presets, the modifiers menu, the League board).
    // - While one is open the players are paused, so the stick / D-pad moves the menu and not the wizard.
    // - Stick / D-pad navigation with a held-direction repeat (the game's menus feel the same on a pad).
    internal static class MenuInput
    {
        static readonly HashSet<string> holders = new HashSet<string>();
        static bool paused;

        // Call every frame with whether this menu is open: the first open menu pauses, the last one closing unpauses.
        // Inside Tomi's / Tomix's spellbook the book keeps its own pause, so closing a menu there doesn't release it.
        internal static void Hold(string who, bool open)
        {
            bool changed = open ? holders.Add(who) : holders.Remove(who);
            if (!changed) return;
            if (holders.Count > 0 && !paused)
            {
                paused = true;
                try { GameController.PauseAllPlayers(true); } catch (Exception) { }
            }
            else if (holders.Count == 0 && paused)
            {
                paused = false;
                if (!TomiInfusion.MenuOpen) try { GameController.PauseAllPlayers(false); } catch (Exception) { }
            }
        }

        class Axis { public int dir; public float next; }
        static readonly Dictionary<string, Axis> axes = new Dictionary<string, Axis>();

        // -1 / 0 / +1 from the stick or D-pad on one axis: once on the press, then repeating while held.
        static int Step(string key, float value)
        {
            Axis a;
            if (!axes.TryGetValue(key, out a)) { a = new Axis(); axes[key] = a; }
            int dir = value > 0.55f ? 1 : value < -0.55f ? -1 : 0;
            float now = Time.unscaledTime;
            if (dir == 0) { a.dir = 0; return 0; }
            if (dir != a.dir) { a.dir = dir; a.next = now + 0.38f; return dir; }
            if (now >= a.next) { a.next = now + 0.13f; return dir; }
            return 0;
        }

        static Vector2 Move(ChaosInputDevice dev)
        {
            if (dev == null) return Vector2.zero;
            try { return dev.GetMoveVector(); } catch (Exception) { return Vector2.zero; }
        }

        // Down on the stick = +1 (the next row), up = -1.
        internal static int Vertical(ChaosInputDevice dev, string menu) { return Step(menu + ".v", -Move(dev).y); }
        internal static int Horizontal(ChaosInputDevice dev, string menu) { return Step(menu + ".h", Move(dev).x); }

        internal static bool Pad(ChaosInputDevice dev, string action)
        {
            if (dev == null) return false;
            try { return dev.GetButtonDown(action); } catch (Exception) { return false; }
        }
    }
}
