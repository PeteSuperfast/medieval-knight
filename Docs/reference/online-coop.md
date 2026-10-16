# Online co-op (สูงสุด 4 คน)

> ภาพรวมสั้นอยู่ที่ CLAUDE.md; flow / ข้อความ / mptest ของ loop ออนไลน์ละเอียดที่ `Docs/phase4-coop/tech-net.md`

เมนู (`Menu.cs`): ชื่อ, **Single Player**, **Host Game**, **Join Game**, Quit (ไม่มี EventSystem: hit-test เองผ่าน `InputX`). `Net.cs` บน `System.Net.Sockets` (TCP, `NoDelay`) port **7777**; นอก LAN ต้อง forward port หรือใช้ Radmin VPN/ZeroTier.

## สองโหมด (host เป็นคนเลือก, client ตาม `Welcome.mode`)
- **loop (ค่าเริ่มต้นตั้งแต่ phase 4)**: ฐาน → READY → host เข้าโลก → ล้ม/ชุบ → บอส → กลับฐานด้วยกัน
- **arena v1** (`-arena`): สนามเดิม ไม่มีฐาน/loop — ให้ `mptest.ps1` (ชุดเทสต์ v1) เท่านั้น

## วิธีเล่น
- Host: Host Game > port > Start Hosting > บอก IP+port. Client: Join Game > IP+port > Connect (ต่อไม่ได้/เต็ม/เวอร์ชันไม่ตรง = กลับหน้า Join พร้อมข้อความ). Host ปิด = client กลับเมนู
- **ทุกคนต้องเวอร์ชันเดียวกัน** (`Net.Protocol` = **6**; p6 (knight-rig-slash): `Act.kind` 0..2 = ท่าฟัน (target.x=1 = ต่อจากท่าก่อน), 20 = กลิ้ง, 21 = เริ่มกิน, 22 = ท่าปัดโล่; `Hit.src` 0..2 / 37 / 38 (อื่นๆ ถูกทิ้ง); `NetFlags` 64 = กำลังกิน (บิต 32 เดิม = ชาร์จ ห้ามนำกลับมาใช้); kind 10..13, 30..36 ถูกถอด; สนาม v1: host ให้อาหารบนพื้นแก่ client เมื่อ flag กินค้าง ≥1.7 วิ ผ่าน `PickupTaken` เดิม ไม่มีข้อความใหม่; อดีต p7: `KnightHit` + attackerNetId/flags(1=หนัก), `Hit.src` 37 parry / 38 clash, `EnemyHit` flags 8 หนัก / 16 parry / 32 clash; p6: Act.kind/Hit.src 30..36, NetFlags 32 = ชาร์จ). parry/clash ตัดสินที่เครื่องเจ้าของตัว (client หน่วง `KnightHit` 0.1 วิ) แล้วรายงาน host; แผนที่ M ไม่ส่งข้อความ

## สถาปัตยกรรม (host-authoritative)
- `Net.Mode` Single / Host / Client; `Net.Authority` = ไม่ใช่ Client. Host จำลองโลกทั้งหมด; client จำลองเฉพาะอัศวินตัวเอง; ฉากสร้างเองทุกเครื่องจาก seed 11
- `Conn` = 1 TCP ต่อ client (thread อ่าน/เขียน); ประมวลผลใน `Net.Poll()` ส่งใน `Net.LateTick()`
- เฟรม `[len u16][type u8][payload]` little endian; ตำแหน่ง cm (i16)
- Client ส่ง `State` 20/วิ; host ส่ง `Snapshot` 15/วิ **เฉพาะคนที่อยู่ในรอบ** (loop); puppet วาดย้อนหลังด้วย `NetInterp`
- `Game.players` = อัศวินในโลก (loop: เฉพาะผู้เข้าร่วมรอบ + ของเครื่องนี้); `Game.knight` = ของเครื่องนี้; `Knight.pid`, สีตาม pid

## ข้อความ (`enum Msg`)
| ทิศทาง | ข้อความ |
| --- | --- |
| client > host | `Hello`, `State` (+`revTarget`), `Act`, `Hit`, `Ping`, `Bye`, `Test`; **loop**: `Pack` (ready + carry + grants ที่รับแล้ว), `Drop`, `Take`, `Revive`, `Reached` |
| host > client | `Welcome` (pid, version, **mode**, รายชื่อ), `Refuse`, `Join`, `Leave`, `Snapshot`, `Act`, `EnemyHit`, `KnightHit`, `Exp`, `PickupTaken` (+`plus`), `Banner`, `Reset`, `Pong`, `Bye`, `Test`; **loop**: `Team`, `Worlds`, `Enter`, `Piles`, `TakeAck`, `Portal`, `HomeIn`, `RunEnd`, `Revive` (relay) |

`TeamNet.cs` เข้ารหัส/ถอดรหัสข้อความ loop; `TeamRun.cs` ตัดสิน; เปลี่ยนรูปแบบข้อความต้องเพิ่ม `Net.Protocol`

## Authority / trust
- Host ตัดสิน HP ศัตรู, การตาย, EXP, Soul, ดรอป, ใครเก็บของ, spawn, F5, **กอง/โลกเก่า/ตัวนับ**
- Client ถูกเชื่อเรื่องอัศวินตัวเอง; host ตรวจหลวมๆ (ดาเมจ 0..20000, ระยะ, ≤150 hit/วิ, เงียบ 10 วิ = ตัด) ไม่กันโกง/ไม่เข้ารหัส
- loop: client เป็นเจ้าของ carry/stash/เซฟตัวเอง; host เก็บ **mirror** ของ carry ทุกคน (`Pack`) ไว้สร้างกองตอน wipe / หลุด / host ปิด. ของที่ host ให้ (เก็บจากพื้น/Take จากกอง) = "grant" — `Pack` ที่ยังไม่รับ grant ล่าสุดถูกข้าม (mirror ไม่ย้อน ของไม่หาย)

