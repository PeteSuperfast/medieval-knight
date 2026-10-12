# Phase 1 loop (ฐาน → เข้าโลก → บอส → กลับฐาน) — คนเดียว

> เขียนตอนจบ phase 1 (2026-10-12). เอกสารแผนเดิม: `Docs/phase1-minimal-loop/` (requirement / design / decisions / tech*.md)

## เปิดเมื่อไหร่ (แยกโหมด)
`Game.Loop` = `InSession && run != null && Net.Mode == Single` — เกิดเฉพาะ **Single Player จากเมนู** (`Menu.StartSingle` → `StartSession(true)`).
`-single` / `-selftest` / Host / Join = v1 เดิมทั้งหมด (ไม่เรียก loop). เทสต์เร็ว: `-single -loop`.
ทุกจุดที่แก้ในไฟล์เดิมอยู่หลัง `Loop`; ไม่มีข้อความเครือข่ายใหม่ (`Net.Protocol` ยัง 2).

## ไฟล์
| ไฟล์ | หน้าที่ |
| --- | --- |
| `Loadout.cs` | `Item` (kind/id/plus), `ItemBag` (3 กลุ่ม Weapons/Foods/Supplies), `Loadout` (stash 12/8/16, carry 2/3/4, `heldSlot`, `inRun`), `RunResult`. กฎเต็ม/ย้าย/รวม +N, `Survive()` / `Die(abandoned)` / `EnsureIron()` / `AutoPack()` |
| `LoopSave.cs` | JSON ไฟล์เดียว `persistentDataPath/medieval-knight-save.json` (เปลี่ยนด้วย `-save <ไฟล์>`), เขียน `.tmp` → `.bak` → ไฟล์จริง, ไฟล์เสีย → `.bak` หรือ `.corrupt` + เริ่มใหม่, validate id/plus |
| `RunLoop.cs` | state `Base → Prepare → World → Summary`, เข้าโลก (fade 0.5 วิ), เก็บของเข้า carry, กิน/ทิ้ง, ตาย, บอส + portal, VICTORY / YOU FELL / ABANDONED |
| `LoopUi.cs` | canvas ของตัวเอง: PREPARE, หน้าต่าง Carry (Tab ในโลก), หน้าสรุป, fade, objective, แถบอาวุธซ้ายล่าง, ป้าย RETURN PORTAL |
| `Portal.cs` | วงแหวนฟ้า + ลำแสง + วงโหลด 24 ชิ้น (`Radius` 2.0 ม., `HoldSeconds` 1.5) |

## กติกา
- เริ่มครั้งแรก: stash มี Iron Sword เล่มเดียว (ใส่ HELD ให้ผ่าน `AutoPack`). ไม่มีอาวุธเหลือเลย → ได้ Iron +0 ฟรี (`Iron Sword restored`).
- ใส่ของ: Weapon ชนิดเดียวกัน**รวมกัน** `plus = min(5, a + b + 1)` (เก็บซ้ำที่ +5 = EXP 15 ในโลก); Weapon → ช่อง Weapons ก่อนแล้ว Supplies; Food → ช่อง Food ก่อนแล้ว Supplies; เต็มทั้งคู่ = ไม่เก็บ (`Pack full! Can't pick up <ชื่อ>`, ของยังอยู่บนพื้น).
- ในโลก: เลเวล / EXP / การ์ด / Soul เริ่มใหม่ทุกรอบ (`Knight.ResetForRun`), ถืออาวุธ HELD (`Knight.SetWeapon`, `Inventory.SetSingle`). Minotaur ฆ่าก่อนถึงกลับได้ ไม่ respawn. ฆ่าแล้ว portal เกิดที่จุดตาย + แบนเนอร์ 4 วิ + ลูกศรชี้ portal. ยืนใน portal 1.5 วิ = VICTORY (ออกก่อน = นับใหม่).
- ตาย = จบรอบทันที (ไม่ revive, ไม่มีหน้า YOU DIED เดิม): dim 1.5 วิ แล้ว YOU FELL (LOST / SAFE IN STASH). ตายหลังบอสล้มแต่ก่อนเข้า portal = YOU FELL.
- รอด: ของใน carry ทั้งหมดเข้า stash (+N ติด, NEW = อาวุธชนิดที่ยังไม่เคยมี). stash เต็ม = `LEFT BEHIND (stash full)` หายถาวร.
- เซฟ: ตอน ENTER WORLD (`inRun = true`), ตอนจบรอบ (ก่อนแสดงสรุป), ตอน RETURN TO BASE, ตอนปิด PREPARE ถ้ามีการย้ายของ. ปิดเกม / Esc Esc กลางโลก = ไม่แตะเซฟ → ครั้งหน้าเปิด Single Player เจอ `inRun` → **ABANDONED** (ของที่พก = LOST).
- F5 ปิดใน loop (`RequestReset` ปฏิเสธ). Soul ซ่อนจาก UI (ยังนับในโค้ด). `H` ที่ฐาน: ตายแล้วลุกที่ฐาน ไม่เสียอะไร.

