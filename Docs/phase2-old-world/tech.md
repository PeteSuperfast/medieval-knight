# Phase 2 Tech — โลกเก่า
คู่กับ requirement/design/decisions; ต่อยอด phase1 `tech*.md`, `reference/loop.md`

## 1. แนวทาง
- ข้อมูลโลกเก่าเป็นคลาสล้วนใน `Loadout` เซฟไฟล์ LoopSave เดิม; สร้าง/ลดอายุที่ `Die`/`Survive` เดิม
- โลกเก่า = แมพเดิม (`Random(11)` ไม่แตะ) + ตัวคูณ + กอง `LootPile`. `seed` = ID โลกเท่านั้น (`#XXXX` = 4 hex ต่ำ) ไม่ใช่ seed ฉาก; phase 3 ใช้เลือก biome
- ทุกจุดหลัง `Game.Loop`; ไม่มีข้อความเน็ตใหม่ → `Net.Protocol` คง 2; ไม่แตะ InputX/Net/Args/MpTest/Menu

## 2. ไฟล์
| ไฟล์ | หน้าที่ |
|---|---|
| ใหม่ `OldWorld.cs` | `OldWorld`, ค่าคงที่, `CrumbNote` (ล้วน) |
| ใหม่ `LootPile.cs` | โมเดลกอง + ลำแสงทอง 12 ม. (`Prims`/`Fx`) |
| แก้ `Loadout.cs` | รายการโลก, Die/Survive/BeginRun, take, Sanitize |
| แก้ `LoopSave.cs` | DTO + validate |
| แก้ `RunLoop.cs` | เข้าโลกเก่า, L, ตาย/รอด, autosave ตำแหน่ง |
| แก้ `LoopUi.cs` | C2, H, L, D2, G2, ลูกศรกอง |
| แก้ `Game.cs` | ตัวคูณ + `SpawnPack` |
| แก้ `SelfTest.cs` | เคสท้าย `LoopTests` |
| docs | `loop.md`, `files.md`, `CLAUDE.md` ตอนจบ |

## 3. คลาส
- `OldWorld { int seed(≠0); long fellUnix; int left; float x,z; List<Item> pile; Code }`; const `StartLeft=3, HpMul=1.5, DmgMul=1.25, PackMul=1.5, MaxPile=48`
- `Loadout` เพิ่ม `worlds`, `runWorld`(0=ใหม่), `runX/runZ/runPosValid`, `unseen` (สลายที่ยังไม่เห็น)
  - `Find(seed)`, `Sorted()`, `BeginRun(seed=0)`, `Die(abandoned, x, z, now)`, `TakeFromPile(w,i)`/`TakeAll` ผ่าน `carry.CanAdd/Add` (รวม +N เดิม เต็ม = ของคงในกอง)
- `RunResult` เพิ่ม `worldNote` (Created/Added/Recovered/StillInPile) + `crumbs` (before→after, crumbled, itemsLost)
- `RunLoop` ถือ `Loadout`+`LootPile`+`LoopUi` (UI อ่านอย่างเดียว ส่ง action กลับ) เหมือน phase 1

## 4. กติกา
- **Die**: ของที่พกทั้งหมดเข้ากอง. `runWorld` พบ → ต่อกองเดิม ไม่รีเซ็ต `left` **กองย้ายไปจุดตายล่าสุด**; ไม่พบ = โลกใหม่ seed ไม่ซ้ำ left=3. พกว่าง = ไม่สร้าง. เกิน MaxPile ส่วนเกินหาย (แจ้งใน G)
- **Survive**: `runWorld` กองว่าง → ปิด (ไม่นับสลาย); ทุกโลกที่เหลือ `left--` (รวมโลกที่เข้า); ≤0 → ลบ + `unseen` + `crumbs`
- **ปิดโลกตอนจบรอบ ไม่ใช่ตอนเก็บหมด** (ตายหลังเก็บหมดจะรวมเข้าโลกเดิม ไม่ซ้อน) ต่างจาก decisions ข้อ 1 เล็กน้อย; UI ขึ้น `Everything recovered.` ทันที
- **ABANDONED**: `Die(true, runX, runZ)` ใน `Begin`; ไม่มี pos → `TownSpawn`
- `Sanitize`: ตัดไอเท็มผิด, left 1..3, ≤99 โลก, seed ซ้ำทิ้ง. **แก้ของเดิม**: ลบสำเนา stash ซ้ำกับ carry เฉพาะ `!inRun` (phase 1: เก็บอาวุธซ้ำ stash แล้วปิดเกม = stash หาย)

## 5. เซฟ
- DTO เพิ่ม `ow[] {seed,t,left,x,z,pile[]}`, `runWorld`, `rx`, `rz`, `rpos`, `unseen[]`. **Version คง 1** (ฟิลด์หาย = ว่าง) → เซฟ phase 1 เปิดได้
- เซฟเมื่อ: ENTER, ตาย, Survive, **ทุกครั้งที่เก็บจากกอง** (กันของซ้ำ), ปิด L, ทุก ~10 วิถ้าเดิน >5 ม., `Shutdown` กลางโลก (เขียนเฉพาะ pos `inRun` ยัง true) — ผ่อนกฎ "ปิดเกมไม่แตะเซฟ" เพื่อให้ ABANDONED รู้จุดตาย
- ไฟล์เสีย/.bak/.corrupt ใช้เดิม

