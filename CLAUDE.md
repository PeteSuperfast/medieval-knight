# Medieval Knight

เกม action RPG 3D มุมมอง top-down (กล้องเอียง 56° ตามตัวอัศวิน ไม่หมุน เหนืออยู่ด้านบนจอ): อัศวินดาบ+โล่ ออกจากเมืองไปสู้กับสไลม์ที่เกิดเป็นแพ็ครอบตัว และบอส Minotaur ถือขวานสองคม (labrys) ในรังทางเหนือ มีหุ่นฟางไว้ซ้อมในลานซ้อมของเมือง
แมพ 380 x 380 ม. (เมืองทางใต้ ทุ่ง ป่าใบส้ม ป่าสน ไร่นา ทะเลสาบ ภูเขา รังบอส) มีระบบเลเวล 1-25 (ตาราง EXP) เลเวลอัปแล้วสุ่มการ์ด 3 ใบให้เลือก 1 ใบ แล้วได้แต้มสเตตัส 1 แต้ม (HP / BLOCK / STAMINA) และสกิล 4 อัน
ตายแล้วเลเวลกลับเป็น 1 (ดาบและ Soul ไม่หาย) มีช่องอาวุธ 1 ช่อง + ดาบ 5 เล่ม (ดรอปจากสไลม์ / Minotaur เก็บโดยเดินทับ) อาหารฟื้นเลือด 2 ชนิด (Apple / Roast Meat) และเสียงดาบ 6 ไฟล์
Unity `6000.6.5f1` (URP Universal Renderer แบบ 3D มีแสง directional + เงา, Input System ใหม่, uGUI)
เขียนด้วยโค้ดล้วน: ฉาก โมเดล (ประกอบจาก mesh ที่สร้างในโค้ด สไตล์ low-poly) เอฟเฟกต์ และ UI ถูกสร้างตอนรันไทม์ ไม่มี prefab ไม่มีไฟล์โมเดล ข้อความในเกมเป็นภาษาอังกฤษ
ขอโดย test_disk (Discord 449209885138223116) — เวอร์ชัน 2.5D เดิมอยู่ที่ commit `e8b39dd`; อัปเดตแมพใหญ่ / dash / เลเวล 25 (เอกสารอยู่ใน `Docs/big-map-update/`) commit ขึ้นต้น "big-map-update A-G"

## โครงสร้างไฟล์

| ที่อยู่ | หน้าที่ |
| --- | --- |
| `Assets/MedievalKnight/Scripts/Game.cs` | จุดเริ่มเกม (`Boot`), กล้อง top-down (far clip 120), แสง, เรียกสร้างฉาก (`WorldBuilder`), ผู้กำกับการเกิดสไลม์เป็นแพ็ค (`UpdatePacks` / `SpawnPack`), แจก EXP + Soul (`OnEnemyKilled`), หน้าต่างเลเวลอัป 2 ขั้น (`LevelStep` 1 = การ์ด, 2 = แต้มสเตตัส; หยุดเกม), สุ่มดรอปดาบ / อาหาร (`RollDrop`), เก็บของ (`UpdatePickups` / `Collect`), หน้าต่าง Tab (`SetInventory`), ตาย / ฟื้น (`OnKnightDown` / `OnKnightRevive`), ป้ายบอกทาง (`UpdateSigns`), camera shake, hit-stop, ปุ่ม F1 / F5 / H / Tab / M / Esc |
| `Assets/MedievalKnight/Scripts/World.cs` | `MapLayout` (ขนาดแมพ โซน `ZoneAt`, จุดเกิด / รัง / ประตู, `InTown`, `Walkable`, `SpawnTier`, รายชื่อป้าย), `Obstacles` (collision วงกลม / กล่องบน grid 8 ม.: `Push`), `StaticBatcher` (รวม mesh ต่อ chunk 38 ม. ต่อ material), `WorldBuilder` (พื้น เมือง ถนน ป้าย รัง หอ กังหัน ไร่ ทะเลสาบ ภูเขา ป่า รั้ว — ใช้ `System.Random(11)` ล้วน เพื่อให้ทุกเครื่องได้ collision ตรงกัน) |
| `Assets/MedievalKnight/Scripts/Knight.cs` | ผู้เล่น: เดิน WASD หันตามเมาส์ วิ่ง (Shift) dash (Space) บล็อกด้วยโล่ (คลิกขวา), HP / Armor / Stamina (ใช้กับ dash อย่างเดียว), ตารางท่าดาบ `Knight.Attacks` (ฟัน 4 ท่า + แทง 1 ท่า เป็นคอมโบ), ตารางสกิล `Knight.Skills` (Q E R F) — ข้อมูลของผู้เล่นหนึ่งคนอยู่บน object นี้ทั้งหมด: `progress`, `inventory`, `souls`; ตัวเลขต่อสู้รวมดาบ: `DamageMult` / `CritChance` / `CritMult` / `SwingSpeed` / `Reach`, `RollDamage`, `Equip`, `SpendPoint`, `Heal`, `ResetProgress` (ตาย) / `ResetAll` (F5), `CancelAction` |
| `Assets/MedievalKnight/Scripts/Progress.cs` | เลเวล / EXP (`NeedTable`, `Need(lvl)`), ผลรวมอัปเกรด Damage / Attack Speed / Crit, ค่าเพดาน, สุ่มการ์ด 3 ใบ (`MakeOffers`), แต้มสเตตัส HP / BLOCK / STAMINA (`statPoints`, `attr[]`, `Spend`, ค่าต่อแต้มและเพดาน `AttrCap`) — เป็น instance ของอัศวินแต่ละคน |
| `Assets/MedievalKnight/Scripts/Weapon.cs` | `WeaponDef` + ตารางดาบ `Weapons.All`, `Weapons.BuildModel` (ประกอบโมเดลดาบจาก `Prims`), `Inventory`, `Foods` (Apple / Roast Meat), `Pickup` (ดาบ / อาหารที่ตกบนพื้น: `kind`, หมุน + วงแหวน, ดาบอยู่ 90 วิ อาหาร 60 วิ) |
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

