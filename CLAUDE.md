# Medieval Knight

เกม action RPG 3D มุมมอง top-down (กล้องเอียง 56° ตามตัวอัศวิน): อัศวินดาบ+โล่ สู้กับสไลม์เป็นเวฟ และบอส Minotaur ถือขวานสองคม (labrys) มีหุ่นฟางไว้ซ้อม
มีระบบเลเวล 1-10 (EXP โตแบบ exponential) เลเวลอัปแล้วสุ่มการ์ด 3 ใบให้เลือก 1 ใบ และสกิล 4 อัน
Unity `6000.6.5f1` (URP Universal Renderer แบบ 3D มีแสง directional + เงา, Input System ใหม่, uGUI)
เขียนด้วยโค้ดล้วน: ฉาก โมเดล (ประกอบจาก mesh ที่สร้างในโค้ด สไตล์ low-poly) เอฟเฟกต์ และ UI ถูกสร้างตอนรันไทม์ ไม่มี prefab ไม่มีไฟล์โมเดล ข้อความในเกมเป็นภาษาอังกฤษ
ขอโดย test_disk (Discord 449209885138223116) — เวอร์ชัน 2.5D เดิมอยู่ที่ commit `e8b39dd`

## โครงสร้างไฟล์

| ที่อยู่ | หน้าที่ |
| --- | --- |
| `Assets/MedievalKnight/Scripts/Game.cs` | จุดเริ่มเกม (`Boot`), กล้อง top-down, แสง, สร้างฉาก (สนาม รั้ว ต้นไม้ เสา), เกิดสไลม์เป็นเวฟ / Minotaur เกิดใหม่, แจก EXP, เปิดหน้าต่างเลเวลอัป (หยุดเกม), camera shake, hit-stop, ปุ่ม F1 / F5 / H / Esc |
| `Assets/MedievalKnight/Scripts/Knight.cs` | ผู้เล่น: เดิน WASD หันตามเมาส์, HP / Armor / Stamina, บล็อกด้วยโล่, ตารางท่าดาบ `Knight.Attacks` (ฟัน 4 ท่า + แทง 1 ท่า เป็นคอมโบ), ตารางสกิล `Knight.Skills` (Q E R F) |
| `Assets/MedievalKnight/Scripts/Progress.cs` | เลเวล / EXP (`Need(lvl) = round(30 * 1.5^(lvl-1))`), ผลรวมอัปเกรด Damage / Attack Speed / Crit, ค่าเพดาน, สุ่มการ์ด 3 ใบ (`MakeOffers`) |
| `Assets/MedievalKnight/Scripts/Enemy.cs` | คลาสแม่ของศัตรู: HP + Armor (เกราะรับก่อน), กระเด็น, สตัน, แถบเลือดลอยเหนือหัว, ตัวเลขดาเมจ |
| `Assets/MedievalKnight/Scripts/Slime.cs` | สไลม์ 3 สี (เขียว / น้ำเงิน / แดง) กระโดดไล่ผู้เล่น ยืด-หดตัว |
| `Assets/MedievalKnight/Scripts/Minotaur.cs` | บอส: โมเดล + ขวาน labrys, AI 3 ท่า (Sweep / Smash / Charge) มีเครื่องหมายสีแดงบนพื้นเตือนก่อนโจมตี |
| `Assets/MedievalKnight/Scripts/Dummy.cs` | หุ่นฟาง: ไม่โจมตี ไม่ให้ EXP โยกเมื่อโดนตี พังแล้วเกิดใหม่ |
| `Assets/MedievalKnight/Scripts/Fx.cs` | เอฟเฟกต์ทำเอง: อนุภาคลูกบาศก์ และ mesh แบนบนพื้นที่จางหาย (เส้นฟัน, วงคลื่น, เส้นพุ่ง, เครื่องหมายเตือน) |
| `Assets/MedievalKnight/Scripts/Hud.cs` | UI ทั้งหมด (uGUI, `UnityEngine.UI.Text` แบบ legacy): แถบ 3 แถบ + เลเวล/EXP, ผลรวมอัปเกรด, ไอคอนสกิล + คูลดาวน์, แถบบอส, การ์ดเลเวลอัป, ตัวเลขดาเมจ, ปุ่มควบคุม |
| `Assets/MedievalKnight/Scripts/Prims.cs` | สร้าง mesh low-poly ในโค้ด (`MeshB`, `Cube`, `Lathe`, `Strip`, `Arc`) และ material (สำเนาจาก `Resources/Mats`), `Tint` สำหรับกะพริบตอนโดนตี |
| `Assets/MedievalKnight/Scripts/InputX.cs` | ตัวกลางรับปุ่มและเมาส์ (`#if ENABLE_INPUT_SYSTEM`) ปุ่มใหม่ต้องเพิ่มใน `Map()` |
| `Assets/MedievalKnight/Scripts/SelfTest.cs` | ทดสอบอัตโนมัติ ทำงานเฉพาะเมื่อรัน `MedievalKnight.exe -selftest <โฟลเดอร์>` (เดิน ตี ใช้สกิล ฆ่าสไลม์ สู้ Minotaur เลเวลอัป เลือกการ์ด เก็บภาพหน้าจอ เขียน log `SELFTEST PASS/FAIL` แล้วปิดเอง) |
| `Assets/MedievalKnight/Resources/Mats/` | material ฐาน 3 ตัว (`Lit`, `Unlit`, `Fx` แบบโปร่งใส) — `BuildScript.Setup3D` สร้างให้เอง มีไว้เพื่อให้ shader ของ URP ติดไปกับ build |
| `Assets/Settings/Renderer3D.asset` | URP Universal Renderer (สร้างโดย `BuildScript.Setup3D` และผูกกับ `UniversalRP.asset`) |
| `Assets/Scenes/Main.unity` | ฉากหลัก มีแค่ Main Camera |
| `Assets/Editor/BuildScript.cs` | `BuildWindows`, `Setup3D` (renderer 3D + เงา + material ฐาน), `CreateMainScene` |
| `build.bat` | บิลด์ Windows ด้วย Unity batchmode → `Builds\MedievalKnight.exe`, log ที่ `build.log` |
| `_reference/` | ไฟล์อ้างอิง 2.5D เดิมของผู้ขอ (ไม่ได้ใช้ในเกมแล้ว เก็บไว้) |