## 6. ความยาก (Game.cs)
- field `worldHpMul/worldDmgMul/worldPackMul` (default 1) คูณใน `EnemyHpScale`/`EnemyDamageScale`; Slime/Minotaur อ่านตอนเกิด → ไม่แก้ไฟล์เหล่านั้น. RunLoop ตั้งก่อน `SpawnActors` รีเซ็ตที่ `UiReturn`/`Shutdown`/โลกใหม่
- `SpawnPack`: `count=ceil(count*PackMul)` หลัง cap 16 ก่อน cap `MaxSlimes` 48 = ขนาดแพ็ค ไม่ใช่ความถี่
- `InitStats` คูณ armor ด้วย scale เดียวกับ HP → ทนมากกว่า x1.5 จริง (เหมือน PartyLevel)

## 7. Flow / UI
- C2: NEW WORLD = `UiEnter()` เดิม; OLD WORLDS เปิด H (`worldsOpen`, Esc ปิดผ่าน `OnEscape()` แบบ askedNoFood). `UiEnterOld(seed)` ตรวจ Weapon/อาหารเหมือนกัน (`pendingSeed`)
- เข้าโลก: `BeginRun(seed)`+Save → `StartWorld` ตั้งตัวคูณ, วาง `LootPile` ที่ `PilePoint`, banner, D2
- `PilePoint(x,z)`: clamp → ไม่ `Walkable`/ชน `Obstacles.Push` ค้นวง 2–30 ม. (ไม่ใช้ Random) → ไม่เจอ `TownSpawn`
- L: `TickPile` รัศมี 1.5 ม. armed ใหม่เมื่อพ้น 4 ม. → `SetInventory(true)`+`lootMode` (pause/Tab/Esc เดิม) กอง >12 เลื่อนล้อ. **Q = Take all ไม่ชนปุ่มเดิม** (panel ใช้ Q/E อยู่แล้ว สกิล Q ถูก `PanelOpen` บล็อก) ไม่ต้องแก้ InputX
- ลูกศรกองวาดเองใน `LoopUi` (`Hud.SetArrowTarget` มีเป้าเดียว lair/portal ใช้อยู่) ซ่อนเมื่อกองว่าง
- แถบแดงสลายใน C2 แสดง `unseen` ล้างเมื่อปิด PREPARE; modal หยุดเวลาเฉพาะ single (`Paused` เดิม)
- เวลาแถว = `FromUnixTimeSeconds(t).ToLocalTime()` แสดงอย่างเดียว

## 8. เคสใหม่ (ต่อ `-looponly`)
- ข้อมูล: ตาย→โลก left 3 seed ไม่ซ้ำ; ตายซ้ำรวมกอง ไม่รีเซ็ต; Survive ลดทุกโลก 0=สลาย; กองหมด=ปิดไม่สลาย; เก็บ merge/เต็ม/Take all; เก็บแล้วตายรวมโลกเดิม
- เซฟ: round-trip; JSON phase 1 → worlds ว่าง stash เท่าเดิม; ฟิลด์เพี้ยน (left 9, id เกิน, seed ซ้ำ) ไม่ crash; ขยะ → `.corrupt`
- ฉาก: ตายจริง→G CREATED→OLD WORLDS(1)→เข้า→กองที่จุดตาย ±1.5 ม., Minotaur HP ≈1.5x, แพ็คแรก ≈1.5x; ชน→L; เก็บ/เต็ม/Take all; ฆ่าบอส+portal→stash, left 2; ชนะ 3 รอบ→สลาย+แถบแดง; ปิดเกมกลางโลกเก่า→ABANDONED เข้ากองเดิม; `-single`/Host ไม่มี `Loop`

## 9. ลำดับ (build+commit แยก; ★=selftest)
1. `OldWorld.cs` + Loadout (Die/Survive/Take/Sanitize) + เคสข้อมูล ★ (RunLoop เรียกแบบเดิม)
2. `LoopSave` DTO + เคสเซฟ/เซฟ phase 1 ★
3. ตัวคูณใน `Game` + เคสสเกล (ตั้ง field ตรง) ★
4. `RunLoop` ตาย/ABANDONED/Victory ใช้ฟังก์ชันใหม่ + autosave pos + G2 ★
5. C2 + H + `UiEnterOld` + banner/D2
6. `LootPile` + ลูกศร + L + take ★
7. แถบสลาย + edge case + `-menutest` + build/selftest/mptest + docs

## 10. เสี่ยง
- `Sanitize` เดิมลบ stash (§4) ต้องแก้ก่อนเปิดฟีเจอร์
- `LoopUi` ยาวแล้ว: เพิ่มเป็นเมธอดกลุ่มแยก; L ใช้ cell ของ Carry ร่วม ระวัง state ค้างเมื่อปิดด้วย Esc/Tab/ตาย
- กองอาจใกล้รัง Minotaur: เจตนา; ตาย 2 จุดในโลกเดียว = ของจุดแรกที่ยังไม่เก็บตามไปจุดล่าสุด
- armor ซ้อน (§6) ปรับ const ที่ `OldWorld`; mptest: ไม่แตะ Net/Menu/Host

## 11. ขนาด: **กลาง** (ไฟล์ใหม่ 2 แก้ 5 UI ใหม่ 3 จอ; UI หนักสุด)
