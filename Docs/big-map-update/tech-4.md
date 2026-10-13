> ส่วน 4/14 ของ `tech.md` (ดัชนี: `tech.md`) · ต่อจาก `tech-3.md` · ต่อที่ `tech-5.md`

### 6.1 จุดที่อ่านปุ่ม (ค้นทั้งโปรเจกต์แล้ว)

| ที่ | ตอนนี้ | แก้เป็น |
| --- | --- | --- |
| `Knight.ReadInput` บรรทัด 365 | `block = Held(LeftShift) \|\| MouseHeld(1)` | `block = MouseHeld(1)`; เพิ่ม `run = Held(LeftShift)` |
| `Knight.ReadInput` บรรทัด 370 | `MouseDown(0) \|\| Down(J) \|\| Down(Space)` | เอา `Space` ออก; เพิ่ม `dash = Down(Space)` (dash ไม่ผ่านระบบ `request`/queue เพราะต้องแทรกได้ทันที) |
| `Game.Update` บรรทัด 1111 | `Down(KeyCode.I)` | `Down(KeyCode.Tab)` toggle |
| **`Game.Update` บรรทัด 1077-1081 (Esc)** | `Down(Escape)` → `LeaveKey()` เสมอ | **ถ้า `InventoryOpen` อยู่: `SetInventory(false)` แล้วข้ามการเรียก `LeaveKey()` ในเฟรมนั้น** (Esc ปิดหน้าต่างก่อน ไม่นับเป็น Esc ออกเมนู ไม่เริ่มนับ 3 วิของ `leaveAskedAt`); ทำตรวจ Esc **ก่อน** บล็อก `LevelStep`/`InventoryOpen` ที่ `return` ในโหมด single (ตอนนี้ Esc อยู่ต้น `Update` ก่อนอยู่แล้ว จึงแค่แทรกเงื่อนไข). หน้าต่างเลเวลอัปยังไม่ปิดด้วย Esc (ต้องเลือกการ์ด) |
| `Hud.InventoryInput` | `Alpha1..5` | คงเดิม + (ทางเลือกของ design) ลูกศร/A D + Enter เลื่อนเลือกช่อง |
| `Hud` controls / footer / `Game.CollectLocal` ("press I to equip") | "I" | "Tab"; controls เพิ่ม "Space Dash", "Shift Run" |
| `InputX.Map()` | มี `Tab`, `Space`, `LeftShift`, `J`, `Escape` | ไม่ต้องเพิ่ม |
| `SelfTest.cs` 339, 818 / `MpTest.cs` 381 | LeftShift = ยกโล่ | เมาส์ขวาค้าง (ขยาย `SetMouse` รองรับปุ่มขวา) |
| `SelfTest.cs` 440/458/542/545, `MpTest.cs` 1203/1206 | `Tap(Key.I)` | `Tap(Key.Tab)` (MpTest 1138 เป็น Tab ของเมนู ไม่เกี่ยว) |

### 6.2 วิ่ง
ใน `Knight.Simulate`: `speedMul` เดิมคงไว้ แล้วคูณ `RunMult` (1.5) เมื่อ `inp.run && moving && canInput && !IsAttacking && !IsBlocking && !Airborne && !Dead` (ยกโล่ตัดสถานะวิ่งตาม design). ไม่กิน stamina. ข้ามแมพ 380 ม.: เดิน 6.2 ม./วิ ≈ 61 วิ, วิ่ง 9.3 ม./วิ ≈ 41 วิ. **จุดเกิดถึงรัง 300 ม.**: เดิน ≈ 48 วิ, วิ่ง ≈ **32 วิ**

### 6.3 Dash (Space)

| รายการ | ค่า | เหตุผล |
| --- | --- | --- |
| stamina | **25** | ฐาน 100 = dash 4 ครั้งติด; 1 ขีดบนแถบ = 1 dash |
| cooldown | **0.8 วิ** | สั้นกว่า 0.5 จะต่อ i-frame ไม่ขาดสาย |
| ระยะ | **6 ม.** ใน **0.20 วิ** | เท่า Dash Thrust (E) `DashLength = 6f`; design ขอ 5-6 ม. |
| i-frame | **0.5 วิ** นับจากกด | ตาม requirement |
| ทิศ | ทิศ WASD; ไม่กด = ถอยหลัง (ตรงข้ามทิศที่หัน) | ตรงกับ design |
| ฟื้น stamina | หน่วง 0.6 วิ (`lastSpendTime`) ฟื้น 26/วิ x `StaminaRegenMult` | |
| ศัตรูตันทาง | ไม่ทะลุศัตรู `solid` | กันบั๊ก |
| ชนสิ่งกีดขวาง | dash ผ่าน `Obstacles.Push` ทุกเฟรม (หยุดที่กำแพง/บ้าน/ทะเลสาบ/ภูเขา) | |

