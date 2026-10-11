# แนวทางแก้โค้ดและการทดสอบ

> แยกมาจาก CLAUDE.md (รายละเอียดเต็ม) — ภาพรวมสั้นอยู่ที่ CLAUDE.md

- ปรับ/เพิ่มท่าดาบ: แก้ `Knight.Attacks` (มุม yaw/pitch ของดาบ, เวลา, ดาเมจ, ระยะ, ครึ่งมุมกวาด) — ปรับสกิล: `Knight.Skills` และ coroutine ของสกิลใน `Knight.cs`; dash / วิ่ง: ค่าคงที่ต้น `Knight.cs` (`RunMult`, `Dash*`)
- ปรับสมดุลเลเวล/อัปเกรด/แต้มสเตตัส: ค่าคงที่ใน `Progress.cs`; ความแข็งของศัตรู: `Game.EnemyHpScale` / `EnemyDamageScale` / `EnemyExpScale`, ตารางใน `Slime.cs`, ค่าคงที่ใน `Minotaur.cs`; จำนวน / ระยะแพ็คสไลม์: `Game.UpdatePacks` / `SpawnPack`, `MaxSlimes`
- ปรับแมพ: ตัวเลขโซน / จุดสำคัญใน `MapLayout`; ของในฉากใน `WorldBuilder` (ใช้ helper `Box` / `Cyl` / `Cone` / `Ball` / `RoofAt` ที่ลง `StaticBatcher`, ถ้าบล็อกการเดินต้องเรียก `Obstacles.AddCircle` / `AddBox` ด้วย, ใช้ `R()` = `System.Random(11)` เท่านั้น ห้าม `UnityEngine.Random`); เมนูและ test ใช้ลานกลาง (|x|<28, Z -42..32) เป็นสนามเทสต์ ห้ามวางของกีดขวางที่นั่น
- เพิ่ม/ปรับดาบ: เพิ่มรายการใน `Weapons.All` (id ต้องตรงกับลำดับ) ถ้าเป็นรูปทรงการ์ดใหม่ให้เพิ่ม `GuardStyle` + case ใน `Weapons.BuildModel` (ไอคอนใน Tab ตามมาเอง; แผนสำรอง `SwordIcons.Draw` ใช้ `GuardStyle` ด้วย); ถ้าเกิน 6 เล่ม (ตอนนี้ปุ่ม 1-6 รองรับแล้ว) ต้องเพิ่มปุ่มใน `Hud.InventoryInput` และ `InputX.Map()` และขยาย layout ของ Tab; โอกาสดรอปอยู่ต้นคลาส `Weapons`; ดาบที่ใช้โมเดลจากไฟล์: ใส่ `modelPath` (Resources เช่น `Models/Banana-sword`, ไฟล์ต้องอยู่ใต้ `Assets/MedievalKnight/Resources`) + `modelEuler/Offset/Scale/Tip` ให้ +Z = มือไปปลายดาบ สีมาจาก bladeColor/guardColor/gripColor ตามชื่อ material (_Blade/_Guard/_Grip)
- เพิ่มเสียง: เพิ่มชื่อใน enum `Snd` และตาราง `Files` / `BaseVolume` / `PitchVar` / `MinGap` ใน `Sfx.cs` (ลำดับต้องตรงกัน) แล้ววางไฟล์ใน `Resources/Audio`
- co-op: ดูหัวข้อ "Online co-op" ด้านล่าง — โค้ดเกมเพลย์ห้ามถือว่า `Game.I.knight` คือผู้เล่นคนเดียว (ใช้ `Game.I.players` / `NearestKnight`), ทุกอย่างที่เปลี่ยนโลก (ดาเมจศัตรู, spawn, ดรอป, EXP, Soul) ต้องอยู่หลัง `Net.Authority`, และห้ามหยุดเวลาตอนออนไลน์ (ดู `Game.Paused`)
- เพิ่มศัตรูใหม่: สืบทอด `Enemy`, สร้างโมเดลด้วย `Prims.Box / Part / Lathe / Strip`, เรียก `BaseTick(dt)` ทุกเฟรม แล้วให้ `Game` เป็นคน spawn
- สีทุกอย่างมาจาก `Prims.Lit(color, smoothness, metallic)` / `Prims.Glow(color)` ไม่ต้องสร้างไฟล์ material เพิ่ม
- hook สำหรับเทสต์ใน `Game`: `dropOverride` (ดรอปดาบ), `foodOverride` (ดรอปอาหาร), `packsOff` (ปิดการเกิดแพ็คและการลบสไลม์ที่ไกล เพื่อให้เทสต์คุมชุดศัตรูเอง)
- หลังแก้ ให้รัน `build.bat` แล้วลอง `Builds\MedievalKnight.exe -selftest selftest -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile player.log` ดู log บรรทัด `SELFTEST` (ต้องไม่มี `FAIL`, ใช้เวลา ~3 นาที) แล้วรัน `powershell -ExecutionPolicy Bypass -File mptest.ps1` (ทดสอบออนไลน์ 4 คนบนเครื่องเดียว ดูหัวข้อ Online co-op)