## ปุ่ม

WASD หรือลูกศร เดิน, **Left Shift (ค้าง) = วิ่ง** (x1.5, ไม่กิน stamina), เมาส์ = เล็ง (อัศวินหันตามเคอร์เซอร์เสมอ), คลิกซ้าย / J = คอมโบดาบ (Side Slash > Overhead Chop > Rising Slash > Spin Slash > Thrust),
**Space = dash** (พุ่ง 6 ม. ใน 0.2 วิ ทิศ WASD ไม่กดทิศ = ถอยหลัง, อมตะ 0.5 วิ, กิน stamina 25, คูลดาวน์ 0.8 วิ, ตัดท่าฟัน/สกิลที่กำลังทำ, ห้ามตอนกระโดด Ground Slam), **คลิกขวาค้าง = ยกโล่** (ไม่กิน stamina),
Q Whirlwind, E Dash Thrust, R Shield Bash, F Ground Slam (กระโดดไปที่เคอร์เซอร์),
1 / 2 / 3 หรือคลิกการ์ด = เลือกอัปเกรดตอนเลเวลอัป แล้วเลือกลงแต้มสเตตัส (1 HP / 2 BLOCK / 3 STAMINA),
**Tab** = เปิด/ปิดหน้าต่างอาวุธ + สเตตัส (เล่นคนเดียวหยุดเกม; กด 1-5 หรือคลิกช่อง = ใส่ดาบที่มี), **Esc ตอน Tab เปิด = ปิดหน้าต่างก่อน** (ไม่นับเป็น Esc ออกเมนู), เดินทับของที่ตกบนพื้น = เก็บ, **M** = ระดับเสียง 100% > 60% > 30% > ปิดเสียง,
H โดนตีทดสอบ 20, F5 เริ่มใหม่ (กลับเลเวล 1 ล้างอัปเกรด แต้มสเตตัส ดาบ (เหลือ Iron Sword) และ Soul กลับเมือง; ออนไลน์กดได้เฉพาะ host และรีเซ็ตทุกคน), F1 ซ่อนปุ่มควบคุม, Esc สองครั้ง = กลับเมนู (host กด = ปิดห้อง), Esc ที่หน้าเมนูหลัก = ออกจากเกม

## แมพ โซน และการเกิดของศัตรู (`World.cs`)

X ไปตะวันออก, Z ไปเหนือ (ด้านบนจอ), ศูนย์กลาง (0,0), ครึ่งด้าน `Game.ArenaHalf` = 190 ม. รั้วไม้รอบแมพ (ผู้เล่นยืนได้ไม่เกิน ±189.6)

| โซน (`Zone`) | ที่ (X, Z) | ตัวคูณสไลม์ (`SpawnTier`) | หมายเหตุ |
| --- | --- | --- | --- |
| Town เมือง | X -60..60, Z -190..-100 (สี่เหลี่ยม) | 0 | **เขตปลอดภัย**: ไม่เกิดสไลม์ สไลม์ไม่ไล่ผู้เล่นที่ยืนในเมือง; จุดเกิด / ฟื้น (0,-150), ลานหุ่นฟาง (25,-140), กำแพงเหนือมีประตู (0,-100) กว้าง ~7 ม., หอนาฬิกา, บ้าน ~20 หลัง |
| Meadow ทุ่ง | X -80..80, Z -95..110 (และพื้นที่ที่ไม่เข้าโซนอื่น) | 1.0 | ถนนดินจากประตูขึ้นเหนือถึงรัง + ทางแยกตะวันตก / ตะวันออกที่ Crossroads (0,-20); หอเฝ้า (-60,70); **ลานกลาง (|x|<28, Z -42..32) ไม่มีต้นไม้** (ใช้เป็นสนามเทสต์) |
| Autumn Forest ป่าใบส้ม | X <= -90, Z -90..110 | 1.5 | ต้นไม้หนาแน่น |
| Dark Pine Forest ป่าสน | X >= 100, Z >= 40 | 2.0 | |
| Farmland ไร่นา | X >= 90, Z -150..-30 | 1.0 | กังหันลม (130,-100) ใบพัดหมุน, โรงนา, ฟาง |
| Lake ทะเลสาบ | X <= -90, Z <= -90 | 0 | **ผ่านไม่ได้** (กล่อง collision) ท่าเรือ + เรือ |
| Mountain ภูเขา | X <= -80, Z >= 100 | 0 | **ผ่านไม่ได้** |
| Lair รัง | (0,150) รัศมี 25 | 2.0 | Minotaur อยู่ที่นี่, เสาหัก, คบไฟส้มสูง, ระยะจุดเกิด → รัง 300 ม. (วิ่ง ~32 วิ) |

