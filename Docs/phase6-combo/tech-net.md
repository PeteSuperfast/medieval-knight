# Phase 6 — เครือข่าย + เทสต์ (แยกจาก tech.md)

## 1. เครือข่าย
- **Bump `Net.Protocol` 3 → 4**: ไม่เพิ่ม `Msg` และไม่เปลี่ยนความยาวเฟรม แต่ความหมายของ `Act.kind` (30..36) และบิต `NetFlags` ใหม่เปลี่ยน; build เก่าจะไม่เห็นท่า/ชาร์จแทนที่จะ error เงียบ → ให้ปฏิเสธด้วย "version" ชัดเจน (mptest ตัว V ใช้ `-netversion 99` ไม่กระทบ)
- ท่าใหม่: `Net.SendAct(NetKind(i), speed, Vector3.zero)` ใน `TryStartAttack` (ที่เดียวกับท่าเดิม) → host/client `PlayRemoteAct` map kind 30..36 → `Attacks[5..11]` (เพิ่ม helper ฝั่งรับ; `kind>=30` ก่อนเช็ค skill)
- ทิศ Sidestep (ซ้าย/ขวา): ฝั่ง remote เล่นซ้ำตำแหน่งจริงจาก state อยู่แล้ว (ตำแหน่งมาจาก `State`) → ท่าเห็นเป็นภาพ sweep เท่านั้น ไม่ต้องส่งด้านเพิ่ม (ถ้าอยากให้ดาบกวาดถูกด้าน ส่งผ่านฟิลด์ `target.x = ±1` ของ `Act` ที่ว่างอยู่ — ทำได้ไม่ต้อง bump เพิ่ม)
- **Charge**: บิต `NetCharging = 32` ใน `NetFlags` (u8; บิต 1,2,4,8,16 ใช้แล้ว) ส่งไปกับ `State` 20/วิ อยู่แล้ว; `ApplyNet` ตั้ง `netCharging` → `Pose()` ให้ท่าเงื้อ + `tint` เรือง (ไม่มีวงแหวนฝั่ง remote ตาม design); ปล่อย = `Act` kind 36 → `AttackRoutine(Heavy)` ตามเดิม
- ดาเมจ/stun: client ส่ง `Hit` เดิม (src = 36 → ชื่อ "Heavy Strike"); host clamp `stun` 0..3 และ `damage <= MaxHitDamage` อยู่แล้ว → Heavy 40×คูณ ไม่เกิน; ไม่ต้องแก้ validate (ยกเว้น map ชื่อ src ใน `Game.ClientHit`)
- Authority: ไม่มีอะไรเปลี่ยนโลกฝั่ง client เพิ่ม; `HitArea` เริ่มด้วย `if (remote) return 0` อยู่แล้ว → replay ไม่ตีของ
- ล้ม/ชุบออนไลน์: ล้ม → flag `NetDead` ชนะ; `RemoteDown` เรียก `StopAct()` ต้องเคลียร์ `netCharging` ด้วย; ชุบกลับแล้วไม่ชาร์จค้าง

## 2. ผลต่อเทสต์เดิม (ต้องแก้)
| ที่ | ปัญหา | แก้ |
| --- | --- | --- |
| `SelfTest.cs` ~482 `for i < Knight.Attacks.Length` | วนครบ 12 ท่า: เช็ค "damage == a.damage", "5 swings" (`swingSounds == 5` ~509), grunt 2..5 (~511) | ลูปเดิมวน `i < Knight.ComboLen`; เปลี่ยน `5` เป็น `ComboLen` |
| `SelfTest.cs` ~1186 วน 5 ท่ากับ Minotaur | ถูกอยู่แล้ว (ฮาร์ดโค้ด 5) | ไม่ต้องแก้ (เปลี่ยนเป็น `ComboLen` เพื่อความสม่ำเสมอ) |
| `SelfTest.cs` 531-547 Tap(J)/คลิก 3 เฟรม | ขั้น 3: คลิกยิงตอนปล่อย; fps ต่ำ → กลายเป็นชาร์จ | ตั้ง `Knight.TestHoldSeconds` ใหญ่ขึ้นในเทสต์ (เช่น 0.5) ยกเว้นเคสชาร์จ; เช็คตอนปล่อยก่อน assert `IsAttacking` (ย้าย assert หลังปล่อย) |
| `SelfTest.cs` เคสที่ยิง `DebugAttack` ติดๆ (849-920, 1013, 1083) | หน่วง 0.5 | `DebugAttack` ข้ามหน่วง → ไม่กระทบ |
| `SelfTest.cs` ~667 dash ตัดฟัน | ตัดแล้วต้องไม่ค้าง charge/คิว | เพิ่ม assert `!k.Charging` |
| `MpTest.cs` ~816 วน `i<1` + `Attacks[i].name` | ไม่กระทบ | ไม่แก้ |
| `mptest*.ps1` | ไม่มี Protocol ฮาร์ดโค้ด | ไม่แก้ |
| `-menutest` | ไม่เกี่ยว | รันยืนยัน |

