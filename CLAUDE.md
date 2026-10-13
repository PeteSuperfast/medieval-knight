# Medieval Knight

เกม action RPG 3D มุมมอง top-down (กล้องเอียง 56° ไม่หมุน เหนืออยู่ด้านบนจอ) อัศวินดาบ+โล่ ผู้กล้าออกจากฐานไปช่วยโลกที่ถูกยึด เข้าโลกสุ่ม เริ่มจากศูนย์ทุกครั้ง ต้องโค่นบอสโลกถึงกลับได้ รอดได้อาวุธกลับ ตายเสียของที่พกไว้ในโลกนั้น
เล่นคนเดียวหรือ online co-op ≤ 4 คน แมพโลกละ 380x380 ม.
Unity `6000.6.5f1` (URP 3D, Input System ใหม่, uGUI) เขียนด้วยโค้ดล้วน: ฉาก โมเดล เอฟเฟกต์ UI สร้างตอนรันไทม์ ไม่มี prefab/ไฟล์โมเดล ข้อความในเกมเป็นภาษาอังกฤษ
ขอโดย test_disk (Discord 449209885138223116) — 2.5D เดิมอยู่ที่ commit `e8b39dd`

## กรอบของเกม: อ่าน `Docs/vision.md` ก่อนรับงานทุกครั้ง (ไฟล์นี้ชนะเสมอถ้าขัดกัน)

- core loop (พีทกำหนด 2026-10-12): ฐาน → เลือกโลก (ใหม่/เก่า) → สู้ (เลเวล/สเตตัส/การ์ดเริ่มใหม่ทุกรอบ) → บอส biome (ไม่บังคับ) → บอสโลก → กลับฐาน
- รอด → อาวุธที่ถือกลับฐานด้วย / ตาย → ของที่พกทั้งหมดตกในโลกนั้น กลายเป็น "โลกเก่า" ยากกว่าเดิม เก็บคืนได้ / โลกเก่าหมดอายุเมื่อกลับฐานครบ 3 ครั้ง
- เสาหลัก (ฟีเจอร์ใหม่ต้องเสริมอย่างน้อย 1 ข้อ): ต่อสู้รู้สึกดี / เดิมพันจริง / ทุกรอบไม่ซ้ำ / ไปด้วยกันดีกว่า
- ไม่ทำ: บทสนทนา/เควสต์/NPC, อาวุธยิงไกลของผู้เล่น, crafting/ฟาร์ม/สร้างฐาน, class ใหม่, ร้านค้า/เศรษฐกิจ, โลกใหญ่กว่า 380x380 ม., เซิร์ฟเวอร์กลาง
- งานนอก phase ปัจจุบัน → ลง `Docs/backlog.md` ไม่ลงมือ; ปรับของเดิมก่อนสร้างระบบใหม่
- คำศัพท์: เรียกรวมว่า **"อาวุธ"** (ไม่ใช้ "ดาบ" เป็นคำรวม) โค้ดใหม่ใช้ `Weapon` ไม่ใช้ `Sword` (ของเดิมค่อยๆ เปลี่ยนเมื่อแก้ไฟล์นั้น)
- phase: ~~0~~ ~~1 loop ขั้นต่ำ~~ ~~2 โลกเก่า~~ ~~4 co-op ของ loop~~ ~~6 ท่าคอมโบ~~ ~~7 clash/parry + ของเล็ก~~; 3 biome เพิ่ม + เกรดอาวุธ Rust→God พักไว้ (พีทเอาไปประชุม), 5 เนื้อเรื่อง/ขัดเกลา พีทไม่เอา — ยังไม่มี phase ถัดไป รอพีทสั่ง
- ห้ามทำให้ออนไลน์พัง (`mptest.ps1` + `mptest-loop.ps1` ต้องผ่าน); หัวข้อ "รอพีทตัดสิน" ใน vision.md ต้องตอบก่อนเริ่ม phase ที่เกี่ยวข้อง

## สถานะโค้ดตอนนี้ (phase 4, 6, 7 เสร็จ 2026-10-14)

**Single Player / Host / Join จากเมนู = loop**: ฐาน → PREPARE (stash 12/8/16 ↔ carry 2/3/4) → NEW WORLD / OLD WORLDS → เลเวล 1 ใหม่ → ฆ่า Minotaur → portal = VICTORY (ของเข้า stash) / ตายหรือปิดเกม = ของที่พกกลายเป็นกองที่จุดนั้น = **โลกเก่า** (HP ×1.5 ดาเมจ ×1.25 แพ็ค ×1.5; สลายเมื่อกลับฐาน 3 ครั้ง; **หลายกองต่อโลก ไม่ย้าย รวมถ้า ≤ 3 ม. 48 ชิ้น/โลก**). **ออนไลน์**: ทุกคนมีฐาน/คลัง/เซฟเอง host ถือโลกเก่า+กอง+mirror ของ carry; READY → host เข้าโลก; ล้ม 60 วิ เพื่อน F ชุบ; ทุกคนล้ม = wipe (กองต่อคนที่จุดล้ม); ชนะ = นับ 15 วิ กลับพร้อมกัน ไม่เกิดโลกเก่า. ยังไม่มี: biome เพิ่ม, เกรดอาวุธ