- ฉากสร้างตอน `Awake` ด้วย `StaticBatcher`: ทุกชิ้นถูกรวมต่อ chunk 38 ม. ต่อ material (ห้ามสร้าง GameObject ต่อชิ้นในฉาก) — ~8,500 ชิ้น เหลือ ~1,270 mesh ใช้เวลาสร้าง < 0.1 วิ เฟรมไทม์ ~4 ms ที่ 1280x720
- collision (`Obstacles`): บ้าน กำแพง ประตู รั้วลานซ้อม หินใหญ่ เสารัง หอ ทะเลสาบ ภูเขา บล็อกทั้งผู้เล่นและศัตรู; **ต้นไม้บล็อกเฉพาะผู้เล่น** (สไลม์กระโดดเส้นตรงจึงไม่ติด); Minotaur Charge ชนแล้วหยุด; `Knight.Simulate` (รวมช่วง dash) และ `Enemy.ClampToArena` เรียก `Push`
- ป้ายบอกทางเป็นลูกศรไม่มีตัวหนังสือ: เดินเข้าใกล้ (< 8 ม.) จะมีข้อความขึ้นเหนือแถบสกิล (`Game.UpdateSigns`); ลูกศรชี้รังที่ HUD (ใต้ banner) แสดงระยะ ซ่อนเมื่อห่างรัง < 30 ม.; แถบบอสแสดงเฉพาะเมื่อ Minotaur ห่าง <= 45 ม.
- **สไลม์เกิดเป็นแพ็คต่อผู้เล่น** (`Game.UpdatePacks`): ผู้เล่นที่ยังไม่ตาย ไม่อยู่ในเมือง ยืนในโซนที่ `SpawnTier > 0` และมีสไลม์ใน 45 ม. <= 1 ตัว → รอ 2.5 วิ แล้วเกิดแพ็ค `min(4 + จำนวนแพ็คของเขา, 10)` ตัว (+2 ต่อผู้เล่นอื่นที่อยู่ใน 45 ม. สูงสุด 16) เป็นวงแหวน 15-26 ม. (แพ็คแรก 10-16 ม.) หลบ: ใกล้ผู้เล่น < 4.5 ม., โซน tier 0, ในสิ่งกีดขวาง, ใกล้ Minotaur < 6 ม.; สไลม์ทั้งโลกไม่เกิน `MaxSlimes` 48; สไลม์ที่ห่างทุกคน > 70 ม. ถูกลบเงียบๆ (ไม่ให้ EXP / ดรอป / Soul); `wave` ตอนนี้แค่นับแพ็คที่เกิด ไม่มีประกาศ "Wave n"
- ชนิดสไลม์: `blue = clamp((แพ็ค-2) x 0.15 x tier, 0, 0.7)`, `red = clamp((แพ็ค-5) x 0.12 x tier, 0, 0.5)`; จำนวนแพ็คของผู้เล่นเป็น 0 ใหม่ตอนตาย / F5
- ตอนฟื้นที่เมือง สไลม์ใน 60 ม. รอบจุดฟื้นถูกลบ (`PushEnemiesFrom`)

## ระบบเลเวลและอัปเกรด

- EXP ที่ต้องใช้จากเลเวล L ไป L+1 อยู่ใน `Progress.NeedTable` (24 ช่อง): 30, 45, 65, 90, 120, 155, 195, 240, 290, 345, 405, 470, 540, 620, 710, 810, 920, 1040, 1170, 1310, 1460, 1620, 1790, 1970 (รวม 16,410) เลเวลสูงสุด 25
- EXP ฐาน: สไลม์เขียว 10 / น้ำเงิน 24 / แดง 48, Minotaur 250, หุ่นฟาง 0; **คูณ `Game.EnemyExpScale` = 1 + 0.15 x (เลเวลปาร์ตี้ - 1)** คิดตอนศัตรูเกิด
- ตายแล้วเลเวล / EXP / การ์ดอัปเกรด / แต้มสเตตัสกลับเป็นเลเวล 1 (ดาบ + Soul ไม่หาย); ระหว่างรอฟื้น (single 3 วิ `ReviveDelaySingle`, ออนไลน์ 5 วิ `ReviveDelayCoop`) มีหน้าจอ **YOU DIED** แสดงค่าก่อนรีเซ็ต + นับถอยหลัง แล้วฟื้นที่เมือง เลือดเต็ม มีโล่ 2.5 วิ
- เลเวลอัป: HP สูงสุด +10 (อัตโนมัติ), เติมเลือดเต็ม, สุ่มการ์ด 3 ใบ (1 ใบสำหรับ ALL ATTACKS สุ่ม +0.5x ถึง +1.0x, 2 ใบสำหรับสกิลเดี่ยว สุ่ม +1.0x ถึง +2.0x) จากนั้นหน้าต่างเดียวกันเปลี่ยนเป็นขั้นที่ 2 ให้ลงแต้มสเตตัส 1 แต้ม
- `+Nx` คือบวกเข้าตัวคูณตรงๆ: Damage (เริ่ม x1.0, เพดาน x10), Attack Speed (เริ่ม x1.0, เพดาน x4; ฟันเร็วขึ้นสูงสุด x3 และหารคูลดาวน์สกิล — **ไม่เกี่ยวกับ stamina แล้ว**), Crit (ตัวคูณคริเริ่ม x1.5 เพดาน x10, โอกาสคริเริ่ม 10% +5% ต่อ 1.0x เพดาน 75%)
- ศัตรูที่เกิดใหม่เก่งขึ้นตามเลเวลผู้เล่น: HP x(1 + 0.45 ต่อเลเวล), ดาเมจ x(1 + 0.08 ต่อเลเวล) (เลเวล 25 เดี่ยว: HP x11.8, ดาเมจ x2.92 — ค่าประมาณ ยังไม่ได้ปรับจากการเล่นจริง ปรับที่ `Game.EnemyHpScale` / `EnemyDamageScale` / `EnemyExpScale`)

