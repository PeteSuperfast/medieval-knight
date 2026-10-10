# Medieval Knight

แซนด์บ็อกซ์ทดสอบระบบต่อสู้แบบ 2.5D (มุมมองด้านข้าง สไปรต์ 2D ในโลก 3D กล้อง perspective พื้นหลัง parallax ตามระยะ Z จริง)
Unity `6000.6.5f1` (URP 2D, Input System ใหม่, uGUI) — ตั้งค่าโปรเจกต์เหมือน PusomFarmGame
เขียนด้วยโค้ดล้วน: ฉาก ตัวละคร เอฟเฟกต์ และ UI ถูกสร้างตอนรันไทม์ ไม่มี prefab ข้อความในเกมเป็นภาษาอังกฤษ
ขอโดย test_disk (Discord 449209885138223116)

## โครงสร้างไฟล์

| ที่อยู่ | หน้าที่ |
| --- | --- |
| `Assets/MedievalKnight/Scripts/Game.cs` | จุดเริ่มเกม (`Boot`), กล้อง, สร้างฉาก (ชั้นพื้นหลัง, พื้น, ของตกแต่ง), camera shake, hit-stop, ปุ่ม T / R / H / Esc |
| `Assets/MedievalKnight/Scripts/Knight.cs` | ผู้เล่น: เดิน/กระโดด, HP / Armor / Stamina, บล็อกด้วยโล่, ตารางท่าโจมตี `Knight.Attacks` (ฟัน 4 ท่า + แทง 1 ท่า) |
| `Assets/MedievalKnight/Scripts/Dummy.cs` | หุ่นฟาง: HP + Armor (เกราะรับก่อน), แถบเหนือหัว, สถิติ, โหมดสวนกลับ, พังแล้วเกิดใหม่ |
| `Assets/MedievalKnight/Scripts/Fx.cs` | เอฟเฟกต์ทำเอง: อนุภาคสี่เหลี่ยม และสไปรต์ที่จางหาย (เส้นฟัน, เส้นแทง, เงาดาบ) |
| `Assets/MedievalKnight/Scripts/Hud.cs` | UI ทั้งหมด (uGUI, `UnityEngine.UI.Text` แบบ legacy): แถบ 3 แถบ, ข้อมูลหุ่น, ชื่อท่า, ตัวเลขดาเมจ, ปุ่มควบคุม |
| `Assets/MedievalKnight/Scripts/Art.cs` | โหลด PNG จาก `Resources/Sprites` แล้วตัดเป็นสไปรต์ในโค้ด (16 px ต่อ unit) + ภาพที่สร้างจากโค้ด |
| `Assets/MedievalKnight/Scripts/InputX.cs` | ตัวกลางรับปุ่ม (`#if ENABLE_INPUT_SYSTEM`) ปุ่มใหม่ต้องเพิ่มใน `Map()` |
| `Assets/MedievalKnight/Scripts/SelfTest.cs` | ทดสอบอัตโนมัติ ทำงานเฉพาะเมื่อรัน `MedievalKnight.exe -selftest <โฟลเดอร์>` (เล่นทุกท่า เก็บภาพหน้าจอ เขียน log แล้วปิดเอง) |
| `Assets/MedievalKnight/Resources/Sprites/` | ภาพจากไฟล์อ้างอิง `_reference/2.5D.zip` ของผู้ขอ |
| `Assets/Scenes/Main.unity` | ฉากหลัก มีแค่ Main Camera |
| `Assets/Editor/BuildScript.cs` | `BuildWindows`, `CreateMainScene`, `FixTextures` (ตั้งค่า import ของ PNG: readable, point, ไม่บีบอัด) |
| `build.bat` | บิลด์ Windows ด้วย Unity batchmode → `Builds\MedievalKnight.exe`, log ที่ `build.log` |

## ปุ่ม

A/D หรือลูกศร เดิน, Space กระโดด, Left Shift ค้าง = ยกโล่, 1-4 ฟัน 4 ท่า, 5 หรือ K แทง, J คอมโบฟัน 1>2>3>4,
H โดนตีทดสอบ 20, T เปิด/ปิดหุ่นสวนกลับ, R รีเซ็ต, F1 ซ่อนปุ่มควบคุม, Esc ออก

## แนวทางแก้โค้ด

- ปรับ/เพิ่มท่า: แก้ `Knight.Attacks` (มุมดาบ, เวลา, ดาเมจ, stamina, เอฟเฟกต์) คอมโบ J วนเฉพาะ 4 ท่าแรก
- เพิ่มภาพ: วาง PNG ใน `Resources/Sprites` แล้วเรียก `Art.Get("ชื่อ")` (build จะรัน `FixTextures` ให้เอง)
- หลังแก้ ให้รัน `build.bat` แล้วลอง `-selftest` ดู log บรรทัด `SELFTEST`
