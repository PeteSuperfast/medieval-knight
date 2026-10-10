# Medieval Knight

เกม action RPG 3D มุมมอง top-down (กล้องเอียง 56° ตามตัวอัศวิน): อัศวินดาบ+โล่ สู้กับสไลม์เป็นเวฟ และบอส Minotaur ถือขวานสองคม (labrys) มีหุ่นฟางไว้ซ้อม
มีระบบเลเวล 1-10 (EXP โตแบบ exponential) เลเวลอัปแล้วสุ่มการ์ด 3 ใบให้เลือก 1 ใบ แล้วได้แต้มสเตตัส 1 แต้ม (HP / BLOCK / STAMINA) และสกิล 4 อัน
มีช่องอาวุธ 1 ช่อง + ดาบ 5 เล่ม (ดรอปจากสไลม์ / Minotaur เก็บโดยเดินทับ) และเสียงดาบ 6 ไฟล์
Unity `6000.6.5f1` (URP Universal Renderer แบบ 3D มีแสง directional + เงา, Input System ใหม่, uGUI)
เขียนด้วยโค้ดล้วน: ฉาก โมเดล (ประกอบจาก mesh ที่สร้างในโค้ด สไตล์ low-poly) เอฟเฟกต์ และ UI ถูกสร้างตอนรันไทม์ ไม่มี prefab ไม่มีไฟล์โมเดล ข้อความในเกมเป็นภาษาอังกฤษ
ขอโดย test_disk (Discord 449209885138223116) — เวอร์ชัน 2.5D เดิมอยู่ที่ commit `e8b39dd`

## โครงสร้างไฟล์