หมายเหตุเทียบ design: design เสนอห่างขั้นต่ำ ~0.25 วิหลังพุ่งจบ; tech ใช้ cooldown 0.8 วิ (ตัวตั้ง) ซึ่งครอบคลุมแล้ว

| state ตอนกด Space | ผล |
| --- | --- |
| ปกติ/เดิน/วิ่ง | dash ได้ถ้า stamina >= 25 และ cooldown หมด; ไม่พอ → `Deny("Dash")` (แถบ stamina แดงกะพริบเดิม `denied` + ขีดที่ขาดสั่น + `armor_impact` เบาๆ) |
| กำลังฟันคอมโบ / สกิล Q E R | **ยกเลิกท่าแล้ว dash** (`CancelAction()`); cooldown สกิลไม่คืน; ล้าง `queued` |
| Ground Slam กลางอากาศ (`Airborne`) | ห้าม dash |
| ยกโล่ค้าง | dash ได้ (ตัดการยกโล่ระหว่าง dash; ถ้ายังกดคลิกขวาอยู่ก็ยกต่อหลังจบ — ตรงกับ design Q10 "ตัด") |
| ถูกกระเด็น | dash ตัด `knockVel = 0` |
| ตาย / เลเวลอัป / Tab เปิด | ไม่รับ (`canInput` false) |
| สตัน | ผู้เล่นยังไม่มี state สตัน; ถ้าเพิ่มภายหลังให้ห้าม dash ตอนสตัน |

`CancelAction()` รวมสิ่งที่ `ResetAll`/`DownRoutine` เคลียร์ซ้ำ (บรรทัด 304-308, 1108-1110) เป็นเมธอดเดียวเรียกจากสามที่ — กันธงค้าง (เช่น `invuln` ของ Dash Thrust)

### 6.4 i-frame ตรวจที่ไหน และ popup "DODGE"
ตรวจที่เดียว: `Knight.TakeDamage` บรรทัดแรก. `Invulnerable` เพิ่ม `|| Dashing`. ไม่แตะ host (trust model เดิม "ดาเมจที่อัศวินรับตัดสินบนเครื่องเจ้าของ"):
- Single / host-local: `Game.DamageKnight` → `k.TakeDamage`
- Client: host ส่ง `Msg.KnightHit` เสมอ → client เรียก `TakeDamage` → i-frame เช็คบนเครื่อง client (เลื่อนตาม latency ครึ่ง RTT — ยอมรับได้)
- ปุ่ม `H` ผ่าน `TakeDamage` → ใช้ทดสอบ i-frame ใน SelfTest

**"DODGE"**: ทำได้โดยไม่กระทบ trust model เพราะอยู่ฝั่งเดียวกับการตัดสิน i-frame: ใน `TakeDamage` แทรกก่อน `if (... Invulnerable) return;`
```
if (remote || Dead) return;
if (Dashing) { if (Time.time - lastDodgeFx > 0.3f) { lastDodgeFx = Time.time; Popup("DODGE", #CFE9FF); Fx.I.Burst(...); Sfx.I.Play(Snd.Swing, 0.35f); } return; }
if (Invulnerable) return;
```
(จำกัดไม่ให้ขึ้นถี่เกิน 0.3 วิ เพราะสไลม์/Minotaur อาจตีหลายครั้ง). เอฟเฟกต์ใช้ `Fx` เดิม (streak/ฝุ่น/burst) + `Tint` สีฟ้าขาวตอนอมตะ **ไม่มี afterimage** ในขอบเขตหลัก (design ขอเป็นข้อเสนอเพิ่ม ทำทีหลังได้ด้วยการก๊อปสำเนา mesh ของ rig ~3 ชุดโปร่งใส — งานเล็ก-กลาง)

