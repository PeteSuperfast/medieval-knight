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
H โดนตีทดสอบ 20, F5 เริ่มใหม่ (กลับเลเวล 1 ล้างอัปเกรด แต้มสเตตัส และดาบ เหลือ Iron Sword), F1 ซ่อนปุ่มควบคุม, Esc ออก

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
- สำหรับ co-op: ข้อมูลผู้เล่นอยู่บน `Knight` (`progress`, `inventory`) แล้ว แต่ยังมี `Game.I.knight` ตัวเดียว (ศัตรู, HUD, กล้อง, `UpdatePickups`, หน้าต่างที่หยุดเกมด้วย `Time.timeScale = 0`) และ `Enemy.All` / `Pickup.All` / `Progress.ForceCrit` เป็น static
- เพิ่มศัตรูใหม่: สืบทอด `Enemy`, สร้างโมเดลด้วย `Prims.Box / Part / Lathe / Strip`, เรียก `BaseTick(dt)` ทุกเฟรม แล้วให้ `Game` เป็นคน spawn
- สีทุกอย่างมาจาก `Prims.Lit(color, smoothness, metallic)` / `Prims.Glow(color)` ไม่ต้องสร้างไฟล์ material เพิ่ม
- หลังแก้ ให้รัน `build.bat` แล้วลอง `Builds\MedievalKnight.exe -selftest selftest -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile player.log` ดู log บรรทัด `SELFTEST` (ต้องไม่มี `FAIL`)