## Stamina และการป้องกัน

Stamina (ฐาน 100, ฟื้น 26/วินาที หลังใช้ 0.6 วิ) ใช้กับ **dash อย่างเดียว** (25 ต่อครั้ง; 1 ขีดบนแถบ = 1 dash) ท่าดาบ สกิล และการยกโล่ไม่กิน stamina; ยกโล่สำเร็จเสมอเมื่อหันถูกทิศ (< 75 องศา) ลดดาเมจ 80% (+BLOCK)
dash ไม่พอ stamina = แถบแดงกะพริบ; อมตะระหว่าง dash แสดง "DODGE" (ตัดสินที่ `Knight.TakeDamage` บนเครื่องเจ้าของ)

## แต้มสเตตัส (HP / BLOCK / STAMINA)

เลเวลอัป 1 ครั้ง = 1 แต้ม (รวม 24 แต้มที่เลเวล 25) เลือกในขั้นที่ 2 ของหน้าต่างเลเวลอัป (คีย์ 1 / 2 / 3 หรือคลิกการ์ด) ลงได้สูงสุด 10 แต้มต่อสเตตัส (`Progress.AttrCap`)
ตายแล้วแต้มหาย (รีเซ็ตเลเวล), F5 ล้าง ค่าคงที่อยู่ใน `Progress.cs`

| สเตตัส | ต่อ 1 แต้ม | เต็ม 10 แต้ม |
| --- | --- | --- |
| HP | HP สูงสุด +20 (`HpPerPoint`) และเติมเลือด +20 ทันที | +200 (เลเวล 25 สาย HP: 100 + 240 + 200 = 540) |
| BLOCK | Armor สูงสุด +10 (`ArmorPerPoint`), ยกโล่กันดาเมจเพิ่ม +1.2% (`BlockPerPoint`; ฐาน 80%) | Armor +100, กัน 92% |
| STAMINA | Stamina สูงสุด +15 (`StaminaPerPoint`), ฟื้น stamina เร็วขึ้น +5% (`RegenPerPoint`) | Stamina 250 (dash 10 ครั้ง), ฟื้น +50% |

## Soul และอาหาร

- **Soul**: `Knight.souls` (ต่อผู้เล่น, ไม่เซฟข้ามเซสชัน, ยังไม่มีที่ใช้): สไลม์เขียว 1 / น้ำเงิน 3 / แดง 6, Minotaur 50 (`Enemy.soulReward`); ตายไม่หาย, F5 ล้าง; โชว์แถวที่ 5 ของแผงซ้ายบน + popup "+N Soul"; ออนไลน์ผู้เล่นที่ยังไม่ตายทุกคนได้เต็มจำนวน (ผ่าน `Msg.Exp`)
- **อาหาร** (`Foods`, สุ่มที่ `Game.RollDrop` อิสระจากดาบ): Apple ฟื้น 15% ของ MaxHp / Roast Meat ฟื้น 40%; โอกาส Apple เขียว 14% / น้ำเงิน 16% / แดง 18%, Meat เขียว 0 / น้ำเงิน 3% / แดง 8%, Minotaur ดรอป Meat 2 ชิ้น; อยู่บนพื้น 60 วิ (สูงสุด 40 ชิ้น); เดินทับ = กิน; **ออนไลน์ใครเดินทับก่อนได้ แต่ข้ามคนที่ HP เต็ม** (คนเลือดเต็มเดินเข้าใกล้ครั้งแรกได้ข้อความ "HP full" ครั้งเดียว)

## หน้าต่าง Tab (Inventory & Stats)

กรอบ ~1000 x 520 กลางจอ: ซ้าย กล่อง EQUIPPED (ไอคอนดาบใหญ่ + ชื่อ / +N / ความหายาก / DMG SPD CRIT REACH) และช่องดาบ 5 ช่องแนวนอน (กรอบสีตามความหายาก: Common เทา / Uncommon เขียว / Rare แดง / Legendary ทอง, ช่องที่ใส่อยู่มีแถบ EQUIPPED, ดาบที่ยังไม่มี = เงาดำ + "???" + บอกที่มา); ขวา CHARACTER / STAT POINTS (n/10) / SKILLS / SOUL + Kills
ข้อมูลตัวอักษรใน `Hud.RefreshTab` (เรียกเฉพาะตอนเปิด / ทุก ~0.2 วิ) ไอคอนมาจาก `SwordIcons`; ออนไลน์เป็น overlay ไม่หยุดเกม ไม่มีโล่กันเพิ่ม (โล่ 6 วิ `LevelUpProtect` ใช้เฉพาะหน้าต่างเลเวลอัป)

## ดาบและช่องอาวุธ

อัศวินมีช่องอาวุธ 1 ช่อง เริ่มด้วย Iron Sword ดาบที่เก็บได้เข้า `knight.inventory` กด Tab แล้วเลือกใส่ โมเดลดาบในมือเปลี่ยนทันที (`Knight.Equip` > `Weapons.BuildModel`)
ตารางอยู่ใน `Weapons.All` (`Weapon.cs`):

