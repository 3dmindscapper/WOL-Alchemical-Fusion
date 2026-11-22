using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.9.16 / v0.9.17: rival rematches. When you beat a rival (not the Echo, not an Archon), it sometimes (config
    // RivalWizards.GrudgeChance) wants a rematch later in the same run: on each later stage there's a 60% chance its
    // portal opens (in the start room, like any rival portal). It comes back one tier harder, with each of its arcana
    // 40% likely to be infused with another element (in that element's colours and damage). Beating the rematch can
    // drop one of its arcana: a regular version (35%) or, more rarely, an infused one (12%). Unlocked arcana you don't
    // hold only. One rematch pending at a time; it ends with the run. (The v0.9.16 version, where a rival fled with
    // one of your arcana, is gone.)
    internal sealed class GrudgeRecord
    {
        public string Prefix;
        public RivalTier Tier;
    }

    internal static class Grudges
    {
        internal static GrudgeRecord Pending;
        internal const float OpenChance = 0.6f, InfuseChance = 0.4f, DropRegular = 0.35f, DropInfused = 0.12f;

        internal static void EndRun()
        {
            if (Pending == null) return;
            Plugin.Log.LogInfo("REMATCH ended with the run (never fought)");
            Pending = null;
        }

        static RivalTier Harder(RivalTier t) { return t == RivalTier.Easy ? RivalTier.Medium : RivalTier.Hard; }

        // A rival was beaten: maybe it wants a rematch.
        internal static bool MaybeRecord(string prefix, RivalTier tier, bool echo, bool archon)
        {
            if (Pending != null || echo || archon || string.IsNullOrEmpty(prefix)) return false;
            if (UnityEngine.Random.value >= Plugin.GrudgeChance.Value) return false;
            Pending = new GrudgeRecord { Prefix = prefix, Tier = Harder(tier) };
            Plugin.Log.LogInfo("REMATCH the " + prefix + " rival will look for you later this run (" + Pending.Tier + ")");
            return true; // the rival says so itself (RivalEvent, v0.9.20)
        }

        // Its arcana, each maybe infused (the rival casts them in that element: EchoInfusions in MakeEnemyAttack).
        internal static void InfuseKit(Enemy e, RivalMarker mk)
        {
            if (e == null || mk == null || mk.Arcana == null) return;
            EchoInfusions ei = e.GetComponent<EchoInfusions>() ?? e.gameObject.AddComponent<EchoInfusions>();
            foreach (ArcanaDef d in mk.Arcana)
                if (d != null && UnityEngine.Random.value < InfuseChance) ei.Map[d.SkillID] = Infusion.RandomOther(d.SkillID);
            mk.Infused = ei.Map;
        }

        // v0.9.18: dual-infused arcana for the strongest rivals: an Archon's (or final champion's) arcana are each 20%
        // likely to be dual, a Hard rival's 7%. Two different elements, alternating cast by cast.
        internal static void DualKit(Enemy e, RivalMarker mk)
        {
            if (e == null || mk == null || mk.Arcana == null || !Plugin.InfusionEnabled.Value) return;
            float chance = (mk.Tier == RivalTier.Final || mk.ArenaArchon) ? 0.2f : mk.Tier == RivalTier.Hard ? 0.07f : 0f;
            if (chance <= 0f) return;
            EchoInfusions ei = null;
            foreach (ArcanaDef d in mk.Arcana)
            {
                if (d == null || UnityEngine.Random.value >= chance) continue;
                ElementType a = Infusion.RandomOther(d.SkillID), b = a;
                for (int tries = 0; tries < 8 && Infusion.Name(b) == Infusion.Name(a); tries++) b = Infusion.RandomOther(d.SkillID);
                if (Infusion.Name(b) == Infusion.Name(a)) continue;
                if (ei == null) ei = e.GetComponent<EchoInfusions>() ?? e.gameObject.AddComponent<EchoInfusions>();
                ei.Map[d.SkillID] = a;
                ei.Second[d.SkillID] = b;
            }
            if (ei == null) return;
            mk.Infused = ei.Map;
            mk.InfusedSecond = ei.Second;
            Plugin.Log.LogInfo("RIVAL dual arcana: " + ei.Second.Count);
        }
    }

    // On a rematch rival: beating it may drop one of its arcana.
    internal class GrudgeReturn : MonoBehaviour
    {
        public GrudgeRecord Record;
        Enemy enemy;
        bool done;

        void Start() { enemy = GetComponent<Enemy>(); }

        void Update()
        {
            if (done || enemy == null || enemy.health == null || enemy.health.CurrentHealthValue > 0) return;
            done = true;
            if (Grudges.Pending == Record) Grudges.Pending = null;
            Plugin.Log.LogInfo("REMATCH won");
            RivalMarker mk = GetComponent<RivalMarker>();
            float roll = UnityEngine.Random.value;
            bool infused = roll < Grudges.DropInfused;
            if (!infused && roll >= Grudges.DropInfused + Grudges.DropRegular) return;
            string id = PickArcana(mk, infused);
            if (id == null) return;
            if (infused)
            {
                ElementType el;
                if (mk == null || mk.Infused == null || !mk.Infused.TryGetValue(id, out el)) el = Infusion.RandomOther(id);
                Infusion.SetPending(id, el);
            }
            try { LootManager.DropSkill((Vector2)transform.position + new Vector2(0f, 1.2f), 1, id, 0f, 0f, null, false, false); }
            catch (Exception ex) { Plugin.Log.LogWarning("REMATCH drop failed: " + ex.Message); return; }
            Plugin.Log.LogInfo("REMATCH dropped " + id + (infused ? " (infused)" : ""));
        }

        // One of its arcana you've unlocked and don't hold (an infused drop prefers the ones it cast infused).
        static string PickArcana(RivalMarker mk, bool infused)
        {
            if (mk == null || mk.Arcana == null) return null;
            HashSet<string> held = TomiInfusion.HeldIds();
            List<string> ids = new List<string>();
            foreach (ArcanaDef d in mk.Arcana)
                if (d != null && d.Port == ArcanosPort.None && !held.Contains(d.SkillID) && ArcanosBridge.Unlocked(d.SkillID) && !ids.Contains(d.SkillID)) ids.Add(d.SkillID);
            if (infused && mk.Infused != null) { List<string> inf = ids.FindAll(i => mk.Infused.ContainsKey(i)); if (inf.Count > 0) ids = inf; }
            return ids.Count > 0 ? ids[UnityEngine.Random.Range(0, ids.Count)] : null;
        }
    }
}
