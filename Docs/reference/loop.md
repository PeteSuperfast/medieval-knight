# Loop (ฐาน → เข้าโลก → บอส → กลับฐาน) + โลกเก่า + หลายกอง

> phase 1 เสร็จ 2026-10-12 (`Docs/phase1-minimal-loop/`); phase 2 โลกเก่า 2026-10-13 (`Docs/phase2-old-world/`); phase 4 co-op + หลายกอง 2026-10-13 (`Docs/phase4-coop/`)
> ออนไลน์ (ห้อง / READY / ล้ม-ชุบ / wipe / portal ทีม / ออกกลางรอบ) อยู่ `online-coop.md` — ไฟล์นี้คือกติกาของ loop เอง (ใช้กับ Single Player และกับผู้เล่นแต่ละคนออนไลน์)

## เปิดเมื่อไหร่
`Game.Loop` = `InSession && run != null` (session ที่มี `RunLoop`); `Game.Coop` = `Loop && Net.Multiplayer`.
เกิดจากเมนู: **Single Player, Host Game, Join Game** (`StartSession(true)`; client ใช้โหมดที่ host บอกใน Welcome). `-single` / `-selftest` ไม่มี `-loop` และทุกอย่างที่ใส่ `-arena` = สนาม v1 เดิม (ชุดเทสต์ v1). เทสต์เร็ว: `-single -loop`.

## ไฟล์
| ไฟล์ | หน้าที่ |
| --- | --- |
| `Loadout.cs` | `Item`, `ItemBag`, `Loadout` (stash 12/8/16, carry 2/3/4, `heldSlot`, `inRun`, `runCoop`, **`worlds`**, `runWorld`, `runX/Z`, `unseen`), `RunResult`. `Survive(decay)` / `LoseCarry` / `Die` → **`Fall(drops)`** / `DecayWorlds` / `TakeFromPile` / `TakeAll` / `Sanitize` |
| `OldWorld.cs` | `Pile {id,x,z,items}`, `OldWorld` (seed, fellUnix, left, **`piles`**, `AddDrop`, `PileNear`, `ItemCount`), `Drop`, `TeamLine`, `CrumbNote` / `CrumbLine` / `WorldNote` |
| `LoopSave.cs` | JSON ไฟล์เดียว `persistentDataPath/medieval-knight-save.json` (`-save <ไฟล์>`), `.tmp` → `.bak` → จริง, ไฟล์เสีย → `.bak` / `.corrupt`. Version 1; `ow[].pv = 2` + `piles[]`; `rc` = รอบออนไลน์ของ client |
| `RunLoop.cs` | state `Base → Prepare → World → Summary`, เข้าโลก (fade 0.5 วิ), เก็บ / กิน / ทิ้ง, ตาย, บอส + portal, VICTORY / YOU FELL / ABANDONED, กอง + หน้าต่างเก็บของ (หลายกอง) |
| `LoopUi.cs` | canvas ของตัวเอง: PREPARE, OLD WORLDS, Carry, LOOT, สรุป, fade, objective, ป้ายโลกเก่า |
| `Portal.cs` / `LootPile.cs` | portal ฟ้า / กองของทอง (ลำแสง 12 ม.) |

## กติกา phase 1
- เริ่มครั้งแรก: stash มี Iron Sword เล่มเดียว; ไม่มีอาวุธเลย → Iron +0 ฟรี.
- ใส่ของ: Weapon ชนิดเดียวกัน**รวมกัน** `plus = min(5, a + b + 1)`; Weapon → ช่อง Weapons ก่อนแล้ว Supplies; Food → Food ก่อนแล้ว Supplies; เต็ม = `Pack full! Can't pick up <ชื่อ>`.
- ในโลก: เลเวล / EXP / การ์ด เริ่มใหม่ทุกรอบ, ถือ HELD. ฆ่า Minotaur (ไม่ respawn) → portal ที่จุดตาย ยืน 1.5 วิ = VICTORY.
- ตาย = จบรอบทันที (YOU FELL; ออนไลน์ดู online-coop.md). รอด: carry เข้า stash (เต็ม = `LEFT BEHIND` หาย).
- F5 ปิดใน loop. Soul ซ่อน. `H` ที่ฐาน: ลุกที่ฐาน ไม่เสียอะไร.

## กติกา phase 2 โลกเก่า
- **ตาย / ABANDONED**: ของที่พก**ทั้งหมด**ตก ณ จุดที่ตาย = `OldWorld` ใหม่ (รหัส `#XXXX` = 4 hex ต่ำของ seed, เหลือ 3 ครั้ง). พกว่าง = ไม่สร้าง.
- **กลับฐานทุกแบบ (รอด / ตาย / ABANDONED)**: `DecayWorlds` ลดตัวนับของทุกโลกเก่าที่มีก่อนรอบนี้ −1 (รวมโลกที่เข้า; โลกที่เพิ่งเกิดไม่ลด); ถึง 0 = โลกและกองหายถาวร (`unseen` → แถบแดงที่ฐาน + บล็อก OLD WORLDS ใน G). รอดแล้วโลกที่เข้ากองหมด = ปิด (ไม่นับสลาย)
- **ความยาก** (`Game.worldHpMul/DmgMul/PackMul` ตั้งก่อน `SpawnActors`, รีเซ็ตเป็น 1 ที่ `UiReturn` / `Shutdown`): HP ×1.5, ดาเมจ ×1.25, แพ็ค ×1.5 (ceil, ≤ 48 ตัว). Armor ไม่คูณ. คูณกับ `CoopHpPerPlayer` ของจำนวนคน
- `Sanitize`: left 1..3, seed ซ้ำ/0 ทิ้ง, ไอเท็มผิดทิ้ง, ≤ 48 ชิ้น/โลก, ≤ 99 โลก, ตำแหน่งในแมพ
- เซฟตำแหน่งกลางโลก: ทุก ~10 วิถ้าเดินเกิน 5 ม. + ตอนปิดเกม → ABANDONED รู้จุด (ไม่มี = `TownSpawn`)

