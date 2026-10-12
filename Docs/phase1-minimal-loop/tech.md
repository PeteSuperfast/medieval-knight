# Phase 1 Tech (ภาพรวม) — อ่านคู่กับ requirement / design / decisions
แยก 4 ไฟล์เพราะจำกัด 10KB: `tech.md` (ไฟล์นี้) | `tech-data-save.md` (ข้อมูล + เซฟ) | `tech-flow.md` (state / แยกโหมด / portal / ปุ่ม / net) | `tech-test.md` (ทดสอบ)

## 1. แนวทาง
- เพิ่ม "ชั้น loop" ครอบเกมเดิม: `Game.Loop` = true เฉพาะ Single Player ที่กดจากเมนู (`Net.Mode==Single` + flag) นอกนั้น (`-single`, `-selftest`, Host, Join) เป็น v1 ทุกอย่าง
- ฐานกับโลกคือฉากเดียวกัน (แมพเดิม = biome เดียว): ฐานคือ "ไม่มี actor" ที่ลานกลาง; เข้าโลก = สร้าง actor + เกิดที่ TownSpawn
- ข้อมูลใหม่ (Stash/Carry) เป็นคลาสล้วน (ไม่พึ่ง Unity) เซฟเป็น JSON ไฟล์เดียว; `Knight.inventory` เดิมกลายเป็น "ภาพฉาย" ของอาวุธที่ถืออยู่ จึงไม่ต้องแตะสูตรดาเมจ/Net.State
- ตาย = จบรอบ ไม่ revive (แยกที่ `Knight.DownRoutine`); ฆ่า Minotaur = เกิด `Portal` ไม่เกิดใหม่
- ไม่เปลี่ยนข้อความเครือข่าย → **ไม่ bump `Net.Protocol` (คง 2)**

