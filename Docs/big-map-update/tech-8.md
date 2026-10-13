> ส่วน 8/14 ของ `tech.md` (ดัชนี: `tech.md`) · ต่อจาก `tech-7.md` · ต่อที่ `tech-9.md`

### 8.2 ไอคอนดาบ (render จากโมเดล `Weapons.BuildModel` ลง RenderTexture)

`SwordIcons.Build()` (ครั้งเดียว `Game.Awake` **หลัง** `SetupLight()`):
1. root ชั่วคราวที่ `(0, -1000, 0)` ตั้ง layer **31** ให้ทุก child (`SetLayerRecursively`)
2. วนทุกดาบ: `Weapons.BuildModel(root, def)`; ปิด cast/receive shadow; หมุนให้ใบเฉียง **45 องศา** (ปลายขึ้นขวา); คำนวณ bounds จาก `Renderer.bounds` ตั้ง `orthographicSize` พอดี (+10%)
3. กล้องชั่วคราว: `orthographic`, `cullingMask = 1 << 31`, มองลงเอียง ~70 องศา, `clearFlags = SolidColor` สี **ทึบ** (0.12, 0.12, 0.17 เท่าสีพื้นช่องดาบ) — ไม่พึ่ง alpha, `targetTexture = RenderTexture(128,128,16, ARGB32)`, `enabled = false`
4. `cam.Render()` ทีละดาบ (นอก callback ของ pipeline เช่นใน `Awake`) → `RenderTexture.active = rt; tex.ReadPixels(...); tex.Apply()` เก็บเป็น **`Texture2D` ถาวร**
5. ทำลายโมเดล/กล้อง/RT ชั่วคราว เก็บ `Texture2D[5]`
6. กล้องหลัก `cullingMask &= ~(1 << 31)`
7. `Hud` ใช้ `RawImage.texture = SwordIcons.Get(i)`; ดาบที่ยังไม่มี = `color` ดำ (silhouette) — พื้นหลังทึบเป็นสีพื้นช่อง จึงเห็นเป็นกล่องดำสนิทถ้าคูณสีดำ: **แก้**: ให้ไอคอนที่ยังไม่มีคูณ `color = (0.08,0.08,0.1)` (มืดมากแต่ยังเห็นรูปร่างจากเส้นขอบ/แสง) หรือวาดซ้ำแบบ mask — dev เลือกวิธีที่เห็น silhouette ชัดสุดตอนทดสอบ; ถ้ายังไม่ชัด ให้เรนเดอร์ไอคอนเงาแยกด้วย material สีดำ `Prims.Glow(black)` บนพื้นหลังเทา (texture ที่ 2 ต่อดาบ)

**ข้อควรระวัง URP:**
- `Light.cullingMask` ไม่มีผลใน URP forward → ห้ามเพิ่มไฟพิเศษ (จะส่องโลกจริง); ใช้ sun + ambient/cubemap จาก `SetupLight()` จึงต้อง `Build` หลัง `SetupLight`
- เงา: ปิด cast/receive ที่โมเดลไอคอน
- Alpha: URP อาจบังคับ alpha = 1 → ใช้พื้นหลังทึบ
- `Camera.Render()` ของ base camera ใน Unity 6 ใช้ได้เมื่อเรียกนอก render loop; ถ้าเตือน/ไม่ render ใช้ `RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt })`
- MSAA pipeline 4x: RT `antiAliasing` ไม่ตรงถูกปรับเอง ไม่เป็นไร
- material ดาบมาจาก `Prims.Lit`/`Prims.Glow` (`Prims.Load` โหลด `Resources/Mats/*` เอง)
- `SwordIcons.Get` ต้องไม่คืน null: ถ้าสร้างไม่สำเร็จ fallback แผน B

**แผน B**: วาด silhouette ดาบเป็น `Texture2D` 128x128 ด้วยโค้ดล้วน (ใบ = สี่เหลี่ยมหมุน 45 องศา กว้าง `bladeWidth` ยาว `bladeLen`, การ์ด = แท่งขวาง `guardColor`, ด้าม `gripColor`, แกนเรืองตาม `coreColor`) ไม่พึ่ง URP

**SelfTest ตรวจ**: ไอคอนทั้ง 5 ไม่ null, 128 px, พิกเซลที่ไม่ใช่สีพื้นหลัง > 300, hash ไม่ซ้ำ

### 8.3 ตัวเลือกเสริมที่ถูกกว่า minimap (ทำได้ในขอบเขตหลักถ้าเวลาเหลือ)
ป้ายชื่อโซนกลางจอ 2 วิตอนข้ามเขต: `Game.Update` เช็ค `MapLayout.ZoneAt(knight.Pos)` ทุก 0.25 วิ เมื่อเปลี่ยนโซนเรียก `hud.Banner(zoneName, color)` เดิม (งานเล็ก ไม่มี net)

---

## 9. แมพใหญ่ (ข้อ 1 ของ requirement) รายละเอียดทางเทคนิค

### 9.0 ยืนยันขนาดเดิมจากโค้ด

| ที่ | ค่า | ผลที่ได้ |
| --- | --- | --- |
| `Game.ArenaHalf = 19f` ("the fenced field is 38 x 38 m") | ครึ่งด้าน 19 ม. | สนาม **38 x 38 ม.** |
| `Prims.Box("Outside" 160x160)`, ต้นไม้/หิน 120 ชิ้นนอกรั้ว, `farClipPlane = 200`, shadow distance 55 | | |
| ที่ใช้ `ArenaHalf` | `Knight.Simulate` clamp, `GroundSlam` `lim`, `Enemy.ClampToArena`, `Pickup.Spawn`, `Net.ReadKnight`, `SpawnWave`, SelfTest 139 | เปลี่ยนค่าคงที่ที่เดียวขยายครบ |

ใหม่ `ArenaHalf = 190f` → **380 x 380 ม.** (พื้นที่ 100 เท่า)

### 9.1 branch wip: เริ่มจาก main ใหม่
แนะนำ **branch ใหม่ `feat/big-map-update` จาก `main` (76fe974)** ไม่ merge/cherry-pick wip ทั้งก้อน: (1) wip แก้ `InputX.cs`/`Knight.cs`/`Progress.cs` ค้างครึ่งกลาง ไม่เคย build (2) `InputX.Map()` บน main มีปุ่มครบแล้ว (3) แผนนี้เปลี่ยนตัวเลข (EXP, AttrCap, stamina) ต่างจากที่ wip น่าจะทำ. architect อ่านได้เฉพาะไฟล์ที่ checkout อยู่ จึงไม่ได้เห็นเนื้อ wip; dev ดูได้ด้วย `git diff main..wip/big-map-update` เป็นข้อมูลอ้างอิงเฉยๆ