| ที่อยู่ | หน้าที่ |
| --- | --- |
| `Assets/MedievalKnight/Scripts/Game.cs` | จุดเริ่มเกม (`Boot`), กล้อง top-down, แสง, สร้างฉาก (สนาม รั้ว ต้นไม้ เสา), เกิดสไลม์เป็นเวฟ / Minotaur เกิดใหม่, แจก EXP, หน้าต่างเลเวลอัป 2 ขั้น (`LevelStep` 1 = การ์ด, 2 = แต้มสเตตัส; หยุดเกม), สุ่มดรอปดาบ (`RollDrop`), เก็บดาบ (`UpdatePickups` / `Collect`), เปิดหน้าต่างอาวุธ (`SetInventory`), camera shake, hit-stop, ปุ่ม F1 / F5 / H / I / M / Esc |
| `Assets/MedievalKnight/Scripts/Knight.cs` | ผู้เล่น: เดิน WASD หันตามเมาส์, HP / Armor / Stamina, บล็อกด้วยโล่, ตารางท่าดาบ `Knight.Attacks` (ฟัน 4 ท่า + แทง 1 ท่า เป็นคอมโบ), ตารางสกิล `Knight.Skills` (Q E R F) — ข้อมูลของผู้เล่นหนึ่งคนอยู่บน object นี้ทั้งหมด: `progress` (เลเวล อัปเกรด แต้มสเตตัส) และ `inventory` (ดาบที่มี + ช่องอาวุธ); ตัวเลขต่อสู้รวมดาบ: `DamageMult` / `CritChance` / `CritMult` / `SwingSpeed` / `Reach`, สุ่มดาเมจที่เดียวใน `RollDamage`, `Equip`, `SpendPoint` |
| `Assets/MedievalKnight/Scripts/Progress.cs` | เลเวล / EXP (`Need(lvl) = round(30 * 1.5^(lvl-1))`), ผลรวมอัปเกรด Damage / Attack Speed / Crit, ค่าเพดาน, สุ่มการ์ด 3 ใบ (`MakeOffers`), แต้มสเตตัส HP / BLOCK / STAMINA (`statPoints`, `attr[]`, `Spend`, ค่าต่อแต้มและเพดาน) — เป็น instance ของอัศวินแต่ละคน (`Game.progress` แค่ชี้ไปที่ `knight.progress`) |
| `Assets/MedievalKnight/Scripts/Weapon.cs` | `WeaponDef` + ตารางดาบ `Weapons.All` (สเตตัส + รูปร่าง), โอกาสดรอป, `Weapons.BuildModel` (ประกอบโมเดลดาบจาก `Prims`), `Inventory` (ดาบที่มี, ระดับ +N, ช่องที่ใส่อยู่ — ของอัศวินแต่ละคน), `Pickup` (ดาบที่ตกบนพื้น หมุน + วงแหวนเรืองแสง หายเองใน 90 วินาที) |
| `Assets/MedievalKnight/Scripts/Sfx.cs` | เสียงทั้งหมด: โหลด 6 คลิปจาก `Resources/Audio`, AudioSource 2D 8 ตัว (จำกัดเสียงซ้อน), สุ่ม pitch / volume เล็กน้อย (ใช้ `System.Random` แยก ไม่กวนเลขสุ่มของเกมเพลย์), ช่วงห่างขั้นต่ำต่อคลิป, เสียงร้อง `Grunt(heavy)`, ปุ่ม M เปลี่ยนระดับเสียง |
| `Assets/MedievalKnight/Scripts/Enemy.cs` | คลาสแม่ของศัตรู: HP + Armor (เกราะรับก่อน), กระเด็น, สตัน, แถบเลือดลอยเหนือหัว, ตัวเลขดาเมจ |
| `Assets/MedievalKnight/Scripts/Slime.cs` | สไลม์ 3 สี (เขียว / น้ำเงิน / แดง) กระโดดไล่ผู้เล่น ยืด-หดตัว |
| `Assets/MedievalKnight/Scripts/Minotaur.cs` | บอส: โมเดล + ขวาน labrys, AI 3 ท่า (Sweep / Smash / Charge) มีเครื่องหมายสีแดงบนพื้นเตือนก่อนโจมตี |
| `Assets/MedievalKnight/Scripts/Dummy.cs` | หุ่นฟาง: ไม่โจมตี ไม่ให้ EXP โยกเมื่อโดนตี พังแล้วเกิดใหม่ |
| `Assets/MedievalKnight/Scripts/Fx.cs` | เอฟเฟกต์ทำเอง: อนุภาคลูกบาศก์ และ mesh แบนบนพื้นที่จางหาย (เส้นฟัน, วงคลื่น, เส้นพุ่ง, เครื่องหมายเตือน) |
| `Assets/MedievalKnight/Scripts/Hud.cs` | UI ทั้งหมด (uGUI, `UnityEngine.UI.Text` แบบ legacy): แถบ 3 แถบ + เลเวล/EXP, ผลรวมอัปเกรด + ดาบที่ถือ + แต้มสเตตัส, ไอคอนสกิล + คูลดาวน์, แถบบอส, การ์ดเลเวลอัป / การ์ดแต้มสเตตัส (`ShowStatChoice`), หน้าต่างอาวุธ (`BuildInventoryPanel`, `InventoryInput`), ข้อความแจ้ง `Notice` (เก็บดาบ / ใส่ดาบ / ระดับเสียง), ตัวเลขดาเมจ, ปุ่มควบคุม |
| `Assets/MedievalKnight/Scripts/Prims.cs` | สร้าง mesh low-poly ในโค้ด (`MeshB`, `Cube`, `Lathe`, `Strip`, `Arc`) และ material (สำเนาจาก `Resources/Mats`), `Tint` สำหรับกะพริบตอนโดนตี |
| `Assets/MedievalKnight/Scripts/InputX.cs` | ตัวกลางรับปุ่มและเมาส์ (`#if ENABLE_INPUT_SYSTEM`) ปุ่มใหม่ต้องเพิ่มใน `Map()` |
| `Assets/MedievalKnight/Scripts/Net.cs` | Online co-op: `Conn` (TCP + thread อ่าน/เขียน), `NetW` / `NetR`, `enum Msg`, host / join / handshake / refuse / timeout, ส่ง snapshot และ hook ที่เกมเพลย์เรียก (`SendAct`, `SendHit`, `BroadcastEnemyHit` ...) |
| `Assets/MedievalKnight/Scripts/NetInterp.cs` | บัฟเฟอร์ interpolate ตำแหน่ง/มุมของ puppet (อัศวินคนอื่น, ศัตรูบน client) |
| `Assets/MedievalKnight/Scripts/Menu.cs` | เมนูเริ่มเกม Single / Host / Join / Connecting |
| `Assets/MedievalKnight/Scripts/Args.cs` | สวิตช์ command line |
| `Assets/MedievalKnight/Scripts/MpTest.cs` | ทดสอบออนไลน์หลายโปรเซส (`-mptest`) และทดสอบเมนู (`-menutest`); `mptest.ps1` เป็นตัวเปิด |
| `Assets/MedievalKnight/Scripts/SelfTest.cs` | ทดสอบอัตโนมัติ ทำงานเฉพาะเมื่อรัน `MedievalKnight.exe -selftest <โฟลเดอร์>` (เดิน ตี ใช้สกิล ฆ่าสไลม์ สู้ Minotaur เลเวลอัป เลือกการ์ด + แต้มสเตตัสด้วยคีย์และเมาส์ ดรอป/เก็บดาบ ใส่ดาบทั้ง 5 เล่มแล้ววัดดาเมจ / เวลาฟัน / คริ ตรวจว่าเสียงทั้ง 6 คลิปโหลดและเล่นตรงเหตุการณ์ เก็บภาพหน้าจอ เขียน log `SELFTEST PASS/FAIL` และ `SFX play ...` แล้วปิดเอง) |
| `Assets/MedievalKnight/Resources/Audio/` | เสียง mp3 6 ไฟล์ (Git LFS): `sword_swing`, `sword_slice`, `armor_impact`, `sword_clash`, `knight_grunt`, `knight_ready` — โหลดด้วย `Resources.Load<AudioClip>("Audio/<ชื่อ>")` ใน `Sfx.cs` |
| `Assets/MedievalKnight/Resources/Mats/` | material ฐาน 3 ตัว (`Lit`, `Unlit`, `Fx` แบบโปร่งใส) — `BuildScript.Setup3D` สร้างให้เอง มีไว้เพื่อให้ shader ของ URP ติดไปกับ build |
| `Assets/Settings/Renderer3D.asset` | URP Universal Renderer (สร้างโดย `BuildScript.Setup3D` และผูกกับ `UniversalRP.asset`) |
| `Assets/Scenes/Main.unity` | ฉากหลัก มีแค่ Main Camera |
| `Assets/Editor/BuildScript.cs` | `BuildWindows`, `Setup3D` (renderer 3D + เงา + material ฐาน), `CreateMainScene` |
| `build.bat` | บิลด์ Windows ด้วย Unity batchmode → `Builds\MedievalKnight.exe`, log ที่ `build.log` |
| `_reference/` | ไฟล์อ้างอิง 2.5D เดิมของผู้ขอ (ไม่ได้ใช้ในเกมแล้ว เก็บไว้) |