## ปุ่ม

WASD หรือลูกศร เดิน, เมาส์ = เล็ง (อัศวินหันตามเคอร์เซอร์เสมอ), คลิกซ้าย / J / Space = คอมโบดาบ (Side Slash > Overhead Chop > Rising Slash > Spin Slash > Thrust),
คลิกขวาค้าง / Left Shift ค้าง = ยกโล่, Q Whirlwind, E Dash Thrust, R Shield Bash, F Ground Slam (กระโดดไปที่เคอร์เซอร์),
1 / 2 / 3 หรือคลิกการ์ด = เลือกอัปเกรดตอนเลเวลอัป, H โดนตีทดสอบ 20, F5 เริ่มใหม่ (กลับเลเวล 1), F1 ซ่อนปุ่มควบคุม, Esc ออก

## ระบบเลเวลและอัปเกรด

- EXP ที่ต้องใช้จากเลเวล L ไป L+1 = `round(30 * 1.5^(L-1))` → 30, 45, 68, 101, 152, 228, 342, 513, 769 (รวม 2248) เลเวลสูงสุด 10
- EXP: สไลม์เขียว 10 / น้ำเงิน 24 / แดง 48, Minotaur 250, หุ่นฟาง 0 — ตายแล้วเสีย EXP ของเลเวลปัจจุบัน
- เลเวลอัป: HP สูงสุด +10, เติมเลือดเต็ม, สุ่มการ์ด 3 ใบ (1 ใบสำหรับ ALL ATTACKS สุ่ม +0.5x ถึง +1.0x, 2 ใบสำหรับสกิลเดี่ยว สุ่ม +1.0x ถึง +2.0x)
- `+Nx` คือบวกเข้าตัวคูณตรงๆ: Damage (เริ่ม x1.0, เพดาน x10), Attack Speed (เริ่ม x1.0, เพดาน x4; ดาบเร็วขึ้นสูงสุด x3 และใช้ stamina ต่อครั้งน้อยลงตามสัดส่วน, คูลดาวน์สกิล = ฐาน / ตัวคูณ), Crit (ตัวคูณคริเริ่ม x1.5 เพดาน x10, โอกาสคริเริ่ม 10% +5% ต่อ 1.0x เพดาน 75%)
- ศัตรูที่เกิดใหม่เก่งขึ้นตามเลเวลผู้เล่น: HP x(1 + 0.45 ต่อเลเวล), ดาเมจ x(1 + 0.08 ต่อเลเวล)

## แนวทางแก้โค้ด

- ปรับ/เพิ่มท่าดาบ: แก้ `Knight.Attacks` (มุม yaw/pitch ของดาบ, เวลา, ดาเมจ, stamina, ระยะ, ครึ่งมุมกวาด) — ปรับสกิล: `Knight.Skills` และ coroutine ของสกิลใน `Knight.cs`
- ปรับสมดุลเลเวล/อัปเกรด: ค่าคงที่ใน `Progress.cs`; ความแข็งของศัตรู: `Game.EnemyHpScale`, ตารางใน `Slime.cs`, ค่าคงที่ใน `Minotaur.cs`
- เพิ่มศัตรูใหม่: สืบทอด `Enemy`, สร้างโมเดลด้วย `Prims.Box / Part / Lathe / Strip`, เรียก `BaseTick(dt)` ทุกเฟรม แล้วให้ `Game` เป็นคน spawn
- สีทุกอย่างมาจาก `Prims.Lit(color, smoothness, metallic)` / `Prims.Glow(color)` ไม่ต้องสร้างไฟล์ material เพิ่ม
- หลังแก้ ให้รัน `build.bat` แล้วลอง `Builds\MedievalKnight.exe -selftest selftest -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile player.log` ดู log บรรทัด `SELFTEST` (ต้องไม่มี `FAIL`)
