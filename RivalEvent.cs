using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WolHybridBosses
{
    // The rival event, with its own portal (the misc room is left alone):
    //   a rival portal opens somewhere on the stage -> straight into the arena -> after the fight, a portal back to
    //   where you came in. v0.9.2: no selector room any more. What the portal holds is rolled when it opens: the Echo
    //   of your last run (config RivalWizards.EchoChance, once per run, pale portal) or a rival scaled to the run.
    // The arena is a real room of the stage's own tileset, loaded off-map with Room.Load and built with Room.Generate
    // (floor, walls, pits, decorations and checkpoints under the room's own transform), so it all happens in the same
    // scene: nothing reloads and the stage's progress is untouched. Checkpoints (safe floor spots) place the arrival
    // point and the rival.
    internal enum RivalMode { Duel, Tournament, Team } // v1.1.0

    internal class RivalEvent : MonoBehaviour
    {
        static readonly Vector2 ArenaOffset = new Vector2(0f, -780f);
        internal static bool Building; // true while one of our rooms runs Room.Generate (see RivalRoomMeshPatch)
        const float PortalSpacing = 4f; // minimum distance between portals and from where the player lands

        internal static RivalEvent Current;

        string prefix;
        Room arenaRoom;
        List<Vector2> arenaSpots = new List<Vector2>();
        Vector2 arenaArrival, entrancePos;
        internal Vector2 EntrancePos { get { return entrancePos; } }
        GameObject entrance;
        RivalTier tier;
        bool echo, inStartRoom;
        RivalMode mode;          // v1.1.0: a duel, a three-round tournament or a 2v2 team match
        GrudgeRecord grudge;     // v0.9.16: a returning rival
        internal bool Fled;      // (v0.9.16 flee; unused since v0.9.17 rematches)
        bool chosen;
        const float AnnounceSeconds = 7f; // v0.9.3: was 4s, too short to read what the portal holds
        Enemy rival;

        // ---------- setup ----------

        // LevelGenerator.levelGeneratedEventHandlers: the stage is fully built.
        internal static void OnLevelGenerated()
        {
            try
            {
                if (!Plugin.RivalEvents.Value || Level.isBossRushMode) return;
                // v0.9.17: a rival you beat that wants a rematch: 60% per later stage.
                if (Grudges.Pending != null && UnityEngine.Random.value < Grudges.OpenChance) { Open(Level.startPosition + new Vector2(4f, 0f), Grudges.Pending.Prefix, true, true, Grudges.Pending); return; }
                if (UnityEngine.Random.value >= Plugin.RivalEventChance.Value) return;
                // v0.9.3: in the start room, beside where the player arrives (placed on open floor once the
                // stage's colliders are registered, in Settle), so it never sits in another room's setup.
                Open(Level.startPosition + new Vector2(4f, 0f), Rivals.StagePrefixOrRandom(), true, true);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("RIVAL event setup failed: " + ex); }
        }

        // F11: open one beside the player right now (element given by the test key's cycle).
        internal static void OpenHere(string prefix)
        {
            Player p = GameController.playerScripts != null && GameController.playerScripts.Length > 0 ? GameController.playerScripts[0] : null;
            if (p == null) return;
            Open((Vector2)p.transform.position + new Vector2(2.5f, 0f), prefix ?? Rivals.StagePrefixOrRandom(), false, false);
        }

        // v1.0.0 test keys: Alt+F10 the Echo of your last run (recorded from your loadout now if there's none), Alt+F5
        // a rematch rival (a tier harder, some arcana infused).
        internal static void OpenEchoHere()
        {
            Player p = Elites.Player0;
            if (p == null) return;
            if (EchoRecorder.Last == null) EchoRecorder.RecordNow();
            EchoRecorder.FoughtThisRun = false;
            Open((Vector2)p.transform.position + new Vector2(2.5f, 0f), Rivals.StagePrefixOrRandom(), false, false, null, true);
        }

        internal static void OpenRematchHere()
        {
            Player p = Elites.Player0;
            if (p == null) return;
            GrudgeRecord g = new GrudgeRecord { Prefix = Rivals.StagePrefixOrRandom(), Tier = RivalTier.Hard };
            Grudges.Pending = g;
            Open((Vector2)p.transform.position + new Vector2(2.5f, 0f), g.Prefix, true, false, g);
        }

        // v1.1.0 test keys: Alt+F9 a tournament portal, Alt+F11 a team match portal, beside you.
        internal static void OpenModeHere(RivalMode m)
        {
            Player p = Elites.Player0;
            if (p == null) return;
            Open((Vector2)p.transform.position + new Vector2(2.5f, 0f), Rivals.StagePrefixOrRandom(), true, false, null, false, m);
        }

        static void Open(Vector2 entranceAt, string prefix, bool announce, bool inStartRoom, GrudgeRecord grudge = null, bool forceEcho = false, RivalMode? forceMode = null)
        {
            if (Current != null) Current.Close();
            GameObject go = new GameObject("HybridBosses_RivalEvent");
            RivalEvent ev = go.AddComponent<RivalEvent>();
            ev.prefix = prefix;
            ev.entrancePos = entranceAt;
            // What waits behind the portal, rolled now so its colour and the announcement can say so.
            ev.echo = EchoRecorder.Available && (forceEcho || UnityEngine.Random.value < Plugin.EchoChance.Value);
            ev.tier = ev.echo ? EchoTier : Plugin.RivalScaleWithProgress.Value ? RollTier(false) : (RivalTier)UnityEngine.Random.Range(0, 3);
            ev.inStartRoom = inStartRoom;
            if (grudge != null) { ev.grudge = grudge; ev.echo = false; ev.tier = grudge.Tier; }
            // v1.1.0: some portals hold a tournament or a team match instead of a duel.
            if (forceMode.HasValue) { ev.mode = forceMode.Value; if (ev.mode != RivalMode.Duel) ev.echo = false; }
            else if (!ev.echo && grudge == null)
            {
                float r = UnityEngine.Random.value;
                if (r < Plugin.TournamentChance.Value) ev.mode = RivalMode.Tournament;
                else if (r < Plugin.TournamentChance.Value + Plugin.TeamMatchChance.Value) ev.mode = RivalMode.Team;
            }
            if (!ev.Build()) { Destroy(go); return; }
            Current = ev;
            if (announce)
            {
                if (ev.mode == RivalMode.Tournament)
                    IntroUI.Show("A rival tournament has opened", "In the starting room  ·  three rounds, one prize", TournamentColor, null, AnnounceSeconds);
                else if (ev.mode == RivalMode.Team)
                    IntroUI.Show("A team match has opened", "In the starting room  ·  you and an ally against two rivals", TeamColor, null, AnnounceSeconds);
                else if (ev.grudge != null)
                    IntroUI.Show("A rival wants a rematch", "In the starting room  ·  " + ElementalGiants.ElementColorName(prefix) + " rival  ·  " + ev.tier + "  ·  some of its arcana infused", ElementalGiants.ElementColor(prefix), null, AnnounceSeconds);
                else if (ev.echo)
                    IntroUI.Show("The Echo of your last run has opened a portal", "In the starting room  ·  your own arcana, robe and relics, turned against you", EchoColor, null, AnnounceSeconds);
                else
                    IntroUI.Show("A rival's portal has opened", "In the starting room  ·  " + ElementalGiants.ElementColorName(prefix) + " rival  ·  " + ev.tier,
                        ElementalGiants.ElementColor(prefix), null, AnnounceSeconds);
            }
        }

        bool Build()
        {
            Vector2 basePos = Level.startPosition;
            arenaRoom = BuildRoom(basePos + ArenaOffset, true, arenaSpots);
            if (arenaRoom == null || arenaSpots.Count == 0)
            {
                Plugin.Log.LogWarning("RIVAL event: could not build the arena (" + (arenaRoom != null) + ", " + arenaSpots.Count + " spots)");
                if (arenaRoom != null) Destroy(arenaRoom.gameObject);
                return false;
            }
            arenaArrival = Central(arenaRoom, arenaSpots);
            entrance = MakePortal(entrancePos, arenaArrival, mode == RivalMode.Tournament ? TournamentColor : mode == RivalMode.Team ? TeamColor : echo ? EchoColor : ElementalGiants.ElementColor(prefix));
            if (entrance != null)
            {
                RivalPortal rp = entrance.AddComponent<RivalPortal>();
                rp.Event = this;
                rp.Tier = tier;
                rp.IsEcho = echo;
            }
            StartCoroutine(Settle());
            Plugin.Log.LogInfo("RIVAL event: portal at " + entrancePos + " -> arena " + arenaRoom.name + " (" + arenaSpots.Count + " spots), "
                + (echo ? "the Echo of your last run (" + tier + ")" : prefix + " " + tier));
            return true;
        }

        // A couple of physics steps after building, so the arena's wall/pit colliders are registered for the checks.
        IEnumerator Settle()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            // 0.5.11: land on the most open floor (not the checkpoint under a wall), and move the stage entrance off
            // walls / out of door nooks the same way.
            arenaArrival = Roomy(arenaArrival, Mean(arenaSpots), arenaRoom);
            if (entrance != null)
            {
                entrancePos = inStartRoom ? StartRoomSpot(Level.startPosition) : Roomy(entrancePos, entrancePos);
                entrance.transform.position = entrancePos;
                Portal ep = entrance.GetComponent<Portal>();
                if (ep != null) ep.targetLocation = arenaArrival;
            }
        }

        // A real room of this stage's tileset, built on its own far off-map. Arena: a room the game designs for fights
        // (Survival / Challenge: the closed rooms where waves and mini-bosses happen), else the largest Standard room.
        // Selector: a small Standard room. Never Start / Exit rooms: they bring the stage's start/exit setup with them
        // (a Start room copy pulled the player's spawn out of the real start room). Emptied of enemies, interactables
        // and portals.
        internal static Room BuildRoom(Vector2 at, bool arena, List<Vector2> spots)
        {
            Dictionary<string, Room> dict = Level.LoadedRoomDict;
            if (dict == null || dict.Count == 0) return null;
            string pick = null;
            int best = int.MinValue;
            foreach (KeyValuePair<string, Room> kv in dict)
            {
                Room t = kv.Value;
                if (t == null) continue;
                int size;
                try { size = t.Width * t.Height; } catch (Exception) { continue; }
                if (size < 120) continue; // too cramped for portals / a fight
                int score;
                if (arena)
                {
                    if (t.type == RoomType.Survival || t.type == RoomType.RandomSurvival) score = 2000000 + size;
                    else if (t.type == RoomType.Challenge) score = 1000000 + size;
                    else if (t.type == RoomType.Standard) score = size;
                    else continue;
                }
                else
                {
                    if (t.type != RoomType.Standard) continue;
                    score = -size; // smallest
                }
                if (score > best) { best = score; pick = kv.Key; }
            }
            if (pick == null) return null;
            // Room.Load(name, instantiate, parent, position): with false it hands back the PREFAB ASSET from the bundle
            // (ChaosBundle.GetWithComponent); only true makes a scene instance (ChaosInst + parent + position). 0.5.4-0.5.6
            // passed false, so they generated onto the prefab asset in memory: nothing rendered, positions didn't stick.
            Room r = Room.Load(pick, true, null, at);
            if (r == null) return null;
            if (!r.gameObject.scene.IsValid()) { Plugin.Log.LogWarning("RIVAL room " + pick + " is not a scene object; skipped"); return null; }
            r.gameObject.SetActive(true);
            r.name = "HybridBosses_" + (arena ? "Arena_" : "Selector_") + pick;
            r.randomEnemies = false;
            // 0.5.11: keep the room's props and let the game scatter its breakables (pots, crates, statues): clearing
            // them left black holes on the obstacle tiles they stand on. Chests / portals / NPCs are removed after.
            r.randomInteractables = true;
            r.randomPits = false;
            r.generateCheckpoints = true;
            if (r.enemies != null) r.enemies.Clear();
            if (r.enemyFactories != null) r.enemyFactories.Clear();
            ClearChildren(r.EnemiesTrans);
            HashSet<GameObject> rootsBefore = new HashSet<GameObject>(r.gameObject.scene.GetRootGameObjects());
            // 0.5.8: the game's generators make every part with new GameObject() at the WORLD origin and then
            // SetParent(room) keeping that world spot, and MeshGenerator static-batches the floor/wall meshes right
            // there. Generating at (268,-1050) put the art near (0,0) (over the real stage) while our room sat far
            // away. So: generate at the origin (parts line up with their room), without static batching
            // (RivalRoomMeshPatch), then move the whole room into place - unbatched meshes follow their root.
            r.transform.position = Vector3.zero;
            Building = true;
            try { r.Generate(); }
            finally { Building = false; }
            // Anything the decorators put at the scene root (instead of under the room) moves with the room too.
            int adopted = 0;
            foreach (GameObject g in r.gameObject.scene.GetRootGameObjects())
                if (g != null && g != r.gameObject && !rootsBefore.Contains(g) && !g.name.StartsWith("HybridBosses") && g.GetComponentInChildren<Player>() == null)
                { g.transform.SetParent(r.InteractablesTrans != null ? r.InteractablesTrans : r.transform, true); adopted++; }
            r.transform.position = at;
            Activate(r.transform, r.EnemiesTrans, r.InteractablesTrans);
            int removed = 0;
            foreach (Portal p in r.GetComponentsInChildren<Portal>(true)) { Destroy(p.gameObject); removed++; } // no stray start/exit portals
            foreach (TreasureChest c in r.GetComponentsInChildren<TreasureChest>(true)) { Destroy(c.gameObject); removed++; }
            foreach (Enemy e in r.GetComponentsInChildren<Enemy>(true)) { Destroy(e.gameObject); removed++; }
            foreach (Component c in r.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                string tn = c.GetType().Name;
                if (tn.Contains("Shop") || tn.Contains("Merchant") || tn.Contains("Npc") || tn.Contains("NPC") || tn.Contains("Pedestal") || tn.Contains("Relic"))
                { Destroy(c.gameObject); removed++; }
            }
            int breakables = r.GetComponentsInChildren<Destructible>(true).Length;
            Plugin.Log.LogInfo("RIVAL room " + r.name + ": " + breakables + " breakables, " + adopted + " root objects adopted, " + removed + " unwanted objects removed");
            foreach (Checkpoint cp in r.GetComponentsInChildren<Checkpoint>(true)) spots.Add(cp.transform.position);
            MatchStageSorting(r);
            Diagnose(r, spots);
            return r;
        }

        static void Activate(Transform t, Transform skipA, Transform skipB)
        {
            if (t == null || t == skipA || t == skipB) return;
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            foreach (MeshRenderer rd in t.GetComponents<MeshRenderer>()) rd.enabled = true; // floor/walls; sprites keep their own state
            foreach (Transform c in t) Activate(c, skipA, skipB);
        }

        // Fix attempt for floors that didn't show (the player saw the stage's sky background with walls they couldn't
        // see): give the room's meshes the same sorting layer/order as the stage's own floor meshes.
        static void MatchStageSorting(Room r)
        {
            MeshRenderer stage = null;
            foreach (MeshRenderer mr in FindObjectsOfType<MeshRenderer>())
            {
                if (mr == null || mr.transform.root.name.StartsWith("HybridBosses") || mr.GetComponentInParent<Room>() == r) continue;
                for (Transform t = mr.transform; t != null; t = t.parent) if (t.name == "Meshes") { stage = mr; break; }
                if (stage != null) break;
            }
            if (stage == null) return;
            foreach (MeshRenderer mr in r.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.sortingLayerID = stage.sortingLayerID;
                if (mr.sortingOrder == 0) mr.sortingOrder = stage.sortingOrder;
                mr.enabled = true;
                mr.gameObject.SetActive(true);
            }
        }

        static void Diagnose(Room r, List<Vector2> spots)
        {
            try
            {
                MeshRenderer[] meshes = r.GetComponentsInChildren<MeshRenderer>(true);
                Collider2D[] cols = r.GetComponentsInChildren<Collider2D>(true);
                List<string> m = new List<string>();
                foreach (MeshRenderer mr in meshes)
                {
                    if (m.Count >= 4) break;
                    MeshFilter mf = mr.GetComponent<MeshFilter>();
                    m.Add(mr.name + "{" + (mr.enabled && mr.gameObject.activeInHierarchy ? "on" : "OFF") + " bounds " + mr.bounds.center.ToString("0") + " size "
                        + mr.bounds.size.ToString("0") + " verts " + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : -1)
                        + " mat " + (mr.sharedMaterial != null ? mr.sharedMaterial.name : "none") + " layer " + mr.sortingLayerName + "/" + mr.sortingOrder + "}");
                }
                Vector2 c = Vector2.zero;
                foreach (Vector2 s in spots) c += s;
                if (spots.Count > 0) c /= spots.Count;
                int on = 0;
                foreach (MeshRenderer mr in meshes) if (mr.enabled && mr.gameObject.activeInHierarchy) on++;
                Plugin.Log.LogInfo("RIVAL room " + r.name + " (" + r.type + ", " + r.Width + "x" + r.Height + ") at " + r.transform.position.ToString("0")
                    + " (root active " + r.gameObject.activeInHierarchy + ", parent " + (r.transform.parent != null ? r.transform.parent.name : "none") + ", " + on + " meshes on)"
                    + ": " + meshes.Length + " meshes, " + cols.Length + " colliders, " + spots.Count + " checkpoints around " + c.ToString("0") + " | " + string.Join(" ", m.ToArray()));
            }
            catch (Exception ex) { Plugin.Log.LogWarning("RIVAL room diagnose failed: " + ex.Message); }
        }

        // An open floor spot near arrival + offset: no wall or pit collider of the room there, nothing solid in between,
        // and not on top of an earlier portal. Tries the offset, then rotated/shorter versions; falls back to arrival.
        internal static Vector2 OpenSpot(Room r, Vector2 arrival, Vector2 offset, List<Vector2> used)
        {
            float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
            for (int step = 0; step < 4; step++)
            {
                float scale = 1f - step * 0.2f;
                foreach (float ang in angles)
                {
                    Vector2 want = arrival + ArcanaCaster.Rotate(offset * scale, ang);
                    if (used.Exists(u => (u - want).sqrMagnitude < PortalSpacing * PortalSpacing)) continue;
                    if (Clearance(want) < 1f || !InsideFloor(r, want)) continue; // not hugging a wall, not out a doorway
                    if (Blocked(r, arrival, want)) continue;
                    return want;
                }
            }
            Plugin.Log.LogWarning("RIVAL no open spot near " + arrival.ToString("0.0") + " offset " + offset + " (last block: " + lastBlock + ")");
            return arrival;
        }

        // 0.5.9: only the game's wall layers (WallGenerator: "Wall" for walls/upper walls, "LowerWall" for the wall
        // faces) and pits count. 0.5.8 counted every solid collider of the room and something room-wide made every
        // spot "blocked", so all portals stacked on the arrival point and the rival fell back into a wall.
        // 0.5.10: "LowerWall" turned out to be walkable (the arrival checkpoint sits inside a LowerWalls collider), so
        // the mask is now built from what the player's own solid colliders actually collide with (the physics matrix).
        static int wallMask = -1;
        static int WallMask
        {
            get
            {
                if (wallMask != -1) return wallMask;
                int playerLayers = 0;
                Player pl = FindObjectOfType<Player>();
                if (pl != null)
                    foreach (Collider2D c in pl.GetComponentsInChildren<Collider2D>(true))
                        if (c != null && !c.isTrigger) playerLayers |= 1 << c.gameObject.layer;
                if (playerLayers == 0) return LayerMask.GetMask("Wall"); // try again next time
                int mask = 0;
                for (int pLayer = 0; pLayer < 32; pLayer++)
                {
                    if ((playerLayers & (1 << pLayer)) == 0) continue;
                    for (int l = 0; l < 32; l++)
                        if (!Physics2D.GetIgnoreLayerCollision(pLayer, l)) mask |= 1 << l;
                }
                // 0.5.11: the matrix lets through a lot of non-terrain layers (Default, Minimap, PlayerDetector, Attack
                // ...); keep only what is terrain.
                mask &= LayerMask.GetMask("Wall", "Pit", "Obstacle", "Destructible", "Environment");
                wallMask = mask;
                string names = "";
                for (int l = 0; l < 32; l++) if ((mask & (1 << l)) != 0) names += LayerMask.LayerToName(l) + "(" + l + ") ";
                Plugin.Log.LogInfo("RIVAL open-spot check blocks on layers: " + names);
                return wallMask;
            }
        }

        static string lastBlock;

        static bool Solid(Collider2D c)
        {
            return c != null && !c.isTrigger && ((1 << c.gameObject.layer) & WallMask) != 0 && c.GetComponentInParent<Player>() == null && c.GetComponentInParent<Enemy>() == null;
        }

        static bool Blocked(Room r, Vector2 from, Vector2 to)
        {
            foreach (Collider2D c in Physics2D.OverlapCircleAll(to, 0.8f))
            {
                if (c == null) continue;
                if (c.CompareTag("BottomlessPit")) { lastBlock = "pit " + c.name; return true; }
                if (Solid(c)) { lastBlock = "wall " + c.name + " [" + LayerMask.LayerToName(c.gameObject.layer) + "]"; return true; }
            }
            // 0.5.11: a real linecast (0.5.10's half-unit steps jumped over thin wall-base colliders, so a spot up on a
            // wall face looked reachable), ignoring only colliders the start point is already inside.
            foreach (RaycastHit2D h in Physics2D.LinecastAll(from, to))
            {
                Collider2D c = h.collider;
                if (!Solid(c) || c.OverlapPoint(from)) continue;
                lastBlock = "line " + c.name + " [" + LayerMask.LayerToName(c.gameObject.layer) + "]";
                return true;
            }
            return false;
        }

        // Inside the room's floor art, 2 units in from its edge (so nothing lands out through a doorway into the void).
        static readonly Dictionary<Room, Bounds> roomBounds = new Dictionary<Room, Bounds>();
        internal static bool InsideFloor(Room r, Vector2 p)
        {
            if (r == null) return true;
            Bounds b;
            if (!roomBounds.TryGetValue(r, out b))
            {
                bool any = false;
                b = new Bounds();
                foreach (MeshRenderer mr in r.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null || mr.bounds.size.sqrMagnitude < 0.01f) continue;
                    if (!any) { b = mr.bounds; any = true; } else b.Encapsulate(mr.bounds);
                }
                if (!any) return true;
                roomBounds[r] = b;
            }
            return p.x > b.min.x + 2f && p.x < b.max.x - 2f && p.y > b.min.y + 2f && p.y < b.max.y - 2f;
        }

        // How far from p the nearest wall / pit / obstacle is (capped at 5).
        internal static float Clearance(Vector2 p)
        {
            for (float rad = 1f; rad <= 5f; rad += 0.5f)
                foreach (Collider2D c in Physics2D.OverlapCircleAll(p, rad))
                    if (c != null && (c.CompareTag("BottomlessPit") || Solid(c))) return rad - 0.5f;
            return 5f;
        }

        // The most open floor reachable from 'from' within 6 units, leaning toward 'centre' (checkpoints often sit
        // right under a wall). Used for where the player lands in each room and for the stage entrance.
        internal static Vector2 Roomy(Vector2 from, Vector2 centre, Room inRoom = null)
        {
            Vector2 best = from;
            float bestScore = Clearance(from) * 10f - Vector2.Distance(from, centre) * 0.4f;
            for (int x = -6; x <= 6; x++)
                for (int y = -6; y <= 6; y++)
                {
                    Vector2 p = from + new Vector2(x, y);
                    if ((p - from).sqrMagnitude > 36f || !InsideFloor(inRoom, p) || Blocked(null, from, p)) continue;
                    float score = Clearance(p) * 10f - Vector2.Distance(p, centre) * 0.4f;
                    if (score > bestScore) { bestScore = score; best = p; }
                }
            return best;
        }

        // v0.9.3: the rival portal's spot in the start room: open floor 3.5-6 units from where the player arrives, in a
        // straight unblocked line from it (so it stays in the same room, not through a wall into the next one), the
        // most open such spot. Falls back to the arrival point's own most open floor.
        internal static Vector2 StartRoomSpot(Vector2 start, List<Vector2> avoid = null)
        {
            Vector2 best = start;
            float bestClear = -1f;
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.PI / 8f;
                for (float rad = 3.5f; rad <= 6f; rad += 1.25f)
                {
                    Vector2 p = start + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                    if (Blocked(null, start, p)) continue;
                    if (avoid != null && avoid.Exists(q => Vector2.Distance(q, p) < 4f)) continue; // v0.9.10: other portals
                    float clear = Clearance(p);
                    if (clear > bestClear) { bestClear = clear; best = p; }
                }
            }
            if (bestClear < 1f) best = Roomy(start + new Vector2(3.5f, 0f), start);
            Plugin.Log.LogInfo("RIVAL portal in the start room at " + best.ToString("0.0") + " (arrival " + start.ToString("0.0") + ", clearance " + bestClear.ToString("0.0") + ")");
            return best;
        }

        static void ClearChildren(Transform t)
        {
            if (t == null) return;
            foreach (Transform c in t) Destroy(c.gameObject);
        }

        // The checkpoint nearest the middle of the room's floor.
        internal static Vector2 Central(Room r, List<Vector2> spots)
        {
            return Snap(Mean(spots), spots);
        }

        internal static Vector2 Mean(List<Vector2> spots)
        {
            Vector2 c = Vector2.zero;
            if (spots.Count == 0) return c;
            foreach (Vector2 s in spots) c += s;
            return c / spots.Count;
        }

        static Vector2 Snap(Vector2 want, List<Vector2> spots)
        {
            Vector2 best = want;
            float d = float.MaxValue;
            foreach (Vector2 s in spots) { float e = (s - want).sqrMagnitude; if (e < d) { d = e; best = s; } }
            return best;
        }

        // A random checkpoint of the stage, not too close to the start and not in our rooms.
        internal static Vector2? PickEntrance(Vector2 start)
        {
            List<Vector2> far = new List<Vector2>(), any = new List<Vector2>();
            foreach (Checkpoint cp in FindObjectsOfType<Checkpoint>())
            {
                if (cp == null || cp.transform.root.name.StartsWith("HybridBosses")) continue;
                Vector2 p = cp.transform.position;
                float d = Vector2.Distance(p, start);
                if (d < 6f) continue;
                any.Add(p);
                if (d >= 15f && d <= 70f) far.Add(p);
            }
            List<Vector2> from = far.Count > 0 ? far : any;
            if (from.Count == 0) return null;
            return from[UnityEngine.Random.Range(0, from.Count)];
        }

        internal static GameObject MakePortal(Vector2 at, Vector2 target, Color color)
        {
            GameObject prefab = Portal.Prefab;
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, at, Quaternion.identity) as GameObject;
            Portal p = go != null ? go.GetComponent<Portal>() : null;
            if (p == null) return go;
            p.targetLocation = target;
            p.targetSpawnOffset = Vector3.zero;
            p.color = color;
            p.createReturnPortal = false;
            return go;
        }

        // ---------- the fight ----------

        // v0.9.3: the rival's difficulty is rolled, with Hard more likely the deeper the run (Easy / Medium / Hard %):
        //   first tier 60 / 30 / 10, second 30 / 45 / 25, third 15 / 40 / 45, any later loop 5 / 30 / 65.
        static readonly int[][] TierWeights = { new[] { 60, 30, 10 }, new[] { 30, 45, 25 }, new[] { 15, 40, 45 }, new[] { 5, 30, 65 } };

        internal static int[] CurrentWeights
        {
            get { return TierWeights[GameController.loopCount >= 1 ? 3 : Mathf.Clamp(GameController.tierCount, 0, 2)]; }
        }

        internal static RivalTier RollTier(bool atLeastMedium)
        {
            int[] w = CurrentWeights;
            int easy = atLeastMedium ? 0 : w[0];
            int hard = w[2] + League.HardWeightBonus; // v1.1.0: league rank makes Hard rivals likelier
            int roll = UnityEngine.Random.Range(0, easy + w[1] + hard);
            return roll < easy ? RivalTier.Easy : roll < easy + w[1] ? RivalTier.Medium : RivalTier.Hard;
        }

        // The Echo is at least Medium (it's your own build).
        static RivalTier EchoTier { get { return RollTier(true); } }

        static readonly Color EchoColor = new Color(0.72f, 0.66f, 0.85f);

        internal void Choose(RivalTier tier) { Choose(tier, false); }

        internal void Choose(RivalTier tier, bool echo)
        {
            if (chosen) return;
            chosen = true;
            Plugin.Log.LogInfo("RIVAL chosen: " + (echo ? "the Echo of your last run (" + tier + ")" : tier.ToString()));
            StartCoroutine(Run(tier, echo));
        }

        IEnumerator Run(RivalTier tier, bool echo = false)
        {
            yield return new WaitForSeconds(1.2f); // let the teleport finish
            // 0.5.10: a portal that appeared on top of the player registered the choice without teleporting. Make sure
            // every player is in the arena before the fight starts.
            foreach (Player pl in FindObjectsOfType<Player>())
            {
                if (pl == null || pl.IsDead) continue;
                if (Vector2.Distance(pl.transform.position, arenaArrival) > 40f)
                {
                    pl.transform.position = arenaArrival + new Vector2(0f, -1f);
                    Plugin.Log.LogInfo("RIVAL moved a player into the arena (the portal didn't teleport them)");
                }
            }
            if (entrance != null) Destroy(entrance); // one fight per portal
            if (mode == RivalMode.Tournament) { yield return StartCoroutine(RunTournament()); if (!PlayerDown()) OpenWayBack(); yield break; }
            if (mode == RivalMode.Team) { yield return StartCoroutine(RunTeam()); if (!PlayerDown()) OpenWayBack(); yield break; }

            // The Hard portal sometimes holds an Archon (a final champion, with its half-health phase) instead.
            bool archon = !echo && grudge == null && tier == RivalTier.Hard && UnityEngine.Random.value < Plugin.ArchonRivalChance.Value;
            Vector2 rivalAt = OpenSpot(arenaRoom, arenaArrival, new Vector2(0f, 6f), new List<Vector2> { arenaArrival });
            if ((rivalAt - arenaArrival).sqrMagnitude < 4f) rivalAt = FarthestWithin(arenaArrival, 9f);
            EchoRecord record = echo ? EchoRecorder.Last : null;
            string rivalPrefix = record != null ? EchoRival.Prefix(record) : archon ? Archons.MaybeVariant(prefix) : prefix; // sometimes a Radiant / Prism Archon (Arcanos)
            rival = Rivals.Spawn(rivalAt, rivalPrefix, archon ? RivalTier.Final : tier, archon);
            if (rival == null) { OpenWayBack(); yield break; }
            if (record != null)
            {
                // Set before its Start runs (the hostile setup reads it): the last run's loadout.
                RivalMarker mk = rival.GetComponent<RivalMarker>();
                if (mk != null) mk.Echo = record;
                EchoRecorder.FoughtThisRun = true;
            }
            // v1.1.0: the league's named rival for this element (not the Echo, not an Archon).
            LeagueRival league = (record == null && !archon) ? League.For(rivalPrefix) : null;
            if (league != null) { RivalMarker lm = rival.GetComponent<RivalMarker>(); if (lm != null) lm.League = league; }
            // v0.9.17: a rematch rival (harder, some arcana infused; may drop one of its arcana when beaten).
            if (grudge != null)
            {
                RivalMarker gm = rival.GetComponent<RivalMarker>();
                if (gm != null) gm.Grudge = grudge;
                rival.gameObject.AddComponent<GrudgeReturn>().Record = grudge;
            }
            yield return null; // Start (and our hostile postfix) runs
            yield return null;

            string title = league != null ? league.Name + ", " + ElementalGiants.ElementColorName(rivalPrefix) + " Rival" + (grudge != null ? ", rematch" : "")
                : grudge != null ? ElementalGiants.ElementColorName(prefix) + " Rival Wizard, rematch"
                : record != null ? "Echo of your last run"
                : archon ? (rivalPrefix == "Chaos" ? "Chaos Sovereign" : ElementalGiants.ElementColorName(rivalPrefix) + " Archon")
                : ElementalGiants.ElementColorName(prefix) + " Rival Wizard";
            if (archon) Plugin.Log.LogInfo("RIVAL Hard portal holds an Archon: " + title);
            string sub = grudge != null ? tier + "  ·  rematch  ·  some of its arcana infused" : record != null ? "You reached " + record.Reached + "  ·  " + tier : archon ? "Hard  ·  Archon" : tier.ToString();
            if (league != null) sub += "  ·  " + league.Record;
            yield return StartCoroutine(Rivals.Intro(rival, title, sub));
            // v0.9.20: voice lines (greeting now; half health and defeat from RivalBanter).
            RivalVoice voice = record != null ? RivalVoice.Echo : archon ? RivalVoice.Archon : grudge != null ? RivalVoice.Rematch : RivalVoice.Rival;
            Color voiceColour = record != null ? EchoColor : ElementalGiants.ElementColor(rivalPrefix);
            RivalSpeech.Say(title, league != null && grudge == null ? League.Greet(league) : RivalLines.Greet(voice, rivalPrefix), voiceColour);
            if (rival != null)
            {
                RivalBanter banter = rival.gameObject.AddComponent<RivalBanter>();
                banter.Voice = voice == RivalVoice.Rematch ? RivalVoice.Rival : voice;
                banter.Prefix = rivalPrefix;
                banter.Speaker = title;
                banter.Colour = voiceColour;
            }

            // After the fight (and its chests): the way back to where the player came in.
            bool lost = false;
            while (rival != null && rival.health != null && rival.health.CurrentHealthValue > 0)
            {
                if (!lost && PlayerDown()) { lost = true; League.RecordLoss(league); } // v1.1.0: it beat you
                yield return new WaitForSeconds(0.25f);
            }
            if (lost) yield break;
            if (league != null) League.RecordWin(league, archon ? RivalTier.Final : tier, rival != null ? (Vector2)rival.transform.position : arenaArrival);
            if (!Fled) Bounties.Report(BountyKind.Rival, null); // v0.9.16
            bool promise = grudge == null && Grudges.MaybeRecord(rivalPrefix, tier, echo, archon); // v0.9.17: maybe a rematch later this run
            yield return new WaitForSeconds(3f);
            if (promise) RivalSpeech.Say(title, RivalLines.Promise(), voiceColour, 3.5f); // v0.9.20: after its defeat line
            OpenWayBack();
        }

        // ---------- v1.1.0: tournaments and team matches ----------

        static readonly Color TournamentColor = new Color(1f, 0.82f, 0.3f), TeamColor = new Color(0.55f, 0.9f, 1f);

        static bool PlayerDown() { Player p = Elites.Player0; return p == null || p.IsDead; }
        static RivalTier Down(RivalTier t) { return t == RivalTier.Hard ? RivalTier.Medium : RivalTier.Easy; }
        static RivalTier Up(RivalTier t) { return t == RivalTier.Easy ? RivalTier.Medium : RivalTier.Hard; }

        Vector2 RivalSpot(Vector2 offset)
        {
            Vector2 at = OpenSpot(arenaRoom, arenaArrival, offset, new List<Vector2> { arenaArrival });
            if ((at - arenaArrival).sqrMagnitude < 4f) at = FarthestWithin(arenaArrival, 9f);
            return at;
        }

        // Other elements for extra rivals / the ally, shuffled (Chaos included).
        List<string> OtherPrefixes()
        {
            List<string> l = new List<string>(ElementalGiants.Prefixes) { "Chaos" };
            l.Remove(prefix);
            for (int i = l.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); string t = l[i]; l[i] = l[j]; l[j] = t; }
            return l;
        }

        static string NameOf(LeagueRival lg, string pfx) { return lg != null ? lg.Name : ElementalGiants.ElementColorName(pfx) + " Rival Wizard"; }

        static void AddBanter(Enemy r, string pfx, string speaker, Color c)
        {
            if (r == null) return;
            RivalBanter b = r.gameObject.AddComponent<RivalBanter>();
            b.Voice = RivalVoice.Rival;
            b.Prefix = pfx;
            b.Speaker = speaker;
            b.Colour = c;
        }

        // A rival for a round / a side: spawned, named, introduced, greeting said.
        IEnumerator Challenger(Enemy r, string pfx, RivalTier t, string subtitle, bool noChest)
        {
            RivalMarker mk = r.GetComponent<RivalMarker>();
            LeagueRival lg = League.For(pfx);
            if (mk != null) { mk.NoChest = noChest; mk.League = lg; }
            yield return null;
            yield return null;
            string title = NameOf(lg, pfx);
            yield return StartCoroutine(Rivals.Intro(r, title, subtitle + (lg != null ? "  ·  " + lg.Record : "")));
            Color c = ElementalGiants.ElementColor(pfx);
            RivalSpeech.Say(title, lg != null ? League.Greet(lg) : RivalLines.Greet(RivalVoice.Rival, pfx), c);
            AddBanter(r, pfx, title, c);
        }

        static bool Alive(Enemy e) { return e != null && e.health != null && e.health.CurrentHealthValue > 0; }

        // Three rounds against three rivals of different elements, one difficulty step apart; a short breather and a
        // little health between rounds; no chests until the end: then a gold chest, and half the time a pre-infused arcana.
        IEnumerator RunTournament()
        {
            IntroUI.Show("Rival Tournament", "Three rounds, one prize  ·  no chests until the end", TournamentColor, null, 3f);
            yield return new WaitForSeconds(2.5f);
            List<string> others = OtherPrefixes();
            string[] order = { prefix, others[0], others[1] };
            RivalTier[] tiers = { Down(tier), tier, Up(tier) };
            for (int i = 0; i < 3; i++)
            {
                Enemy r = Rivals.Spawn(RivalSpot(new Vector2(0f, 6f)), order[i], tiers[i]);
                if (r == null) continue;
                rival = r;
                yield return StartCoroutine(Challenger(r, order[i], tiers[i], "Round " + (i + 1) + " of 3  ·  " + tiers[i], true));
                LeagueRival lg = League.For(order[i]);
                bool lost = false;
                while (Alive(r))
                {
                    if (!lost && PlayerDown()) { lost = true; League.RecordLoss(lg); }
                    yield return new WaitForSeconds(0.25f);
                }
                if (lost || PlayerDown()) yield break;
                League.RecordWin(lg, tiers[i], r != null ? (Vector2)r.transform.position : arenaArrival, true);
                Bounties.Report(BountyKind.Rival, null);
                if (i < 2)
                {
                    Player p = Elites.Player0;
                    if (p != null && p.health != null) try { p.health.RestoreHealth(Mathf.Max(1, Mathf.RoundToInt(p.health.healthStat.CurrentValue * 0.2f)), false, false, false, false); } catch (Exception) { }
                    IntroUI.Show("Round " + (i + 1) + " won", "A breather and a little health  ·  the next challenger enters in 5s", TournamentColor, null, 3f);
                    yield return new WaitForSeconds(5f);
                }
            }
            Vector2 prize = arenaArrival + new Vector2(0f, 2f);
            RivalRewards.SpawnChest(prize, true, StageVariant.None);
            string arcana = UnityEngine.Random.value < 0.5f ? PrizeArcana(prize + new Vector2(2f, 0f)) : null;
            IntroUI.Show("Tournament champion!", "A gold chest" + (arcana != null ? "  ·  and " + arcana : ""), TournamentColor, null, 4f);
            Plugin.Log.LogInfo("RIVAL tournament won" + (arcana != null ? " (prize " + arcana + ")" : ""));
        }

        // A pre-infused arcana you've unlocked and don't hold.
        static string PrizeArcana(Vector2 at)
        {
            HashSet<string> held = TomiInfusion.HeldIds();
            List<string> ids = new List<string>();
            foreach (ArcanaDef d in ArcanaCatalog.Pool("Chaos"))
                if (d.Port == ArcanosPort.None && !d.Basic && !held.Contains(d.SkillID) && ArcanosBridge.Unlocked(d.SkillID) && !ids.Contains(d.SkillID)) ids.Add(d.SkillID);
            if (ids.Count == 0) return null;
            string id = ids[UnityEngine.Random.Range(0, ids.Count)];
            ElementType el = Infusion.RandomOther(id);
            Infusion.SetPending(id, el);
            try { LootManager.DropSkill(at, 1, id, 0f, 0f, null, false, false); } catch (Exception) { return null; }
            return "a " + Infusion.Name(el) + "-infused " + IntroUI.SkillName(id);
        }

        static readonly string[] AllyHello = { "Stay close, I'll cover you!", "Two on two? I like those odds.", "Let's show them how it's done!" };
        static readonly string[] AllyThanks = { "Good teamwork! Call me anytime.", "We make a scary pair, you know.", "That was fun. Same time next run?" };

        // You and an allied wizard (a league rival of another element, on your side) against two rivals, each a step
        // below the portal's difficulty. Their chests drop as usual.
        IEnumerator RunTeam()
        {
            List<string> others = OtherPrefixes();
            string allyPrefix = others[0], second = others[1];
            LeagueRival allyLg = League.For(allyPrefix);
            string allyName = allyLg != null ? allyLg.Name : ElementalGiants.ElementColorName(allyPrefix) + " wizard";
            Enemy ally = SpawnAlly(RivalSpot(new Vector2(-2.5f, -1.5f)), allyPrefix);
            IntroUI.Show("Team match", "You and " + allyName + " against two rivals", TeamColor, null, 3f);
            if (ally != null) RivalSpeech.Say(allyName + " (ally)", AllyHello[UnityEngine.Random.Range(0, AllyHello.Length)], ElementalGiants.ElementColor(allyPrefix), 3f);
            yield return new WaitForSeconds(2.5f);
            RivalTier t = Down(tier);
            Enemy a = Rivals.Spawn(RivalSpot(new Vector2(-3f, 6f)), prefix, t);
            if (a != null) { rival = a; yield return StartCoroutine(Challenger(a, prefix, t, "Team match  ·  " + t, false)); }
            Enemy b = Rivals.Spawn(RivalSpot(new Vector2(3f, 6f)), second, t);
            if (b != null) yield return StartCoroutine(Challenger(b, second, t, "Team match  ·  " + t, false));
            LeagueRival la = League.For(prefix), lb = League.For(second);
            bool doneA = !Alive(a), doneB = !Alive(b), lost = false;
            while (!doneA || !doneB)
            {
                if (!lost && PlayerDown()) { lost = true; if (Alive(a)) League.RecordLoss(la); if (Alive(b)) League.RecordLoss(lb); }
                if (lost) yield break;
                if (!doneA && !Alive(a)) { doneA = true; League.RecordWin(la, t, a != null ? (Vector2)a.transform.position : arenaArrival); Bounties.Report(BountyKind.Rival, null); }
                if (!doneB && !Alive(b)) { doneB = true; League.RecordWin(lb, t, b != null ? (Vector2)b.transform.position : arenaArrival); Bounties.Report(BountyKind.Rival, null); }
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(1.5f);
            if (Alive(ally))
            {
                RivalSpeech.Say(allyName + " (ally)", AllyThanks[UnityEngine.Random.Range(0, AllyThanks.Length)], ElementalGiants.ElementColor(allyPrefix), 3f);
                yield return new WaitForSeconds(2f);
            }
            if (ally != null)
            {
                ArcanosBridge.Fx("Teletransporte", ally.transform.position);
                try { GameController.allies.Remove(ally.gameObject); } catch (Exception) { }
                Destroy(ally.gameObject);
            }
            Plugin.Log.LogInfo("RIVAL team match won");
        }

        // The game's own wizard AI on your side: a Contestant left as it spawns (friendly), in an element's colours.
        Enemy SpawnAlly(Vector2 at, string pfx)
        {
            Enemy a = null;
            try { a = Enemy.Spawn("Contestant", at, null, false, true, false, false, false); } catch (Exception) { a = null; }
            if (a == null) { Plugin.Log.LogWarning("RIVAL team match: no ally"); return null; }
            ElementType el;
            if (ElementalGiants.TryGetElement(a, pfx, out el)) a.element = el;
            a.gameObject.AddComponent<BossEventEnemy>(); // never an elite, never re-elemented
            StartCoroutine(AllyHealth(a));
            Plugin.Log.LogInfo("RIVAL team match ally: " + pfx);
            return a;
        }

        static IEnumerator AllyHealth(Enemy a)
        {
            yield return null;
            yield return null;
            if (a == null || a.health == null) yield break;
            float hp = Rivals.TierHP(RivalTier.Hard);
            try { a.health.healthStat.AddMod(new NumVarStatMod("HB_AllyHP", hp, 10, VarStatModType.Override, true)); a.health.CurrentHealthValue = Mathf.RoundToInt(hp); } catch (Exception) { }
        }

        Vector2 FarthestWithin(Vector2 from, float max)        {
            Vector2 best = from;
            float bd = -1f;
            foreach (Vector2 s in arenaSpots)
            {
                float d = Vector2.Distance(s, from);
                if (d <= max && d > bd && !Blocked(arenaRoom, s, s)) { bd = d; best = s; }
            }
            return best;
        }

        void OpenWayBack()
        {
            MakePortal(arenaArrival, entrancePos + new Vector2(0f, -1.5f), new Color(0.85f, 0.85f, 0.85f));
            IntroUI.Show("The way back is open", "Return to the floor", new Color(0.9f, 0.9f, 0.9f), null, 3f);
            Plugin.Log.LogInfo("RIVAL event: way back opened");
        }

        void Close()
        {
            if (entrance != null) Destroy(entrance);
            if (arenaRoom != null) Destroy(arenaRoom.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy() { if (Current == this) Current = null; }
    }

    // MeshGenerator.GenerateMeshes ends with StaticBatchingUtility.Combine, which bakes the meshes where they are, so
    // a room generated at the origin and moved afterwards would leave its floor behind. For our rooms only, run the
    // same steps without the batching (two rooms' worth of extra draw calls).
    [HarmonyPatch(typeof(MeshGenerator), "GenerateMeshes")]
    internal static class RivalRoomMeshPatch
    {
        static System.Reflection.MethodInfo makeObj, makeMesh;

        static bool Prefix(TileGrid __0, Transform __1, string __2, bool __3)
        {
            if (!RivalEvent.Building) return true;
            try
            {
                if (makeObj == null) makeObj = AccessTools.Method(typeof(MeshGenerator), "CreateLayerMeshObj");
                if (makeMesh == null) makeMesh = AccessTools.Method(typeof(MeshGenerator), "CreateLayerMesh");
                Transform parent = __1;
                if (__3) parent = Globals.FindOrCreateGameObject("Meshes", __1).transform;
                foreach (string layer in Tile.tileLayerNames)
                {
                    object pair = makeObj.Invoke(null, new object[] { layer, parent });
                    object mesh = makeMesh.Invoke(null, new object[] { __0, layer, __2 });
                    Traverse p = Traverse.Create(pair), m = Traverse.Create(mesh);
                    p.Property("First").GetValue<MeshFilter>().sharedMesh = m.Property("First").GetValue<Mesh>();
                    p.Property("Second").GetValue<MeshRenderer>().materials = m.Property("Second").GetValue<Material[]>();
                }
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("RIVAL unbatched mesh build failed, using the game's: " + ex.Message);
                return true;
            }
        }
    }
}