## 2. ไฟล์ใหม่ (Assets/MedievalKnight/Scripts/)
| ไฟล์ | เหตุผล |
|---|---|
| `Loadout.cs` | `Item`, `ItemBag`, `Loadout`, `RunResult` — logic ล้วน เทสต์ง่าย |
| `LoopSave.cs` | อ่าน/เขียน JSON, atomic write, .bak, validate |
| `RunLoop.cs` | `LoopState`, state machine, enter world / ตาย / portal / สรุป |
| `LoopUi.cs` | canvas ของตัวเอง (แบบ Menu): PREPARE (C), สรุป (G), fade/dim, วงโหลด portal |
| `Portal.cs` | โมเดล portal ด้วย `Prims`/`Fx` (วงแหวน + ลำแสง ฟ้า #6FC8FF) |

## 3. ไฟล์เดิมที่แก้ (เฉพาะจุด)
| ไฟล์ | จุดแก้ |
|---|---|
| `Game.cs` | `StartSession(bool loop=false)` (ถ้า loop: ไม่เรียก `SpawnActors`, สร้าง `RunLoop`); `public bool Loop`; `EndSession` ล้าง run/ui; `Update`: Esc/Tab/1/2/C route ไป run เมื่อ Loop; `PanelOpen` รวม Prepare/Summary; `SimulateWorld`: `if(Loop && !run.InWorld) return;` และห้าม respawn Minotaur เมื่อ Loop; `OnEnemyKilled`: Minotaur+Loop → `run.OnBossKilled(pos)`; `UpdatePickups`/`Collect`: ทางแยก loop (เก็บเข้า Carry, Pack full, ข้ามกติกา "HP เต็มไม่กิน"); `FoodFullHints` ข้ามเมื่อ Loop; `OnKnightDown`: Loop → ไม่เรียก `ShowDeath`; `RequestReset`: Loop → ปฏิเสธ; ใหม่: `EatFromCarry(slot)`, `DropCarried(slot)` |
| `Knight.cs` | `DownRoutine`: หลัง `FallRoutine` ถ้า `Game.I.Loop` → `Game.I.OnLoopDeath(); yield break;`; แยก `ResetForRun(Vector3 at)` (ตัวเดียวกับ `ResetAll` ที่ไม่แตะ inventory/souls=0/progress ใหม่/Dead=false/ตำแหน่ง); `ResetAll` เรียกต่อกัน พฤติกรรมเดิม |
| `Weapon.cs` | `Inventory.SetSingle(id,plus)` (owned มีชิ้นเดียว + equipped); `Pickup`: เพิ่ม `plus`, `dropped` (ไม่เก็บจนกว่าผู้เล่นเดินพ้นรัศมี); ไม่มีการเปลี่ยน snapshot |
| `Hud.cs` | โหมด loop ของ Tab (E): 2 ช่อง Weapon ใหญ่ + แถว FOOD 3 + SUPPLIES 4, ซ่อน SOUL (แผงซ้ายบน + Tab); `Banner(text,color,seconds=1.6f)`; `SetObjective`; ลูกศร lair ชี้ portal ได้ + ป้าย `RETURN PORTAL`; ซ่อน "Minotaur returns in" เมื่อ Loop; `InventoryInput` loop: คลิกซ้าย=กิน/ถือ, ขวา=Drop |
| `Menu.cs` | `StartSingle` → `Game.I.StartSession(true)` (Host/Join ไม่แตะ); ข้อความ sub ไม่ต้องเปลี่ยน |
| `World.cs` | `MapLayout.BasePoint = (0,0,0)` (ลานกลาง ไม่เพิ่มของกีดขวาง) |
| `Args.cs` | `-save <ไฟล์>`, `-loop` (ใช้กับ `-single` เพื่อเทสต์) |
| `InputX.cs` | `Map()`: เพิ่ม `KeyCode.C → Key.C` (ปุ่มเดียวที่ขาด; 1/2/Enter/Space/ลูกศร/Q/E/Tab/Esc/เมาส์ขวามีแล้ว) |
| `SelfTest.cs` | ส่วนใหม่ท้ายไฟล์ (ดู tech-test.md) |
| `MpTest.cs` | เฉพาะ `-menutest` ส่วน "single player from the menu" (เข้าฐาน+Prepare เปิดเอง ต้องปิดก่อนเดิน) — `-mptest` ไม่แก้ |

## 4. คลาสและความสัมพันธ์
- `Item` (struct: `kind` None/Weapon/Food, `id`, `plus`) → อยู่ใน `ItemBag` (3 หมวด: weapons/foods/supplies, ความจุกำหนดตอนสร้าง)
- `Loadout` ถือ `stash`(12/8/16) + `carry`(2/3/4) + `heldSlot` + `inRun` + `introShown`; เป็นเจ้าของกติกาเต็ม/ย้าย/รวม และ `Survive()` / `Die(abandoned)` ที่คืน `RunResult`
- `LoopSave` อ่าน/เขียน `Loadout` (DTO `[Serializable]` สำหรับ `JsonUtility`)
- `RunLoop` (สร้างโดย `Game` เมื่อ Loop) ถือ `Loadout`, `LoopUi`, `Portal`; เรียก `Knight`/`Hud`/`Game` ผ่าน API สาธารณะ
- `LoopUi` อ่านข้อมูลจาก `Loadout` อ่านอย่างเดียว ส่ง action กลับ `RunLoop` (ไม่แก้ข้อมูลเอง)

## 5. ลำดับงาน (build ผ่านทุกขั้น; ★ = รัน selftest)
1. `Args` (-save/-loop), `InputX` C, `MapLayout.BasePoint` — ยังไม่เปลี่ยนพฤติกรรม
2. `Loadout.cs` (ล้วน) + เคส unit ใน selftest ★
3. `LoopSave.cs` + เคสเซฟ/โหลด/ไฟล์เสีย ★
4. `Game.Loop` + `StartSession(loop)` + `RunLoop` โครง (ฐานว่าง, Tab สลับ placeholder) + `Menu.StartSingle`; `-single` ยัง v1 ★
5. `LoopUi` PREPARE เต็ม (ย้าย/ถือ/เตือนไม่มีอาหาร/ENTER WORLD)
6. เข้าโลก: `Knight.ResetForRun`, `SpawnActors`, เซฟ `inRun`, ภาพฉายอาวุธ, ปุ่ม 1/2, Esc ข้อความใหม่
7. เก็บของเข้า Carry + Pack full + `Pickup.dropped` + Drop + กินด้วย C + Tab (Hud) + objective ★
8. ตาย → `OnLoopDeath` → สรุป YOU FELL → กลับฐาน; ABANDONED ตอนเปิดเกม; Iron restored ★
9. บอส: ไม่ respawn, `Portal`, ค้าง 1.5 วิ → VICTORY, รวมเข้า Stash, LEFT BEHIND ★
10. ซ่อน Soul / ปิด F5 ใน loop, ข้อความ F1, แก้ `-menutest`, รัน build + selftest + mptest + menutest

## 6. ความเสี่ยง
- **ปะปนโหมด**: ถ้าลืมเช็ค `Game.Loop` ในจุดแก้ใด v1/mptest จะเพี้ยน → ทุกจุดแก้ต้องมีเงื่อนไข `Loop` และมี selftest v1 เดิมคุม
- `Knight.DownRoutine` / `Hud.RefreshInventory` เป็นโค้ดแน่น แก้น้อยที่สุด; Hud.cs ยาว (~1150 บรรทัด) ระวังซ้ำซ้อน → ใส่โค้ดโหมด loop เป็นเมธอดแยก
- pickup ดาบอยู่ 90 วิ อาหาร 60 วิ: หลังฆ่าบอสต้องเก็บให้ทันก่อนหมดอายุ (ปล่อยตามเดิม ไม่ขยายใน phase 1)
- `Time.timeScale=0` ตอน Prepare/Summary (single เท่านั้น); ห้ามทำตอนออนไลน์ (Loop ไม่มีวัน true ตอนออนไลน์)
- เซฟเสีย/เขียนทับเซฟจริงจากเทสต์ → ดู tech-data-save.md

## 7. ขนาดงาน: **กลาง-ใหญ่** (ไฟล์ใหม่ 5, แก้ 9 ไฟล์, UI 3 จอ, เซฟ + เทสต์ใหม่)