## กติกากอง (phase 4 แทน "กองย้าย" ของ phase 2 — ใช้กับ Single Player ด้วย)
- ตกที่ไหนเป็นกองที่นั่น; **ห่างกองเดิม ≤ 3 ม. = รวมเข้ากองนั้น** (`OldWorld.MergeRadius`); กองไม่ย้าย
- เพดาน 48 ชิ้น**ต่อโลก** (ทุกกองรวมกัน; เกิน = หาย แจ้งใน G). Weapon ชนิดเดียวกันแยกชิ้นในกอง รวม +N ตอนเก็บ
- `Fall(drops)`: หลาย drop (ทีม wipe: ต่อคน) เข้าโลกเดียว; `Die` = drop เดียว
- กองละ 1 `LootPile` + ป้าย `LOOT xN`; ลูกศรทอง 1 อันชี้กองใกล้สุด `Loot 142 m (3 piles)` เมื่อกองนั้นอยู่นอกจอ; เดินชน 1.5 ม. = เปิดหน้าต่างของกองนั้น (เกมหยุดใน SP / ไม่หยุดออนไลน์), ปิดแล้วต้องห่าง 4 ม. ก่อนเปิดใหม่; เก็บหมดกอง = กองหาย `Pile emptied - n piles left`; หมดทั้งโลก = `Everything recovered.`
- แถว OLD WORLDS แถวเดียวต่อโลก แสดง `n piles`; เก็บแล้วเซฟทันที

## หน้าจอ / ปุ่ม
| ที่ | ปุ่ม |
| --- | --- |
| Tab | ฐาน: เปิด/ปิด PREPARE; โลก: Carry (SP: เกมหยุด) |
| PREPARE | คลิก/Enter ย้ายของ, คลิกขวา Weapon = HELD, ลูกศร/WASD เคอร์เซอร์ (ลงสุด = ปุ่มล่าง, ซ้าย/ขวาสลับ), Q/E สลับฝั่ง, Esc ถอย. ออนไลน์: client ได้ READY แทน NEW WORLD, host ได้ `NEW WORLD n/m` |
| OLD WORLDS | ขึ้น/ลงเลือกแถว (เหลือน้อยสุดก่อน แล้วใหม่สุด), Enter = ENTER OLD WORLD (ไม่มี Weapon = `Pack a Weapon first`; ไม่มีอาหาร = ถาม) |
| LOOT | คลิก / Enter = เก็บ, **Q = Take all**, คลิกขวาของใน Pack = ทิ้ง, Esc/Tab ปิด |
| 1 / 2, C | ถือ Weapon ช่อง 1 / 2, กินอาหารช่องแรก |
| Esc ในโลก | `Leaving now counts as a death. Press Esc again.` แล้วออกเมนู (= ABANDONED) |

## ทดสอบ
- selftest เต็ม (~7 นาที): เคส loop อยู่ท้าย `SelfTest.cs` (`LoopTests`: ข้อมูล / old world data / เซฟ / ตัวคูณ / ฐาน / PREPARE / เข้าโลก / เก็บ-กิน-ทิ้ง / ตาย / บอส-portal / old world run, ui, loot / **several piles** / **online run cut short**); ใช้เซฟของเทสต์เอง `<โฟลเดอร์ selftest>\save_loop.json`
- เฉพาะ loop (~3 นาที): ต่อท้าย `-looponly`
- scripted ไม่มี `-save`: เซฟในหน่วยความจำ; `StartLoopSession` ต้อง `LeaveToMenu` ก่อนล้างไฟล์
- `-menutest` ต้อง Esc ปิด PREPARE ก่อนเดิน (Host ก็เข้า loop แล้ว: ไม่มีศัตรูที่ฐาน)

## ข้อควรระวัง
- ช่องคลัง `Loadout.StashCaps` / `CarryCaps`; ฐาน = ลานกลาง ห้ามวางของกีดขวาง
- ทุกจุดใน `Game` / `Knight` / `Hud` ที่ใช้ `Loop` ต้องคิดถึง `Coop` (ห้ามหยุดเวลาออนไลน์, ห้ามถือว่ามีผู้เล่นคนเดียว)

## biome ของโลก (snow-map-models)
- `OldWorld.biome` (เก็บตอนสร้างโลกเก่า ไม่เปลี่ยน), `Loadout.runBiome` (รอบที่เปิดอยู่; `BeginRun(seed, coop, biome)` — โลกเก่าใช้ biome ของมันเอง). เซฟ: `ow[].bm` และราก `rb` (ไม่มี = ทุ่ง; ค่าที่ไม่รู้จัก = ทุ่ง; Version ยัง 1). `Sanitize` ย้ายกองหิมะที่อยู่ในกำแพง -> หมู่บ้าน
- `RunLoop.StartWorld` เรียก `Game.SetBiome(runBiome)` ตอนจอดำ; ทุกที่ที่กลับฐานเรียก `SetBiome(Meadow)` ก่อน `ResetForRun(BasePoint)`. แถว OLD WORLDS / ป้ายมุมขวา / banner ใส่ `SNOW`; แผนที่ M วาดพื้นตาม biome (texture แยกต่อ biome)