## ปุ่ม

WASD หรือลูกศร เดิน, เมาส์ = เล็ง (อัศวินหันตามเคอร์เซอร์เสมอ), คลิกซ้าย / J / Space = คอมโบดาบ (Side Slash > Overhead Chop > Rising Slash > Spin Slash > Thrust),
คลิกขวาค้าง / Left Shift ค้าง = ยกโล่, Q Whirlwind, E Dash Thrust, R Shield Bash, F Ground Slam (กระโดดไปที่เคอร์เซอร์),
1 / 2 / 3 หรือคลิกการ์ด = เลือกอัปเกรดตอนเลเวลอัป แล้วเลือกลงแต้มสเตตัส (1 HP / 2 BLOCK / 3 STAMINA),
**I** = เปิด/ปิดหน้าต่างอาวุธ + สเตตัส (หยุดเกม; กด 1-5 หรือคลิกแถว = ใส่ดาบที่มี), เดินทับดาบที่ตกบนพื้น = เก็บ, **M** = ระดับเสียง 100% > 60% > 30% > ปิดเสียง,
H โดนตีทดสอบ 20, F5 เริ่มใหม่ (กลับเลเวล 1 ล้างอัปเกรด แต้มสเตตัส และดาบ เหลือ Iron Sword; ออนไลน์กดได้เฉพาะ host และรีเซ็ตทุกคน), F1 ซ่อนปุ่มควบคุม, Esc สองครั้ง = กลับเมนู (host กด = ปิดห้อง), Esc ที่หน้าเมนูหลัก = ออกจากเกม

