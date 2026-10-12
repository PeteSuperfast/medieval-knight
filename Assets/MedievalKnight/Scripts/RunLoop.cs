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

        public bool InWorld { get { return State == LoopState.World; } }
        public bool SummaryOpen { get { return State == LoopState.Summary; } }
        public bool Busy { get { return false; } }                  // fading into the world
        public bool Dying { get { return false; } }                 // fallen, the screen is dimming

        public RunLoop(Game game) { g = game; }

        // The session just started: load the save, stand at the base and open PREPARE.
        public void Begin()
        {
            data = LoopSave.Load();
            ui = LoopUi.Create(g.transform, this);
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
        public void Tick() { }

        // Esc: true when it only closed a question of the loop
        public bool OnEscape() { return false; }

        public void OnInventoryOpened()
        {
            if (State == LoopState.Base) State = LoopState.Prepare;
            ui.ShowPrepare(data);
        }

        public void OnInventoryClosed()
        {
            ui.HideAll();
            if (State == LoopState.Prepare) State = LoopState.Base;
        }

        public void InventoryInput() { }
        public void SummaryInput() { }
        public void WorldKeys() { }
        public void OnDeath() { }
    }
}
