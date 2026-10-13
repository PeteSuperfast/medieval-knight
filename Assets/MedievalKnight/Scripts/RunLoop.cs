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
        public LoopState State { get; private set; }
        bool dirty;                                                 // PREPARE changed the stash / carry since the last save
        bool askedNoFood;                                           // "Enter without food?" is on screen
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
        public LootPile pile;                                       // the pile of the old world being played (null: none / all taken)
        public Vector3 pilePos;                                     // where it stands (PilePoint of the saved spot)
        OldWorld pileWorld;
        Pile curPile;                                               // the pile the loot window shows (phase 4 step 2: the first one)
        bool lootMode;                                              // the inventory window is the loot window, not the Carry window
        bool pileArmed = true;                                      // false from closing the window until the knight has stepped away
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
            if (LoopSave.LastLoadNote.Length > 0) g.hud.Notice(LoopSave.LastLoadNote, LoopUi.Yellow);
            else if (data.trimNote.Length > 0) g.hud.Notice(data.trimNote, LoopUi.Yellow);
            g.knight.ResetForRun(MapLayout.BasePoint);
            ResetWorldMultipliers();
            if (data.inRun)
            {
                // the game was closed in the middle of a run: that counts as a death (no save-scumming). The loot lies
                // where the knight last was (autosaved while he walked); no spot saved = at the town gate.
                float ax = data.runPosValid ? data.runX : MapLayout.TownSpawn.x, az = data.runPosValid ? data.runZ : MapLayout.TownSpawn.z;
                result = data.Die(true, ax, az, Loadout.NowUnix());
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
            SavePosition(true);                                     // closing the game mid-run: ABANDONED knows where the loot lies
            ResetWorldMultipliers();
            DropPortal();
            if (ui != null) Object.Destroy(ui.gameObject);
            ui = null;
        }

        // every frame, before the rest of Game.Update
        public void Tick()
        {
            if (entering && Time.unscaledTime >= enterAt) StartWorld();
            if (State == LoopState.World) TickPortal();
            if (State == LoopState.World) TickPosition();
            if (State == LoopState.World) TickPile();
            if (dying)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - dieStart) / DimSeconds);
                ui.SetDeathDim(t * 0.6f);
                if (t >= 1f) { dying = false; State = LoopState.Summary; ui.ShowSummary(result); }
            }
            ui.SetStrip(data, State == LoopState.World);
            OldWorld here = State == LoopState.World && !dying ? data.Find(data.runWorld) : null;     // top right badge of an old world
            if (here == null) ui.SetOldBadge(null, false);
            else ui.SetOldBadge("OLD WORLD " + here.Code + "\nHP x" + OldWorld.HpMul.ToString("0.0") + "  DMG x" + OldWorld.DmgMul.ToString("0.00")
                + "\nCrumbles in " + here.left + (here.left == 1 ? " return" : " returns"), here.left <= 1);
        }

        // Esc: true when it only closed a question of the loop
        public bool OnEscape()
        {
            if (askedNoFood) { SetAsk(false); return true; }
            if (worldsOpen && State == LoopState.Prepare) { UiCloseWorlds(); return true; }
            return false;
        }

        void SetAsk(bool on)
        {
            askedNoFood = on;
            ui.SetPrompt(on ? "Enter without food?  [Enter] Go   [Esc] Back" : "");
        }

        // ---------------------------------------------------------------- PREPARE

        public void OnInventoryOpened()
        {
            askedNoFood = false;
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
            askedNoFood = false;
            worldsOpen = false;
            if (lootMode)
            {
                lootMode = false;
                pileArmed = false;                                  // not again until he has stepped away from the pile
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
            if (askedNoFood) SetAsk(false);
        }

        // Right click on a carried Weapon: that one is held in the world.
        public void UiHold(int slot)
        {
            if (State != LoopState.Prepare) return;
            if (data.SetHeld(slot)) dirty = true;
        }

        public void UiBack()
        {
            if (State == LoopState.Prepare) g.SetInventory(false);
        }

        // OLD WORLDS button: the list of old worlds opens over PREPARE (nothing to open when there are none).
        public void UiOpenWorlds()
        {
            if (State != LoopState.Prepare || data.worlds.Count == 0) return;
            if (askedNoFood) SetAsk(false);
            worldsOpen = true;
            ui.ShowWorlds(data);
        }

        public void UiCloseWorlds()
        {
            if (!worldsOpen) return;
            worldsOpen = false;
            if (askedNoFood) SetAsk(false);
            ui.HideWorlds();
            ui.ShowPrepare(data);
        }

        // NEW WORLD (seed 0) / ENTER OLD WORLD (the old world's seed). Same rules for both: a Weapon is needed, no food asks first.
        public void UiEnter(int seed = 0)
        {
            if (State != LoopState.Prepare) return;
            if (seed != 0 && data.Find(seed) == null) return;
            if (!data.CanEnter)
            {
                ui.SetWarn("Pack a Weapon first", LoopUi.Red, 1.5f);
                return;
            }
            bool food = data.carry.Count(Section.Foods) > 0;
            foreach (Item it in data.carry.supplies) if (it.kind == ItemKind.Food) food = true;
            if (!food && !(askedNoFood && askedSeed == seed)) { askedSeed = seed; SetAsk(true); return; }
            SetAsk(false);
            EnterWorld(seed);
        }

        // Enter on the "enter without food?" question: the world that was asked about
        public void UiConfirmEnter() { UiEnter(askedSeed); }

        // The run is opened and saved first (closing the game from here on counts as a death), then the screen fades
        // and the world is built.
        void EnterWorld(int seed)
        {
            EnterRequests++;
            worldsOpen = false;
            g.SetInventory(false);                                  // closes PREPARE (saves a changed pack)
            data.BeginRun(seed);
            Save();
            entering = true;
            enterAt = Time.unscaledTime + 0.5f;
            ui.FadeTo(1f, 0.5f);
            Debug.Log("LOOP enter world" + (data.runWorld != 0 ? " OLD " + OldWorld.CodeOf(data.runWorld) : "") + " (held " + data.Held.Name + ", carry " + data.carry.Total + " items)");
        }

        // Fully dark: build the world, level 1 with the held weapon, at the town gate spawn.
        void StartWorld()
        {
            entering = false;
            DropPortal();
            Knight k = g.knight;
            g.ClearWorld();
            k.ResetForRun(MapLayout.TownSpawn);
            Item held = data.Held;
            k.SetWeapon(held.id, held.plus);
            SetWorldMultipliers(data.Find(data.runWorld) != null);   // before the enemies are made: they read the scale when they appear
            lastSavedPos = k.Pos;
            nextPosSave = Time.unscaledTime + PosSaveEvery;
            g.SpawnActors();
            State = LoopState.World;
            g.SnapCamera();
            ui.FadeTo(0f, 0.5f);
            ui.SetObjective("Objective: Defeat the Minotaur to return");
            OldWorld ow = data.Find(data.runWorld);
            Pile first = ow != null && ow.piles.Count > 0 ? ow.piles[0] : null;
            if (ow != null)
            {
                Vector3 pp = first != null ? PilePoint(first.x, first.z) : MapLayout.TownSpawn;
                float far = Mathf.Round(Vector3.Distance(MapLayout.TownSpawn, pp));
                g.hud.Banner("OLD WORLD " + ow.Code + "\nYour loot lies where you fell. " + far.ToString("0") + " m away.", LoopUi.OldText, 4f);
                ui.SetObjective2(ow.ItemCount > 0 ? "Recover your loot" : "");
            }
            else ui.SetObjective2("");
            if (ow != null && first != null)
            {
                pileWorld = ow;
                curPile = first;
                pilePos = PilePoint(first.x, first.z);
                pile = LootPile.Create(g.transform, pilePos);
                pileArmed = true;
            }
            Sfx.I.Play(Snd.Ready);
            Debug.Log("LOOP world start: " + held.Name + ", level " + g.progress.level + (ow != null ? ", old world " + ow.Code + " items " + ow.ItemCount : ""));
        }

        public void SummaryInput() { ui.SummaryInput(); }

        // Keys that only mean something while fighting: 1 / 2 swap the held weapon, C eats.
        public void WorldKeys()
        {
            if (g.knight.IsAttacking) return;
            if (InputX.Down(KeyCode.Alpha1) || InputX.Down(KeyCode.Keypad1)) SwitchHeld(0);
            else if (InputX.Down(KeyCode.Alpha2) || InputX.Down(KeyCode.Keypad2)) SwitchHeld(1);
            if (InputX.Down(KeyCode.C)) EatFirst();
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
            g.knight.ResetForRun(MapLayout.BasePoint);
            g.knight.SetWeapon(data.Held.id, data.Held.plus);
        }

        // ---------------------------------------------------------------- old worlds: the pile and the loot window

        // Walking into the pile (1.5 m) opens the loot window; closing it arms it again only after 4 m of distance.
        void TickPile()
        {
            Knight k = g.knight;
            bool show = pile != null && pileWorld != null && curPile != null && !dying && !k.Dead;
            ui.SetLootMarker(g.cam, pilePos, show ? FlatDist(k.Pos, pilePos) : 0f, show ? curPile.items.Count : 0, show && !g.PanelOpen);
            if (!show || lootMode) return;
            float d = FlatDist(k.Pos, pilePos);
            if (!pileArmed && d > LootPile.RearmRadius) pileArmed = true;
            if (!pileArmed || d > LootPile.TouchRadius) return;
            if (g.PanelOpen || g.progress.pendingChoices > 0 || k.Airborne || entering) return;
            lootMode = true;
            g.SetInventory(true);
            if (!g.InventoryOpen) lootMode = false;                 // refused (a level-up panel was about to open): try again
            else pileArmed = false;
        }

        static float FlatDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return (a - b).magnitude; }

        // Loot window: take one item (click / Enter). The pack rules decide; a full pack leaves it in the pile.
        public void UiTake(int index)
        {
            if (!LootOpen || pileWorld == null || curPile == null || index < 0 || index >= curPile.items.Count) return;
            Item it = curPile.items[index];
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

        // Every take is saved at once (no item can be taken twice by closing the game), the last one closes the pile.
        void AfterPileChange()
        {
            Save();
            if (pileWorld != null && curPile != null && curPile.items.Count == 0)
            {
                g.hud.Notice("Everything recovered.", LoopUi.Green);
                g.hud.Banner("Everything recovered.", LoopUi.Green, 2.5f);
                ui.SetObjective2("");
                if (pile != null) pile.Vanish();
                pile = null;
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
            g.ClearWorld();
            g.knight.ResetForRun(MapLayout.BasePoint);
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
            Knight k = g.knight;
            if (!data.carry.CanAdd(it))
            {
                if (Time.unscaledTime >= nextFullHint)
                {
                    nextFullHint = Time.unscaledTime + 1.5f;
                    PackFullHints++;
                    g.lastNotice = "Pack full! Can't pick up " + it.Name;
                    g.hud.Notice(g.lastNotice, LoopUi.Yellow);
                    g.hud.Popup(k.transform.position + new Vector3(0f, 2.7f, 0f), "Pack full", LoopUi.Yellow, 18);
                }
                return false;
            }
            bool maxed = false;
            if (it.IsWeapon)
            {
                foreach (Item c in data.carry.weapons) if (c.IsWeapon && c.id == it.id && c.plus >= Weapons.MaxPlus) maxed = true;
                foreach (Item c in data.carry.supplies) if (c.IsWeapon && c.id == it.id && c.plus >= Weapons.MaxPlus) maxed = true;
            }
            BagResult r = data.carry.Add(it);
            p.gameObject.SetActive(false);
            Object.Destroy(p.gameObject);
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
            return true;
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
            return true;
        }

        // C: the first food (Food group first, then Supplies)
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
            if (it.kind == ItemKind.Food) { Eat(sec, index); return; }
            if (sec == Section.Weapons) { SwitchHeld(index); return; }
            if (data.MoveWithinCarry(index)) { SyncHeld(); g.hud.Notice(it.Name + " moved to a Weapon slot", LoopUi.Green); }
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
            Pickup p = it.IsWeapon ? g.SpawnPickup(it.id, at) : g.SpawnFood(it.id, at);
            p.plus = it.plus;
            p.dropped = true;
            g.lastNotice = "Dropped " + it.Name;
            g.hud.Notice(g.lastNotice, LoopUi.Gray);
            Debug.Log("DROP " + it.Name);
            SyncHeld();
            return true;
        }
        // ---------------------------------------------------------------- the world boss and the portal

        void DropPile()
        {
            if (pile != null) Object.Destroy(pile.gameObject);
            pile = null;
            pileWorld = null;
            curPile = null;
            lootMode = false;
            pileArmed = true;
            if (ui != null) ui.SetLootMarker(null, Vector3.zero, 0f, 0, false);
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
            if (portalHold >= Portal.HoldSeconds) Victory();
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
