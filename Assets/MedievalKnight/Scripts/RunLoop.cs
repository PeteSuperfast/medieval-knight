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
        bool entering;                                              // ENTER WORLD pressed: the screen is fading to black
        float enterAt;

        public int EnterRequests;                                   // times ENTER WORLD went through (test read-out)

        public bool InWorld { get { return State == LoopState.World; } }
        public bool SummaryOpen { get { return State == LoopState.Summary; } }
        public bool PrepareOpen { get { return State == LoopState.Prepare && g.InventoryOpen; } }
        public bool CarryOpen { get { return State == LoopState.World && g.InventoryOpen; } }
        public bool Busy { get { return entering; } }               // fading into the world
        public bool Dying { get { return false; } }                 // fallen, the screen is dimming

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
            g.knight.ResetForRun(MapLayout.BasePoint);
            data.AutoPack();
            State = LoopState.Base;
            g.SetInventory(true);
        }

        public void Shutdown()
        {
            if (ui != null) Object.Destroy(ui.gameObject);
            ui = null;
        }

        // every frame, before the rest of Game.Update
        public void Tick()
        {
            if (entering && Time.unscaledTime >= enterAt) StartWorld();
            ui.SetStrip(data, State == LoopState.World);
        }

        // Esc: true when it only closed a question of the loop
        public bool OnEscape()
        {
            if (askedNoFood) { SetAsk(false); return true; }
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
            if (State == LoopState.World) { ui.ShowCarry(data, g.knight); return; }
            if (State == LoopState.Base) State = LoopState.Prepare;
            ui.ShowPrepare(data);
        }

        public void OnInventoryClosed()
        {
            ui.HideAll();
            askedNoFood = false;
            if (State == LoopState.Prepare)
            {
                State = LoopState.Base;
                if (!data.introShown) { data.introShown = true; dirty = true; }
                if (dirty) Save();
            }
        }

        public void InventoryInput()
        {
            if (State == LoopState.Prepare) ui.PrepareInput(data);
            else if (State == LoopState.World) ui.CarryInput(data, g.knight);
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

        // ENTER WORLD (button, or Enter on the "enter without food?" question)
        public void UiEnter()
        {
            if (State != LoopState.Prepare) return;
            if (!data.CanEnter)
            {
                ui.SetWarn("Pack a Weapon first", LoopUi.Red, 1.5f);
                return;
            }
            bool food = data.carry.Count(Section.Foods) > 0;
            foreach (Item it in data.carry.supplies) if (it.kind == ItemKind.Food) food = true;
            if (!food && !askedNoFood) { SetAsk(true); return; }
            SetAsk(false);
            EnterWorld();
        }

        // ENTER WORLD: the run is opened and saved first (closing the game from here on counts as a death), then the
        // screen fades and the world is built.
        void EnterWorld()
        {
            EnterRequests++;
            g.SetInventory(false);                                  // closes PREPARE (saves a changed pack)
            data.BeginRun();
            Save();
            entering = true;
            enterAt = Time.unscaledTime + 0.5f;
            ui.FadeTo(1f, 0.5f);
            Debug.Log("LOOP enter world (held " + data.Held.Name + ", carry " + data.carry.Total + " items)");
        }

        // Fully dark: build the world, level 1 with the held weapon, at the town gate spawn.
        void StartWorld()
        {
            entering = false;
            Knight k = g.knight;
            g.ClearWorld();
            k.ResetForRun(MapLayout.TownSpawn);
            Item held = data.Held;
            k.SetWeapon(held.id, held.plus);
            g.SpawnActors();
            State = LoopState.World;
            g.SnapCamera();
            ui.FadeTo(0f, 0.5f);
            ui.SetObjective("Objective: Defeat the Minotaur to return");
            Sfx.I.Play(Snd.Ready);
            Debug.Log("LOOP world start: " + held.Name + ", level " + g.progress.level);
        }

        public void SummaryInput() { }

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
        public void OnDeath() { }
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
    }
}
