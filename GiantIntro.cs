using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // Shows an elemental giant's skills and arcana (one by one, IntroUI) the first time the player gets close.
    // Unlike rivals, nothing freezes: it's a heads-up, not a cutscene.
    internal class GiantIntro : MonoBehaviour
    {
        const float TriggerDistance = 9f;
        Enemy enemy;
        string prefix, kind;
        bool shown;
        static Player player;

        internal static void Attach(Enemy e, string prefix, string kind)
        {
            GiantIntro gi = e.GetComponent<GiantIntro>() ?? e.gameObject.AddComponent<GiantIntro>();
            gi.enemy = e;
            gi.prefix = prefix;
            gi.kind = kind;
            gi.shown = false; // F10 rerolls show the new set
        }

        void Update()
        {
            if (shown || enemy == null) return;
            if (player == null) { player = FindObjectOfType<Player>(); if (player == null) return; }
            if (Vector2.Distance(player.transform.position, enemy.transform.position) > TriggerDistance) return;
            shown = true;
            IntroUI.Show(ElementalGiants.ElementColorName(prefix) + " " + GiantName(enemy), "Mini-boss",
                ElementalGiants.ElementColor(prefix), Entries(), 3f);
        }

        static string GiantName(Enemy e)
        {
            string n = e.GetType().Name; // SuperKnight -> Giant Knight
            return n.StartsWith("Super") ? "Giant " + n.Substring(5) : n;
        }

        List<IntroEntry> Entries()
        {
            List<IntroEntry> list = new List<IntroEntry>();
            if (enemy.availableEnemySkillsDict == null) return list;
            Color el = ElementalGiants.ElementColor(prefix);

            foreach (SkillDef d in ElementalGiants.Skills)
            {
                if (d.Element != prefix || d.Kind != kind || !enemy.availableEnemySkillsDict.ContainsKey(d.Name)) continue;
                IntroEntry ie = new IntroEntry(IntroUI.SkillIcon(d.Name), SkillLabel(d));
                if (ie.Icon == null) ie.Tint = el;
                list.Add(ie);
            }
            foreach (string key in enemy.availableEnemySkillsDict.Keys)
            {
                if (!key.StartsWith("Arcana")) continue;
                ArcanaDef a = ArcanaCatalog.Get(key.Substring(6));
                if (a == null) continue;
                IntroEntry ie = new IntroEntry(IntroUI.SkillIcon(a.SkillID), IntroUI.SkillName(a.SkillID));
                if (ie.Icon == null) ie.Tint = el;
                list.Add(ie);
            }
            return list;
        }

        // "FlameKnightFireWall" -> "Fire Wall"
        static string SkillLabel(SkillDef d)
        {
            string s = d.Name;
            if (s.StartsWith(d.Element)) s = s.Substring(d.Element.Length);
            if (s.StartsWith(d.Kind)) s = s.Substring(d.Kind.Length);
            return EnemyArcana.DisplayName(s);
        }
    }
}
