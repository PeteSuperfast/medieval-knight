# Phase 4 Tech — co-op ของ loop
คู่กับ requirement/design/decisions | เครือข่าย flow mptest อยู่ `tech-net.md`

## 1. แนวทาง
- ต่อยอด `RunLoop`: Host/Join เข้า loop. `Game.Loop` = session มี `run` (ทุกโหมด), `Game.Coop` = `Loop && Net.Multiplayer`. logic ทีมแยกเป็น `TeamRun` (host ตัดสิน)
- host ถือโลกเก่า/กอง/ตัวนับ + "mirror" carry ทุกคน (client ส่ง `Pack` เมื่อเปลี่ยน) ไว้สร้างกองตอน wipe/หลุด; client ถือ stash/carry/เซฟตัวเอง ออนไลน์ไม่แตะ `worlds` ของตัวเอง
- Knight ของคนอื่นมีเฉพาะ "ผู้เข้าโลกรอบนั้น" (สร้างตอน Enter ทิ้งตอนจบ) -> `players` = ผู้เข้าร่วม: scaling / wipe / packs / EXP ของ v1 ใช้ได้ตรงๆ
- กอง: `OldWorld.piles` หลายกอง ไม่ย้าย รวมถ้า <= 3 ม. cap 48/โลก ใช้ทั้ง SP และทีม

## 2. decisions ข้อ 4: ฐาน = โลก พื้นที่เดียว ไม่ต้องรื้อ scene
แมพสร้างครั้งเดียวใน `Awake`; ฐาน = `BasePoint` ไม่มี actors, `SimulateWorld` ข้ามเมื่อ `!InWorld`; โลก = `SpawnActors` ตอน `StartWorld`. คนรอที่ฐาน "ไม่เห็นทีม": host ไม่ส่ง Snapshot ให้เขา + ไม่สร้าง Knight ให้เขา + เขา `ClearWorld` แล้ว

## 3. ไฟล์
| ไฟล์ | | หน้าที่ |
|---|---|---|
| `TeamRun.cs` | ใหม่ | roster, mirror, Ready/Enter, ล้ม-ชุบ-ตาย, wipe, portal 15 วิ, Take, RunEnd |
| `TeamNet.cs` | ใหม่ | encode/decode ข้อความใหม่ (ไม่ยัด Net.cs) |
| `TeamUi.cs` | ใหม่ | R1 R4 K1 S1 T1 X1 |
| `MpLoopTest.cs` | ใหม่ | mptest ชุด loop |
| `OldWorld.cs` | แก้ | `Pile`, `piles`, `AddDrop` |
| `Loadout.cs` | แก้ | `Die` -> `Fall(drops)`, `LoseCarry`, `Survive(decay)`, `runCoop`, Sanitize ต่อกอง |
| `LoopSave.cs` | แก้ | DTO `pv`/`piles`/`rc` (Version คง 1) |
| `RunLoop.cs` | แก้ | หลายกอง, เส้นทาง Coop เรียก `TeamRun` |
| `LoopUi.cs` | แก้ | R2, R3 `n piles`, LOOT ต่อกอง, ลูกศรกองใกล้สุด, G3 |
| `Game.cs` | แก้ | Loop/Coop, join/left, snapshot gating, pickup ของ remote, wipe, กล้อง spectate, กฎ F |
| `Knight.cs` | แก้ | `Fallen`, `DownLeft`, `NetFallen`, `revTarget`, DownRoutine Coop |
| `Net.cs` | แก้ | Protocol 3, Msg ใหม่, Welcome mode, WriteKnight, gating, helper internal |
| `Menu.cs` `Args.cs` `Hud.cs` | แก้เล็ก | Host/Join -> loop; `-arena`; ซ่อน party rows ตอน Coop |
| `MpTest.cs` `mptest.ps1` `SelfTest.cs` | แก้ | `-arena`, `-menutest`, เคสกองเดิม ~113 จุด + เคสใหม่ |

