using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    public enum LoopState { Base, Prepare, World, Summary }

    // The Phase 1 loop for one player: base -> PREPARE -> world -> (Minotaur dies -> portal) -> summary -> base.
    // Game owns one of these only for "Single Player" from the menu (Game.Loop); everything else is the v1 arena.
    // This class decides what happens; LoopUi draws it and Loadout / LoopSave hold and store the data.
    public class RunLoop
    {
        readonly Game g;
        public Loadout data;
        public LoopUi ui;
        public TeamUi teamUi;
        public TeamRun team;                                        // online (Game.Coop): the room and the run of the team; null in single player
        public LoopState State { get; private set; }
        bool dirty;                                                 // PREPARE changed the stash / carry since the last save
        bool askedNoFood;                                           // "Enter without food?" is on screen
        bool askedForce;                                            // online: "<name> is not ready. Start without waiting?" is on screen
        int askedSeed;                                              // ... for this world (0 = a new world)
        bool worldsOpen;                                            // the OLD WORLDS list is open on top of PREPARE
        bool entering;                                              // ENTER WORLD pressed: the screen is fading to black
        float enterAt;

        public int EnterRequests;                                   // times ENTER WORLD went through (test read-out)

        public bool InWorld { get { return State == LoopState.World; } }
        public bool SummaryOpen { get { return State == LoopState.Summary; } }
        public bool PrepareOpen { get { return State == LoopState.Prepare && g.InventoryOpen; } }
        public bool CarryOpen { get { return State == LoopState.World && g.InventoryOpen; } }
        public bool Busy { get { return entering; } }               // fading into the world
        public bool WorldsOpen { get { return worldsOpen && PrepareOpen; } }
        public bool LootOpen { get { return lootMode && State == LoopState.World && g.InventoryOpen; } }
        // The piles of the old world being played, one view each: the data, the object in the world, the spot it stands at
        // (PilePoint of the saved spot) and whether walking into it opens the window (armed: not from closing the window until
        // the knight has stepped 4 m away). Piles never move; one that runs empty vanishes.
        class PileView { public Pile data; public LootPile obj; public Vector3 pos; public bool armed = true; }
        readonly List<PileView> views = new List<PileView>();
        PileView curView;                                           // the pile whose window is open
        public LootPile pile { get { return views.Count > 0 ? views[0].obj : null; } }      // the first pile (null: none / all taken)
        public Vector3 pilePos { get { return views.Count > 0 ? views[0].pos : Vector3.zero; } }
        public int PileObjects { get { return views.Count; } }
        public Vector3 PilePosAt(int i) { return views[i].pos; }                       // test read-outs
        public int PileItemsAt(int i) { return views[i].data.items.Count; }
        public OldWorld PileWorldInPlay { get { return Net.IsClient && team != null ? team.view : data.Find(data.runWorld); } }   // the world whose piles are in play
        OldWorld pileWorld;                                         // the world they belong to
        Pile curPile;                                               // the pile the loot window shows
        bool lootMode;                                              // the inventory window is the loot window, not the Carry window
        public bool Dying { get { return dying; } }                 // fallen, the screen is dimming
        public RunResult result;                                    // the last finished run (summary screen, tests)
        bool dying;
        float dieStart;
        public bool BossDown { get; private set; }                  // the world boss has fallen: the portal is open
        public Portal portal;
        public float portalHold;                                    // seconds the knight has stood in the portal
        float bannerAt;
        bool bannerPending;
        const float DimSeconds = 1.5f;

        public RunLoop(Game game) { g = game; }

        void Save()
        {
            if (!LoopSave.Save(data)) g.hud.Notice("Could not save  (" + LoopSave.LastError + ")", LoopUi.Red);
            dirty = false;
        }

        // The session just started: load the save, stand at the base and open PREPARE.
        public void Begin()
        {
            data = LoopSave.Load();
            ui = LoopUi.Create(g.transform, this);
            if (Net.Multiplayer)
            {
                team = new TeamRun(this, g);                        // online: the room (host) or the host's room (client)
                team.AddSelf(Net.LocalPid, Net.LocalName);
                ui.SetCoop(true, Net.IsHost);
                teamUi = TeamUi.Create(g.transform, this);
            }
            if (LoopSave.LastLoadNote.Length > 0) g.hud.Notice(LoopSave.LastLoadNote, LoopUi.Yellow);
            else if (data.trimNote.Length > 0) g.hud.Notice(data.trimNote, LoopUi.Yellow);
            g.SetBiome(Biome.Meadow); g.knight.ResetForRun(MapLayout.BasePoint);
            ResetWorldMultipliers();
            if (data.inRun)
            {
                // the game was closed in the middle of a run: that counts as a death (no save-scumming). The loot lies
                // where the knight last was (autosaved while he walked); no spot saved = at the town gate.
                // An online run of a client has no world of his own: what he carried is simply gone.
                Vector3 home = MapLayout.TownSpawnOf(data.runBiome);
                float ax = data.runPosValid ? data.runX : home.x, az = data.runPosValid ? data.runZ : home.z;
                bool online = data.runCoop;
                result = online ? data.LoseCarry(true) : data.Die(true, ax, az, Loadout.NowUnix());
                if (online) g.hud.Notice("An online run was cut short: what you carried is gone", LoopUi.Yellow);
                Save();
                State = LoopState.Summary;
                ui.ShowSummary(result);
                Debug.Log("LOOP abandoned run: " + result.lost.Count + " items lost");
                return;
            }
            data.AutoPack();
            State = LoopState.Base;
            ui.SetObjective("Press Tab to prepare, then enter the world"); ui.SetObjective2("");
            g.knight.SetWeapon(data.Held.id, data.Held.plus);
            g.SetInventory(true);
        }

        public void Shutdown()
        {
            if (team != null && data != null && data.inRun)
            {
                // closing the game (or losing the connection) in the middle of an online run
                if (data.runCoop)
                {
                    data.LoseCarry(true);                           // a client: what he carried is gone from him (it lies in the host's world if the team falls)
                    Save();
                }
                else if (team.runLive) HostAbandon();               // the host: everybody's things lie where they were
                else SavePosition(true);
            }
            else SavePosition(true);                                // closing the game mid-run: ABANDONED knows where the loot lies
            ResetWorldMultipliers();
            DropPortal();
            if (ui != null) Object.Destroy(ui.gameObject);
            ui = null;
            if (teamUi != null) Object.Destroy(teamUi.gameObject);
            teamUi = null;
        }

        // every frame, before the rest of Game.Update
        public void Tick()
        {
            if (entering && Time.unscaledTime >= enterAt) StartWorld();
            if (team != null) { team.Tick(); ui.SetRosterLine(TeamUi.RosterLine(g, team)); }
            if (team != null && State == LoopState.World) team.TickLocal();
            if (State == LoopState.World) TickPortal();
            if (State == LoopState.World) TickPosition();
            if (State == LoopState.World) TickPile();
            if (State == LoopState.World && team != null && Net.IsClient && !dying) TickWorldFullHint();
            if (dying)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - dieStart) / DimSeconds);
                ui.SetDeathDim(t * 0.6f);
                if (t >= 1f) { dying = false; State = LoopState.Summary; ui.ShowSummary(result); }
            }
            ui.SetStrip(data, State == LoopState.World);
            int hereSeed = 0, hereLeft = 0;                          // the badge of an old world
            if (State == LoopState.World && !dying) WorldInPlay(out hereSeed, out hereLeft);
            if (hereSeed == 0) ui.SetOldBadge(null, false);
            else ui.SetOldBadge("OLD WORLD " + OldWorld.CodeOf(hereSeed) + "\nHP x" + OldWorld.HpMul.ToString("0.0") + "  DMG x" + OldWorld.DmgMul.ToString("0.00")
                + "\nCrumbles in " + hereLeft + (hereLeft == 1 ? " return" : " returns"), hereLeft <= 1);
        }

        // The old world this run is played in: its seed and returns left (0 = a new world). Online it is the host's world.
        void WorldInPlay(out int seed, out int left)
        {
            seed = 0; left = 0;
            if (team != null && Net.IsClient)
            {
                if (team.runLive && team.worldSeed != 0) { seed = team.worldSeed; left = team.worldLeft; }
                return;
            }
            OldWorld w = data.Find(data.runWorld);
            if (w != null) { seed = w.seed; left = w.left; }
        }

        // Esc: true when it only closed a question of the loop
        public bool OnEscape()
        {
            if (askedNoFood || askedForce) { SetAsk(false); return true; }
            if (worldsOpen && State == LoopState.Prepare) { UiCloseWorlds(); return true; }
            return false;
        }

        void SetAsk(bool on)
        {
            askedNoFood = on;
            askedForce = false;
            ui.SetPrompt(on ? "Enter without food?  [Enter] Go   [Esc] Back" : "");
        }

        // ---------------------------------------------------------------- online: READY and the old worlds of the room

        // The READY button (a client): needs a Weapon, locks the carry, tells the host. Pressed again it lets go.
        public void UiReady()
        {
            if (team == null || State != LoopState.Prepare || entering) return;
            if (team.LocalReady) { team.SetLocalReady(false); ui.SetWarn("", LoopUi.Gray, 0f); dirty = true; return; }
            if (!data.CanEnter) { ui.SetWarn("Pack a Weapon first", LoopUi.Red, 1.5f); return; }
            data.EnsureHeld();
            team.SetLocalReady(true);
            if (dirty) Save();
        }

        // A packed carry is locked while READY (the host has a copy of it).
        bool PackLocked
        {
            get
            {
                if (team == null || !team.LocalReady) return false;
                ui.SetWarn("You are READY: un-ready to change your pack", LoopUi.Yellow, 1.6f);
                return true;
            }
        }

        // The old worlds PREPARE lists: the save's own (single player, host) or the host's (a client, read only).
        public List<OldWorld> WorldList()
        {
            if (team != null && Net.IsClient) return team.HostWorlds();
            return data.Sorted();
        }

        public int WorldCount { get { return team != null && Net.IsClient ? team.HostWorlds().Count : data.worlds.Count; } }

        // ---------------------------------------------------------------- PREPARE

        public void OnInventoryOpened()
        {
            askedNoFood = false; askedForce = false;
            if (State == LoopState.World)
            {
                if (lootMode && pileWorld != null && curPile != null) ui.ShowLoot(data, pileWorld.Code, curPile);
                else { lootMode = false; ui.ShowCarry(data, g.knight); }
                return;
            }
            if (State == LoopState.Base) State = LoopState.Prepare;
            ui.ShowPrepare(data);
        }

        public void OnInventoryClosed()
        {
            ui.HideAll();
            askedNoFood = false; askedForce = false;
            worldsOpen = false;
            if (lootMode)
            {
                lootMode = false;
                if (curView != null) curView.armed = false;         // not again until he has stepped away from that pile
                curView = null;
                if (Net.Multiplayer && g.knight != null) g.knight.Protect(0.6f);
                if (State == LoopState.World && data.inRun) Save();
            }
            if (State == LoopState.Prepare)
            {
                State = LoopState.Base;
                if (!data.introShown) { data.introShown = true; dirty = true; }
                if (data.unseen.Count > 0) { data.unseen.Clear(); dirty = true; }   // the red "crumbled" bar was seen
                if (dirty) Save();
            }
        }

        public void InventoryInput()
        {
            if (State == LoopState.Prepare) { if (worldsOpen) ui.WorldsInput(data); else ui.PrepareInput(data); }
            else if (State == LoopState.World) { if (lootMode) { if (curPile != null) ui.LootInput(data, pileWorld.Code, curPile); } else ui.CarryInput(data, g.knight); }
        }

        // A cell was clicked (or Enter on it): the item goes to the other side.
        public void UiMove(int side, Section sec, int index)
        {
            if (State != LoopState.Prepare) return;
            if (PackLocked) return;
            ItemBag from = side == 0 ? data.stash : data.carry;
            if (from.Slots(sec)[index].IsNone) return;
            bool ok = side == 0 ? data.MoveToCarry(sec, index) : data.MoveToStash(sec, index);
            if (!ok)
            {
                ui.Shake(side == 0 ? 1 : 0, sec);
                ui.SetWarn(side == 0 ? "Carry is full" : "Stash is full", LoopUi.Red, 1.5f);
                return;
            }
            data.EnsureHeld();
            dirty = true;
            if (askedNoFood || askedForce) SetAsk(false);
        }

        // Right click on a carried Weapon: that one is held in the world.
        public void UiHold(int slot)
        {
            if (State != LoopState.Prepare) return;
            if (PackLocked) return;
            if (data.SetHeld(slot)) dirty = true;
        }

        public void UiBack()
        {
            if (State == LoopState.Prepare) g.SetInventory(false);
        }

        // OLD WORLDS button: the list of old worlds opens over PREPARE (nothing to open when there are none).
        public void UiOpenWorlds()
        {
            if (State != LoopState.Prepare || WorldCount == 0) return;
            if (askedNoFood || askedForce) SetAsk(false);
            worldsOpen = true;
            ui.ShowWorlds(data);
        }

        public void UiCloseWorlds()
        {
            if (!worldsOpen) return;
            worldsOpen = false;
            if (askedNoFood || askedForce) SetAsk(false);
            ui.HideWorlds();
            ui.ShowPrepare(data);
        }

        // NEW WORLD (seed 0) / ENTER OLD WORLD (the old world's seed). Same rules for both: a Weapon is needed, no food asks
        // first. Online only the host enters; somebody who is not ready is asked about last ("Start without waiting?").
        public void UiEnter(int seed = 0)
        {
            if (State != LoopState.Prepare) return;
            if (team != null && Net.IsClient) return;               // the host chooses; a client presses READY
            if (seed != 0 && data.Find(seed) == null) return;
            if (!data.CanEnter)
            {
                ui.SetWarn("Pack a Weapon first", LoopUi.Red, 1.5f);
                return;
            }
            bool food = data.carry.Count(Section.Foods) > 0;
            foreach (Item it in data.carry.supplies) if (it.kind == ItemKind.Food) food = true;
            if (!food && !(askedNoFood && askedSeed == seed)) { askedSeed = seed; SetAsk(true); return; }
            if (team != null)
            {
                Member w = team.FirstWaiting();
                if (w != null && !(askedForce && askedSeed == seed))
                {
                    askedSeed = seed;
                    askedNoFood = false;
                    askedForce = true;
                    ui.SetPrompt(w.name + " is not ready. Start without waiting?  [Enter] START   [Esc] CANCEL");
                    return;
                }
            }
            SetAsk(false);
            EnterWorld(seed);
        }

        // Enter on the question that is showing: the world that was asked about
        public void UiConfirmEnter() { UiEnter(askedSeed); }

        // The map of a NEW world (single player / host only; an old world keeps its own): -biome forces one; scripted runs
        // (selftest / mptest) are always the meadow; otherwise a 50 : 50 roll once RandomBiomes is on (snow-map-models step 8).
        public static bool RandomBiomes = false;
        static int rollCounter;
        Biome PickBiome()
        {
            if (Args.biome >= 0) return Biomes.Valid(Args.biome) ? (Biome)Args.biome : Biome.Meadow;
            if (Args.Scripted || !RandomBiomes) return Biome.Meadow;
            return Biomes.Roll(new System.Random(unchecked((int)(Loadout.NowUnix() * 31 + data.worlds.Count * 7919 + (rollCounter++) * 104729 + 5))));
        }

        // The run is opened and saved first (closing the game from here on counts as a death), then the screen fades
        // and the world is built.
        void EnterWorld(int seed)
        {
            EnterRequests++;
            worldsOpen = false;
            g.SetInventory(false);                                  // closes PREPARE (saves a changed pack)
            OldWorld ow = data.Find(seed);
            data.BeginRun(seed, false, ow != null ? ow.biome : PickBiome());
            Save();
            if (team != null) team.StartRunHost(data.runWorld, ow != null ? ow.left : 0, data.runBiome);       // tells the others (Enter)
            entering = true;
            enterAt = Time.unscaledTime + 0.5f;
            ui.FadeTo(1f, 0.5f);
            Debug.Log("LOOP enter world" + (data.runWorld != 0 ? " OLD " + OldWorld.CodeOf(data.runWorld) : "") + " (held " + data.Held.Name + ", carry " + data.carry.Total + " items)"
                + (team != null ? ", team of " + team.roster.FindAll(m => m.inRun).Count : ""));
        }

        // client: the host started a run. Playing: the screen fades and the world is built like the host's (StartWorld);
        // not in the list (not ready, or came in late): stays at the base and waits for the next one.
        public void OnTeamEnter(bool iPlay, List<int> pids, Biome biome)
        {
            Debug.Log("LOOP team enter: " + (iPlay ? "playing" : "waiting") + ", " + pids.Count + " player(s)");
            if (!iPlay || entering || State == LoopState.World || State == LoopState.Summary) return;
            worldsOpen = false;
            g.SetInventory(false);
            data.BeginRun(0, true, biome);                           // the host's world: none of mine, but the run is open (rc); its map is the host's
            Save();
            entering = true;
            enterAt = Time.unscaledTime + 0.5f;
            ui.FadeTo(1f, 0.5f);
        }

        // Fully dark: build the world, level 1 with the held weapon, at the spawn point of this player.
        void StartWorld()
        {
            entering = false;
            DropPortal();
            Knight k = g.knight;
            g.ClearWorld();
            g.SetBiome(data.runBiome);                               // the map of this world (the meadow scene stays for the base)
            k.ResetForRun(Game.SpawnPointFor(Net.LocalPid));         // (pid 0 = the town gate, as always)
            Item held = data.Held;
            k.SetWeapon(held.id, held.plus);
            if (team != null) team.SpawnTeammates();                // the others who play: before the enemies, they scale with the party
            SetWorldMultipliers(data.Find(data.runWorld) != null);   // before the enemies are made: they read the scale when they appear
            lastSavedPos = k.Pos;
            nextPosSave = Time.unscaledTime + PosSaveEvery;
            if (Net.Authority) g.SpawnActors();                      // a client gets the host's world through snapshots
            State = LoopState.World;
            g.SnapCamera();
            ui.FadeTo(0f, 0.5f);
            ui.SetObjective("Objective: Defeat the Minotaur to return");
            OldWorld ow = data.Find(data.runWorld);
            int seedHere, leftHere;
            WorldInPlay(out seedHere, out leftHere);
            if (ow == null && team != null && Net.IsClient && seedHere != 0 && team.view != null && team.view.piles.Count > 0) ow = team.view;   // the host's world (his piles, as he said)
            if (ow != null)
            {
                BuildPileViews(ow);
                float far = float.MaxValue;
                foreach (PileView v in views) far = Mathf.Min(far, Vector3.Distance(MapLayout.TownSpawn, v.pos));
                far = views.Count > 0 ? Mathf.Round(far) : 0f;
                g.hud.Banner("OLD WORLD " + OldWorld.CodeOf(ow.seed) + "\n" + (team != null ? "Loot lies where the team fell" : "Your loot lies where you fell") + (views.Count > 1 ? " (" + views.Count + " piles)" : "") + ". "
                    + far.ToString("0") + " m away.", LoopUi.OldText, 4f);
                ui.SetObjective2(ow.ItemCount > 0 ? "Recover your loot" : "");
            }
            else if (team != null && seedHere != 0)
            {
                g.hud.Banner("OLD WORLD " + OldWorld.CodeOf(seedHere) + "\nStay together. Revive with F.", LoopUi.OldText, 4f);
                ui.SetObjective2("");
            }
            else
            {
                ui.SetObjective2("");
                if (team != null) g.hud.Banner("WORLD\nStay together. Revive with F.", Color.white, 3.5f);
            }
            Sfx.I.Play(Snd.Ready);
            if (team != null && Net.IsHost) team.BroadcastPiles(255);          // the others build the same piles
            Debug.Log("LOOP world start: " + held.Name + ", level " + g.progress.level + (ow != null ? ", old world " + ow.Code + " items " + ow.ItemCount : ""));
        }

        public void SummaryInput() { ui.SummaryInput(); }

        // Keys that only mean something while fighting: 1 / 2 swap the held weapon, C eats.
        public void WorldKeys()
        {
            if (g.knight.IsAttacking) return;
            if (InputX.Down(KeyCode.Alpha1) || InputX.Down(KeyCode.Keypad1)) SwitchHeld(0);
            else if (InputX.Down(KeyCode.Alpha2) || InputX.Down(KeyCode.Keypad2)) SwitchHeld(1);
            if (InputX.Down(KeyCode.C)) g.knight.TryBeginEat(true);
        }

        public void SwitchHeld(int slot)
        {
            if (slot < 0 || slot >= data.carry.weapons.Length) return;
            Item it = data.carry.weapons[slot];
            if (!it.IsWeapon)
            {
                g.hud.Notice("Weapon slot " + (slot + 1) + " is empty", LoopUi.Yellow);
                return;
            }
            if (slot == data.heldSlot) return;
            data.heldSlot = slot;
            g.knight.SetWeapon(it.id, it.plus);
            g.hud.Notice("Holding " + it.Name, Weapons.All[it.id].uiColor);
        }
        // The knight went down (Game.OnKnightDown). In the world that is the end of the run: the carry is gone and the
        // result is saved at once (closing the game now changes nothing); the summary comes after the screen has dimmed.
        public void OnKnightDown()
        {
            if (State != LoopState.World || dying) return;
            if (team != null) { team.OnLocalDown(); return; }       // online: a fall is not the end of the run (revive, wipe)
            dying = true;
            dieStart = Time.unscaledTime;
            Vector3 at = g.knight.Pos;
            result = data.Die(false, at.x, at.z, Loadout.NowUnix());
            Save();
            ui.SetObjective(""); ui.SetObjective2("");
            Debug.Log("LOOP fell: lost " + result.lost.Count + ", stash " + data.stash.Total + (result.ironRestored ? ", Iron Sword restored" : ""));
        }

        // Knight.DownRoutine finished falling. Dying at the base (the H test hit) is no loss: just stand up again.
        public void OnDeath()
        {
            if (State == LoopState.World || State == LoopState.Summary) return;
            g.SetBiome(Biome.Meadow); g.knight.ResetForRun(MapLayout.BasePoint);
            g.knight.SetWeapon(data.Held.id, data.Held.plus);
        }

        // ---------------------------------------------------------------- old worlds: the pile and the loot window

        // The piles of the world: one LootPile object each. (Phase 4: also called when the host changes the piles.)
        void BuildPileViews(OldWorld w)
        {
            DropPile();
            pileWorld = w;
            foreach (Pile p in w.piles)
            {
                if (p.items.Count == 0) continue;
                Vector3 at = PilePoint(p.x, p.z);
                views.Add(new PileView { data = p, pos = at, obj = LootPile.Create(g.transform, at) });
            }
        }

        // Walking into a pile (1.5 m) opens the loot window for it; closing it arms that pile again only after 4 m of distance.
        // Markers: a label over every pile that is on screen and one arrow to the nearest pile when that one is off screen.
        // Phase 7 (the M map): where this world's piles lie and how many items each holds (nothing while the run is ending).
        public void PileMarks(List<Vector3> pos, List<int> count)
        {
            if (dying) return;
            foreach (PileView v in views) { pos.Add(v.pos); count.Add(v.data.items.Count); }
        }

        readonly List<Vector3> markPos = new List<Vector3>();
        readonly List<int> markCount = new List<int>();

        void TickPile()
        {
            Knight k = g.knight;
            bool show = views.Count > 0 && pileWorld != null && !dying && !k.Dead;
            markPos.Clear(); markCount.Clear();
            int nearest = -1;
            float nd = float.MaxValue;
            if (show)
                for (int i = 0; i < views.Count; i++)
                {
                    float d = FlatDist(k.Pos, views[i].pos);
                    markPos.Add(views[i].pos); markCount.Add(views[i].data.items.Count);
                    if (d < nd) { nd = d; nearest = i; }
                }
            ui.SetLootMarkers(g.cam, markPos, markCount, nearest, nd, show && !g.PanelOpen);
            if (!show || lootMode) return;
            foreach (PileView v in views)
            {
                float d = FlatDist(k.Pos, v.pos);
                if (!v.armed && d > LootPile.RearmRadius) v.armed = true;
                if (!v.armed || d > LootPile.TouchRadius) continue;
                if (g.PanelOpen || g.progress.pendingChoices > 0 || k.Airborne || entering) return;
                lootMode = true;
                curView = v;
                curPile = v.data;
                g.SetInventory(true);
                if (!g.InventoryOpen) { lootMode = false; curView = null; curPile = null; }     // refused (a level-up panel was about to open): try again
                else
                {
                    v.armed = false;
                    if (Net.Multiplayer) k.Protect(Game.LevelUpProtect);        // online nothing pauses: a shield for the time he is looking (once per opening)
                }
                return;
            }
        }

        static float FlatDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return (a - b).magnitude; }

        // Loot window: take one item (click / Enter). The pack rules decide; a full pack leaves it in the pile.
        public void UiTake(int index)
        {
            if (!LootOpen || pileWorld == null || curPile == null || index < 0 || index >= curPile.items.Count) return;
            Item it = curPile.items[index];
            if (team != null && Net.IsClient)                        // online, a client: the host decides who gets it
            {
                if (!data.carry.CanAdd(it))
                {
                    PackFullHints++;
                    g.lastNotice = "Pack full! Can't take " + it.Name;
                    ui.SetLootWarn(g.lastNotice, LoopUi.Yellow, 1.8f, it.IsWeapon ? Section.Weapons : Section.Foods);
                    return;
                }
                ClientTake(new List<int> { index }, new List<Item> { it });
                return;
            }
            bool maxed = false;
            if (it.IsWeapon)
            {
                foreach (Item c in data.carry.weapons) if (c.IsWeapon && c.id == it.id && c.plus >= Weapons.MaxPlus) maxed = true;
                foreach (Item c in data.carry.supplies) if (c.IsWeapon && c.id == it.id && c.plus >= Weapons.MaxPlus) maxed = true;
            }
            bool merged = it.IsWeapon && data.carry.HasWeaponId(it.id);
            if (!data.TakeFromPile(pileWorld, curPile, index))
            {
                PackFullHints++;
                g.lastNotice = "Pack full! Can't take " + it.Name;
                ui.SetLootWarn(g.lastNotice, LoopUi.Yellow, 1.8f, it.IsWeapon ? Section.Weapons : Section.Foods);
                return;
            }
            Color col = it.IsWeapon ? Weapons.All[it.id].uiColor : Foods.All[it.id].color;
            string text;
            if (merged && maxed)
            {
                text = it.BaseName + " is already +" + Weapons.MaxPlus + "   -   duplicate turned into EXP";
                g.GrantExp(Weapons.DuplicateExp, g.knight.Pos + Vector3.up * 2f);
            }
            else if (merged) text = it.BaseName + " refined (duplicate: +5% damage)";
            else text = "Took " + it.Name;
            g.lastNotice = text;
            g.hud.Notice(text, col);
            Fx.I.Burst(g.knight.Pos + Vector3.up, 10, col, 1.5f, 5f, Vector3.up, 0.9f, 0.5f, 5f, 0.1f);
            SyncHeld();
            AfterPileChange();
        }

        // Take all: everything that fits (in pile order).
        public void UiTakeAll()
        {
            if (!LootOpen || pileWorld == null || curPile == null || curPile.items.Count == 0) return;
            if (team != null && Net.IsClient)
            {
                // what fits, in pile order (each one on top of the ones before it), asked for in one message
                var copy = new ItemBag(Loadout.CarryCaps[0], Loadout.CarryCaps[1], Loadout.CarryCaps[2]);
                for (int i = 0; i < copy.weapons.Length; i++) copy.weapons[i] = data.carry.weapons[i];
                for (int i = 0; i < copy.foods.Length; i++) copy.foods[i] = data.carry.foods[i];
                for (int i = 0; i < copy.supplies.Length; i++) copy.supplies[i] = data.carry.supplies[i];
                var idx = new List<int>();
                var its = new List<Item>();
                for (int i = 0; i < curPile.items.Count; i++)
                    if (copy.CanAdd(curPile.items[i])) { copy.Add(curPile.items[i]); idx.Add(i); its.Add(curPile.items[i]); }
                if (idx.Count == 0)
                {
                    PackFullHints++;
                    g.lastNotice = "Pack full! Nothing more fits";
                    ui.SetLootWarn(g.lastNotice, LoopUi.Yellow, 1.8f, Section.Weapons);
                    return;
                }
                ClientTake(idx, its);
                return;
            }
            int n = data.TakeAll(pileWorld, curPile);
            if (n == 0)
            {
                PackFullHints++;
                g.lastNotice = "Pack full! Nothing more fits";
                ui.SetLootWarn(g.lastNotice, LoopUi.Yellow, 1.8f, Section.Weapons);
                return;
            }
            g.lastNotice = "Took " + n + (n == 1 ? " item" : " items") + (curPile.items.Count > 0 ? "   -   " + curPile.items.Count + " left in the pile" : "");
            g.hud.Notice(g.lastNotice, LoopUi.LootGold);
            SyncHeld();
            AfterPileChange();
        }

        // client: asks the host for these items of the open pile; the answer (TakeAck) puts them into the carry
        float takeSentAt = -10f;

        void ClientTake(List<int> index, List<Item> items)
        {
            if (Time.unscaledTime - takeSentAt < 0.25f) return;      // one question at a time
            takeSentAt = Time.unscaledTime;
            Net.ToServer(TeamNet.Take(curPile.id, index, items));
        }

        // client: the host answered. The granted items are mine (merged / refined like any pickup), the others were gone.
        public void OnTakeAck(List<Item> got, int failed)
        {
            Vector3 at = g.knight.Pos;
            foreach (Item it in got) OnGrant(it, at);
            if (got.Count > 0 && LootOpen)
            {
                g.lastNotice = "Took " + (got.Count == 1 ? got[0].Name : got.Count + " items");
                g.hud.Notice(g.lastNotice, LoopUi.LootGold);
            }
            if (failed > 0)
            {
                Member by = team != null ? team.Find(team.lastTakerPid) : null;
                string who = by != null && team.lastTakerPid != Net.LocalPid ? by.name : "somebody";
                g.lastNotice = "Taken by " + who;
                if (LootOpen) ui.SetLootWarn("Taken by " + who, LoopUi.Yellow, 1.8f, null);
                g.hud.Notice(g.lastNotice, LoopUi.Yellow);
            }
        }

        // The piles changed (a teammate took something, or the host sent the list): the objects and the window follow.
        // Host and client both: the world in play is the one whose piles changed.
        public void OnPilesChanged(int byPid)
        {
            OldWorld w = PileWorldInPlay;
            if (w == null || State != LoopState.World) return;
            if (pileWorld == null || pileWorld != w) { if (views.Count == 0 && w.piles.Count > 0) BuildPileViews(w); return; }
            int before = 0;
            foreach (PileView v in views) before += v.data.items.Count;
            bool closeWindow = false;
            for (int i = views.Count - 1; i >= 0; i--)
            {
                PileView v = views[i];
                if (w.piles.Contains(v.data) && v.data.items.Count > 0) continue;
                if (v.obj != null) v.obj.Vanish();
                if (v == curView) closeWindow = true;
                views.RemoveAt(i);
            }
            foreach (Pile p in w.piles)
            {
                if (p.items.Count == 0) continue;
                bool has = false;
                foreach (PileView v in views) if (v.data == p) has = true;
                if (has) continue;
                Vector3 at = PilePoint(p.x, p.z);
                views.Add(new PileView { data = p, pos = at, obj = LootPile.Create(g.transform, at) });
            }
            int after = 0;
            foreach (PileView v in views) after += v.data.items.Count;
            if (before > 0 && after == 0)
            {
                g.hud.Notice("Everything recovered.", LoopUi.Green);
                g.hud.Banner("Everything recovered.", LoopUi.Green, 2.5f);
                ui.SetObjective2("");
            }
            if (byPid != Net.LocalPid && byPid != 255 && after < before && LootOpen)
            {
                Member by = team != null ? team.Find(byPid) : null;
                ui.SetLootWarn("Taken by " + (by != null ? by.name : "somebody"), LoopUi.Yellow, 1.8f, null);
            }
            if (closeWindow && (LootOpen || lootMode)) { g.SetInventory(false); }
        }

        // host: a piles change (a teammate's take) is saved at once like his own
        public void SaveWorldChange() { Save(); }

        // host, online: everybody who plays is down or fallen: the run is lost. Each player's things lie where he was last
        // (his mirror at his last place; the host's own carry where the host's knight is), a player who left too. One old
        // world holds all the piles (the new one, or the one this run was played in).
        public void HostWipe()
        {
            if (State != LoopState.World || team == null || !Net.IsHost) return;
            var drops = new List<Drop>();
            var e = new RunEndInfo { kind = 2, leftBefore = team.worldLeft };
            foreach (Member m in team.RunMembers())
            {
                var d = new Drop { who = m.name };
                if (m.pid == Net.LocalPid)
                {
                    Vector3 p = g.knight.Pos;
                    d.x = p.x; d.z = p.z;
                    d.items.AddRange(data.carry.All());
                }
                else
                {
                    d.x = m.lastPos.x; d.z = m.lastPos.z;
                    d.items.AddRange(m.mirror.All());
                }
                drops.Add(d);
                e.pids.Add(m.pid);
                e.team.Add(new TeamLine { name = m.name, status = m.status == Stat.Left ? TeamRun.Left : TeamRun.Fell, items = d.items.Count });
            }
            result = data.Fall(drops, false, Loadout.NowUnix());
            e.worldSeed = result.worldSeed; e.worldNote = (int)result.worldNote; e.leftAfter = result.worldLeft; e.pilesN = result.pilesN; e.itemsInPiles = result.pileCount;
            e.crumbs.AddRange(result.crumbs);
            result.teamSize = e.team.Count;
            result.team.AddRange(e.team);
            Save();
            Net.ToAll(TeamNet.RunEnd(e), null);
            EnterSummary();
            Debug.Log("TEAM wipe: " + e.team.Count + " players, " + result.lost.Count + " items fell in " + result.pilesN + " piles of world " + result.WorldCode);
        }

        // host, online: the game is closed in the middle of a run (decisions 7). Not only the host's own things: everybody who
        // plays gets a pile where his mirror says he was, so nobody loses his carry because the host went away. It counts as
        // ABANDONED for the whole team (the way back counts down the other old worlds, the new one starts at 3).
        void HostAbandon()
        {
            var drops = new List<Drop>();
            foreach (Member m in team.RunMembers())
            {
                var d = new Drop { who = m.name };
                if (m.pid == Net.LocalPid)
                {
                    Vector3 p = g.knight != null ? g.knight.Pos : MapLayout.TownSpawn;
                    d.x = p.x; d.z = p.z;
                    d.items.AddRange(data.carry.All());
                }
                else
                {
                    d.x = m.lastPos.x; d.z = m.lastPos.z;
                    d.items.AddRange(m.mirror.All());
                }
                drops.Add(d);
            }
            RunResult r = data.Fall(drops, true, Loadout.NowUnix());
            Save();
            Debug.Log("TEAM host closed mid-run: " + drops.Count + " carries became " + r.pilesN + " piles of world " + r.WorldCode);
        }

        // Every take is saved at once (no item can be taken twice by closing the game). A pile that ran empty vanishes; the
        // last one of the world says "Everything recovered".
        void AfterPileChange()
        {
            Save();
            if (team != null) team.BroadcastPiles(Net.LocalPid);
            if (pileWorld == null || curPile == null) return;
            if (curPile.items.Count == 0)
            {
                PileView v = curView;
                if (v != null) { if (v.obj != null) v.obj.Vanish(); views.Remove(v); }
                if (pileWorld.ItemCount == 0)
                {
                    g.hud.Notice("Everything recovered.", LoopUi.Green);
                    g.hud.Banner("Everything recovered.", LoopUi.Green, 2.5f);
                    ui.SetObjective2("");
                }
                else g.hud.Notice("Pile emptied - " + views.Count + (views.Count == 1 ? " pile" : " piles") + " left", LoopUi.LootGold);
                g.SetInventory(false);                              // the window closes (OnInventoryClosed saves again)
            }
        }

        // ---------------------------------------------------------------- old worlds: difficulty, autosaved position

        // Where the pile of an old world really stands: the saved spot, moved to the nearest free ground (no Random:
        // the same answer every time). Nothing free within 30 m = the town gate.
        public static Vector3 PilePoint(float x, float z)
        {
            float lim = Game.ArenaHalf - 4f;
            Vector3 c = new Vector3(Mathf.Clamp(x, -lim, lim), 0f, Mathf.Clamp(z, -lim, lim));
            if (PileSpotFree(c)) return c;
            for (float r = 2f; r <= 30f; r += 2f)
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    p.x = Mathf.Clamp(p.x, -lim, lim); p.z = Mathf.Clamp(p.z, -lim, lim);
                    if (PileSpotFree(p)) return p;
                }
            return new Vector3(MapLayout.TownSpawn.x, 0f, MapLayout.TownSpawn.z);
        }

        static bool PileSpotFree(Vector3 p)
        {
            if (!MapLayout.Walkable(p.x, p.z)) return false;
            return (Obstacles.Push(p, 1.1f, Obstacles.Both) - p).sqrMagnitude < 0.0001f;
        }

        void SetWorldMultipliers(bool oldWorld)
        {
            g.worldHpMul = oldWorld ? OldWorld.HpMul : 1f;
            g.worldDmgMul = oldWorld ? OldWorld.DmgMul : 1f;
            g.worldPackMul = oldWorld ? OldWorld.PackMul : 1f;
        }

        void ResetWorldMultipliers()
        {
            if (g != null) SetWorldMultipliers(false);
        }

        const float PosSaveEvery = 10f, PosSaveMoved = 5f;
        float nextPosSave;
        Vector3 lastSavedPos;

        // While a run is open the file knows where the knight is, so closing the game (ABANDONED) leaves the loot there.
        void TickPosition()
        {
            if (dying || !data.inRun || g.knight == null || g.knight.Dead || Time.unscaledTime < nextPosSave) return;
            nextPosSave = Time.unscaledTime + PosSaveEvery;
            Vector3 d = g.knight.Pos - lastSavedPos; d.y = 0f;
            if (data.runPosValid && d.magnitude <= PosSaveMoved) return;
            SavePosition(false);
        }

        void SavePosition(bool onExit)
        {
            if (data == null || State != LoopState.World || dying || !data.inRun || g == null || g.knight == null || g.knight.Dead) return;
            Vector3 p = g.knight.Pos;
            data.runX = p.x; data.runZ = p.z; data.runPosValid = true;
            lastSavedPos = p;
            if (onExit) LoopSave.Save(data); else Save();
        }

        // RETURN TO BASE
        public void UiReturn()
        {
            if (State != LoopState.Summary) return;
            ui.HideSummary();
            ui.SetDeathDim(0f);
            ResetWorldMultipliers();
            DropPortal();
            if (team != null) team.RemoveTeammates();                // the knights of the others go with the world
            g.ClearWorld();
            g.SetBiome(Biome.Meadow); g.knight.ResetForRun(MapLayout.BasePoint);
            data.AutoPack();
            g.knight.SetWeapon(data.Held.id, data.Held.plus);
            Save();
            State = LoopState.Base;
            g.SnapCamera();
            ui.SetObjective("Press Tab to prepare, then enter the world"); ui.SetObjective2("");
            g.SetInventory(true);
        }
        // ---------------------------------------------------------------- picking up, eating, dropping (in the world)

        float nextFullHint;
        public int PackFullHints;                                   // times "Pack full!" was shown (test read-out)

        // The held weapon in the carry is what the knight swings: refresh it (after a merge, a drop, a swap).
        void SyncHeld()
        {
            data.EnsureHeld();
            Item h = data.Held;
            if (h.IsWeapon) g.knight.SetWeapon(h.id, h.plus);
        }

        // A knight stands on an item. Taken into the carry (true) or left lying (false: the pack is full).
        public bool TryCollect(Pickup p)
        {
            Item it = p.kind == PickupKind.Food ? Item.Food(p.weaponId) : Item.Weapon(p.weaponId, p.plus);
            Vector3 at = p.transform.position;
            if (!data.carry.CanAdd(it))
            {
                FullHint(it);
                return false;
            }
            int netId = p.netId, wid = p.weaponId, plus = p.plus;
            PickupKind pk = p.kind;
            p.gameObject.SetActive(false);
            Object.Destroy(p.gameObject);
            ApplyCollected(it, at);
            if (team != null && Net.IsHost) Net.BroadcastPickupTaken(netId, Net.LocalPid, (int)pk, wid, at, plus);     // the others lose the item from the ground
            return true;
        }

        // "Pack full! Can't pick up X" (not more often than every 1.5 s)
        void FullHint(Item it)
        {
            if (Time.unscaledTime < nextFullHint) return;
            nextFullHint = Time.unscaledTime + 1.5f;
            PackFullHints++;
            g.lastNotice = "Pack full! Can't pick up " + it.Name;
            g.hud.Notice(g.lastNotice, LoopUi.Yellow);
            g.hud.Popup(g.knight.transform.position + new Vector3(0f, 2.7f, 0f), "Pack full", LoopUi.Yellow, 18);
        }

        // Client, online: an item lies near his knight that does not fit (the host's mirror says so too, so it stays on the ground).
        float nextWorldFullCheck;

        void TickWorldFullHint()
        {
            if (Time.unscaledTime < nextWorldFullCheck || g.knight.Dead) return;
            nextWorldFullCheck = Time.unscaledTime + 0.3f;
            Vector3 kp = g.knight.Pos;
            foreach (Pickup p in Pickup.All)
            {
                if (p.hidden || p.dropped) continue;
                Vector3 d = p.transform.position - kp; d.y = 0f;
                if (d.sqrMagnitude > Pickup.CollectRadius * Pickup.CollectRadius) continue;
                Item it = p.kind == PickupKind.Food ? Item.Food(p.weaponId) : Item.Weapon(p.weaponId, 0);
                if (!data.carry.CanAdd(it)) FullHint(it);
                return;
            }
        }

        // The item goes into the carry (the caller checked that it fits): merge / own group / Supplies, notice, effects.
        void ApplyCollected(Item it, Vector3 at)
        {
            bool maxed = false;
            if (it.IsWeapon)
            {
                foreach (Item c in data.carry.weapons) if (c.IsWeapon && c.id == it.id && c.plus >= Weapons.MaxPlus) maxed = true;
                foreach (Item c in data.carry.supplies) if (c.IsWeapon && c.id == it.id && c.plus >= Weapons.MaxPlus) maxed = true;
            }
            BagResult r = data.carry.Add(it);
            Color col = it.IsWeapon ? Weapons.All[it.id].uiColor : Foods.All[it.id].color;
            string text;
            if (it.IsWeapon && r == BagResult.Merged)
            {
                if (maxed)
                {
                    text = it.BaseName + " is already +" + Weapons.MaxPlus + "   -   duplicate turned into EXP";
                    g.GrantExp(Weapons.DuplicateExp, at + Vector3.up * 2f);
                }
                else
                {
                    int now = 0;
                    foreach (Item c in data.carry.weapons) if (c.IsWeapon && c.id == it.id) now = c.plus;
                    foreach (Item c in data.carry.supplies) if (c.IsWeapon && c.id == it.id) now = c.plus;
                    text = it.BaseName + " refined to +" + now + "   (duplicate: +5% damage)";
                }
            }
            else if (it.IsWeapon) text = "Picked up " + it.Name;
            else text = "Picked up " + it.Name + "   -   press C to eat";
            g.lastNotice = text;
            g.hud.Notice(text, col);
            Fx.I.Ring(new Vector3(at.x, 0.06f, at.z), 0.4f, 2.6f, new Color(col.r, col.g, col.b, 0.9f), 0.45f);
            Fx.I.Burst(at + Vector3.up, 16, col, 2f, 7f, Vector3.up, 0.9f, 0.6f, 6f, 0.12f);
            Debug.Log("PICKUP " + text);
            SyncHeld();
            if (team != null) { Save(); team.PackChanged(); }       // online: saved and reported (the host keeps a copy)
        }

        // Online, a client: the host gave him this item (a pickup he stood on, an item he took from a pile). Counted
        // first, so the next Pack tells the host it has arrived. It fits (the host checked a copy of the pack); if it
        // somehow does not, it goes back on the ground and he is told - nothing is lost quietly.
        public void OnGrant(Item it, Vector3 at)
        {
            if (team == null) return;
            team.applied++;
            if (data.carry.CanAdd(it)) { ApplyCollected(it, at); return; }
            Debug.Log("GRANT could not fit " + it.Name + ": put back on the ground");
            Vector3 p = g.knight.Pos;
            Net.ToServer(TeamNet.Drop(it, p.x, p.z));
            g.lastNotice = "Pack full! " + it.Name + " was put back on the ground";
            g.hud.Notice(g.lastNotice, LoopUi.Yellow);
            team.PackChanged();
        }

        public bool Eat(Section sec, int index)
        {
            Item it = data.carry.Slots(sec)[index];
            Knight k = g.knight;
            if (it.kind != ItemKind.Food || k.Dead) return false;
            if (k.hp >= k.MaxHp - 0.5f)
            {
                g.hud.Popup(k.transform.position + new Vector3(0f, 2.7f, 0f), "HP full", new Color(0.75f, 0.75f, 0.78f), 16);
                g.hud.Notice("HP full", new Color(0.75f, 0.75f, 0.78f));
                g.fullHints++;
                return false;
            }
            data.carry.Slots(sec)[index] = Item.None;
            g.CollectFood(it.id, k.transform.position);              // heals, shows "+N", plays the sound
            g.hud.Notice(g.lastNotice, Foods.All[it.id].color);
            if (team != null) { Save(); team.PackChanged(); }
            return true;
        }

        // The first food in the carry (Food group first, then Supplies) - what a meal will eat.
        public bool PeekFood(out Section sec, out int index)
        {
            for (int i = 0; i < data.carry.foods.Length; i++) if (data.carry.foods[i].kind == ItemKind.Food) { sec = Section.Foods; index = i; return true; }
            for (int i = 0; i < data.carry.supplies.Length; i++) if (data.carry.supplies[i].kind == ItemKind.Food) { sec = Section.Supplies; index = i; return true; }
            sec = Section.Foods; index = -1;
            return false;
        }

        // The instant version (test hook, and what a finished meal calls): the first food, eaten now. Players eat through Knight.TryBeginEat.
        public bool EatFirst()
        {
            for (int i = 0; i < data.carry.foods.Length; i++) if (data.carry.foods[i].kind == ItemKind.Food) return Eat(Section.Foods, i);
            for (int i = 0; i < data.carry.supplies.Length; i++) if (data.carry.supplies[i].kind == ItemKind.Food) return Eat(Section.Supplies, i);
            g.hud.Notice("No food to eat", LoopUi.Yellow);
            return false;
        }

        // Carry window, left click: hold a Weapon, eat a food, or move a Weapon from Supplies into a free Weapons slot.
        public void UiCarryUse(Section sec, int index)
        {
            Item it = data.carry.Slots(sec)[index];
            if (it.IsNone) return;
            if (it.kind == ItemKind.Food)
            {
                if (!InWorld) { Eat(sec, index); return; }                 // at the base: only the "HP full" answer
                if (g.knight.TryBeginEat(true)) g.SetInventory(false);     // a meal: the window closes and he sits down to eat
                return;
            }
            if (sec == Section.Weapons) { SwitchHeld(index); return; }
            if (data.MoveWithinCarry(index)) { SyncHeld(); g.hud.Notice(it.Name + " moved to a Weapon slot", LoopUi.Green); if (team != null) team.PackChanged(); }
            else g.hud.Notice("No free Weapon slot  -  drop a Weapon first", LoopUi.Yellow);
        }

        // Carry window, right click: the item goes down on the ground in front of the knight. It is not taken again
        // until the knight has stepped away from it. The last Weapon in the Weapons slots stays.
        public bool UiCarryDrop(Section sec, int index)
        {
            Item it = data.carry.Slots(sec)[index];
            if (it.IsNone) return false;
            if (it.IsWeapon && sec == Section.Weapons && data.carry.Count(Section.Weapons) <= 1)
            {
                g.hud.Notice("Can't drop your last Weapon", LoopUi.Yellow);
                return false;
            }
            data.carry.Slots(sec)[index] = Item.None;
            Transform t = g.knight.transform;
            Vector3 at = t.position + t.forward * 1.3f;
            if (team != null && Net.IsClient) Net.ToServer(TeamNet.Drop(it, at.x, at.z));       // the host puts it on the ground (it comes back in a snapshot)
            else
            {
                Pickup p = it.IsWeapon ? g.SpawnPickup(it.id, at) : g.SpawnFood(it.id, at);
                p.plus = it.plus;
                p.dropped = true;
            }
            if (team != null) { Save(); team.PackChanged(); }
            g.lastNotice = "Dropped " + it.Name;
            g.hud.Notice(g.lastNotice, LoopUi.Gray);
            Debug.Log("DROP " + it.Name);
            SyncHeld();
            return true;
        }
        // ---------------------------------------------------------------- the world boss and the portal

        void DropPile()
        {
            foreach (PileView v in views) if (v.obj != null) Object.Destroy(v.obj.gameObject);
            views.Clear();
            curView = null;
            pileWorld = null;
            curPile = null;
            lootMode = false;
            if (ui != null) ui.SetLootMarkers(null, null, null, -1, 0f, false);
        }

        void DropPortal()
        {
            DropPile();
            if (portal != null) Object.Destroy(portal.gameObject);
            portal = null;
            BossDown = false;
            bannerPending = false;
            portalHold = 0f;
            if (ui != null) ui.SetPortalLabel(null, Vector3.zero, false);
            if (g != null && g.hud != null) g.hud.SetArrowTarget(null, "Lair", 30f);
        }

        // Game.OnEnemyKilled: the Minotaur is dead. No respawn (Game takes care of that); the way home opens where he fell.
        public void OnBossKilled(Vector3 at)
        {
            if (State != LoopState.World || BossDown) return;
            BossDown = true;
            Vector3 p = new Vector3(Mathf.Clamp(at.x, -Game.ArenaHalf + 4f, Game.ArenaHalf - 4f), 0f, Mathf.Clamp(at.z, -Game.ArenaHalf + 4f, Game.ArenaHalf - 4f));
            portal = Portal.Create(g.transform, p);
            portalHold = 0f;
            bannerAt = Time.unscaledTime + 1.7f;                    // after the Minotaur's own "MINOTAUR SLAIN!" has gone
            bannerPending = true;
            ui.SetObjective("Objective: Step into the Portal");
            g.hud.SetArrowTarget(p, "Portal", 6f);
            Sfx.I.Play(Snd.Ready, 0.8f);
            if (team != null && Net.IsHost) Net.ToAll(TeamNet.Portal(p.x, p.z), null);      // the others build it where he fell
            Debug.Log("LOOP boss down: portal at (" + p.x.ToString("0.0") + ", " + p.z.ToString("0.0") + ")");
        }

        // Standing inside the portal for 1.5 s ends the run well (the loading ring fills; stepping out starts it again).
        void TickPortal()
        {
            if (!BossDown || portal == null || dying) return;
            if (bannerPending && Time.unscaledTime >= bannerAt && !g.PanelOpen && g.progress.pendingChoices == 0)     // (the level-up cards come first and clear banners)
            {
                bannerPending = false;
                g.hud.Banner("THE MINOTAUR HAS FALLEN.  Portal open. Collect your loot, then step in.", LoopUi.Green, 4f);
            }
            Knight k = g.knight;
            Vector3 d = k.Pos - portal.transform.position; d.y = 0f;
            // the level-up cards (and the Tab window) come first: the ring does not fill behind an open panel
            bool blocked = g.PanelOpen || g.progress.pendingChoices > 0;
            bool inside = !blocked && !k.Dead && d.magnitude <= Portal.Radius;
            portalHold = inside ? portalHold + Time.unscaledDeltaTime : 0f;
            portal.SetProgress(portalHold / Portal.HoldSeconds);
            ui.SetPortalLabel(g.cam, portal.transform.position + Vector3.up * 4.2f, true);
            if (portalHold >= Portal.HoldSeconds)
            {
                if (team != null) team.OnLocalReached();            // online: the countdown home starts for everybody (RunEnd ends it)
                else Victory();
            }
        }

        // host, online: the countdown is over - everybody who is still in the world goes home together. The host's own
        // carry goes into his stash; the table says what happened to each player; the clients do the same on their side.
        public void HostVictory()
        {
            if (State != LoopState.World || dying || team == null) return;
            portalHold = 0f;
            int leftBefore = team.worldLeft;
            result = data.Survive();
            var e = new RunEndInfo { kind = 1, worldSeed = result.worldSeed, worldNote = (int)result.worldNote, leftBefore = leftBefore, leftAfter = result.worldLeft,
                pilesN = result.pilesN, itemsInPiles = result.pileCount };
            e.crumbs.AddRange(result.crumbs);
            foreach (Member m in team.RunMembers())
            {
                int st = m.status == Stat.Left ? TeamRun.Left : m.status == Stat.Alive ? TeamRun.Survived : TeamRun.Fell;
                int items = m.pid == Net.LocalPid ? result.brought.Count : st == TeamRun.Left ? 0 : m.mirror.Total;
                e.pids.Add(m.pid);
                e.team.Add(new TeamLine { name = m.name, status = st, items = items });
            }
            result.teamSize = e.team.Count;
            result.team.AddRange(e.team);
            Save();
            Net.ToAll(TeamNet.RunEnd(e), null);
            EnterSummary();
            Debug.Log("TEAM victory: " + e.team.Count + " players, " + result.brought.Count + " items home for the host");
        }

        // client, online: the host ended the run. A win: what I carried goes into my stash (the host's old worlds are not
        // mine to count down). A loss: what I carried is gone (it lies in the host's world as piles).
        public void OnTeamRunEnd(RunEndInfo e)
        {
            if (team == null) return;
            if (State != LoopState.World && State != LoopState.Summary) { team.EndRun(); return; }      // did not play this run
            if (State == LoopState.Summary) return;
            RunResult r = e.kind == 1 ? data.Survive(false) : data.LoseCarry(e.kind == 3);
            r.worldSeed = e.worldSeed;
            r.worldNote = (WorldNote)Mathf.Clamp(e.worldNote, 0, (int)WorldNote.Crumbled);
            r.worldLeft = e.leftAfter;
            r.pileCount = e.itemsInPiles;
            r.pilesN = e.pilesN;
            r.inOldWorld = e.leftBefore > 0;
            r.crumbs.AddRange(e.crumbs);
            r.teamSize = e.team.Count;
            r.team.AddRange(e.team);
            result = r;
            Save();
            EnterSummary();
            Debug.Log("TEAM run end: kind " + e.kind + ", " + (e.kind == 1 ? r.brought.Count + " items home, " + r.leftBehind.Count + " left behind" : r.lost.Count + " items lost"));
        }

        // The run is over on this machine: the summary opens, the room is free for the next run.
        void EnterSummary()
        {
            ui.SetObjective(""); ui.SetObjective2("");
            ui.SetPortalLabel(null, Vector3.zero, false);
            g.hud.SetArrowTarget(null, "Lair", 30f);
            State = LoopState.Summary;
            ui.ShowSummary(result);
            Sfx.I.Play(Snd.Ready);
            team.EndRun();
            if (Net.IsHost) team.SendWorlds(-1);                     // the clients' OLD WORLDS list: what this run did to the host's worlds
        }

        void Victory()
        {
            portalHold = 0f;
            result = data.Survive();
            Save();
            State = LoopState.Summary;
            ui.SetObjective(""); ui.SetObjective2("");
            ui.SetPortalLabel(null, Vector3.zero, false);
            g.hud.SetArrowTarget(null, "Lair", 30f);
            ui.ShowSummary(result);
            Sfx.I.Play(Snd.Ready);
            Debug.Log("LOOP victory: " + result.brought.Count + " items home, " + result.leftBehind.Count + " left behind");
        }
    }
}
