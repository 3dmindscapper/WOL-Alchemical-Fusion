using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // v0.6.3: WoL Arcanos' Radiant and Prism arcana, rebuilt for enemies.
    // Arcanos writes them for the player (mouse aim, inventory, HUD, the player's skill events), so they can't run on
    // an enemy. What makes them what they are is simple, and is reused here from the installed mod (nothing shipped):
    //   - their own sprites (Recursos.Sprite: lanza_luz, latigo_luz, fogonazo_luz, halo_sagrado, pluma_luz, mota_luz,
    //     arco_prisma_<element>...), their own effects (Efectos.HacesDeLuz, Destellos, ImpactoRayo...) and game sounds;
    //   - the game's generic circle hitbox (Attack.CirclePrefab + EnableForOneFrame), as Arcanos' attacking training
    //     dummy does, with the arcana's own stat data (real damage), owned by the enemy;
    //   - Radiant: hits stack Flashes; enough Flashes dazzle the player (Arcanos blinds enemies; on the player a short
    //     glare, controls untouched). Prism: each hit is the next of the five base elements, with its status.
    internal enum ArcanosPort { None, LightLance, Flash, HolyHalo, LightWhip, LightFeathers, ElementalPrism, Triad }

    internal static class ArcanosArts
    {
        // The arcana rebuilt so far: Arcanos id, port, variant, AI delivery (for range / priority) and timing.
        static readonly object[][] Table =
        {
            new object[] { "LanzaDeLuz",      ArcanosPort.LightLance,     "Radiant", Delivery.Projectile, 0.6f, 7f, 2f, 11f, 5 },
            new object[] { "FogonazoDeLuz",   ArcanosPort.Flash,          "Radiant", Delivery.AtTarget,   0.8f, 9f, 1f, 9f,  6 },
            new object[] { "HaloSagrado",     ArcanosPort.HolyHalo,       "Radiant", Delivery.AtSelf,     0.7f, 11f, 0f, 4f, 7 },
            new object[] { "LatigoDeLuz",     ArcanosPort.LightWhip,      "Radiant", Delivery.Melee,      0.5f, 5f, 0f, 4.5f, 4 },
            new object[] { "AlasDeLuz",       ArcanosPort.LightFeathers,  "Radiant", Delivery.Projectile, 0.7f, 8f, 2f, 10f, 5 },
            new object[] { "PrismaElemental", ArcanosPort.ElementalPrism, "Prism",   Delivery.AtTarget,   0.8f, 9f, 1f, 9f,  6 },
            new object[] { "Triada",          ArcanosPort.Triad,          "Prism",   Delivery.Projectile, 0.6f, 7f, 2f, 11f, 5 },
        };

        // Defs for the catalog, only for arcana this Arcanos version really has.
        internal static List<ArcanaDef> Defs()
        {
            List<ArcanaDef> list = new List<ArcanaDef>();
            if (!ArcanosBridge.Present) return list;
            HashSet<string> known = new HashSet<string>(ArcanosBridge.IdsOf(StageVariant.Radiant));
            known.UnionWith(ArcanosBridge.IdsOf(StageVariant.Prism));
            foreach (object[] r in Table)
            {
                string id = (string)r[0];
                if (known.Count > 0 && !known.Contains(id)) continue;
                ArcanaDef d = new ArcanaDef((string)r[2], id, null, (Delivery)r[3], (float)r[4], (float)r[5], (float)r[6], (float)r[7], (int)r[8]);
                d.Port = (ArcanosPort)r[1];
                d.Family = d.Delivery == Delivery.Melee ? ArcanaFamily.Melee : d.Delivery == Delivery.Projectile ? ArcanaFamily.Projectile : ArcanaFamily.Spell;
                list.Add(d);
            }
            return list;
        }

        // ---------- casting ----------

        internal static void Cast(Enemy caster, ArcanaDef def, Vector2 dir, bool empowered, bool log)
        {
            if (caster == null || !ArcanosBridge.Present) return;
            Vector2 origin = caster.hurtBoxTransform != null ? (Vector2)caster.hurtBoxTransform.position : (Vector2)caster.transform.position;
            Vector2? tgt = ArcanaCaster.TargetOf(caster);
            Vector2 target = tgt.HasValue ? tgt.Value : origin + dir * 4f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
            dir.Normalize();
            IEnumerator run = null;
            switch (def.Port)
            {
                case ArcanosPort.LightLance: run = Lance(caster, def, origin, dir, empowered, "lanza_luz", 22f, 0.65f, 0.7f, empowered ? 2 : 1); break;
                case ArcanosPort.LightFeathers: run = Feathers(caster, def, origin, dir, empowered); break;
                case ArcanosPort.Flash: run = Flash(caster, def, target, empowered); break;
                case ArcanosPort.HolyHalo: run = Halo(caster, def, empowered); break;
                case ArcanosPort.LightWhip: run = Whip(caster, def, origin, dir, empowered); break;
                case ArcanosPort.ElementalPrism: run = PrismBursts(caster, def, target, empowered); break;
                case ArcanosPort.Triad: run = Triad(caster, def, origin, dir, empowered); break;
            }
            if (run != null) caster.StartCoroutine(Guard(run));
            if (log) Plugin.Log.LogInfo("ARCANA " + caster.GetType().Name + " cast " + def.SkillID + " [Arcanos " + def.Element + " port" + (empowered ? ", enhanced" : "") + "]");
        }

        // A port must never take its caster down with it.
        static IEnumerator Guard(IEnumerator inner)
        {
            while (true)
            {
                object cur;
                try { if (!inner.MoveNext()) yield break; cur = inner.Current; }
                catch (Exception ex) { Plugin.Log.LogWarning("ARCANA Arcanos port failed: " + ex.Message); yield break; }
                yield return cur;
            }
        }

        static bool Alive(Enemy e) { return e != null && e.health != null && e.health.CurrentHealthValue > 0; }

        // Light Lance: a spear of light flies straight; it bursts into sparkles on a hit or a wall.
        static IEnumerator Lance(Enemy caster, ArcanaDef def, Vector2 from, Vector2 dir, bool empowered, string sprite, float speed, float life, float radius, int flashes, ElementType? element = null, Color? tint = null)
        {
            GameObject vis = Visual(sprite, from, dir, empowered ? 1.25f : 1f, tint);
            ArcanosBridge.Sound("PortalShine", from);
            float t = 0f;
            Vector2 pos = from;
            while (t < life)
            {
                float step = speed * Time.deltaTime;
                Vector2 next = pos + dir * step;
                if (Physics2D.Linecast(pos, next, LayerMask.GetMask("Wall"))) break;
                pos = next;
                if (vis != null) vis.transform.position = pos;
                if (PlayerNear(pos, radius)) { Hit(caster, def, pos, radius, empowered, element, flashes); break; }
                t += Time.deltaTime;
                yield return null;
            }
            ArcanosBridge.Fx("Destellos", pos, 6);
            if (vis != null) UnityEngine.Object.Destroy(vis);
        }

        // Wings of Light: a fan of light feathers.
        static IEnumerator Feathers(Enemy caster, ArcanaDef def, Vector2 from, Vector2 dir, bool empowered)
        {
            int n = empowered ? 7 : 5;
            float spread = empowered ? 70f : 50f;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = ArcanaCaster.Rotate(dir, Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(n - 1)));
                caster.StartCoroutine(Guard(Lance(caster, def, from, d, false, "pluma_luz", 15f, 0.8f, 0.5f, 1)));
            }
            yield break;
        }

        // Flash: a mote of light gathers on the target spot, then bursts in a blinding flash.
        static IEnumerator Flash(Enemy caster, ArcanaDef def, Vector2 at, bool empowered)
        {
            float radius = empowered ? 3f : 2.4f;
            GameObject mote = Visual("mota_luz", at, Vector2.right, 0.6f, null);
            ArcanosBridge.Sound("MagicianSparkle", at);
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                if (mote != null) mote.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, radius * 0.9f, t / 0.7f);
                yield return null;
            }
            if (mote != null) UnityEngine.Object.Destroy(mote);
            if (!Alive(caster)) yield break;
            GameObject burst = Visual("fogonazo_luz", at, Vector2.right, radius * 0.8f, null);
            ArcanosBridge.Fx("HacesDeLuz", at, 6, 1f);
            ArcanosBridge.Sound("Shockwave", at);
            Hit(caster, def, at, radius, empowered, null, 2);
            for (float t = 0f; t < 0.3f && burst != null; t += Time.deltaTime)
            {
                SpriteRenderer sr = burst.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - t / 0.3f);
                yield return null;
            }
            if (burst != null) UnityEngine.Object.Destroy(burst);
        }

        // Holy Halo: a halo hangs over the caster and pulses holy light around it three times.
        static IEnumerator Halo(Enemy caster, ArcanaDef def, bool empowered)
        {
            float radius = empowered ? 3.2f : 2.6f;
            GameObject halo = Visual("halo_sagrado", caster.transform.position, Vector2.right, 1f, null);
            int pulses = empowered ? 4 : 3;
            for (int p = 0; p < pulses; p++)
            {
                for (float t = 0f; t < 0.75f; t += Time.deltaTime)
                {
                    if (!Alive(caster)) { if (halo != null) UnityEngine.Object.Destroy(halo); yield break; }
                    if (halo != null) halo.transform.position = (Vector2)caster.transform.position + new Vector2(0f, 2.2f);
                    yield return null;
                }
                Vector2 c = caster.transform.position;
                ArcanosBridge.Fx("Destellos", c, 10);
                ArcanosBridge.Sound("PortalShine", c);
                Hit(caster, def, c, radius, empowered, null, 1);
            }
            if (halo != null) UnityEngine.Object.Destroy(halo);
        }

        // Light Whip: a lash of light across the front.
        static IEnumerator Whip(Enemy caster, ArcanaDef def, Vector2 from, Vector2 dir, bool empowered)
        {
            float length = empowered ? 5.5f : 4.5f;
            GameObject vis = Visual("latigo_luz", from + dir * (length * 0.5f), dir, 1f, null);
            ArcanosBridge.Sound("WindSnap", from);
            bool hit = false;
            for (int i = 1; i <= 3 && !hit; i++)
            {
                Vector2 p = from + dir * (length * i / 3f);
                if (PlayerNear(p, 0.9f)) { Hit(caster, def, p, 0.9f, empowered, null, 1); hit = true; }
            }
            yield return new WaitForSeconds(0.25f);
            if (vis != null) UnityEngine.Object.Destroy(vis);
        }

        // Elemental Prism: five bursts around the target, each the next element of the cycle, with its own effect.
        static IEnumerator PrismBursts(Enemy caster, ArcanaDef def, Vector2 at, bool empowered)
        {
            int n = empowered ? 7 : 5;
            float start = UnityEngine.Random.value * 360f;
            for (int i = 0; i < n; i++)
            {
                if (!Alive(caster)) yield break;
                Vector2 p = i == 0 ? at : at + ArcanaCaster.Rotate(Vector2.right, start + i * 360f / (n - 1)) * 1.7f;
                PrismElement el = Prism.Next(caster);
                ElementFx(el, p);
                Hit(caster, def, p, 1.3f, empowered, el.Type, 0, el);
                yield return new WaitForSeconds(0.15f);
            }
        }

        // Triad: three prism arcs, each a different element.
        static IEnumerator Triad(Enemy caster, ArcanaDef def, Vector2 from, Vector2 dir, bool empowered)
        {
            int n = empowered ? 5 : 3;
            for (int i = 0; i < n; i++)
            {
                PrismElement el = Prism.Next(caster);
                Vector2 d = ArcanaCaster.Rotate(dir, n == 1 ? 0f : Mathf.Lerp(-25f, 25f, i / (float)(n - 1)));
                caster.StartCoroutine(Guard(PrismBolt(caster, def, from, d, el)));
            }
            yield break;
        }

        static IEnumerator PrismBolt(Enemy caster, ArcanaDef def, Vector2 from, Vector2 dir, PrismElement el)
        {
            GameObject vis = Visual("arco_prisma_" + el.Sprite, from, dir, 1f, null);
            float t = 0f;
            Vector2 pos = from;
            while (t < 0.75f)
            {
                Vector2 next = pos + dir * 17f * Time.deltaTime;
                if (Physics2D.Linecast(pos, next, LayerMask.GetMask("Wall"))) break;
                pos = next;
                if (vis != null) vis.transform.position = pos;
                if (PlayerNear(pos, 0.6f)) { Hit(caster, def, pos, 0.6f, false, el.Type, 0, el); break; }
                t += Time.deltaTime;
                yield return null;
            }
            ElementFx(el, pos);
            if (vis != null) UnityEngine.Object.Destroy(vis);
        }

        static void ElementFx(PrismElement el, Vector2 p)
        {
            switch (el.Sprite)
            {
                case "fuego": ArcanosBridge.Fx("EstallidoDeLlamas", p, false); break;
                case "agua": ArcanosBridge.Fx("Salpicon", p, 10); break;
                case "tierra": ArcanosBridge.Fx("Escombros", p, 8); break;
                case "aire": ArcanosBridge.Fx("Rafaga", p, 8); break;
                default: ArcanosBridge.Fx("ImpactoRayo", p, 6); break;
            }
        }

        // ---------- the hit ----------

        static bool PlayerNear(Vector2 p, float radius)
        {
            if (GameController.playerScripts == null) return false;
            foreach (Player pl in GameController.playerScripts)
            {
                if (pl == null || pl.IsDead) continue;
                Vector2 body = pl.hurtBoxTransform != null ? (Vector2)pl.hurtBoxTransform.position : (Vector2)pl.transform.position;
                if ((body - p).sqrMagnitude <= (radius + 0.4f) * (radius + 0.4f)) return true;
            }
            return false;
        }

        // One hit: the game's circle hitbox for one physics frame, with the arcana's stat damage owned by the enemy.
        // The element is always a base one (Arcanos' own element values break the game's damage handling on
        // enemies), so Radiant hits are neutral damage plus Flashes, Prism hits the cycle's element and status.
        static void Hit(Enemy owner, ArcanaDef def, Vector2 at, float radius, bool empowered, ElementType? element, int flashes, PrismElement prismEl = null)
        {
            GameObject prefab = Attack.CirclePrefab;
            if (prefab == null || owner == null) return;
            Attack atk = Globals.ChaosInst<Attack>(prefab, null, (Vector3)at, null);
            if (atk == null) return;
            atk.transform.localScale = Vector3.one;
            CircleCollider2D cc = atk.GetComponent<CircleCollider2D>();
            if (cc != null) { cc.isTrigger = true; cc.radius = radius; cc.offset = Vector2.zero; }
            EnemyArcana.MakeEnemyAttack(atk, owner, def, empowered);
            AttackInfo info = atk.atkInfo;
            if (info == null)
            {
                // No player stat data for this arcana: a plain enemy hit, like Arcanos' attacking dummy.
                info = atk.SetAttackInfo(Enemy.globalEnemySkillCategory, "NeutralKnightAttack", 1, false);
                EnemyArcana.ReOwn(info, owner);
            }
            if (info != null)
            {
                info.elementType = element ?? ElementType.Neutral;
                info.burnChance = info.slowChance = info.poisonChance = info.shockChance = info.freezeChance = info.chaosChance = 0f;
                if (prismEl != null) prismEl.ApplyStatus(info);
            }
            atk.EnableForOneFrame(true, false);
            UnityEngine.Object.Destroy(atk.gameObject, 0.5f);
            if (flashes > 0) RadiantFlash.CheckHit(at, radius, flashes);
        }

        // A sprite of Arcanos' own, pointed along dir.
        static GameObject Visual(string sprite, Vector2 at, Vector2 dir, float scale, Color? tint)
        {
            Sprite s = ArcanosBridge.Sprite(sprite);
            if (s == null) return null;
            GameObject go = new GameObject("HB_Arcanos_" + sprite);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            go.transform.localScale = Vector3.one * scale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingLayerName = "Effects";
            if (tint.HasValue) sr.color = tint.Value;
            UnityEngine.Object.Destroy(go, 4f); // never left behind
            return go;
        }
    }

    // Prism: each hit takes the next of the five base elements (Arcanos' Prismatico.Ciclo), with that element's
    // status and colour. Per caster, so every Prism enemy runs its own cycle.
    internal sealed class PrismElement
    {
        public ElementType Type; public string Prefix, Sprite;
        public PrismElement(ElementType t, string prefix, string sprite) { Type = t; Prefix = prefix; Sprite = sprite; }

        public Color Color { get { return ElementalGiants.ElementColor(Prefix); } }

        public void ApplyStatus(AttackInfo info)
        {
            const float chance = 0.25f;
            switch (Type)
            {
                case ElementType.Fire: info.burnChance = Mathf.Max(info.burnChance, chance); info.burnLevel = Mathf.Max(1, info.burnLevel); break;
                case ElementType.Water: info.freezeChance = Mathf.Max(info.freezeChance, chance * 0.6f); break;
                case ElementType.Earth: info.poisonChance = Mathf.Max(info.poisonChance, chance); info.poisonLevel = Mathf.Max(1, info.poisonLevel); break;
                case ElementType.Air: info.slowChance = Mathf.Max(info.slowChance, chance); info.slowLevel = Mathf.Max(1, info.slowLevel); break;
                case ElementType.Lightning: info.shockChance = Mathf.Max(info.shockChance, chance); info.shockLevel = Mathf.Max(1, info.shockLevel); break;
            }
        }
    }

    internal static class Prism
    {
        static readonly PrismElement[] Cycle =
        {
            new PrismElement(ElementType.Fire, "Flame", "fuego"),
            new PrismElement(ElementType.Water, "Frost", "agua"),
            new PrismElement(ElementType.Earth, "Earth", "tierra"),
            new PrismElement(ElementType.Air, "Wind", "aire"),
            new PrismElement(ElementType.Lightning, "Thunder", "rayo"),
        };
        static readonly Dictionary<int, int> next = new Dictionary<int, int>();

        internal static PrismElement Next(Component caster)
        {
            int id = caster != null ? caster.GetInstanceID() : 0;
            int i;
            next.TryGetValue(id, out i);
            next[id] = (i + 1) % Cycle.Length;
            if (next.Count > 512) next.Clear();
            return Cycle[i % Cycle.Length];
        }
    }

    // Radiant's ailment on the player, in a controlled form. Arcanos' Radiant hits stack Flashes on enemies; enough
    // blind them, more purify them. On the player: Radiant hits stack Flashes (forgotten after 6s); at 3 the player is
    // Dazzled for 2.5s: a bright glare washes over the screen and fades. Controls are never taken away.
    internal class RadiantFlash : MonoBehaviour
    {
        const int ToDazzle = 3;
        const float Forget = 6f, DazzleSeconds = 2.5f;
        static RadiantFlash instance;
        int stacks;
        float lastFlash = -99f, dazzledUntil = -1f;
        static Texture2D white;

        static RadiantFlash Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("HybridBosses_RadiantFlash");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<RadiantFlash>();
                }
                return instance;
            }
        }

        // After a Radiant hit at 'at': if a player there actually lost health a moment later, it counts.
        internal static void CheckHit(Vector2 at, float radius, int flashes)
        {
            Player target = null;
            float before = 0f;
            if (GameController.playerScripts != null)
                foreach (Player pl in GameController.playerScripts)
                {
                    if (pl == null || pl.IsDead || pl.health == null) continue;
                    Vector2 body = pl.hurtBoxTransform != null ? (Vector2)pl.hurtBoxTransform.position : (Vector2)pl.transform.position;
                    if ((body - at).sqrMagnitude <= (radius + 0.5f) * (radius + 0.5f)) { target = pl; before = pl.health.CurrentHealthValue; break; }
                }
            if (target != null) Instance.StartCoroutine(Instance.Confirm(target, before, flashes));
        }

        IEnumerator Confirm(Player p, float before, int flashes)
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            if (p == null || p.health == null || p.health.CurrentHealthValue >= before) yield break;
            if (Time.time - lastFlash > Forget) stacks = 0;
            lastFlash = Time.time;
            stacks += flashes;
            if (stacks >= ToDazzle)
            {
                stacks = 0;
                dazzledUntil = Time.time + DazzleSeconds;
                ArcanosBridge.Fx("HacesDeLuz", (Vector2)p.transform.position, 8, 1.2f);
                Plugin.Log.LogInfo("RADIANT player dazzled for " + DazzleSeconds + "s");
            }
        }

        void OnGUI()
        {
            if (Time.time >= dazzledUntil) return;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            float left = (dazzledUntil - Time.time) / DazzleSeconds; // 1 -> 0
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.97f, 0.85f, 0.75f * left * left);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = prev;
        }
    }
}
