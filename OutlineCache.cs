using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.15 (Steam Deck): outline colours that change every frame (elite pulses, the Archon's shimmer, the
    // Vengeance glow) used to look up every sprite renderer and touch its material each frame. This keeps the list of
    // the enemy's own outline materials (the same per-renderer instances sr.material hands out, so the game still
    // draws exactly these) and refreshes it every 2 seconds, so parts added or swapped later are still picked up.
    internal sealed class OutlineCache
    {
        readonly Component owner;
        readonly List<Material> mats = new List<Material>();
        float refreshAt = -1f;

        internal OutlineCache(Component owner) { this.owner = owner; }

        List<Material> Materials
        {
            get
            {
                if (Time.time >= refreshAt) Refresh();
                return mats;
            }
        }

        void Refresh()
        {
            refreshAt = Time.time + 2f;
            mats.Clear();
            if (owner == null) return;
            foreach (SpriteRenderer sr in owner.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                Material sm = sr.sharedMaterial;
                if (sm == null || !sm.HasProperty("_OutlineColor")) continue;
                mats.Add(sr.material);
            }
        }

        internal void Set(Color c)
        {
            List<Material> list = Materials;
            for (int i = 0; i < list.Count; i++) if (list[i] != null) list[i].SetColor("_OutlineColor", c);
        }

        // The current outline colour (of the first outlined sprite), if any.
        internal Color? Current
        {
            get
            {
                List<Material> list = Materials;
                for (int i = 0; i < list.Count; i++) if (list[i] != null) return list[i].GetColor("_OutlineColor");
                return null;
            }
        }
    }
}