| # | ดาบ | Damage | Speed | Crit chance | Crit mult | Reach | รูปร่าง | ได้จาก |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Iron Sword (Common) | x1.00 | x1.00 | +0% | +0.0x | x1.00 | ใบเหล็กตรง กาดทอง | เริ่มเกม |
| 2 | Swift Rapier (Uncommon) | x0.75 | x1.35 | +5% | +0.0x | x1.05 | ใบยาวเรียวสีฟ้าอมเขียว การ์ดถ้วยกลม | สไลม์ (45% ของดรอป) |
| 3 | Iron Greatsword (Uncommon) | x1.70 | x0.72 | +0% | +0.0x | x1.25 | ใบกว้างยาวสีเหล็กเข้ม มีร่องกลาง กาดยาว ด้ามสองมือ | สไลม์ (35%) |
| 4 | Crimson Fang (Rare) | x0.90 | x1.10 | +20% | +1.0x | x0.90 | ใบสั้นโค้งสีแดง กาดดำรูปเขี้ยว อัญมณีแดง | สไลม์ (20%) |
| 5 | Horned King's Blade (Legendary) | x1.50 | x1.10 | +10% | +0.5x | x1.15 | ใบทองมีแกนเรืองแสงสีส้ม กาดเป็นเขาวัวคู่ | Minotaur |

วิธีรวมกับอัปเกรดใน `Progress.cs` (ทำใน `Knight.cs` ที่เดียว):
- Damage = `min(10, 1 + อัปเกรด) x Damage ของดาบ x (1 + 0.05 x ระดับ +N)` — ใช้กับคอมโบดาบและสกิล Q / E / F; Shield Bash (R) ใช้โล่ ไม่ได้โบนัสดาบ
- Crit chance = `min(75%, อัปเกรด + ของดาบ)`, Crit mult = `min(x10, อัปเกรด + ของดาบ)` (ไม่รวม R เช่นกัน)
- ความเร็วฟันคอมโบ = `clamp(min(3, 1 + อัปเกรด) x Speed ของดาบ, 0.4, 4)`; คูลดาวน์สกิลไม่ขึ้นกับดาบ
- Reach = ระยะของท่าคอมโบ x Reach ของดาบ (สกิลไม่เปลี่ยน)

ดรอป (สุ่มที่ `Game.RollDrop` ที่เดียว):
- สไลม์เขียว 4% / น้ำเงิน 10% / แดง 20% ต่อตัว (`Weapons.SlimeDropChance`) ได้ดาบ 1 ใน 3 เล่มตามสัดส่วน 45 / 35 / 20 (`SlimeDropWeights`)
- Minotaur ดรอป Horned King's Blade 100% ทุกครั้งที่ตาย (`MinotaurDropChance`), หุ่นฟางไม่ดรอป
- ดาบที่ตกลอยหมุนมีวงแหวน + ลำแสงสีตามระดับ เดินเข้าใกล้ 1.25 ม. = เก็บ มีข้อความแจ้งเหนือแถบสกิล อยู่ได้ 90 วินาที (กะพริบ 8 วินาทีสุดท้าย)
- ได้ดาบซ้ำ: ดาบเล่มนั้นเป็น +1 ถึง +5 (ดาเมจของดาบ +5% ต่อขั้น, `Weapons.DamagePerPlus`); ซ้ำอีกหลัง +5 ได้ 15 EXP แทน
- ตายแล้วดาบไม่หาย, F5 ล้างเหลือ Iron Sword และลบของที่ตกอยู่บนพื้น

## เสียง

ไฟล์ทั้ง 6 มาจากผู้ขอ (ชื่อไฟล์ตั้งใหม่ตามที่เดาจากชื่อเดิม) อยู่ใน `Resources/Audio` เล่นผ่าน `Sfx.I.Play(Snd.X)`:

| คลิป | เล่นเมื่อ |
| --- | --- |
| `sword_swing` | ตอนเริ่มจังหวะฟันของคอมโบทุกท่า (ไม่ใช่ตอนง้าง), Whirlwind 3 ครั้ง, Dash Thrust, ตอนกระโดด Ground Slam, dash — pitch ต่ำลงสำหรับดาบช้า สูงขึ้นสำหรับดาบเร็ว |
| `sword_slice` | ดาบโดนศัตรูแล้วดาเมจส่วนใหญ่เข้า HP |
| `armor_impact` | ดาบโดนศัตรูแล้วเกราะรับส่วนใหญ่ (สไลม์แดง หุ่นฟาง), Shield Bash โดน, อัศวินโดนตี (ไม่ได้บล็อก) แล้วเกราะรับส่วนใหญ่ |
| `sword_clash` | บล็อกด้วยโล่สำเร็จ, ดาบโดน Minotaur ตอนเกราะยังรับอยู่ (`Enemy.ArmorSound`) |
| `knight_grunt` | ใช้สกิลทุกครั้ง, ท่าคอมโบหนัก (Chop / Spin / Thrust) ทุกครั้ง, ท่าเบาสุ่ม 25% และห่างกันอย่างน้อย 1.8 วินาที — ทั้งหมดห่างกันอย่างน้อย 0.9 วินาที |
| `knight_ready` | เริ่มเกม, ฟื้นหลังตาย, กด F5 |

ต่อการฟัน 1 ครั้งเล่นเสียงโดนอย่างมากชนิดละ 1 ครั้งไม่ว่าจะโดนกี่ตัว (`Knight.HitArea`) ค่า volume / pitch / ช่วงห่างของแต่ละคลิปอยู่ต้นไฟล์ `Sfx.cs`
ไฟล์ mp3 import ด้วยค่า default (ไม่ preload) จึงเรียก `LoadAudioData()` ตอนเริ่มเกม