## ระบบเลเวลและอัปเกรด

- EXP ที่ต้องใช้จากเลเวล L ไป L+1 = `round(30 * 1.5^(L-1))` → 30, 45, 68, 101, 152, 228, 342, 513, 769 (รวม 2248) เลเวลสูงสุด 10
- EXP: สไลม์เขียว 10 / น้ำเงิน 24 / แดง 48, Minotaur 250, หุ่นฟาง 0 — ตายแล้วเสีย EXP ของเลเวลปัจจุบัน
- เลเวลอัป: HP สูงสุด +10 (อัตโนมัติ ยังอยู่เหมือนเดิม), เติมเลือดเต็ม, สุ่มการ์ด 3 ใบ (1 ใบสำหรับ ALL ATTACKS สุ่ม +0.5x ถึง +1.0x, 2 ใบสำหรับสกิลเดี่ยว สุ่ม +1.0x ถึง +2.0x) จากนั้นหน้าต่างเดียวกันเปลี่ยนเป็นขั้นที่ 2 ให้ลงแต้มสเตตัส 1 แต้ม
- `+Nx` คือบวกเข้าตัวคูณตรงๆ: Damage (เริ่ม x1.0, เพดาน x10), Attack Speed (เริ่ม x1.0, เพดาน x4; ดาบเร็วขึ้นสูงสุด x3 และใช้ stamina ต่อครั้งน้อยลงตามสัดส่วน, คูลดาวน์สกิล = ฐาน / ตัวคูณ), Crit (ตัวคูณคริเริ่ม x1.5 เพดาน x10, โอกาสคริเริ่ม 10% +5% ต่อ 1.0x เพดาน 75%)
- ศัตรูที่เกิดใหม่เก่งขึ้นตามเลเวลผู้เล่น: HP x(1 + 0.45 ต่อเลเวล), ดาเมจ x(1 + 0.08 ต่อเลเวล)

## แต้มสเตตัส (HP / BLOCK / STAMINA)

เลเวลอัป 1 ครั้ง = 1 แต้ม (รวมสูงสุด 9 แต้มที่เลเวล 10) เลือกในขั้นที่ 2 ของหน้าต่างเลเวลอัป (คีย์ 1 / 2 / 3 หรือคลิกการ์ด) ลงได้สูงสุด 6 แต้มต่อสเตตัส (`Progress.AttrCap`)
ตายแล้วไม่หาย, F5 ล้าง ค่าคงที่อยู่ใน `Progress.cs`

| สเตตัส | ต่อ 1 แต้ม | เต็ม 6 แต้ม |
| --- | --- | --- |
| HP | HP สูงสุด +20 (`HpPerPoint`) และเติมเลือด +20 ทันที | +120 (เลเวล 10: 100 + 90 + 120 = 310) |
| BLOCK | Armor สูงสุด +10 (`ArmorPerPoint`), ยกโล่กันดาเมจเพิ่ม +2% (`BlockPerPoint`; ฐาน 80%) | Armor 120, กัน 92% |
| STAMINA | Stamina สูงสุด +12 (`StaminaPerPoint`), ฟื้น stamina เร็วขึ้น +4% (`RegenPerPoint`; ฐาน 26/วินาที) | Stamina 172, ฟื้น +24% |

## ดาบและช่องอาวุธ

อัศวินมีช่องอาวุธ 1 ช่อง เริ่มด้วย Iron Sword ดาบที่เก็บได้เข้า `knight.inventory` กด I แล้วเลือกใส่ โมเดลดาบในมือเปลี่ยนทันที (`Knight.Equip` > `Weapons.BuildModel`)
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
- ความเร็วฟันคอมโบ = `clamp(min(3, 1 + อัปเกรด) x Speed ของดาบ, 0.4, 4)`; stamina ต่อครั้ง = ค่าฐาน / ความเร็วฟัน (ดาบช้ากิน stamina มากขึ้น ดาบเร็วกินน้อยลง); คูลดาวน์สกิลไม่ขึ้นกับดาบ
- Reach = ระยะของท่าคอมโบ x Reach ของดาบ (สกิลไม่เปลี่ยน)

