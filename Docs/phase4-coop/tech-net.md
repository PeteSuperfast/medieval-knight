# Phase 4 Tech — เครือข่าย / flow / mptest
คู่กับ `tech.md` | `Net.Protocol` 2 -> **3**

## 1. ของเดิมที่เปลี่ยนรูป
- `Welcome`: เพิ่ม `mode u8` (0 arena / 1 loop) หลัง version; รายชื่อ = roster (รวมคนรอ). client ใช้ mode ของ host (host `-arena` -> client เล่น v1)
- `WriteKnight` (State + Snapshot): เพิ่ม `revTarget u8` (255 = ไม่ได้ชุบใคร); flags เพิ่ม `NetFallen = 16` (Dead + Fallen = ตายจริง)
- `PickupTaken`: เพิ่ม `plus u8`
- Snapshot คงเดิม แต่ host ส่งเฉพาะ conn ที่ `inWorld`; client ไม่ส่ง State ถ้าไม่ได้อยู่ในโลก (ยังส่ง Ping)

## 2. ข้อความใหม่ (ต่อท้าย `Msg` หลัง `Test`)
| ทิศ | ข้อความ | payload / เมื่อไหร่ |
|---|---|---|
| C>H | `Pack` | ready u8 + carry 9 ช่อง (kind,id,plus) — กด READY, carry เปลี่ยน (<= 4/วิ) |
| C>H | `Drop` | item + x,z — ทิ้งของ (host สร้าง Pickup `dropped`) |
| C>H | `Take` | pileId, n, n x (index,item) — เก็บจากกอง / Take all |
| C>H | `Revive` | target, phase 0 หยุด / 1 ค้าง / 2 ครบ 2 วิ |
| C>H | `Reached` | ยืนใน portal ครบ 1.5 วิ |
| H>C | `Team` | roster (pid,name,status,ready) + runLive + worldCode — เมื่อเปลี่ยน |
| H>C | `Worlds` | รายการโลก (seed,left,t,piles,items) — ตอน join / จบรอบ (client อ่านอย่างเดียว) |
| H>C | `Enter` | worldSeed, left, participants[] — ไม่อยู่ใน list = รอรอบหน้า |
| H>C | `Piles` | byPid, n x (id,x,z,items) — ทุกครั้งที่กองเปลี่ยน |
| H>C | `TakeAck` | ok + items — ตอบ `Take` (ok=0 = ถูกเก็บไปก่อน) |
| H>C | `Portal` | x,z — Minotaur ตาย |
| H>C | `HomeIn` | วินาที — คนรอดครบ portal แล้ว (15) |
| H>C | `RunEnd` | win/wipe, ตารางต่อ pid (status,items), worldNote, left ก่อน/หลัง, piles, items, crumbs[] |
| H>C | `Revive` | (relay) reviver,target,phase: 0/1 = แสดง "X is reviving you", 2 = เฉพาะเป้าหมาย ลุกเลย |

ขนาด: Piles <= ~250 B, Pack 30 B, RunEnd <= ~200 B. Trust เหมือนเดิม: client เป็นเจ้าของ carry/HP ตัวเอง host ตรวจหลวมๆ (ระยะชุบ <= 3.5 ม., เป้า Down จริง, ผู้ชุบยังไม่ Dead, `Take` ต้องตรง index+item)