## แนวทางแก้โค้ด

- ปรับ/เพิ่มท่าดาบ: แก้ `Knight.Attacks` (มุม yaw/pitch ของดาบ, เวลา, ดาเมจ, ระยะ, ครึ่งมุมกวาด) — ปรับสกิล: `Knight.Skills` และ coroutine ของสกิลใน `Knight.cs`; dash / วิ่ง: ค่าคงที่ต้น `Knight.cs` (`RunMult`, `Dash*`)
- ปรับสมดุลเลเวล/อัปเกรด/แต้มสเตตัส: ค่าคงที่ใน `Progress.cs`; ความแข็งของศัตรู: `Game.EnemyHpScale` / `EnemyDamageScale` / `EnemyExpScale`, ตารางใน `Slime.cs`, ค่าคงที่ใน `Minotaur.cs`; จำนวน / ระยะแพ็คสไลม์: `Game.UpdatePacks` / `SpawnPack`, `MaxSlimes`
- ปรับแมพ: ตัวเลขโซน / จุดสำคัญใน `MapLayout`; ของในฉากใน `WorldBuilder` (ใช้ helper `Box` / `Cyl` / `Cone` / `Ball` / `RoofAt` ที่ลง `StaticBatcher`, ถ้าบล็อกการเดินต้องเรียก `Obstacles.AddCircle` / `AddBox` ด้วย, ใช้ `R()` = `System.Random(11)` เท่านั้น ห้าม `UnityEngine.Random`); เมนูและ test ใช้ลานกลาง (|x|<28, Z -42..32) เป็นสนามเทสต์ ห้ามวางของกีดขวางที่นั่น
- เพิ่ม/ปรับดาบ: เพิ่มรายการใน `Weapons.All` (id ต้องตรงกับลำดับ) ถ้าเป็นรูปทรงการ์ดใหม่ให้เพิ่ม `GuardStyle` + case ใน `Weapons.BuildModel` (ไอคอนใน Tab ตามมาเอง; แผนสำรอง `SwordIcons.Draw` ใช้ `GuardStyle` ด้วย); ถ้าเกิน 5 เล่มต้องเพิ่มปุ่มใน `Hud.InventoryInput` และ `InputX.Map()` และขยาย layout ของ Tab; โอกาสดรอปอยู่ต้นคลาส `Weapons`
- เพิ่มเสียง: เพิ่มชื่อใน enum `Snd` และตาราง `Files` / `BaseVolume` / `PitchVar` / `MinGap` ใน `Sfx.cs` (ลำดับต้องตรงกัน) แล้ววางไฟล์ใน `Resources/Audio`
- co-op: ดูหัวข้อ "Online co-op" ด้านล่าง — โค้ดเกมเพลย์ห้ามถือว่า `Game.I.knight` คือผู้เล่นคนเดียว (ใช้ `Game.I.players` / `NearestKnight`), ทุกอย่างที่เปลี่ยนโลก (ดาเมจศัตรู, spawn, ดรอป, EXP, Soul) ต้องอยู่หลัง `Net.Authority`, และห้ามหยุดเวลาตอนออนไลน์ (ดู `Game.Paused`)
- เพิ่มศัตรูใหม่: สืบทอด `Enemy`, สร้างโมเดลด้วย `Prims.Box / Part / Lathe / Strip`, เรียก `BaseTick(dt)` ทุกเฟรม แล้วให้ `Game` เป็นคน spawn
- สีทุกอย่างมาจาก `Prims.Lit(color, smoothness, metallic)` / `Prims.Glow(color)` ไม่ต้องสร้างไฟล์ material เพิ่ม
- hook สำหรับเทสต์ใน `Game`: `dropOverride` (ดรอปดาบ), `foodOverride` (ดรอปอาหาร), `packsOff` (ปิดการเกิดแพ็คและการลบสไลม์ที่ไกล เพื่อให้เทสต์คุมชุดศัตรูเอง)
- หลังแก้ ให้รัน `build.bat` แล้วลอง `Builds\MedievalKnight.exe -selftest selftest -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile player.log` ดู log บรรทัด `SELFTEST` (ต้องไม่มี `FAIL`, ใช้เวลา ~3 นาที) แล้วรัน `powershell -ExecutionPolicy Bypass -File mptest.ps1` (ทดสอบออนไลน์ 4 คนบนเครื่องเดียว ดูหัวข้อ Online co-op)

## Online co-op (สูงสุด 4 คน, ขอโดย test_disk)

เกมเริ่มที่เมนู (`Menu.cs`): ช่องชื่อผู้เล่น, **Single Player**, **Host Game** (ช่อง port, แสดง IPv4 ของเครื่อง, คำอธิบาย LAN / port forward / virtual LAN / firewall), **Join Game** (ช่อง IP + port, Ctrl+V วางได้), Quit. ค่าที่พิมพ์จำไว้ใน PlayerPrefs. ไม่มี EventSystem: ปุ่ม/ช่องพิมพ์ hit-test เองผ่าน `InputX`
ไม่มี package / library เพิ่ม: `Net.cs` เขียนบน `System.Net.Sockets` (TCP, `NoDelay`), port เริ่มต้น **7777** (`Net.DefaultPort`). นอก LAN ต้อง forward TCP port หรือใช้ Radmin VPN / ZeroTier

