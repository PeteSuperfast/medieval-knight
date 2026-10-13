# Loop (ฐาน → เข้าโลก → บอส → กลับฐาน) + โลกเก่า — คนเดียว

> phase 1 เสร็จ 2026-10-12 (`Docs/phase1-minimal-loop/`); phase 2 โลกเก่า 2026-10-13 (`Docs/phase2-old-world/` — decisions ข้อ 7–14 ชนะ tech)

## เปิดเมื่อไหร่ (แยกโหมด)
`Game.Loop` = `InSession && run != null && Net.Mode == Single` — เกิดเฉพาะ **Single Player จากเมนู** (`Menu.StartSingle` → `StartSession(true)`).
`-single` / `-selftest` / Host / Join = v1 เดิมทั้งหมด (ไม่เรียก loop). เทสต์เร็ว: `-single -loop`.
ทุกจุดที่แก้ในไฟล์เดิมอยู่หลัง `Loop`; ไม่มีข้อความเครือข่ายใหม่ (`Net.Protocol` ยัง 2).

## ไฟล์
| ไฟล์ | หน้าที่ |
| --- | --- |
| `Loadout.cs` | `Item`, `ItemBag`, `Loadout` (stash 12/8/16, carry 2/3/4, `heldSlot`, `inRun`, **`worlds`**, `runWorld`, `runX/Z`, `unseen`), `RunResult`. `Survive()` / `Die(abandoned, x, z, now)` / `DecayWorlds` / `TakeFromPile` / `TakeAll` / `Sanitize()` |
| `OldWorld.cs` | `OldWorld` (seed, fellUnix, left, x/z, pile), ค่าคงที่, `CrumbNote` / `CrumbLine` / `WorldNote` |
| `LoopSave.cs` | JSON ไฟล์เดียว `persistentDataPath/medieval-knight-save.json` (`-save <ไฟล์>`), `.tmp` → `.bak` → จริง, ไฟล์เสีย → `.bak` / `.corrupt`. Version 1 |
| `RunLoop.cs` | state `Base → Prepare → World → Summary`, เข้าโลก (fade 0.5 วิ), เก็บ / กิน / ทิ้ง, ตาย, บอส + portal, VICTORY / YOU FELL / ABANDONED, กอง + หน้าต่างเก็บของ |
| `LoopUi.cs` | canvas ของตัวเอง: PREPARE, OLD WORLDS, Carry, LOOT, สรุป, fade, objective, ป้ายโลกเก่า |
| `Portal.cs` / `LootPile.cs` | portal ฟ้า / กองของทอง (ลำแสง 12 ม.) |

## กติกา phase 1
- เริ่มครั้งแรก: stash มี Iron Sword เล่มเดียว; ไม่มีอาวุธเลย → Iron +0 ฟรี.
- ใส่ของ: Weapon ชนิดเดียวกัน**รวมกัน** `plus = min(5, a + b + 1)`; Weapon → ช่อง Weapons ก่อนแล้ว Supplies; Food → Food ก่อนแล้ว Supplies; เต็ม = `Pack full! Can't pick up <ชื่อ>`.
- ในโลก: เลเวล / EXP / การ์ด / Soul เริ่มใหม่ทุกรอบ, ถือ HELD. ฆ่า Minotaur (ไม่ respawn) → portal ที่จุดตาย ยืน 1.5 วิ = VICTORY.
- ตาย = จบรอบทันที (YOU FELL). รอด: carry เข้า stash (เต็ม = `LEFT BEHIND` หาย).
- F5 ปิดใน loop. Soul ซ่อน. `H` ที่ฐาน: ลุกที่ฐาน ไม่เสียอะไร.