ดรอป (สุ่มที่ `Game.RollDrop` ที่เดียว):
- สไลม์เขียว 4% / น้ำเงิน 10% / แดง 20% ต่อตัว (`Weapons.SlimeDropChance`) ได้ดาบ 1 ใน 3 เล่มตามสัดส่วน 45 / 35 / 20 (`SlimeDropWeights`)
- Minotaur ดรอป Horned King's Blade 100% ทุกครั้งที่ตาย (`MinotaurDropChance`), หุ่นฟางไม่ดรอป
- ดาบที่ตกลอยหมุนมีวงแหวน + ลำแสงสีตามระดับ เดินเข้าใกล้ 1.25 ม. = เก็บ มีข้อความแจ้งเหนือแถบสกิล อยู่ได้ 90 วินาที (กะพริบ 8 วินาทีสุดท้าย)
- ได้ดาบซ้ำ: ดาบเล่มนั้นเป็น +1 ถึง +5 (ดาเมจของดาบ +5% ต่อขั้น, `Weapons.DamagePerPlus`); ซ้ำอีกหลัง +5 ได้ 15 EXP แทน
- ตายแล้วดาบไม่หาย, F5 ล้างเหลือ Iron Sword และลบดาบที่ตกอยู่บนพื้น

## เสียง

ไฟล์ทั้ง 6 มาจากผู้ขอ (ชื่อไฟล์ตั้งใหม่ตามที่เดาจากชื่อเดิม) อยู่ใน `Resources/Audio` เล่นผ่าน `Sfx.I.Play(Snd.X)`:

| คลิป | เล่นเมื่อ |
| --- | --- |
| `sword_swing` | ตอนเริ่มจังหวะฟันของคอมโบทุกท่า (ไม่ใช่ตอนง้าง), Whirlwind 3 ครั้ง, Dash Thrust, ตอนกระโดด Ground Slam — pitch ต่ำลงสำหรับดาบช้า สูงขึ้นสำหรับดาบเร็ว |
| `sword_slice` | ดาบโดนศัตรูแล้วดาเมจส่วนใหญ่เข้า HP |
| `armor_impact` | ดาบโดนศัตรูแล้วเกราะรับส่วนใหญ่ (สไลม์แดง หุ่นฟาง), Shield Bash โดน, อัศวินโดนตี (ไม่ได้บล็อก) แล้วเกราะรับส่วนใหญ่ |
| `sword_clash` | บล็อกด้วยโล่สำเร็จ, ดาบโดน Minotaur ตอนเกราะยังรับอยู่ (`Enemy.ArmorSound`) |
| `knight_grunt` | ใช้สกิลทุกครั้ง, ท่าคอมโบหนัก (Chop / Spin / Thrust) ทุกครั้ง, ท่าเบาสุ่ม 25% และห่างกันอย่างน้อย 1.8 วินาที — ทั้งหมดห่างกันอย่างน้อย 0.9 วินาที |
| `knight_ready` | เริ่มเกม, ฟื้นหลังตาย, กด F5 |

ต่อการฟัน 1 ครั้งเล่นเสียงโดนอย่างมากชนิดละ 1 ครั้งไม่ว่าจะโดนกี่ตัว (`Knight.HitArea`) ค่า volume / pitch / ช่วงห่างของแต่ละคลิปอยู่ต้นไฟล์ `Sfx.cs`
ไฟล์ mp3 import ด้วยค่า default (ไม่ preload) จึงเรียก `LoadAudioData()` ตอนเริ่มเกม

## แนวทางแก้โค้ด