### วิธีเล่น
- Host: Host Game > ตั้ง port > Start Hosting (Windows อาจถาม firewall ครั้งแรก ต้องกด allow) > บอก IP ที่หน้าจอแสดง + port ให้เพื่อน. มุมซ้ายของ HUD แสดง `HOSTING ip : port  players n / 4`
- Client: Join Game > พิมพ์ IP + port > Connect. ต่อไม่ได้ / ห้องเต็ม / เวอร์ชันไม่ตรง จะกลับมาหน้า Join พร้อมข้อความ. Host ปิดห้อง = client กลับเมนูพร้อมข้อความ "The host closed the game."
- **ทุกคนต้องใช้เวอร์ชันเดียวกัน** (`Net.Protocol` = 2 หลังอัปเดตแมพใหญ่; เวอร์ชันเก่า = "version" ถูกปฏิเสธ)

### สถาปัตยกรรม (host-authoritative)
- `Net.Mode` = Single / Host / Client; `Net.Authority` = ไม่ใช่ Client. Host จำลองโลกทั้งหมด (ศัตรู, แพ็ค, ดาเมจ, ดรอป, EXP, Soul, pickup); client จำลองเฉพาะอัศวินของตัวเอง; ฉากและ collision สร้างเองทุกเครื่องจาก seed 11 (ต้องตรงกัน)
- `Conn` = 1 TCP ต่อ client มี thread อ่าน + thread เขียนของตัวเอง main thread ไม่ block; ทุกอย่างเข้าคิวแล้วประมวลผลใน `Net.Poll()` (ต้น `Game.Update`) และส่งใน `Net.LateTick()` (`Game.LateUpdate`)
- เฟรมข้อความ: `[len u16][type u8][payload]` little endian (`NetW` / `NetR`); ตำแหน่งเป็น cm (i16 = ±327 ม. พอสำหรับแมพ ±190 ม.), มุมเป็น u16, HP เป็น u16 ทศนิยม 1 ตำแหน่ง
- Client ส่ง `State` ของอัศวินตัวเอง 20 ครั้ง/วินาที; host ส่ง `Snapshot` 15 ครั้ง/วินาที (ตัวนับแพ็ค, อัศวินทุกคน, ศัตรูทุกตัว, pickup ทุกอัน — เลวร้ายสุด 48 สไลม์ + 60 pickup ≈ 2 KB ต่อ snapshot ≈ 30 KB/s ต่อ client). อัศวินคนอื่นและศัตรูบน client เป็น puppet วาดย้อนหลัง 0.13 / 0.1 วินาทีด้วย `NetInterp` (interpolate)
- `Game.players` = อัศวินทุกคน (`Knight.pid`, `playerName`, `remote`); `Game.knight` = ของเครื่องนี้. สี + ป้ายชื่อมาจาก `Knight.MainColors` / `AccentColors` ตาม pid. HUD มี party list (ชื่อ เลเวล HP), ข้อความเข้า/ออก และบรรทัดสถานะ (`Net.StatusLine`, ping)

### ข้อความ (`enum Msg`, protocol = `Net.Protocol` 2)
| ทิศทาง | ข้อความ |
| --- | --- |
| client > host | `Hello` (magic, version, ชื่อ), `State`, `Act` (เริ่มท่าดาบ 0-4 / สกิล 10-13 / **dash 20**), `Hit` (ดาบฉันโดนศัตรู id นี้ ดาเมจเท่านี้), `Ping`, `Bye` |
| host > client | `Welcome` (pid + รายชื่อ), `Refuse` (เหตุผล), `Join`, `Leave`, `Snapshot` (pickup มี `kind`), `Act` (relay พร้อม pid), `EnemyHit` (ตัวเลข/ประกาย/HP ที่เหลือ/ตาย), `KnightHit` (มอนสเตอร์ตีอัศวินของคุณ), `Exp` (EXP + Soul), `PickupTaken` (id, pid, kind, item), `Banner`, `Reset` (F5), `Pong`, `Bye` |
| ทั้งสองทาง | `Test` (ใช้โดย `MpTest.cs` เท่านั้น) |

เปลี่ยนรูปแบบข้อความเมื่อไหร่ต้องเพิ่ม `Net.Protocol` (คนละเวอร์ชันจะถูก `Refuse`). การรีเซ็ตตอนตายไม่ส่งข้อความ: client รีเซ็ตเลเวลเอง เพื่อนเห็น Lv 1 ผ่าน `State`

### Authority / trust model
- Host เป็นคนตัดสิน HP ศัตรู, การตาย, EXP, Soul, ดรอป, ใครเก็บของ, การเกิดของสไลม์ / บอส และ F5
- Client ถูกเชื่อในเรื่องของอัศวินตัวเอง: ตำแหน่ง, HP/เกราะ (รวม i-frame ของ dash และการกินอาหาร), เลเวล, ดาบที่ถือ, การบล็อก (`KnightHit` ให้ client คิดบล็อก/เกราะเอง) และดาเมจที่รายงานใน `Hit`. Host ตรวจแบบหลวมๆ เท่านั้น (`Game.ClientHit`: ศัตรูยังไม่ตาย, ดาเมจ 0..20000, ระยะจากอัศวิน/ศัตรูสมเหตุผล, ไม่เกิน 150 hit/วินาที, ข้อความพังหรือเงียบ 10 วินาที = ตัดการเชื่อมต่อ) — ออกแบบสำหรับเล่นกับเพื่อน ไม่กันโกง ไม่เข้ารหัส ไม่มีรหัสผ่านห้อง