## กติกา phase 2 โลกเก่า
- **ตาย / ABANDONED**: ของที่พก**ทั้งหมด**รวมเป็นกองเดียว ณ จุดที่ตาย = `OldWorld` ใหม่ (รหัส `#XXXX` = 4 hex ต่ำของ seed, เหลือ 3 ครั้ง). พกว่าง = ไม่สร้าง. กองสูงสุด 48 ชิ้น (เกิน = หาย แจ้งใน G). Weapon ชนิดเดียวกันแยกชิ้นในกอง รวม +N ตอนเก็บ
- **ตายในโลกเก่า**: ของรวมเข้ากองเดิม ย้ายไปจุดตายล่าสุด (ของที่ยังไม่เก็บตามไปด้วย)
- **กลับฐานทุกแบบ (รอด / ตาย / ABANDONED, ตั้งแต่ 2026-10-13 11:19 UTC)**: `Loadout.DecayWorlds` ลดตัวนับของทุกโลกเก่าที่มีก่อนรอบนี้ −1 (รวมโลกที่เข้า/ตายข้างใน; โลกที่เพิ่งเกิดจากการตายนี้ไม่ลด เริ่ม 3); ถึง 0 = โลกและกองหายถาวร แม้เป็นโลกที่เพิ่งตาย (ของที่เพิ่งตกหายด้วย, `WorldNote.Crumbled`) (`unseen` → แถบแดงที่ฐาน + บล็อก OLD WORLDS ใน G). รอดแล้วโลกที่เข้ากองหมด = ปิดตอนจบรอบ (ไม่นับสลาย)
- **ความยาก** (`Game.worldHpMul/DmgMul/PackMul`, ตั้งก่อน `SpawnActors`, รีเซ็ตเป็น 1 ที่ `UiReturn` / `Shutdown`): HP ×1.5, ดาเมจ ×1.25, ขนาดแพ็ค ×1.5 (ceil, ยังจำกัด 48 ตัว). **Armor ไม่คูณ** (`EnemyArmorScale`). บอสเกิดใหม่ทุกรอบ
- **กอง**: วางที่ `PilePoint` (จุดตายที่ย้ายไปพื้นว่างใกล้สุด ไม่ใช้ Random). เดินชน 1.5 ม. = เปิดหน้าต่างเก็บของ (เกมหยุด); ปิดแล้วต้องออกห่าง 4 ม. ก่อนจะเปิดอีก. เก็บทีละชิ้น / Take all ตามกฎช่องพก (เต็ม = ของคงในกอง). เก็บแล้วเซฟทันที. เก็บหมด = กองหายพร้อมแจ้ง `Everything recovered.`
- **เซฟตำแหน่ง** กลางโลก: ทุก ~10 วิถ้าเดินเกิน 5 ม. + ตอนปิดเกม (`Shutdown`) → ABANDONED รู้จุด (ไม่มี = ประตูเมือง `TownSpawn`). ผ่อนกฎ phase 1 "ปิดเกมไม่แตะเซฟ" เฉพาะการเขียนตำแหน่ง (`inRun` ยัง true)
- `Sanitize`: left 1..3, seed ซ้ำ/0 ทิ้ง, ไอเท็มผิดทิ้ง, ≤ 48 ชิ้น/โลก, ≤ 99 โลก (เกิน = สลายและแจ้ง), ตำแหน่งในแมพ. แก้บั๊ก phase 1: ลบสำเนา stash ที่ซ้ำกับ carry **เฉพาะเมื่อไม่มีรอบเปิดอยู่**
- แมพเดิม (`Random(11)` ไม่แตะ); `seed` เป็นแค่ ID (phase 3 ใช้เลือก biome)

## หน้าจอ / ปุ่ม (เฉพาะ loop; ที่เหลือเหมือน controls.md)
| ที่ | ปุ่ม |
| --- | --- |
| Tab | ฐาน: เปิด/ปิด PREPARE; โลก: Carry (เกมหยุด) |
| PREPARE | คลิก/Enter ย้ายของ, คลิกขวา Weapon = HELD, ลูกศร/WASD เคอร์เซอร์ (ลงสุด = ปุ่มล่าง, ซ้าย/ขวาสลับ **NEW WORLD / OLD WORLDS (n)**), Q/E สลับฝั่ง, Esc ถอย. แถบแดงบน = โลกที่สลายตั้งแต่ครั้งก่อน (หายเมื่อปิด PREPARE) |
| OLD WORLDS | ขึ้น/ลงเลือกแถว (เหลือน้อยสุดก่อน แล้วใหม่สุด), Enter / ปุ่ม = ENTER OLD WORLD (ใช้ carry ที่จัดไว้; ไม่มี Weapon = `Pack a Weapon first`; ไม่มีอาหาร = ถาม), Esc = กลับ PREPARE |
| LOOT (เดินชนกอง) | คลิก / Enter ที่ชิ้น = เก็บ, **Q = Take all**, คลิกขวาของใน Pack = ทิ้ง, ลูกศร/WASD เคอร์เซอร์, Esc/Tab ปิด |
| 1 / 2, C | ถือ Weapon ช่อง 1 / 2, กินอาหารช่องแรก |
| Esc ในโลก | `Leaving now counts as a death. Press Esc again.` แล้วออกเมนู (= ABANDONED ครั้งหน้า → ของเข้ากอง) |
| HUD โลกเก่า | บรรทัดสองทอง `Recover your loot`, ป้ายซ้ายใต้กล่องสถานะ `OLD WORLD #X / HP x1.5 DMG x1.25 / Crumbles in N returns` (เหลือ 1 = กรอบเหลือง), ลูกศรทองขอบจอ `Loot N m` / ป้าย `YOUR LOOT xN` |

## ทดสอบ
- selftest เต็ม (~6 นาที): เคส loop อยู่ท้าย `SelfTest.cs` (`LoopTests`: ข้อมูล / **old world data** / เซฟ / **old world save** / ตัวคูณ / ฐาน / PREPARE / เข้าโลก / เก็บ-กิน-ทิ้ง / ตาย / บอส-portal / **old world run, ui, loot**) ใช้ไฟล์เซฟของเทสต์เอง (`<โฟลเดอร์ selftest>\save_loop.json`)
- เฉพาะเคส loop (~3 นาที): ต่อท้าย `-looponly`; ใช้ path เต็มของโฟลเดอร์ selftest ถ้าอยากได้ภาพ
- scripted ไม่มี `-save`: เซฟในหน่วยความจำ; `StartLoopSession` ต้อง `LeaveToMenu` ก่อนล้างไฟล์
- `-menutest` ต้อง Esc ปิด PREPARE ก่อนเดิน; เช็ค host `Enemy.All.Count >= 2`

## ข้อควรระวังเมื่อต่อยอด (phase 3+)
- ข้อมูลโลกเก่าอยู่ที่เครื่องผู้เล่น (phase 4: host ถือ) อยู่หลัง `Loop` ยังไม่แตะ `Net`
- ช่องคลัง `Loadout.StashCaps` / `CarryCaps`; ฐาน = ลานกลาง ห้ามวางของกีดขวาง
