# Tech design: big-map-update (medieval-knight)
> ส่วน 1/14 ของ `tech.md` (ดัชนี: `tech.md`) · ต่อที่ `tech-2.md`

- อ้างอิงโค้ด: branch `main` commit `76fe974` (ที่ checkout อยู่)
- ผู้ออกแบบ: architect, วันที่ 2026-10-11 (แก้ครั้งที่ 2: ปรับให้ตรง design.md — ผังแมพ เมืองทางใต้ โซน 8 ชนิด หน้าต่าง Tab หน้าจอตาย Soul บน HUD ฯลฯ)
- คู่กับ `design.md`: ตัวเลขสมดุลและกติกาในเอกสารนี้เป็นตัวตั้ง ส่วนผังแมพ layout UI ใช้ตาม design.md (ดูหัวข้อ 13)

## 0. สรุปการตัดสินใจหลัก

1. สนามเดิมคือ `Game.ArenaHalf = 19f` = **38 x 38 ม.** (ยืนยันจากโค้ด) → ใหม่ `ArenaHalf = 190f` = **380 x 380 ม.** ศูนย์กลาง origin; X ไปตะวันออก, Z ไปเหนือ, กล้องไม่หมุน (`Game.PlaceCamera` ใช้ `Quaternion.Euler(CamPitch,0,0)` ตายตัว) → เหนืออยู่ด้านบนจอ
2. **ไม่ต้องเปลี่ยน encoding ตำแหน่ง** `NetW.Pos` (cm, i16 ±327.67 ม.) รองรับ ±190 ม. แต่ต้องเพิ่ม `Net.Protocol` เป็น **2** เพราะ payload ของ `Snapshot` (pickup), `PickupTaken`, `Exp` เปลี่ยน และมี `Act` kind ใหม่ (dash) โดย **ไม่ต้องเพิ่ม `enum Msg` ตัวใหม่**
3. ผังแมพตาม design: **เมืองอยู่ทางใต้** (สี่เหลี่ยม X -60..60, Z -190..-100) จุดเกิด (0,-150) รังอยู่เหนือสุด (0,+150) ห่างกัน **300 ม.**; ฉากสร้างเป็น **chunk 38 ม. (10 x 10 = 100 chunk)** รวม mesh ด้วย `Mesh.CombineMeshes` ต่อ chunk ต่อ material
4. สไลม์เกิดเป็น "แพ็ค" รอบผู้เล่นแต่ละคน นอกจอ 15-26 ม. ไม่เกิดในเมือง/ทะเลสาบ/ภูเขา ลบเงียบเมื่อไกลทุกคน > 70 ม. Minotaur อยู่รังคงที่ + leash; **ไม่มีประกาศ "Wave n"**
5. Space = dash (stamina 25, cooldown 0.8 วิ, 6 ม., อมตะ 0.5 วิ), Shift = วิ่ง x1.5, stamina ใช้กับ dash อย่างเดียว (แถบมีขีดแบ่งทุก 25), โล่ฟรี
6. Level 25: ตาราง EXP รวม 16,410, EXP ต่อมอนสเตอร์สเกล 1+0.15 ต่อเลเวล, `AttrCap` 10 (รองรับ 24 แต้ม)
7. ตายแล้วรีเซ็ต: `Game.OnKnightDown` แสดงหน้าจอ YOU DIED (ค่าก่อนรีเซ็ต) → `OnKnightRevive` เรียก `Knight.ResetProgress()`; เวลาฟื้นเล่นคนเดียว 1.4 → 3 วิ; Soul เก็บที่ `Knight.souls` (ไม่เซฟข้ามเซสชัน เกมนี้ไม่มีระบบ save); อาหารใช้โครง `Pickup` เพิ่ม `kind`
8. ไอคอนดาบ: render โมเดลจาก `Weapons.BuildModel` ด้วยกล้อง ortho layer 31 → RenderTexture ตอนเริ่มเกม → คัดลอกเป็น `Texture2D` ใช้กับ `RawImage` (แผน B วาดพิกเซล)
9. นำทาง (ขอบเขตหลัก): ถนนดิน + ป้ายบอกทาง + landmark สูง + ลูกศรชี้รังพร้อมระยะ — **ไม่มี minimap** (เป็น "ตัวเลือก" หัวข้อ 9.6)
10. branch `wip/big-map-update`: **เริ่มจาก `main` ใหม่** (ดูหัวข้อ 9.1) — architect อ่านได้เฉพาะไฟล์ที่ checkout อยู่ ดูเนื้อ wip ไม่ได้

---

## 1. ภาพรวมแนวทาง

ใช้แพทเทิร์นเดิมทั้งหมด: โค้ดล้วน ไม่มี prefab, host-authoritative, client เชื่อใจเรื่องอัศวินตัวเอง, ข้อมูลผู้เล่นอยู่บน `Knight`. งานแบ่งเป็น 3 ก้อน: (A) กติกาผู้เล่น — input, dash, วิ่ง, stamina, เลเวล 25, ตาย/รีเซ็ต, Soul, อาหาร; (B) UI — Tab + ไอคอนดาบ, แถบ stamina มีขีด, Soul บน HUD, หน้าจอตาย; (C) โลก — แมพ 380 ม. แบบ chunk + โซน + collision + ระบบเกิดศัตรูใหม่. A, B ทำบนแมพเดิมได้และ build ผ่านทีละขั้น; C เสี่ยงและใหญ่สุดจึงทำท้าย

---

## 2. ไฟล์ที่จะสร้างใหม่ / แก้

### 2.1 ไฟล์ใหม่ (ใต้ `Assets/MedievalKnight/Scripts/` — Unity สร้าง .meta ตอน build ให้ commit ไปด้วยถ้าโปรเจกต์ track .meta)

| ไฟล์ | หน้าที่ | เหตุผลที่แยก |
| --- | --- | --- |
| `World.cs` | `MapLayout` (ขนาด โซน จุดสำคัญ `ZoneAt(x,z)`), `StaticBatcher` (รวม mesh ต่อ chunk), `WorldBuilder` (พื้น เมือง ป่า ทุ่ง ทะเลสาบ ภูเขา ถนน ป้าย landmark รั้ว), `Obstacles` (collision grid) | `Game.BuildWorld` ตอนนี้ ~110 บรรทัดและจะโตเป็นหลายร้อยบรรทัด |
| `SwordIcons.cs` | สร้างและเก็บไอคอนดาบ 5 เล่ม (`SwordIcons.Get(int weaponId)` คืน `Texture2D`) | ใช้ครั้งเดียวตอนเริ่มเกม แยกจาก `Hud.cs` เพื่อถอยแผนได้ง่าย |

อาหารไม่สร้างไฟล์ใหม่: `FoodDef` + `Foods` อยู่ใน `Weapon.cs` ข้าง `Weapons`/`Pickup` (แยกเป็น `Food.cs` ได้ถ้าอ้วน)