## ปุ่ม (เฉพาะ loop; ที่เหลือเหมือน controls.md)
| ปุ่ม | ผล |
| --- | --- |
| Tab | ฐาน: เปิด/ปิด PREPARE; โลก: หน้าต่าง Carry (เกมหยุด) |
| PREPARE | คลิก/Enter/Space ย้ายของ stash ↔ carry, คลิกขวา Weapon ใน carry = HELD, ลูกศร/WASD ย้ายเคอร์เซอร์, Q/E สลับฝั่ง, Esc = ถอย |
| ENTER WORLD | ไม่มี Weapon → `Pack a Weapon first`; ไม่มีอาหาร → ถาม `[Enter] Go [Esc] Back` |
| 1 / 2 (ในโลก) | ถือ Weapon ช่อง 1 / 2 (ตอนมีการ์ดเลเวลอัปเปิด 1-3 เลือกการ์ดเหมือนเดิม) |
| C | กินอาหารช่องแรก (HP เต็ม = `HP full` ไม่กิน) |
| Carry (Tab): คลิก | Weapon = ถือ, Food = กิน, Weapon ใน Supplies = ย้ายเข้าช่อง Weapons ที่ว่าง |
| Carry (Tab): คลิกขวา | Drop ลงพื้น (เก็บคืนได้เมื่อเดินพ้นรัศมีแล้วกลับมา; ห้ามทิ้ง Weapon ชิ้นสุดท้ายในช่อง Weapons) |
| Esc ในโลก | ครั้งแรก `Leaving now counts as a death. Press Esc again.` ครั้งที่สอง = ออกเมนู (ไม่แตะเซฟ) |

## ทดสอบ
- selftest เต็ม (~4.5 นาที): เคส loop อยู่ท้าย `SelfTest.cs` (`LoopTests`, ข้อมูล / เซฟ / ฐาน / PREPARE / เข้าโลก / เก็บ-กิน-ทิ้ง / ตาย / บอส-portal) ใช้ไฟล์เซฟของเทสต์เอง (`<โฟลเดอร์ selftest>\save_loop.json`) ไม่แตะเซฟจริง
- เฉพาะเคส loop (~100 วิ): เพิ่ม `-looponly` ต่อท้ายคำสั่ง selftest
- รันที่ scripted (`-selftest` / `-mptest` / `-menutest`) โดยไม่มี `-save`: `LoopSave.Disabled` = เซฟอยู่ในหน่วยความจำเท่านั้น
- `-menutest` ส่วน Single Player ต้อง Esc ปิด PREPARE ก่อนเดิน (แก้แล้วใน `MpTest.cs`); เช็ค "Start Hosting ... Enemy.All.Count >= 5" ตกอยู่ก่อนแล้ว (เมืองเป็นเขตปลอดภัยตั้งแต่ big-map-update F) ไม่เกี่ยวกับ loop

## ข้อควรระวังเมื่อต่อยอด (phase 2+)
- ข้อมูลโลก (โลกเก่า / ของที่ตก / อายุ) ยังไม่มี: `Loadout.Die` คืน `RunResult.lost` ไว้ให้ phase 2 ใช้เป็นของที่ตก
- ช่องคลังฐานเป็นค่าคงที่ `Loadout.StashCaps` / `CarryCaps`
- ฐานคือลานกลาง `MapLayout.BasePoint` (0,0,0) ไม่มี actor; ห้ามวางของกีดขวางในลานกลาง (ใช้เป็นสนามเทสต์)