## 3. เคสใหม่ที่ต้องเพิ่ม
**SelfTest (single, ใกล้เคสท่าเดิม ~ข้อ 477-550):**
1. ทุกท่าใหม่ (5..10) ผ่าน `DebugMove(i, side)` ตีหุ่น 1 ครั้ง ดาเมจ = `a.damage` ไม่กิน stamina (เหมือนเคสท่าเดิม)
2. เลือกท่าด้วย `moveOverride` + คลิก: ยืน→Side Slash; W→Advance Cleave; A/D→Sidestep (ตัวขยับตามด้าน ≈2 ม.); S→Shield Jab (ถอย ≈1.5 ม.); คลิก 2 ทุกทิศ=Overhead Chop; คลิก 3: W→Vault, S→Retreat, A/D→Rising Slash; เคาะ dash แล้วคลิก ≤0.35 วิ→Dash Slash; >0.35 วิ→ท่าเดิม
3. หน่วง: 2 คลิกติดกัน (ท่าเร็ว) เวลาเริ่มฟัน ≥0.5 วิ; dash ล้างหน่วง; คลิกรัวไม่หาย (คิว 1)
4. ชาร์จ: ค้าง 0.1 วิปล่อย=คลิกปกติ; ค้าง 0.6 วิปล่อย=ท่าเดิมตามคลิกที่ N; ค้าง ≥1.0 วิปล่อย=Heavy 40×, หุ่น/สไลม์ stun, stamina ไม่ลด; `Charging` เดิน ×0.5; โดนตี (`TakeDamage`) ไม่ยกเลิก; คลิกขวา/dash/Q ยกเลิก ไม่ฟัน; ล้ม (hp→0) เคลียร์ charge
5. คอมโบต่อ: ฟัน1 > Heavy > คลิก = Rising Slash (ตัวชี้ขยับ 1 ขั้น); Heavy เป็นท่าแรก > คลิก = Overhead Chop; timeout 0.9 วิ รีเซ็ต
6. ทั้ง 6 อาวุธ: `Equip(id)` 1 ท่าใหม่ ใช้ได้ (reach คูณตามอาวุธ)
7. loop (`-looponly`): ฟันท่าใหม่ในโลก loop ไม่กระทบเซฟ/ของ (smoke 1 ท่า)

**MpTest (`-arena`, ต่อท้ายเคส attack ~829):**
8. A สั่ง `atk 7` (Vault) → `an`/`ac` ที่ H และ B เห็นชื่อ "Vault Chop" 1 ครั้ง; ดาเมจที่ host = `Attacks[7].damage` (เคสเดียว เหมือนที่ทำกับท่า 0)
9. คำสั่ง test ใหม่ `charge <วิ>`: A ค้างชาร์จ → H และ B เห็นบิต `NetCharging` (summary key `chg<pid>`); ปล่อย → `an` = "Heavy Strike" + ดาเมจ 40 ที่ host; client ปิด/ล้มกลางชาร์จ → flag ดับ
**MpLoopTest:** ไม่ต้องเพิ่มเคส (ต้องผ่านเดิมทั้งชุด); ถ้ามีเวลา: ล้มระหว่างชาร์จ แล้วเพื่อนชุบ ต้องไม่ชาร์จค้างที่หน้าจอเพื่อน

## 4. เกณฑ์ผ่านรวม
build ผ่าน; selftest 0 FAIL; `mptest.ps1` + `mptest-loop.ps1` FAIL=0 (เช็คผันผวนตาม CPU รันซ้ำได้ตามเดิม); `-menutest` ผ่าน; เอกสารแก้แล้ว ≤10KB/ไฟล์
