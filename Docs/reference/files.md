# โครงสร้างไฟล์

> แยกมาจาก CLAUDE.md (รายละเอียดเต็ม) — ภาพรวมสั้นอยู่ที่ CLAUDE.md. ส่วน loop (ฐาน / โลก / เซฟ) ดู `loop.md`

| ที่อยู่ | หน้าที่ |
| --- | --- |
| `Assets/MedievalKnight/Scripts/Game.cs` | จุดเริ่มเกม (`Boot`), กล้อง top-down (far clip 120), แสง, เรียกสร้างฉาก (`WorldBuilder`), แพ็คสไลม์ (`UpdatePacks` / `SpawnPack`), EXP + Soul (`OnEnemyKilled`), เลเวลอัป 2 ขั้น (`LevelStep`; หยุดเกม), ดรอป (`RollDrop`), เก็บของ (`UpdatePickups` / `Collect`), หน้าต่าง Tab (`SetInventory`), ตาย / ฟื้น (`OnKnightDown` / `OnKnightRevive`), ป้ายบอกทาง, camera shake, hit-stop, ปุ่ม F1 / F5 / H / Tab / M / Esc. **Loop**: `StartSession(loop)`, `run`, `Loop`, `OnLoopDeath` (ทุกจุดอยู่หลัง `Loop`); phase 2: `worldHpMul` / `worldDmgMul` / `worldPackMul` (1 นอกโลกเก่า), `EnemyArmorScale` (armor ไม่คูณตัวคูณโลก) |
| `Assets/MedievalKnight/Scripts/World.cs` | `MapLayout` (ขนาดแมพ `ZoneAt`, จุดเกิด / รัง / ประตู / `BasePoint`, `InTown`, `Walkable`, `SpawnTier`, ป้าย), `Obstacles` (collision วงกลม / กล่องบน grid 8 ม.: `Push`), `StaticBatcher` (รวม mesh ต่อ chunk 38 ม.), `WorldBuilder` (พื้น เมือง ถนน รัง หอ ไร่ ทะเลสาบ ภูเขา ป่า รั้ว — `System.Random(11)` ล้วน) |
| `Assets/MedievalKnight/Scripts/Knight.cs` | ผู้เล่น: เดิน หัน วิ่ง dash โล่, HP / Armor / Stamina, `Knight.Attacks` (คอมโบ 5 ท่า), `Knight.Skills` (Q E R F), `progress` / `inventory` / `souls`, ตัวเลขต่อสู้รวมดาบ (`DamageMult` / `CritChance` / `CritMult` / `SwingSpeed` / `Reach`), `RollDamage`, `Equip`, `SetWeapon` (loop), `SpendPoint`, `Heal`, `ResetProgress` (ตาย) / `ResetAll` (F5) / `ResetForRun` (loop), `CancelAction` |
| `Assets/MedievalKnight/Scripts/Progress.cs` | เลเวล / EXP (`Need(lvl)`), ผลรวมอัปเกรด, สุ่มการ์ด 3 ใบ (`MakeOffers`), แต้มสเตตัส HP / BLOCK / STAMINA (`Spend`, `AttrCap`) — instance ของอัศวินแต่ละคน |
| `Assets/MedievalKnight/Scripts/Weapon.cs` | `WeaponDef` + `Weapons.All`, `Weapons.BuildModel` (จาก `Prims` หรือไฟล์ `modelPath`), `Inventory` (`SetSingle` = ภาพฉายของ HELD ใน loop), `Foods`, `Pickup` (`kind`, หมุน + วงแหวน, ดาบ 90 วิ อาหาร 60 วิ, `plus` / `dropped` ของ loop) |
| `Assets/MedievalKnight/Scripts/Loadout.cs` | **Loop**: `Item`, `ItemBag`, `Loadout` (stash / carry / HELD / `worlds` โลกเก่า / `runWorld` / `unseen`), `RunResult`; `Die` (ของที่พกเข้ากอง) / `Survive` (ลดตัวนับ) / `TakeFromPile` / `TakeAll` / `Sanitize` — logic ล้วน |
| `Assets/MedievalKnight/Scripts/OldWorld.cs` | **Loop phase 2**: `OldWorld` (seed, เวลา, left, จุดกอง, pile), ค่าคงที่ (`StartLeft` 3, `HpMul` 1.5, `DmgMul` 1.25, `PackMul` 1.5, `MaxPile` 48, `MaxWorlds` 99), `CrumbNote` / `CrumbLine` / `WorldNote` — ข้อมูลล้วน |
| `Assets/MedievalKnight/Scripts/LoopSave.cs` | **Loop**: อ่าน/เขียนเซฟ JSON (atomic, `.bak`, `.corrupt`, validate); DTO โลกเก่า `ow[]` / `runWorld` / `rx rz rpos` / `unseen[]` (Version ยัง 1) |
| `Assets/MedievalKnight/Scripts/RunLoop.cs` | **Loop**: state ฐาน / PREPARE / โลก / สรุป, เข้าโลก (ใหม่ / เก่า), เก็บ / กิน / ทิ้ง, ตาย, บอส + portal; phase 2: ตัวคูณโลกเก่า, `PilePoint`, `TickPile` (เดินชนกอง), `UiTake` / `UiTakeAll`, autosave ตำแหน่ง |
| `Assets/MedievalKnight/Scripts/LoopUi.cs` | **Loop**: PREPARE (NEW WORLD / OLD WORLDS), หน้ารายการโลกเก่า, หน้าต่าง Carry, หน้าต่างเก็บของจากกอง, หน้าสรุป, fade, objective 1-2, ป้ายโลกเก่า, ลูกศร/ป้ายกอง, แถบอาวุธ (canvas ของตัวเอง) |
| `Assets/MedievalKnight/Scripts/Portal.cs` | **Loop**: portal โลก (วงแหวน + ลำแสง + วงโหลด) |
| `Assets/MedievalKnight/Scripts/LootPile.cs` | **Loop phase 2**: กองของโลกเก่า (ถุง + ด้ามอาวุธ + วงทอง + ลำแสงทอง 12 ม.); `Vanish()` ตอนเก็บหมด |
| `Assets/MedievalKnight/Scripts/SwordIcons.cs` | ไอคอนดาบ 128 x 128: ถ่ายโมเดลจริงด้วยกล้อง ortho (layer 31) ลง RenderTexture ตอนเริ่ม; แผนสำรองวาดพิกเซล; `Shadow(i)` = เงาดำ |
| `Assets/MedievalKnight/Scripts/Sfx.cs` | เสียง: โหลด 6 คลิปจาก `Resources/Audio`, AudioSource 2D 8 ตัว, สุ่ม pitch / volume, ปุ่ม M |
| `Assets/MedievalKnight/Scripts/Enemy.cs` | คลาสแม่ศัตรู: HP + Armor, กระเด็น, สตัน, แถบเลือด, ตัวเลขดาเมจ, `soulReward`, `ClampToArena` |
| `Assets/MedievalKnight/Scripts/Slime.cs` | สไลม์ 3 สี กระโดดไล่ผู้เล่นใน 30 ม. ยกเว้นคนที่ยืนในเมือง |
| `Assets/MedievalKnight/Scripts/Minotaur.cs` | บอส: AI 3 ท่า (Sweep / Smash / Charge) มีเครื่องหมายเตือนบนพื้น, leash (ห่างรัง > 40 ม. หรือไม่มีใครใน 45 ม. 5 วิ = กลับรัง) |
| `Assets/MedievalKnight/Scripts/Dummy.cs` | หุ่นฟาง: ไม่โจมตี ไม่ให้ EXP / Soul พังแล้วเกิดใหม่ |
| `Assets/MedievalKnight/Scripts/Fx.cs` | เอฟเฟกต์: อนุภาคลูกบาศก์ + mesh แบนบนพื้น (เส้นฟัน วงคลื่น เส้นพุ่ง เครื่องหมายเตือน) |
| `Assets/MedievalKnight/Scripts/Hud.cs` | UI (uGUI `Text` legacy): แผงซ้ายบน (HP / Armor / Stamina / EXP / Soul), สกิล, แถบบอส (<= 45 ม.), ลูกศรชี้รัง (`SetArrowTarget` ชี้ portal ได้), การ์ดเลเวลอัป, หน้าต่าง Tab (v1), YOU DIED, `Notice`, `Banner`, ปุ่มควบคุม (ข้อความ loop แยกใน `SetLoopTexts`) |
| `Assets/MedievalKnight/Scripts/Prims.cs` | mesh low-poly ในโค้ด (`Cube`, `Lathe`, `Strip`, `Arc`) + material (`Lit` / `Glow`), `Tint` |
| `Assets/MedievalKnight/Scripts/InputX.cs` | ตัวกลางรับปุ่ม / เมาส์; ปุ่มใหม่ต้องเพิ่มใน `Map()` |
| `Assets/MedievalKnight/Scripts/Net.cs` | Online co-op: `Conn` (TCP + thread), `NetW` / `NetR`, `enum Msg`, host / join / handshake, snapshot, hook เกมเพลย์ |
| `Assets/MedievalKnight/Scripts/NetInterp.cs` | บัฟเฟอร์ interpolate ตำแหน่ง/มุมของ puppet |
| `Assets/MedievalKnight/Scripts/Menu.cs` | เมนู Single / Host / Join / Connecting (Single → loop) |
| `Assets/MedievalKnight/Scripts/Args.cs` | สวิตช์ command line (`-loop`, `-save` ของ loop อยู่ที่นี่) |
| `Assets/MedievalKnight/Scripts/MpTest.cs` | ทดสอบออนไลน์หลายโปรเซส (`-mptest`) + เมนู (`-menutest`); `mptest.ps1` เป็นตัวเปิด |
| `Assets/MedievalKnight/Scripts/SelfTest.cs` | ทดสอบอัตโนมัติ `-selftest <โฟลเดอร์>` (v1 ทั้งหมด แล้วเคส loop ท้ายไฟล์; `-looponly` = เฉพาะ loop) เขียน `SELFTEST PASS/FAIL` แล้วปิดเอง |
| `Assets/MedievalKnight/Resources/Audio/` | เสียง mp3 6 ไฟล์ (Git LFS) โหลดใน `Sfx.cs` |
| `Assets/MedievalKnight/Resources/Mats/` | material ฐาน 3 ตัว (`Lit`, `Unlit`, `Fx`) — `BuildScript.Setup3D` สร้างให้ |
| `Assets/Settings/Renderer3D.asset` | URP Universal Renderer (สร้างโดย `BuildScript.Setup3D`) |
| `Assets/Scenes/Main.unity` | ฉากหลัก มีแค่ Main Camera |
| `Assets/Editor/BuildScript.cs` | `BuildWindows`, `Setup3D`, `CreateMainScene` |
| `build.bat` | บิลด์ Windows ด้วย Unity batchmode → `Builds\MedievalKnight.exe`, log ที่ `build.log` |
| `Docs/big-map-update/`, `Docs/phase1-minimal-loop/`, `Docs/phase2-old-world/` | requirement / design / tech / decisions / status ของแต่ละงานใหญ่ |
| `_reference/` | ไฟล์อ้างอิง 2.5D เดิม (ไม่ได้ใช้ในเกมแล้ว) |