**phase 6**: ท่าคอมโบใหม่ 7 ท่า (ทิศ WASD + คลิกที่ 1/3, หลัง dash) + ค้างซ้าย 1 วิ = Heavy Strike + หน่วง 0.5 วิ (ปุ่ม: `controls.md`; net kind 30..36)
**phase 7**: parry/clash/ท่าหนัก, เลขสีหนัก/คริ, ล้อเมาส์ = กล้อง, **M = แผนที่**, **F2 = เสียง** (`controls.md`)
`-arena` (Host / Join = สนาม v1) / `-single` / `-selftest` ไม่มี `-loop` = สนาม v1 เดิม

## รายละเอียดอยู่ใน Docs\ (อ่านเฉพาะเรื่องที่เกี่ยวกับงาน)

| ไฟล์ | เนื้อหา |
| --- | --- |
| `Docs/vision.md` | **กรอบเกม core loop เสาหลัก ไม่ทำ ลำดับ phase เรื่องรอตัดสิน** |
| `Docs/backlog.md` | ไอเดียที่ยังไม่ทำ (นอก phase / รอคิว) |
| `Docs/reference/files.md` | แต่ละสคริปต์ทำอะไร |
| `Docs/reference/loop.md` | **loop + โลกเก่า + หลายกอง: ไฟล์ กติกา ปุ่ม เซฟ เทสต์** |
| `Docs/reference/controls.md` | ปุ่มควบคุมทั้งหมด |
| `Docs/reference/map-and-spawning.md` · `progression.md` · `weapons-and-tab.md` · `sound.md` | โซนแมพ/การเกิดแพ็ค · EXP/เลเวล/อาหาร · อาวุธ/สูตรดาเมจ/Tab · เสียง |
| `Docs/reference/online-coop.md` | Net / protocol 5 / authority / กฎ loop ออนไลน์ / command line / mptest |
| `Docs/reference/dev-guide.md` | วิธีเพิ่ม/ปรับของ (ท่า อาวุธ เสียง ศัตรู แมพ)+เทสต์เต็ม |
| `Docs/big-map-update/`, `Docs/phase1-minimal-loop/`, `Docs/phase2-old-world/`, `Docs/phase4-coop/`, `Docs/phase6-combo/`, `Docs/phase7-clash-polish/` | งานที่เสร็จแล้ว |

## สิ่งที่ต้องรู้ก่อนแก้โค้ด

- โค้ดอยู่ใน `Assets/MedievalKnight/Scripts/` (`Game.cs` ศูนย์กลาง, `Knight.cs` ผู้เล่น, `World.cs` แมพ, `Progress.cs` เลเวล, `Weapon.cs` อาวุธ, `Net.cs` ออนไลน์) — ดูรายละเอียดใน `Docs/reference/files.md`
- ตัวเลขบาลานซ์อยู่ในค่าคงที่ของโค้ด (`Progress.cs`, `Knight.Attacks`, `Weapons.All`, `OldWorld` ฯลฯ) ไม่มี config
- ฉากสร้างด้วย `StaticBatcher` + `System.Random(11)` เท่านั้น (ห้าม `UnityEngine.Random`) collision ตรงกันทุกเครื่อง; ห้ามวางของกีดขวางในลานกลาง (|x|<28, Z -42..32) = สนามเทสต์
- co-op: ห้ามถือว่า `Game.I.knight` คือผู้เล่นคนเดียว (`Game.I.players` / `NearestKnight`); ทุกอย่างที่เปลี่ยนโลกอยู่หลัง `Net.Authority`; ห้ามหยุดเวลาออนไลน์ (`Game.Paused`); โลก/กองเก็บที่ host; ของผู้เล่นอยู่ที่เครื่องเขา (host มีแค่ mirror)
- เปลี่ยนรูปแบบข้อความเครือข่ายต้องเพิ่ม `Net.Protocol` (ตอนนี้ = 5)
- ปุ่มใหม่ใน `InputX.Map()`; เสียงใหม่ใน enum `Snd` + ตารางใน `Sfx.cs` (ลำดับต้องตรงกัน); สีใช้ `Prims.Lit`/`Prims.Glow` (ไม่มี material)

## Build และทดสอบ

- build: `build.bat` → `Builds\MedievalKnight.exe` (log ที่ `build.log`)
- selftest (~7 นาที ไม่มี `FAIL`): `Builds\MedievalKnight.exe -selftest selftest -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile player.log`
- ออนไลน์ชุด v1 (`-arena`, ~70 วินาที): `powershell -ExecutionPolicy Bypass -File mptest.ps1`; ชุด loop (~5 นาที): `mptest-loop.ps1`
- loop: เคสอยู่ท้าย selftest; เร็ว: `-looponly` ~3 นาที, `-p6only`, `-p7only`; `-menutest` (Host = loop)
- ขั้นตอนเต็ม: `dev-guide.md`, `online-coop.md`