- ปรับ/เพิ่มท่าดาบ: แก้ `Knight.Attacks` (มุม yaw/pitch ของดาบ, เวลา, ดาเมจ, stamina, ระยะ, ครึ่งมุมกวาด) — ปรับสกิล: `Knight.Skills` และ coroutine ของสกิลใน `Knight.cs`
- ปรับสมดุลเลเวล/อัปเกรด/แต้มสเตตัส: ค่าคงที่ใน `Progress.cs`; ความแข็งของศัตรู: `Game.EnemyHpScale`, ตารางใน `Slime.cs`, ค่าคงที่ใน `Minotaur.cs`
- เพิ่ม/ปรับดาบ: เพิ่มรายการใน `Weapons.All` (id ต้องตรงกับลำดับ) ถ้าเป็นรูปทรงการ์ดใหม่ให้เพิ่ม `GuardStyle` + case ใน `Weapons.BuildModel`; ถ้าเกิน 5 เล่มต้องเพิ่มปุ่มใน `Hud.InventoryInput` และ `InputX.Map()`; โอกาสดรอปอยู่ต้นคลาส `Weapons`
- เพิ่มเสียง: เพิ่มชื่อใน enum `Snd` และตาราง `Files` / `BaseVolume` / `PitchVar` / `MinGap` ใน `Sfx.cs` (ลำดับต้องตรงกัน) แล้ววางไฟล์ใน `Resources/Audio`
- co-op: ดูหัวข้อ "Online co-op" ด้านล่าง — โค้ดเกมเพลย์ห้ามถือว่า `Game.I.knight` คือผู้เล่นคนเดียว (ใช้ `Game.I.players` / `NearestKnight`), ทุกอย่างที่เปลี่ยนโลก (ดาเมจศัตรู, spawn, ดรอป, EXP) ต้องอยู่หลัง `Net.Authority`, และห้ามหยุดเวลาตอนออนไลน์ (ดู `Game.Paused`)
- เพิ่มศัตรูใหม่: สืบทอด `Enemy`, สร้างโมเดลด้วย `Prims.Box / Part / Lathe / Strip`, เรียก `BaseTick(dt)` ทุกเฟรม แล้วให้ `Game` เป็นคน spawn
- สีทุกอย่างมาจาก `Prims.Lit(color, smoothness, metallic)` / `Prims.Glow(color)` ไม่ต้องสร้างไฟล์ material เพิ่ม
- หลังแก้ ให้รัน `build.bat` แล้วลอง `Builds\MedievalKnight.exe -selftest selftest -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile player.log` ดู log บรรทัด `SELFTEST` (ต้องไม่มี `FAIL`) แล้วรัน `powershell -ExecutionPolicy Bypass -File mptest.ps1` (ทดสอบออนไลน์ 4 คนบนเครื่องเดียว ดูหัวข้อ Online co-op)

## Online co-op (สูงสุด 4 คน, ขอโดย test_disk)

เกมเริ่มที่เมนู (`Menu.cs`): ช่องชื่อผู้เล่น, **Single Player**, **Host Game** (ช่อง port, แสดง IPv4 ของเครื่อง, คำอธิบาย LAN / port forward / virtual LAN / firewall), **Join Game** (ช่อง IP + port, Ctrl+V วางได้), Quit. ค่าที่พิมพ์จำไว้ใน PlayerPrefs. ไม่มี EventSystem: ปุ่ม/ช่องพิมพ์ hit-test เองผ่าน `InputX`
ไม่มี package / library เพิ่ม: `Net.cs` เขียนบน `System.Net.Sockets` (TCP, `NoDelay`), port เริ่มต้น **7777** (`Net.DefaultPort`). นอก LAN ต้อง forward TCP port หรือใช้ Radmin VPN / ZeroTier

### วิธีเล่น
- Host: Host Game > ตั้ง port > Start Hosting (Windows อาจถาม firewall ครั้งแรก ต้องกด allow) > บอก IP ที่หน้าจอแสดง + port ให้เพื่อน. มุมซ้ายของ HUD แสดง `HOSTING ip : port  players n / 4`
- Client: Join Game > พิมพ์ IP + port > Connect. ต่อไม่ได้ / ห้องเต็ม / เวอร์ชันไม่ตรง จะกลับมาหน้า Join พร้อมข้อความ. Host ปิดห้อง = client กลับเมนูพร้อมข้อความ "The host closed the game."

