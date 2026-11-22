using System;
using System.Collections.Generic;
using UnityEngine;

namespace WolHybridBosses
{
    // Safety net for giants: one stuck in an action state (a charge, a borrowed skill, an arcana) for too long is sent
    // back to Chase. FSM.ChangeState runs the state's OnExit, which also undoes things like the Earth Knight
    // Explosion Shield's invulnerability. Logs the state so the real cause can be fixed.
    internal class GiantWatchdog : MonoBehaviour
    {
        const float MaxSeconds = 6f;
        // Never touched: idle/movement states, and everything in the stun/combo cycle. Breaking a giant's armour puts
        // it in "Hurt" (MiniBossHurtState), each combo hit re-enters Hurt and extends the stun (a long combo can stay
        // there well past 6s), and "Ready" (MiniBossReadyState) is its short recovery that re-allows hurt.
        static readonly HashSet<string> Free = new HashSet<string>
        { "Idle", "Chase", "GoBack", "Dead", "Fall", "Frozen", "Hurt", "Ready", "Regen", "Stealth", "Roam" };

        Enemy owner;
        string state;
        float since;

        internal static void Attach(Enemy e)
        {
            if (e == null || e.GetComponent<GiantWatchdog>() != null) return;
            e.gameObject.AddComponent<GiantWatchdog>().owner = e;
        }

        void Update()
        {
            if (owner == null || owner.fsm == null || !owner.enabled) return;
            if (owner.health == null || owner.health.CurrentHealthValue <= 0) return;
            string now = owner.fsm.currentStateName ?? "";
            if (now != state) { state = now; since = Time.time; return; }
            if (Free.Contains(now) || now.Contains("Hurt") || now.Contains("Frozen") || IsHurtState(now)) { since = Time.time; return; }
            if (Time.time - since < MaxSeconds) return;
            since = Time.time;
            try
            {
                // Through "Ready", the giant's own post-attack recovery (re-allows hurt, then its normal transitions),
                // so the armour/stun rules flow as usual; Chase only if it has no Ready state.
                string to = HasState("Ready") ? "Ready" : "Chase";
                Plugin.Log.LogWarning("GIANT " + owner.GetType().Name + " stuck in '" + now + "' for " + MaxSeconds + "s, recovering via " + to);
                if (owner.movement != null) owner.movement.EndMovement();
                owner.fsm.ChangeState(to, false);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("GIANT unstick failed: " + ex.Message); }
        }

        object StateObject(string name)
        {
            System.Collections.IDictionary states = HarmonyLib.Traverse.Create(owner.fsm).Field("states").GetValue() as System.Collections.IDictionary;
            return states != null && states.Contains(name) ? states[name] : null;
        }

        bool HasState(string name) { return StateObject(name) != null; }

        // Any hurt-type state class (EnemyHurtState, MiniBossHurtState, ...) counts as part of the stun cycle.
        bool IsHurtState(string name)
        {
            object s = StateObject(name);
            for (Type t = s != null ? s.GetType() : null; t != null; t = t.BaseType)
                if (t.Name.Contains("HurtState")) return true;
            return false;
        }
    }
}