### กฎ multiplayer
- EXP / Soul: ผู้เล่นทุกคนที่ยังไม่ตายได้เต็มจำนวนต่อการฆ่า 1 ครั้ง; จำนวน kill นับให้คนที่ปิดฉากเท่านั้น
- ศัตรูเล็งผู้เล่นที่ยังไม่ตายที่ใกล้ที่สุด (`Game.NearestKnight`); แพ็คสไลม์เกิดรอบผู้เล่นแต่ละคน (คนแยกกันไกล = ต่างคนต่างมีแพ็ค)
- HP + เกราะศัตรู x(1 + 0.6 ต่อผู้เล่นเพิ่ม 1 คน) (`Game.CoopHpPerPlayer`) และใช้เลเวลเฉลี่ยของปาร์ตี้ — คิด **ตอนศัตรูเกิด**
- ดาบจากสไลม์: ใครเดินทับก่อนได้. ดาบบอส: Minotaur ดรอปคนละ 1 เล่ม แต่ละคนเห็นและเก็บได้เฉพาะของตัวเอง. อาหาร: ใครเดินทับก่อนที่ HP ไม่เต็มได้
- เลเวล / EXP / อัปเกรด / แต้มสเตตัส / Soul / inventory / ดาบที่ใส่ เป็นของแต่ละคน (อยู่บนเครื่องของคนนั้น)
- ออนไลน์ไม่มีการหยุดเกม: หน้าต่างเลเวลอัป / Tab เป็น overlay โลกเดินต่อ ยังเดินได้ และได้โล่ป้องกัน 6 วินาทีตอนหน้าต่างเลเวลอัปเปิด (`Game.LevelUpProtect`); hit-stop ปิดตอนออนไลน์
- ตาย: ฟื้นที่เมือง (จุดเกิดเรียงข้างกัน 1.8 ม.) หลัง 5 วินาที (`ReviveDelayCoop`; single player 3 วินาที) เลเวลกลับเป็น 1. ทุกคนล้มพร้อมกัน = สไลม์ทั้งหมดถูกลบ แพ็คเริ่มนับใหม่ Minotaur กลับรังเลือดเต็ม (`Game.OnPartyWipe`)
- F5 เฉพาะ host (รีเซ็ตทุกคนเป็นเลเวล 1 กลับเมือง). ห้องเต็ม 4 คน คนที่ 5 ถูกปฏิเสธ. คนเข้ากลางเกมได้ (เริ่มเลเวล 1 ในเมือง)

### Command line (`Args.cs`)
`-single` ข้ามเมนูเข้า single player, `-host` / `-join <ip>` พร้อม `-port <n>` `-name <ชื่อ>`, `-bind <ip>` (host ฟังเฉพาะ address นั้น; เทสต์ใช้ 127.0.0.1 จึงไม่เด้ง firewall), `-netversion <n>` (ปลอมเวอร์ชันเพื่อทดสอบ), `-joindelay <วินาที>`, `-selftest <โฟลเดอร์>`, `-mptest <โฟลเดอร์> -role H|A|B|C -expect <จำนวน client> [-refusals] [-expecttext <ข้อความ>]`, `-menutest <โฟลเดอร์>`

### ทดสอบหลายโปรเซส (เฉพาะ 127.0.0.1)
- `powershell -ExecutionPolicy Bypass -File mptest.ps1` เปิด host 1 + client 3 (Alice / Bob / Cara) + 3 ตัวที่ต้องเข้าไม่ได้ (ห้องเต็ม, เวอร์ชันไม่ตรง, port ที่ไม่มีใคร host) หน้าต่าง 960x540 ใช้เวลาราว 2.5 นาที แล้วสรุป PASS / FAIL ต่อ role; log + ภาพอยู่ที่ `mptest\<role>\` (host เป็นคนสั่งผ่าน `Msg.Test`, เช็คส่วนใหญ่เทียบ summary ของทุกโปรเซส)
- ทุกคนเริ่มในเมือง (ปลอดภัย ไม่มีสไลม์): สคริปต์ทดสอบเดินทุกคนไปลานกลางก่อนแล้วรอแพ็ค จากนั้นปิดผู้กำกับด้วย `packsOff` และใช้ `EnsureSlimes` เมื่อต้องมีสไลม์; มีเช็คตำแหน่ง 4 มุมแมพ (±5 ซม. ทุกจอ)
- **รู้อยู่แล้ว**: เช็ค "A's walk looks smooth on B" (การเดินของ client A เนียนบนจอ client B) ตกบนเครื่องนี้ตั้งแต่ commit `4b146ba` (ก่อนอัปเดตแมพใหญ่) ตอนรัน 7 หน้าต่างพร้อมกัน — ยังไม่ได้ตามหาสาเหตุ (buffer margin ติดลบ ~0.4 วิบน B)
- เมนู: `Builds\MedievalKnight.exe -menutest <โฟลเดอร์เต็ม> -port 47821 -bind 127.0.0.1 -screen-fullscreen 0 -logFile <log>` ดูบรรทัด `MENUTEST`
- ยังไม่ครอบคลุม: เครือข่ายจริง (LAN / internet / latency / packet loss), ทุกคนล้มพร้อมกัน (`OnPartyWipe`), การสเกล HP ตามจำนวนคน, คนเข้ากลางเกมหลังมีการต่อสู้ไปแล้ว, firewall prompt ตอน host แบบปกติ (bind ทุก address), ประสิทธิภาพบนเครื่องอ่อน / จอ 4K
