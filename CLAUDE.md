# Medieval Knight

เกม action RPG 3D มุมมอง top-down (กล้องเอียง 56° ไม่หมุน เหนืออยู่ด้านบนจอ) อัศวินดาบ+โล่ ผู้กล้าออกจากฐานไปช่วยโลกที่ถูกยึด เข้าโลกสุ่ม เริ่มจากศูนย์ทุกครั้ง ต้องโค่นบอสโลกถึงกลับได้ รอดได้อาวุธกลับ ตายเสียของที่พกไว้ในโลกนั้น
เล่นคนเดียวหรือ online co-op สูงสุด 4 คน แมพโลกละ 380 x 380 ม.
Unity `6000.6.5f1` (URP 3D, Input System ใหม่, uGUI) เขียนด้วยโค้ดล้วน: ฉาก โมเดล เอฟเฟกต์ UI สร้างตอนรันไทม์ ไม่มี prefab ไม่มีไฟล์โมเดล ข้อความในเกมเป็นภาษาอังกฤษ
ขอโดย test_disk (Discord 449209885138223116) — เวอร์ชัน 2.5D เดิมอยู่ที่ commit `e8b39dd`

## กรอบของเกม: อ่าน `Docs/vision.md` ก่อนรับงานทุกครั้ง (ไฟล์นี้ชนะเสมอถ้าขัดกับเอกสารอื่น)

- core loop (พีทกำหนด 2026-10-12): ฐาน → เลือกโลก (ใหม่/เก่า) → สู้ (เลเวล/สเตตัส/การ์ดเริ่มใหม่ทุกรอบ) → บอส biome (ไม่บังคับ) → บอสโลก → กลับฐาน
- รอด → อาวุธที่ถือกลับฐานด้วย / ตาย → ของที่พกทั้งหมดตกในโลกนั้น โลกกลายเป็น "โลกเก่า" ยากกว่าเดิม ไปเก็บคืนได้ / โลกเก่าหมดอายุเมื่อกลับฐานสำเร็จครบ 3 ครั้ง
- เสาหลัก (ฟีเจอร์ใหม่ต้องเสริมอย่างน้อย 1 ข้อ): ต่อสู้รู้สึกดี / เดิมพันจริง / ทุกรอบไม่ซ้ำ / ไปด้วยกันดีกว่า
- ไม่ทำ: บทสนทนา/เควสต์/NPC, อาวุธยิงไกลของผู้เล่น, crafting/ฟาร์ม/สร้างฐาน, class ใหม่, ร้านค้า/เศรษฐกิจ, โลกใหญ่กว่า 380x380 ม., เซิร์ฟเวอร์กลาง
- งานที่ไม่อยู่ใน phase ปัจจุบัน → ลง `Docs/backlog.md` ไม่ลงมือ; ปรับของเดิมก่อนสร้างระบบใหม่
- คำศัพท์: เรียกรวมว่า **"อาวุธ"** (ไม่ใช้ "ดาบ" เป็นคำรวม) โค้ดใหม่ใช้ `Weapon` ไม่ใช้ `Sword` (ของเดิมค่อยๆ เปลี่ยนเมื่อแก้ไฟล์นั้น)
- phase: ~~0~~ ~~1 loop ขั้นต่ำคนเดียว~~ (เสร็จ) → 2 โลกเก่า → 3 biome เพิ่ม + เกรดอาวุธ Rust→God → 4 co-op ของ loop ใหม่ → 5 เนื้อเรื่อง/ขัดเกลา ทำทีละ phase ต้องเล่นได้จริงก่อนไปต่อ
- ระหว่าง phase 1–3 ห้ามทำให้ออนไลน์เดิมพัง (mptest ต้องผ่าน); หัวข้อ "รอพีทตัดสิน" ใน vision.md ต้องตอบก่อนเริ่ม phase ที่เกี่ยวข้อง

## สถานะโค้ดตอนนี้ (phase 1 เสร็จ 2026-10-12)

**Single Player จากเมนู = loop phase 1** (`Game.Loop`): ฐาน → PREPARE (stash 12/8/16 ↔ carry 2/3/4) → โลกเดียว เลเวล 1 ใหม่ → ฆ่า Minotaur → portal 1.5 วิ = VICTORY (ของเข้า stash) / ตาย = ของที่พกหาย (YOU FELL) / ปิดเกมกลางโลก = ABANDONED; เซฟ JSON ในเครื่อง. ยังไม่มี: โลกเก่า, biome เพิ่ม, เกรดอาวุธ, co-op ของ loop

Host / Join / `-single` / `-selftest` = สนาม v1 เดิม ห้ามให้ loop รั่วเข้าไป (ทุกจุดแก้อยู่หลัง `Game.Loop`)

## รายละเอียดอยู่ใน Docs\ (อ่านเฉพาะเรื่องที่เกี่ยวกับงาน)