## กฎ loop ออนไลน์ (ย่อ; ละเอียด `tech-net.md`)
- **ห้อง**: ทุกคนมีฐาน+PREPARE ของตัวเอง (คลัง/เซฟเครื่องตัวเอง). READY ต้องมี Weapon, ล็อก carry. host กด NEW WORLD / OLD WORLDS ได้เมื่อพร้อมหรือบังคับ (`<ชื่อ> is not ready. Start without waiting?`; คนไม่พร้อมรอที่ฐาน). คนเข้ากลางรอบ = ฐาน + `Run in progress - you'll join the next one`; คนที่ฐานไม่เห็นทีม
- **ในโลก**: เลเวล 1 ใหม่ทุกคน ข้างกัน; ศัตรู HP ×(1+0.6/คนเพิ่ม) (และ ×1.5 โลกเก่า); EXP เต็มต่อคนที่ยังไม่ล้ม; ไม่มีการหยุดเกม (Tab / LOOT เป็น overlay, LOOT ให้โล่ 6 วิต่อการเปิด)
- **ล้ม / ชุบ**: HP 0 = Down 60 วิ (ขยับ/ตีไม่ได้). เพื่อน ≤ 3 ม. ค้าง **F** 2 วิ (ถูกตี = รีเซ็ต) → ลุก HP 30% + โล่ 3 วิ (host ตรวจระยะ 3.5 ม. + เป้า Down จริง); ไม่มีเพื่อนล้มใกล้ F = Ground Slam. หมดเวลา = Fallen (`NetFallen`): ดูเพื่อน (Q/E)
- **wipe**: ทุกคนในรอบ Down/Fallen พร้อมกัน (ยังไม่เริ่มนับกลับ) = จบรอบ: ของแต่ละคน (mirror) เป็นกองที่ตำแหน่งสุดท้ายของเขา (≤3 ม. รวมกอง) ในโลกเก่าเดียว (ใหม่/ที่เล่น); ตัวนับอื่น −1; `RunEnd` → client `LoseCarry` + G3 `ALL FELL`
- **ชนะ**: Minotaur ตาย (host) → `Portal`; ใครรอดยืนครบ 1.5 วิ → `Reached` → นับ 15 วิ (`HomeIn`; ชุบได้) → ทุกคนที่ยังอยู่กลับพร้อมกัน: host `Survive()` ครั้งเดียว (ตัวนับ −1 ต่อทีม), client `Survive(decay:false)`; ไม่เกิดโลกเก่า; G3 `VICTORY - TEAM RETURNS HOME`
- **เก็บของ**: host เก็บเข้า mirror แล้ว `PickupTaken`; เต็ม = ของอยู่พื้น. Take จากกอง: host ตรวจ index+ของ+mirror ลบแล้ว `TakeAck`+`Piles`; แย่งชิ้นเดียว = คนแรกได้ อีกคนเห็น `Taken by X`
- **หลุด / ปิดเกม**: client ปิดกลางรอบ = LEFT (ช่องว่างให้คนใหม่; ของใน mirror ยังนับตอนจบ: wipe = กองที่จุดที่หลุด, ชนะ = หายถาวร); `rc` ในเซฟ client = เปิดใหม่แล้ว `LoseCarry`. **host ปิดกลางรอบ**: host สร้างกองให้**ทุกคน**จาก mirror (ABANDONED ทั้งทีม) client กลับเมนู ของ client ไม่เสียเพราะ host ปิด
- โลกเก่าของ client จาก SP ไม่ถูกนับตอนออนไลน์; client ดูโลกเก่าของ host (`Worlds`) อ่านอย่างเดียว

## Command line (`Args.cs`)
`-single`, `-host` / `-join <ip>` + `-port` `-name`, `-bind <ip>`, `-arena`, `-netversion <n>`, `-joindelay <วิ>`, `-selftest <โฟลเดอร์>` (`-looponly` `-slashonly` `-p7only`), `-mptest <โฟลเดอร์> -role H|A|B|C|D -expect <n> [-refusals] [-expecttext <ข้อความ>]`, `-menutest <โฟลเดอร์>`, `-save <ไฟล์>`.

## ทดสอบหลายโปรเซส (127.0.0.1)
- `mptest.ps1` (ใส่ `-arena` ให้เอง): ชุด v1 host + 3 client + 2 ตัวที่ถูกปฏิเสธ ~100 วิ. เช็คผันผวนตาม CPU (`smooth on B/H`, `same place on every screen`) ตกแล้วรันซ้ำ; เคส p7 (3c) ต้อง `ParkSlimes` ก่อน (สไลม์จริงตีในหน้าต่าง parry ทำให้นับเพี้ยน)
- `mptest-loop.ps1` (`MpLoopTest.cs`) ~4.5 นาที: host + A B C + X V (ปฏิเสธ) + D (เข้ากลางรอบ) ครอบคลุมห้อง/READY, mirror, ชนะ, ล้ม-ชุบ, wipe, เก็บคืน, หลุด, host ปิด. hook เวลา: `Game.TestDownSeconds` 60→6, `TeamRun.TestHomeSeconds` 15→3. `MK_EXE` = ชี้เวอร์ชัน exe
- `-menutest`: `Builds\MedievalKnight.exe -menutest <โฟลเดอร์เต็ม> -port 47821 -bind 127.0.0.1 -screen-fullscreen 0 -logFile <log>` ดูบรรทัด `MENUTEST`