## 3. Flow
- **Join**: Hello -> `PidFree` (roster) -> Welcome(mode) -> `StartSession(loop)` -> `Begin` -> `Team`/`Worlds`. รอบกำลังเล่น = client เห็น R4 ไม่ได้ Knight/Snapshot
- **READY**: ต้องมี Weapon; ล็อก carry; `Pack` -> host mirror -> `Team`. host ENTER: มีคนไม่ READY = ถามก่อน (START ได้ คนนั้นรอรอบนี้)
- **Enter**: host `BeginRun(seed)`+Save -> `Enter` -> ทุกคน fade 0.5. client: `BeginRun(coop)` (+`rc`), `SpawnKnight` ต่อผู้เข้าร่วม, `ClearWorld`, ยืนที่ `SpawnPointFor(pid)`. host: ตัวคูณโลก, `SpawnActors`, `Piles`
- **เก็บของ**: host `UpdatePickups`: k remote -> `mirror.CanAdd` -> add -> `PickupTaken(plus)` -> client เพิ่มเข้า carry + Save. กิน/ทิ้ง/รวม -> `Pack`
- **Take กอง**: client ส่ง `Take` (เฉพาะที่ CanAdd) -> host ลบถ้าตรง -> `TakeAck` + `Piles` ทุกคน (มี byPid ไว้ขึ้น `Taken by X`); ระหว่างรอ ack ปิดการเก็บของโลก
- **ล้ม**: เจ้าของตัวเอง: hp 0 -> Down, `DownLeft` 60 -> 0 = Fallen (flag `NetFallen`). คนอื่นนับจากตอนเห็น Dead. ไม่ขยับ/ไม่ตี (Dead เดิม)
- **ชุบ**: ผู้ชุบ ค้าง F ใกล้ Down <= 3 ม. (F ไม่ยิง Ground Slam) -> `Revive` 1 -> ครบ 2 วิ -> 2; ถูกตี = รีเซ็ต. host validate -> `Revive`(2) ถึงเป้าหมาย: ยัง Down? hp 30% + `Protect(3)`. ระหว่าง HomeIn ยังชุบได้
- **wipe**: host ทุกเฟรม: `players` ทั้งหมด Dead และยังไม่ HomeIn -> drops = mirror ต่อคน ที่ตำแหน่งคนนั้น (+ ของคนหลุด) -> `Loadout.Fall` (โลกใหม่/เก่า, ตัวนับอื่น -1) -> Save -> `RunEnd(wipe)`; client `LoseCarry`
- **ชนะ**: Minotaur ตายที่ host -> `Portal`; ใครยืนครบ -> `Reached` -> host `homeAt = +15` -> `HomeIn` -> หมดเวลา host `Survive()` (ตัวนับ -1 ครั้งเดียว) -> `RunEnd(win)`; client `Survive(decay:false)` ของทุกคนรวมคนตายเข้า stash
- **หลุด**: `Net.Drop` -> `OnPlayerLeft` -> `TeamRun.OnLeft`: LEFT, เก็บ mirror + ตำแหน่ง เป็น drop (ใช้ถ้า wipe; ถ้าทีมชนะ = หาย). client ที่ปิดเกม: เซฟ `rc` -> เปิดใหม่ `LoseCarry`
- **host ปิดเกม**: `Bye` -> client กลับเมนู "The host closed the game."; ของ host = `Die(true)` เดิมตอนเปิดครั้งหน้า; ของ client = หาย (`rc`)
- **ปลอดภัย**: client ที่ยังไม่เข้าโลก/ยังไม่ `Enter` ไม่เซฟ `inRun` -> เซฟไม่เสีย

## 4. mptest ชุด loop (`MpLoopTest.cs` + `mptest-loop.ps1`, H + A B C, conductor `Msg.Test` เดิม)
hook เฉพาะ Scripted: `Game.TestDownSeconds` 60 -> 6, `TeamRun.TestHomeSeconds` 15 -> 3
1. join + roster 4 คน + READY ไม่มี Weapon ถูกปฏิเสธ
2. host force start: C ไม่ READY -> A B เข้า, C รอ (C: ไม่มี Knight อื่น, `Enemy.All == 0`)
3. เข้าโลก: Lv 1 ทุกคน อยู่ข้างกัน
4. A ล้ม -> B ชุบ: HP ~30%, โล่ 3 วิ, C เห็นสถานะ
5. A ล้ม ไม่มีใครชุบ -> Fallen, spectate ได้
6. ทีมฆ่าบอส -> `HomeIn` -> ทุกคน (รวม A ตาย) stash ครบ, `worlds` ไม่เพิ่ม, ตัวนับ -1 ครั้งเดียว
7. wipe: โลกใหม่ piles = จำนวนคนที่พกของ ที่ตำแหน่งแต่ละคน; ใกล้ <= 3 ม. รวมกอง
8. เข้าโลกเก่า: B เก็บกองของ A ได้; 2 คนแย่งชิ้นเดียว -> ok 1 + `Taken by`
9. C ปิดเกมกลางโลก -> LEFT ทีมเล่นต่อ; wipe -> มีกองของ C
10. host ปิด -> client กลับเมนู เซฟ client ไม่เสีย; version ไม่ตรงยังถูกปฏิเสธ
11. ชุด v1 เดิมรันด้วย `-arena` ผ่านครบ (`mptest.ps1`)