### สถาปัตยกรรม (host-authoritative)
- `Net.Mode` = Single / Host / Client; `Net.Authority` = ไม่ใช่ Client. Host จำลองโลกทั้งหมด (ศัตรู, เวฟ, ดาเมจ, ดรอป, EXP, pickup); client จำลองเฉพาะอัศวินของตัวเอง
- `Conn` = 1 TCP ต่อ client มี thread อ่าน + thread เขียนของตัวเอง main thread ไม่ block; ทุกอย่างเข้าคิวแล้วประมวลผลใน `Net.Poll()` (ต้น `Game.Update`) และส่งใน `Net.LateTick()` (`Game.LateUpdate`)
- เฟรมข้อความ: `[len u16][type u8][payload]` little endian (`NetW` / `NetR`); ตำแหน่งเป็น cm (i16), มุมเป็น u16, HP เป็น u16 ทศนิยม 1 ตำแหน่ง
- Client ส่ง `State` ของอัศวินตัวเอง 20 ครั้ง/วินาที; host ส่ง `Snapshot` 15 ครั้ง/วินาที (ตัวนับเวฟ, อัศวินทุกคน, ศัตรูทุกตัว, pickup ทุกอัน). อัศวินคนอื่นและศัตรูบน client เป็น puppet วาดย้อนหลัง 0.13 / 0.1 วินาทีด้วย `NetInterp` (interpolate)
- `Game.players` = อัศวินทุกคน (`Knight.pid`, `playerName`, `remote`); `Game.knight` = ของเครื่องนี้. สี + ป้ายชื่อมาจาก `Knight.MainColors` / `AccentColors` ตาม pid. HUD มี party list (ชื่อ เลเวล HP), ข้อความเข้า/ออก และบรรทัดสถานะ (`Net.StatusLine`, ping)

### ข้อความ (`enum Msg`, protocol = `Net.Protocol` 1)
| ทิศทาง | ข้อความ |
| --- | --- |
| client > host | `Hello` (magic, version, ชื่อ), `State`, `Act` (เริ่มท่าดาบ 0-4 / สกิล 10-13), `Hit` (ดาบฉันโดนศัตรู id นี้ ดาเมจเท่านี้), `Ping`, `Bye` |
| host > client | `Welcome` (pid + รายชื่อ), `Refuse` (เหตุผล), `Join`, `Leave`, `Snapshot`, `Act` (relay พร้อม pid), `EnemyHit` (ตัวเลข/ประกาย/HP ที่เหลือ/ตาย), `KnightHit` (มอนสเตอร์ตีอัศวินของคุณ), `Exp`, `PickupTaken`, `Banner`, `Reset` (F5), `Pong`, `Bye` |
| ทั้งสองทาง | `Test` (ใช้โดย `MpTest.cs` เท่านั้น) |

เปลี่ยนรูปแบบข้อความเมื่อไหร่ต้องเพิ่ม `Net.Protocol` (คนละเวอร์ชันจะถูก `Refuse`)

### Authority / trust model
- Host เป็นคนตัดสิน HP ศัตรู, การตาย, EXP, ดรอป, ใครเก็บดาบ, เวฟ และ F5
- Client ถูกเชื่อในเรื่องของอัศวินตัวเอง: ตำแหน่ง, HP/เกราะ, เลเวล, ดาบที่ถือ, การบล็อก (`KnightHit` ให้ client คิดบล็อก/เกราะเอง) และดาเมจที่รายงานใน `Hit`. Host ตรวจแบบหลวมๆ เท่านั้น (`Game.ClientHit`: ศัตรูยังไม่ตาย, ดาเมจ 0..20000, ระยะจากอัศวิน/ศัตรูสมเหตุผล, ไม่เกิน 150 hit/วินาที, ข้อความพังหรือเงียบ 10 วินาที = ตัดการเชื่อมต่อ) — ออกแบบสำหรับเล่นกับเพื่อน ไม่กันโกง ไม่เข้ารหัส ไม่มีรหัสผ่านห้อง

