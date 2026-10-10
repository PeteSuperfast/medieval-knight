# Medieval Knight

เกม action RPG 3D มุมมอง top-down (กล้องเอียง 56° ไม่หมุน เหนืออยู่ด้านบนจอ): อัศวินดาบ+โล่ ออกจากเมืองไปสู้สไลม์ที่เกิดเป็นแพ็ค และบอส Minotaur ในรังทางเหนือ
แมพ 380 x 380 ม. เลเวล 1-25 การ์ดอัปเกรด + แต้มสเตตัส ดาบ 5 เล่ม อาหาร 2 ชนิด Online co-op สูงสุด 4 คน
Unity `6000.6.5f1` (URP 3D, Input System ใหม่, uGUI) เขียนด้วยโค้ดล้วน: ฉาก โมเดล เอฟเฟกต์ UI สร้างตอนรันไทม์ ไม่มี prefab ไม่มีไฟล์โมเดล ข้อความในเกมเป็นภาษาอังกฤษ
ขอโดย test_disk (Discord 449209885138223116) — เวอร์ชัน 2.5D เดิมอยู่ที่ commit `e8b39dd`

## รายละเอียดอยู่ใน Docs\ (อ่านเฉพาะเรื่องที่เกี่ยวกับงาน)

| ไฟล์ | เนื้อหา |
| --- | --- |
| `Docs/reference/files.md` | แต่ละสคริปต์ทำอะไร (ตารางโครงสร้างไฟล์ทั้งหมด) |
| `Docs/reference/controls.md` | ปุ่มควบคุมทั้งหมด |
| `Docs/reference/map-and-spawning.md` | โซนแมพ collision การเกิดของแพ็คสไลม์ |
| `Docs/reference/progression.md` | ตาราง EXP เลเวลอัป stamina แต้มสเตตัส Soul อาหาร |
| `Docs/reference/weapons-and-tab.md` | ตารางดาบ สูตรดาเมจ ดรอป หน้าต่าง Tab |
| `Docs/reference/sound.md` | เสียง 6 คลิปและจังหวะที่เล่น |
| `Docs/reference/online-coop.md` | Net / protocol / authority / กฎ multiplayer / command line / mptest |
| `Docs/reference/dev-guide.md` | วิธีเพิ่ม/ปรับของ (ท่าดาบ ดาบ เสียง ศัตรู แมพ) และขั้นตอนทดสอบเต็ม |
| `Docs/big-map-update/` | requirement / design / tech / decisions / status ของอัปเดตแมพใหญ่ |
| `Docs/ref/`, `Docs/style.md`, `_reference/` | ภาพอ้างอิงและสไตล์ (ไม่ได้ใช้ในเกมแล้ว) |

## สิ่งที่ต้องรู้ก่อนแก้โค้ด

- โค้ดอยู่ใน `Assets/MedievalKnight/Scripts/` (`Game.cs` ศูนย์กลาง, `Knight.cs` ผู้เล่น, `World.cs` แมพ, `Progress.cs` เลเวล, `Weapon.cs` ดาบ, `Net.cs` ออนไลน์) — ดูรายละเอียดใน `Docs/reference/files.md`
- ตัวเลขบาลานซ์อยู่ในค่าคงที่ของโค้ด (`Progress.cs`, `Knight.Attacks` / `Knight.Skills`, `Weapons.All`, `Game.EnemyHpScale` ฯลฯ) ไม่ต้องสร้างไฟล์ config
- ฉากสร้างด้วย `StaticBatcher` และ `System.Random(11)` เท่านั้น (ห้าม `UnityEngine.Random`) เพื่อให้ collision ตรงกันทุกเครื่อง; ห้ามวางของกีดขวางในลานกลาง (|x|<28, Z -42..32) เพราะใช้เป็นสนามเทสต์
- co-op: ห้ามถือว่า `Game.I.knight` คือผู้เล่นคนเดียว (ใช้ `Game.I.players` / `NearestKnight`); ทุกอย่างที่เปลี่ยนโลกต้องอยู่หลัง `Net.Authority`; ห้ามหยุดเวลาตอนออนไลน์ (`Game.Paused`)
- เปลี่ยนรูปแบบข้อความเครือข่ายเมื่อไหร่ ต้องเพิ่ม `Net.Protocol` (ตอนนี้ = 2)
- ปุ่มใหม่ต้องเพิ่มใน `InputX.Map()`; เสียงใหม่เพิ่มใน enum `Snd` + ตารางใน `Sfx.cs` (ลำดับต้องตรงกัน)
- สีใช้ `Prims.Lit(...)` / `Prims.Glow(...)` ไม่ต้องสร้างไฟล์ material

## Build และทดสอบ

- build: `build.bat` → `Builds\MedievalKnight.exe` (log ที่ `build.log`)
- selftest (~3 นาที ต้องไม่มี `FAIL`): `Builds\MedievalKnight.exe -selftest selftest -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile player.log` ดูบรรทัด `SELFTEST`
- ออนไลน์ (4 คนบนเครื่องเดียว ~2.5 นาที): `powershell -ExecutionPolicy Bypass -File mptest.ps1`
- เช็ค "A's walk looks smooth on B" ใน mptest ตกมาตั้งแต่ commit `4b146ba` (รู้อยู่แล้ว ยังไม่ได้ตามหาสาเหตุ)
- ขั้นตอนเต็มและสิ่งที่ยังไม่ครอบคลุม: `Docs/reference/dev-guide.md`, `Docs/reference/online-coop.md`