| ไฟล์ | เนื้อหา |
| --- | --- |
| `Docs/vision.md` | **กรอบเกม core loop เสาหลัก ไม่ทำ ลำดับ phase เรื่องรอตัดสิน** |
| `Docs/backlog.md` | ไอเดียที่ยังไม่ทำ (นอก phase / รอคิว) |
| `Docs/reference/files.md` | แต่ละสคริปต์ทำอะไร (ตารางโครงสร้างไฟล์ทั้งหมด) |
| `Docs/reference/loop.md` | **loop phase 1: ไฟล์ กติกา ปุ่ม เซฟ วิธีเทสต์** |
| `Docs/reference/controls.md` | ปุ่มควบคุมทั้งหมด |
| `Docs/reference/map-and-spawning.md` | โซนแมพ collision การเกิดของแพ็คสไลม์ |
| `Docs/reference/progression.md` | ตาราง EXP เลเวลอัป stamina แต้มสเตตัส Soul อาหาร |
| `Docs/reference/weapons-and-tab.md` | ตารางอาวุธ สูตรดาเมจ ดรอป หน้าต่าง Tab |
| `Docs/reference/sound.md` | เสียง 6 คลิปและจังหวะที่เล่น |
| `Docs/reference/online-coop.md` | Net / protocol / authority / กฎ multiplayer / command line / mptest |
| `Docs/reference/dev-guide.md` | วิธีเพิ่ม/ปรับของ (ท่าโจมตี อาวุธ เสียง ศัตรู แมพ) และขั้นตอนทดสอบเต็ม |
| `Docs/big-map-update/`, `Docs/phase1-minimal-loop/` | งานใหญ่ที่เสร็จแล้ว (แมพใหญ่ = biome แรก; loop phase 1) |
| `Docs/ref/`, `Docs/style.md`, `_reference/` | ภาพอ้างอิงและสไตล์ (ไม่ได้ใช้ในเกมแล้ว) |

## สิ่งที่ต้องรู้ก่อนแก้โค้ด

- โค้ดอยู่ใน `Assets/MedievalKnight/Scripts/` (`Game.cs` ศูนย์กลาง, `Knight.cs` ผู้เล่น, `World.cs` แมพ, `Progress.cs` เลเวล, `Weapon.cs` อาวุธ, `Net.cs` ออนไลน์) — ดูรายละเอียดใน `Docs/reference/files.md`
- ตัวเลขบาลานซ์อยู่ในค่าคงที่ของโค้ด (`Progress.cs`, `Knight.Attacks` / `Knight.Skills`, `Weapons.All`, `Game.EnemyHpScale` ฯลฯ) ไม่ต้องสร้างไฟล์ config
- ฉากสร้างด้วย `StaticBatcher` และ `System.Random(11)` เท่านั้น (ห้าม `UnityEngine.Random`) เพื่อให้ collision ตรงกันทุกเครื่อง; ห้ามวางของกีดขวางในลานกลาง (|x|<28, Z -42..32) เพราะใช้เป็นสนามเทสต์
- co-op: ห้ามถือว่า `Game.I.knight` คือผู้เล่นคนเดียว (ใช้ `Game.I.players` / `NearestKnight`); ทุกอย่างที่เปลี่ยนโลกต้องอยู่หลัง `Net.Authority`; ห้ามหยุดเวลาตอนออนไลน์ (`Game.Paused`); ข้อมูลโลกเก็บที่ host เท่านั้น
- เปลี่ยนรูปแบบข้อความเครือข่ายเมื่อไหร่ ต้องเพิ่ม `Net.Protocol` (ตอนนี้ = 2)
- ปุ่มใหม่ต้องเพิ่มใน `InputX.Map()`; เสียงใหม่เพิ่มใน enum `Snd` + ตารางใน `Sfx.cs` (ลำดับต้องตรงกัน)
- สีใช้ `Prims.Lit(...)` / `Prims.Glow(...)` ไม่ต้องสร้างไฟล์ material

## Build และทดสอบ

- build: `build.bat` → `Builds\MedievalKnight.exe` (log ที่ `build.log`)
- selftest (~3 นาที ต้องไม่มี `FAIL`): `Builds\MedievalKnight.exe -selftest selftest -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile player.log`
- ออนไลน์ (host + client 3 + ตัวที่ต้องถูกปฏิเสธ 2 ~70 วินาที): `powershell -ExecutionPolicy Bypass -File mptest.ps1`
- เช็ค "A's walk looks smooth on B" (แก้แล้ว 2026-10-12 ด้วย `NetInterp` window ~2 วินาที) ถ้าตกอีกแปลว่าบั๊กจริง
- loop: เคสอยู่ท้าย selftest; เร็ว ~100 วิ ต่อท้าย `-looponly`; `-menutest` เช็ค host `Enemy.All.Count >= 2` (ลดจาก 5 เมื่อ 2026-10-13 เพราะเมืองเป็นเขตปลอดภัย ตอนเริ่มเหลือแค่หุ่น + Minotaur)
- ขั้นตอนเต็ม: `Docs/reference/dev-guide.md`, `online-coop.md`