### กฎ multiplayer
- EXP: ผู้เล่นทุกคนที่ยังไม่ตายได้ EXP เต็มจำนวนต่อการฆ่า 1 ครั้ง; จำนวน kill นับให้คนที่ปิดฉากเท่านั้น
- ศัตรูเล็งผู้เล่นที่ยังไม่ตายที่ใกล้ที่สุด (`Game.NearestKnight`)
- HP + เกราะศัตรู x(1 + 0.6 ต่อผู้เล่นเพิ่ม 1 คน) (`Game.CoopHpPerPlayer`) และใช้เลเวลเฉลี่ยของปาร์ตี้ — คิด **ตอนศัตรูเกิด** (ศัตรูที่เกิดก่อนเพื่อนเข้ามาไม่ถูกปรับย้อนหลัง)
- ดาบจากสไลม์: ใครเดินทับก่อนได้. ดาบบอส: Minotaur ดรอปคนละ 1 เล่ม แต่ละคนเห็นและเก็บได้เฉพาะของตัวเอง
- เลเวล / EXP / อัปเกรด / แต้มสเตตัส / inventory / ดาบที่ใส่ เป็นของแต่ละคน (อยู่บนเครื่องของคนนั้น)
- ออนไลน์ไม่มีการหยุดเกม: หน้าต่างเลเวลอัป / อาวุธเป็น overlay โลกเดินต่อ ยังเดินได้ และได้โล่ป้องกัน 6 วินาทีตอนหน้าต่างเลเวลอัปเปิด (`Game.LevelUpProtect`); hit-stop ปิดตอนออนไลน์
- ตาย: ฟื้นที่จุดเกิดหลัง 5 วินาที (`ReviveDelayCoop`; single player 1.4 วินาที) เสีย EXP ของเลเวลปัจจุบัน. ทุกคนล้มพร้อมกัน = สไลม์ของเวฟนั้นถูกลบและเวฟเริ่มใหม่ Minotaur กลับรังเลือดเต็ม (`Game.OnPartyWipe`)
- F5 เฉพาะ host (รีเซ็ตทุกคนเป็นเลเวล 1). ห้องเต็ม 4 คน คนที่ 5 ถูกปฏิเสธ. คนเข้ากลางเกมได้ (เริ่มเลเวล 1)

### Command line (`Args.cs`)
`-single` ข้ามเมนูเข้า single player, `-host` / `-join <ip>` พร้อม `-port <n>` `-name <ชื่อ>`, `-bind <ip>` (host ฟังเฉพาะ address นั้น; เทสต์ใช้ 127.0.0.1 จึงไม่เด้ง firewall), `-netversion <n>` (ปลอมเวอร์ชันเพื่อทดสอบ), `-joindelay <วินาที>`, `-selftest <โฟลเดอร์>`, `-mptest <โฟลเดอร์> -role H|A|B|C -expect <จำนวน client> [-refusals] [-expecttext <ข้อความ>]`, `-menutest <โฟลเดอร์>`

### ทดสอบหลายโปรเซส (เฉพาะ 127.0.0.1)
- `powershell -ExecutionPolicy Bypass -File mptest.ps1` เปิด host 1 + client 3 (Alice / Bob / Cara) + 3 ตัวที่ต้องเข้าไม่ได้ (ห้องเต็ม, เวอร์ชันไม่ตรง, port ที่ไม่มีใคร host) หน้าต่าง 960x540 ใช้เวลาราว 1.5 นาที แล้วสรุป PASS / FAIL ต่อ role; log + ภาพอยู่ที่ `mptest\<role>\` (host เป็นคนสั่งผ่าน `Msg.Test`, เช็คส่วนใหญ่เทียบ summary ของทุกโปรเซส)
- เมนู: `Builds\MedievalKnight.exe -menutest <โฟลเดอร์เต็ม> -port 47821 -bind 127.0.0.1 -screen-fullscreen 0 -logFile <log>` ดูบรรทัด `MENUTEST`
- ยังไม่ครอบคลุม: เครือข่ายจริง (LAN / internet / latency / packet loss), ทุกคนล้มพร้อมกัน (`OnPartyWipe`), การสเกล HP ตามจำนวนคน, คนเข้ากลางเกมหลังมีการต่อสู้ไปแล้ว, firewall prompt ตอน host แบบปกติ (bind ทุก address)