## 4. คลาส
- `Pile {id,x,z,items}`; `OldWorld.AddDrop(x,z,items,out overflow)`; `id` รันต่อโลก ไม่เซฟ
- `Loadout.Fall(drops, abandoned, now)`: `Die` เดิม = drop เดียว; host wipe = drop ต่อคน. client: `LoseCarry()` (ไม่สร้างโลก ไม่ลดตัวนับ), `Survive(decay:false)`
- `TeamRun` (null ใน SP): `roster[pid] {name,status,ready,mirror,lastPos}`, `homeAt`; host: `OnPack OnRevive CheckWipe OnReached OnTake OnLeft`; client: `OnTeam OnEnter OnPiles OnRunEnd`
- `Knight`: Alive/Down/Fallen; `Dead` ยัง true ทั้ง Down และ Fallen -> NearestKnight/EXP/ศัตรูเดิมถูกเอง

## 5. เซฟ
- JSON เดิม Version 1: `ow[]` เพิ่ม `pv:2` + `piles[{x,z,items}]` (เขียน x/z/pile กองแรกด้วย เผื่อ rollback); `pv` หาย = เซฟเก่า -> กองเดียวที่ (x,z). เพิ่ม `rc` (run ออนไลน์ของ client)
- `Begin`: `inRun && rc` -> `LoseCarry` + แจ้ง "online run abandoned"; host/SP ใช้ `Die(true)` เดิม
- เซฟเมื่อ: Ready เปลี่ยน, เก็บ/กิน/ทิ้ง, take, Enter, จบรอบ. ไม่เก็บ: roster, mirror, ID กอง, ตัวนับ 60/15 วิ

## 6. ลำดับ (build ผ่าน + commit แยก; ★ selftest ◆ mptest)
1. `Args.arena` + `mptest.ps1 -arena` + แยก `Loop`/`Coop` (พฤติกรรมเท่าเดิม) ★◆
2. `Pile` + `Fall` + `LoopSave` (เซฟเก่าเปิดได้) + แก้เคสกอง SP ★
3. RunLoop/LoopUi หลายกอง (LootPile ต่อกอง, `n piles`) SP ★
4. Net: Protocol 3 + Msg/Welcome/WriteKnight/PickupTaken.plus + โครง `TeamNet` ◆(-arena)
5. Host/Join เข้า loop: roster, READY, `Enter`, Knight เฉพาะผู้เข้าโลก, gating, R1 R2 R4 (ล้ม = Fallen ทันทีชั่วคราว)
6. mirror + เก็บ/กิน/ทิ้งของ client (`Pack` `Drop`)
7. portal ทีม: `Portal` `Reached` `HomeIn` `RunEnd` victory, G3, X1
8. ล้ม/ชุบ/ตาย/spectate: DownRoutine Coop, `Revive`, กฎ F, K1 S1 T1
9. wipe + หลายกองในทีม: `Piles` `Take` `TakeAck`, LOOT ในทีม, `Worlds`
10. หลุด/ABANDONED/เข้ากลางรอบ/host quit/`rc` + `-menutest` ใหม่
11. `MpLoopTest` + `mptest-loop.ps1` + selftest เต็ม + docs (loop online-coop files CLAUDE)

## 7. เสี่ยง
- `Game.Loop` เดิม = SP: ไล่ ~15 จุด (Game.cs 47/217/849/968/1080/1164/1234/1297/1312, Knight 1228, Hud 951/991/995) หลุดจุดเดียว = ค้าง
- `SummaryOpen` ตั้ง `timeScale=0` (Game.cs 1291) ต้องเลิกตอนออนไลน์
- mirror ช้ากว่า carry จริง ~100 ms: เก็บพร้อมกันอาจเต็ม -> ของหาย (log ไม่ crash)
- กฎ "กองย้าย" phase 2 ถูกแทน (ตั้งใจ) เทสต์ ~113 จุดต้องแก้
- `StopAllCoroutines` ใน `TakeDamage`/`RemoteDown`/`ResetForRun`: ระวังล้ม-ชุบซ้อน
- mptest v1 (`-arena`) ต้องผ่านทุกขั้น ห้ามเปลี่ยนความหมายข้อความเดิม นอกจากเพิ่ม field

## 8. ขนาด: **ใหญ่** (ไฟล์ใหม่ 4, แก้ ~12, protocol ใหม่, 11 ขั้น)
