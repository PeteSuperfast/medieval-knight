# Phase 7 — UI: เลขสี / กล้อง scroll / แผนที่ M (แยกจาก tech.md)

## 1. เลขดาเมจสี
- `Enemy.ShowHit(..., bool heavy)` เรียก `static NumberStyle(crit, heavy, toHp, out Color, out int size)` ลำดับ คริ > หนัก > เกราะรับหมด > ธรรมดา: คริ ส้ม #FF9E1A (heavy 38 / ไม่ 34), หนัก #FF5040 36, เกราะ #A6D1FF 22, ธรรมดา ขาว 24 (ค่าเดิมทั้งหมดยกเว้นหนัก)
- เก็บ `lastNumberKind` (0 ปกติ, 1 เกราะ, 2 หนัก, 3 คริ, 4 คริ+หนัก) ให้เทสต์/MpTest อ่าน
- `heavy` มาถึงยังไง: SP/host `HitEnemy`→`TakeHit`; client รับจาก `EnemyHit` บิต 8 → `NetHit(..., heavy)` (tech-net §2). เลขที่ผู้เล่นโดนไม่เปลี่ยน

## 2. กล้อง scroll
- `Game.CamPitch` (const 56) → ค่าเริ่ม `CamPitchDefault` + ตัวแปร `camPitch`; `PlaceCamera` ใช้ `camPitch`. `AimPoint` ใช้ `ScreenPointToRay` อยู่แล้ว → เล็งตรงทุกมุมโดยไม่แก้
- `InputX.Scroll()` ใหม่: Input System `Mouse.current.scroll.ReadValue().y` / legacy `Input.mouseScrollDelta.y` (ไม่ผ่าน `Map()` เพราะไม่ใช่ KeyCode); ใช้เฉพาะเครื่องหมาย (ค่าต่อ notch ต่างกันตามเครื่อง)
- `Game.Update`: เมื่อ `!PanelOpen && !MapOpen && !(Loop && run.ui.PromptShown)` → `target = Clamp(target - 3*sign, 40, 70)` (เลื่อนขึ้น = มุมต่ำลง = เห็นไกล) แล้ว lerp `camPitch` ~0.15 วิ; `hud.Notice("Camera 52°")`; `PlayerPrefs` `mk_campitch` (ไม่ลง LoopSave) โหลดตอนเริ่ม session
- **ไม่ชนการเลื่อนรายการ**: grep แล้วไม่มีโค้ดอ่านล้อเมาส์เลย (`worldScroll`/`lootScroll` เป็นดัชนีที่ลูกศร/WASD ขับ); PREPARE, OLD WORLDS (อยู่ใต้ `InventoryOpen`), LOOT, Carry, การ์ดเลเวล, สรุป ล้วนอยู่ใน `PanelOpen` อยู่แล้ว
- **ไม่ชนปุ่มเดิม**: ล้อไม่ใช่ปุ่มใดเลย; F2 ว่าง (`Map()` ไม่มี F2 → เพิ่ม `case KeyCode.F2: return Key.F2`)

## 3. แผนที่ M
- **ข้อมูลโซน**: `MapLayout.ZoneAt(x,z)` เป็นค่าคงที่ใน `World.cs` ไม่มี seed/random → **แมพเหมือนกันทุกโลก ทุก seed** (seed ของโลกเก่าเป็นแค่ ID/#CODE; ฉากใช้ `System.Random(11)`) ถ้า phase 3 เพิ่ม biome ค่อยให้ `MapUi` อ่านจากข้อมูลโลกนั้น
- `MapUi.cs` (MonoBehaviour เหมือน `TeamUi`: `Create(parent, game)`, canvas sortingOrder 120, CanvasScaler 1280×720, `raycastTarget=false` ทั้งหมด): `RawImage` 520×520 จาก `Texture2D` 190×190 (2 ม./px, Point) สร้างครั้งเดียวตอนเปิดครั้งแรกด้วย `ZoneAt` + จานสี design-ui; ตัวชี้เป็น pool; `Show(bool)`, `Tick()` ทุกเฟรมตอนเปิด; `px=(x/380+0.5)*520`
- **แหล่งข้อมูล (มีอยู่ในเครื่องแล้วทั้งหมด ไม่เพิ่ม Msg)**:
| แสดง | อ่านจาก |
| --- | --- |
| ตัวเอง | `knight.Pos` + yaw |
| เพื่อน / ล้ม | `players` ที่ `remote`: `Pos`, `pid`, `Dead && !Fallen`; สี `Knight.UiColor(pid)` |
| portal | `run.portal != null` (client ได้จาก `Portal` เดิม) |
| กองของ | `RunLoop.PileMarks()` ใหม่ อ่าน `views` (client ได้จาก `Piles` เดิม) |
| รัง / จุดเริ่ม | `MapLayout.Lair` + ระยะ / `TownSpawn` |
- ไม่อ้าง `Enemy.All`, HP, กระเป๋าเพื่อน เลย (ห้ามตาม decisions ข้อ 6)
- **`MapOpen`** ใน `Game`: M สลับเมื่อ `InSession && !(Loop && !run.InWorld) && !PanelOpen`; ไม่เป็น `PanelOpen`/`Paused` → เกมไม่หยุด (`timeScale` คงเดิม) ผู้เล่นเดิน/สู้ได้; ถ้า panel อื่นเปิดทับ → ซ่อนไว้ ปิดแล้วกลับมา
- **Esc**: ลำดับ `run.OnEscape()` > `InventoryOpen` > **`MapOpen` → ปิด** > `LeaveKey` (ไม่นับเป็น Esc ออกโลก = ตาย)
- **F2** = `Sfx.I.CycleVolume()` + `Notice("Sound: …")` (ย้ายบล็อก M เดิม); `Sound 100%` แสดงมุม panel
- **F1**: แก้ข้อความ 2 ที่ใน `Hud.cs` (V1 ~257, loop ~1097) เป็น `M  Map   F2  Sound   Scroll  Camera` ในบรรทัดเดิม (กรอบ 246 px ไม่ล้น)

## 4. เคสใหม่ (SelfTest `Phase7`, `-p7only`)
- **สี**: Heavy Strike ใส่หุ่น `ForceCrit=1` → `lastNumberKind==2`; `ForceCrit=2` → 4; ท่าเดิม → 0; สไลม์แดงเกราะรับหมด → 1
- **กล้อง**: inject `MouseState{scroll}` ผ่าน `InputSystem.QueueStateEvent`: ทีละ notch 56→53; 15 notch ลง = 70 ค้าง; ขึ้น = 40 ค้าง; Tab เปิด = ไม่เปลี่ยน; `AimPoint()` ตอนเมาส์ทับตำแหน่งอัศวินบนจอ ≈ ตำแหน่งอัศวิน (<0.6 ม.) ที่ 40° และ 70°; PlayerPrefs ตรง
- **แผนที่**: M → `MapOpen`, `timeScale==1`, อัศวินเดินต่อ; Esc ปิดโดยยัง `InWorld`; ที่ฐาน M ไม่เปิด; sample 5 จุด texture = `ZoneAt`; มาร์กเกอร์กอง = `views.Count`; portal = 0 ก่อนฆ่าบอส; มาร์กเกอร์ศัตรู = 0
- **MpTest**: `-arena` จำนวนมาร์กเกอร์เพื่อน = จำนวนเพื่อน; loop: เพิ่ม 1 assert ที่ client ว่า marker portal โผล่หลัง `Portal` msg (ใช้เคสที่มีอยู่)
