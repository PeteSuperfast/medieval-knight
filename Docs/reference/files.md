# โครงสร้างไฟล์

> แยกมาจาก CLAUDE.md (รายละเอียดเต็ม) — ภาพรวมสั้นอยู่ที่ CLAUDE.md

| ที่อยู่ | หน้าที่ |
| --- | --- |
| `Assets/MedievalKnight/Scripts/Game.cs` | จุดเริ่มเกม (`Boot`), กล้อง top-down (far clip 120), แสง, เรียกสร้างฉาก (`WorldBuilder`), ผู้กำกับการเกิดสไลม์เป็นแพ็ค (`UpdatePacks` / `SpawnPack`), แจก EXP + Soul (`OnEnemyKilled`), หน้าต่างเลเวลอัป 2 ขั้น (`LevelStep` 1 = การ์ด, 2 = แต้มสเตตัส; หยุดเกม), สุ่มดรอปดาบ / อาหาร (`RollDrop`), เก็บของ (`UpdatePickups` / `Collect`), หน้าต่าง Tab (`SetInventory`), ตาย / ฟื้น (`OnKnightDown` / `OnKnightRevive`), ป้ายบอกทาง (`UpdateSigns`), camera shake, hit-stop, ปุ่ม F1 / F5 / H / Tab / M / Esc |
| `Assets/MedievalKnight/Scripts/World.cs` | `MapLayout` (ขนาดแมพ โซน `ZoneAt`, จุดเกิด / รัง / ประตู, `InTown`, `Walkable`, `SpawnTier`, รายชื่อป้าย), `Obstacles` (collision วงกลม / กล่องบน grid 8 ม.: `Push`), `StaticBatcher` (รวม mesh ต่อ chunk 38 ม. ต่อ material), `WorldBuilder` (พื้น เมือง ถนน ป้าย รัง หอ กังหัน ไร่ ทะเลสาบ ภูเขา ป่า รั้ว — ใช้ `System.Random(11)` ล้วน เพื่อให้ทุกเครื่องได้ collision ตรงกัน) |
| `Assets/MedievalKnight/Scripts/Knight.cs` | ผู้เล่น: เดิน WASD หันตามเมาส์ วิ่ง (Shift) dash (Space) บล็อกด้วยโล่ (คลิกขวา), HP / Armor / Stamina (ใช้กับ dash อย่างเดียว), ตารางท่าดาบ `Knight.Attacks` (ฟัน 4 ท่า + แทง 1 ท่า เป็นคอมโบ), ตารางสกิล `Knight.Skills` (Q E R F) — ข้อมูลของผู้เล่นหนึ่งคนอยู่บน object นี้ทั้งหมด: `progress`, `inventory`, `souls`; ตัวเลขต่อสู้รวมดาบ: `DamageMult` / `CritChance` / `CritMult` / `SwingSpeed` / `Reach`, `RollDamage`, `Equip`, `SpendPoint`, `Heal`, `ResetProgress` (ตาย) / `ResetAll` (F5), `CancelAction` |
| `Assets/MedievalKnight/Scripts/Progress.cs` | เลเวล / EXP (`NeedTable`, `Need(lvl)`), ผลรวมอัปเกรด Damage / Attack Speed / Crit, ค่าเพดาน, สุ่มการ์ด 3 ใบ (`MakeOffers`), แต้มสเตตัส HP / BLOCK / STAMINA (`statPoints`, `attr[]`, `Spend`, ค่าต่อแต้มและเพดาน `AttrCap`) — เป็น instance ของอัศวินแต่ละคน |
| `Assets/MedievalKnight/Scripts/Weapon.cs` | `WeaponDef` + ตารางดาบ `Weapons.All`, `Weapons.BuildModel` (ประกอบโมเดลดาบจาก `Prims` หรือโหลดไฟล์ผ่าน `modelPath`), `Inventory`, `Foods` (Apple / Roast Meat), `Pickup` (ดาบ / อาหารที่ตกบนพื้น: `kind`, หมุน + วงแหวน, ดาบอยู่ 90 วิ อาหาร 60 วิ) |
| `Assets/MedievalKnight/Scripts/SwordIcons.cs` | ไอคอนดาบ 128 x 128 สำหรับหน้าต่าง Tab: ถ่ายโมเดลดาบจริงด้วยกล้อง ortho (layer 31, ใต้ฉาก) ลง RenderTexture ตอนเริ่มเกม แล้วคัดลอกเป็น `Texture2D`; แผนสำรองวาดพิกเซล (`Method` = "render" / "drawn"); `Shadow(i)` = เงาดำของดาบที่ยังไม่มี |
| `Assets/MedievalKnight/Scripts/Sfx.cs` | เสียงทั้งหมด: โหลด 6 คลิปจาก `Resources/Audio`, AudioSource 2D 8 ตัว (จำกัดเสียงซ้อน), สุ่ม pitch / volume เล็กน้อย, ช่วงห่างขั้นต่ำต่อคลิป, เสียงร้อง `Grunt(heavy)`, ปุ่ม M เปลี่ยนระดับเสียง |
| `Assets/MedievalKnight/Scripts/Enemy.cs` | คลาสแม่ของศัตรู: HP + Armor (เกราะรับก่อน), กระเด็น, สตัน, แถบเลือดลอยเหนือหัว, ตัวเลขดาเมจ, `soulReward`, ชนสิ่งกีดขวาง (`ClampToArena` เรียก `Obstacles.Push` ถ้าไม่ใช่ puppet) |
| `Assets/MedievalKnight/Scripts/Slime.cs` | สไลม์ 3 สี (เขียว / น้ำเงิน / แดง) กระโดดไล่ผู้เล่นใน 30 ม. (`ChaseRange`) ยกเว้นผู้เล่นที่ยืนในเมือง ยืด-หดตัว |
| `Assets/MedievalKnight/Scripts/Minotaur.cs` | บอส: โมเดล + ขวาน labrys, AI 3 ท่า (Sweep / Smash / Charge) มีเครื่องหมายสีแดงบนพื้นเตือนก่อนโจมตี, leash (ห่างรัง > 40 ม. หรือไม่มีใครเป็นๆ ใน 45 ม. นาน 5 วิ = กลับรังเลือดเต็ม) |
| `Assets/MedievalKnight/Scripts/Dummy.cs` | หุ่นฟาง: ไม่โจมตี ไม่ให้ EXP / Soul โยกเมื่อโดนตี พังแล้วเกิดใหม่ |
| `Assets/MedievalKnight/Scripts/Fx.cs` | เอฟเฟกต์ทำเอง: อนุภาคลูกบาศก์ และ mesh แบนบนพื้นที่จางหาย (เส้นฟัน, วงคลื่น, เส้นพุ่ง, เครื่องหมายเตือน) |
| `Assets/MedievalKnight/Scripts/Hud.cs` | UI ทั้งหมด (uGUI, `UnityEngine.UI.Text` แบบ legacy): แผงซ้ายบน (HP / Armor / Stamina มีขีดทุก 25 / EXP / Soul), ไอคอนสกิล + คูลดาวน์, แถบบอส (เฉพาะเมื่อห่าง <= 45 ม.), ลูกศรชี้รัง + ระยะ, การ์ดเลเวลอัป / แต้มสเตตัส, หน้าต่าง Tab (`BuildInventoryPanel`, `InventoryInput`, `RefreshTab`), หน้าจอ YOU DIED (`ShowDeath` / `HideDeath`), `Notice`, ตัวเลขดาเมจ, ปุ่มควบคุม |
| `Assets/MedievalKnight/Scripts/Prims.cs` | สร้าง mesh low-poly ในโค้ด (`MeshB`, `Cube`, `Lathe`, `Strip`, `Arc`) และ material (สำเนาจาก `Resources/Mats`), `Tint` สำหรับกะพริบตอนโดนตี |
| `Assets/MedievalKnight/Scripts/InputX.cs` | ตัวกลางรับปุ่มและเมาส์ (`#if ENABLE_INPUT_SYSTEM`) ปุ่มใหม่ต้องเพิ่มใน `Map()` |
| `Assets/MedievalKnight/Scripts/Net.cs` | Online co-op: `Conn` (TCP + thread อ่าน/เขียน), `NetW` / `NetR`, `enum Msg`, host / join / handshake / refuse / timeout, ส่ง snapshot และ hook ที่เกมเพลย์เรียก (`SendAct`, `SendHit`, `BroadcastEnemyHit` ...) |
| `Assets/MedievalKnight/Scripts/NetInterp.cs` | บัฟเฟอร์ interpolate ตำแหน่ง/มุมของ puppet (อัศวินคนอื่น, ศัตรูบน client) |
| `Assets/MedievalKnight/Scripts/Menu.cs` | เมนูเริ่มเกม Single / Host / Join / Connecting |
| `Assets/MedievalKnight/Scripts/Args.cs` | สวิตช์ command line |
| `Assets/MedievalKnight/Scripts/MpTest.cs` | ทดสอบออนไลน์หลายโปรเซส (`-mptest`) และทดสอบเมนู (`-menutest`); `mptest.ps1` เป็นตัวเปิด |
| `Assets/MedievalKnight/Scripts/SelfTest.cs` | ทดสอบอัตโนมัติ ทำงานเฉพาะเมื่อรัน `MedievalKnight.exe -selftest <โฟลเดอร์>` (วิ่ง dash i-frame ตี ใช้สกิล ฆ่าสไลม์ เลเวลอัปถึง 25 ตาย + รีเซ็ต + Soul อาหาร ดรอป/เก็บดาบ Tab + ไอคอนดาบ แมพ (มุม / ทะเลสาบ / ภูเขา / กำแพงเมือง / ต้นไม้) การเกิดแพ็ค leash ลูกศรรัง เสียง 6 คลิป เก็บภาพหน้าจอ เขียน log `SELFTEST PASS/FAIL` แล้วปิดเอง) |
| `Assets/MedievalKnight/Resources/Audio/` | เสียง mp3 6 ไฟล์ (Git LFS): `sword_swing`, `sword_slice`, `armor_impact`, `sword_clash`, `knight_grunt`, `knight_ready` — โหลดด้วย `Resources.Load<AudioClip>("Audio/<ชื่อ>")` ใน `Sfx.cs` |
| `Assets/MedievalKnight/Resources/Mats/` | material ฐาน 3 ตัว (`Lit`, `Unlit`, `Fx` แบบโปร่งใส) — `BuildScript.Setup3D` สร้างให้เอง มีไว้เพื่อให้ shader ของ URP ติดไปกับ build |
| `Assets/Settings/Renderer3D.asset` | URP Universal Renderer (สร้างโดย `BuildScript.Setup3D` และผูกกับ `UniversalRP.asset`) |
| `Assets/Scenes/Main.unity` | ฉากหลัก มีแค่ Main Camera |
| `Assets/Editor/BuildScript.cs` | `BuildWindows`, `Setup3D` (renderer 3D + เงา + material ฐาน), `CreateMainScene` |
| `build.bat` | บิลด์ Windows ด้วย Unity batchmode → `Builds\MedievalKnight.exe`, log ที่ `build.log` |
| `Docs/big-map-update/` | requirement / design / tech / decisions / status ของอัปเดตแมพใหญ่ (status.md บอกว่าทำถึงไหน ตัดสินอะไรไป) |
| `_reference/` | ไฟล์อ้างอิง 2.5D เดิมของผู้ขอ (ไม่ได้ใช้ในเกมแล้ว เก็บไว้) |

