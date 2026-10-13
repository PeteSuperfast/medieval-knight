# Phase 6 — ท่าคอมโบ + ชาร์จ: tech
> Architect | อ่านคู่กับ `tech-net.md` (เครือข่าย + เทสต์) | ตรวจกับโค้ดจริง 2026-10-13

## 1. แนวทาง
- ไม่สร้างระบบใหม่ ขยาย `Knight.cs` เป็นหลัก: ท่าใหม่ = `AttackDef` ต่อท้าย `Knight.Attacks` (index 5..11) เล่นผ่าน `AttackRoutine` + `Net.SendAct` เดิม
- ชาร์จ = state machine เล็กๆ ใน `Knight` (ไม่ใช่ coroutine) ปล่อยแล้วเรียก `AttackRoutine(Heavy)`; ดาเมจ/stun ผ่าน `HitArea`→`Game.HitEnemy` เดิม (host ตัดสิน)
- ออนไลน์: **ไม่เพิ่ม `Msg` ใหม่** ใช้ `Act` (kind ใหม่) + บิต `NetCharging` ใน `NetFlags` แต่ **bump `Net.Protocol` 3→4** (ความหมายของ kind/flags เปลี่ยน)
- ไม่แตะ loop/โลกเก่า/เซฟ/ปุ่ม (ใช้ปุ่มเดิม: คลิกซ้าย/J, WASD, Space, คลิกขวา) ไม่เพิ่มเสียง (ติ๊งตอนเต็ม ใช้ `Snd.Ready` เดิม)

## 2. ไฟล์
| ไฟล์ | แก้/ใหม่ | หน้าที่ |
| --- | --- | --- |
| `Scripts/Knight.cs` | แก้ (หลัก) | `AttackDef` ฟิลด์ใหม่, ท่าใหม่ 6+Heavy, เลือกท่า, หน่วง 0.5, Dash Slash, charge, ring/glow, `DebugMove`/`DebugCharge` |
| `Scripts/Net.cs` | แก้เล็ก | `Protocol = 4`; ไม่เปลี่ยนรูปเฟรม; comment kind ใหม่ใน `SendAct` |
| `Scripts/Game.cs` | แก้เล็ก | `ClientHit`: map `src` 30+ → ชื่อท่า (ดูข้อ 7 ข้อ 1) |
| `Scripts/TeamRun.cs` | แก้ 1 บรรทัด | `holding` ชุบ: เพิ่ม `&& !me.Charging` (ชาร์จอยู่ = ไม่ชุบ) |
| `Scripts/SelfTest.cs`, `MpTest.cs` | แก้ | ขอบเขตลูป `Attacks.Length`→`Knight.ComboLen`, เคสใหม่ (tech-net.md) |
| `Scripts/Hud.cs` | แก้เล็ก (ถ้าต้อง) | ไม่ต้องมี UI ใหม่ — `hud.Action()` ใช้ต่อ; วงชาร์จเป็นวัตถุ 3D ใน Knight |
| `Docs/reference/controls.md`, `dev-guide.md`, `online-coop.md`, `files.md`, `CLAUDE.md` | แก้ตอนจบ | ปุ่ม/ท่า, Protocol 4, ตาราง kind |
ไม่มีไฟล์ใหม่ (ไม่ต้องแยกคลาส: logic ผูกกับ pose/coroutine ใน Knight; ถ้า Knight.cs ยาวเกิน ~1500 บรรทัดค่อยแยก `partial class Knight` ภายหลัง)

## 3. คลาส / ฟิลด์ / state machine
ย้ายไป `tech-state.md` (AttackDef ฟิลด์ใหม่, ตารางท่า, เลือกท่า, หน่วง 0.5, charge state machine, ล้ม/ชุบ/โล่/F)

## 4. ลำดับขั้น (แต่ละขั้น build ผ่าน + selftest ย่อย + commit แยก)
1. **ข้อมูลท่า**: ฟิลด์ `AttackDef` + 6 ท่าใหม่ + Heavy (ยังไม่มี input) + `ComboLen` + `NetKind` mapping + `PlayRemoteAct`/`ClientHit` + `Protocol=4` + `DebugMove(i,side)`. แก้ SelfTest ลูป 5 ท่า. build + selftest `-looponly` + mptest -arena
2. **เลือกท่า + หน่วง + Dash Slash**: 3.3–3.4 (ยังใช้ `MouseDown` เดิม) + เคสทดสอบทิศ/หน่วง/dash
3. **Charge ฝั่ง local**: 3.5 (ย้ายคลิกเป็นตัดสินตอนปล่อย) + ring/glow/เดินช้า + ยกเลิก + TeamRun 1 บรรทัด + เคสทดสอบ
4. **ออนไลน์**: `NetCharging` + remote pose/glow + เคส mptest (tech-net.md)
5. **เอกสาร + รันครบ**: selftest เต็ม, `mptest.ps1`, `mptest-loop.ps1`, `-menutest`; ปิด status

## 5. ความเสี่ยง
| ความเสี่ยง | ผล / ทางกัน |
| --- | --- |
| คลิกตัดสินตอนปล่อย (ขั้น 3) ทำให้ฟันช้า ≤0.15 วิ (พีทยอมรับแล้ว) | เทสต์เดิมคลิก 3 เฟรม: ถ้า fps ต่ำ >0.15 วิ จะกลายเป็นชาร์จ → เพิ่ม hook `Knight.TestHoldSeconds` (ค่าเริ่ม 0.15) |
| `% Attacks.Length` / ลูปเทสต์วน 12 ท่า | ใช้ `ComboLen` ทุกที่ (grep `Attacks.Length` ต้องเหลือเฉพาะที่ตั้งใจ) |
| `src`/kind ชน 10..13 | ใช้ 30+ (ข้อ 7 ข้อ 1) |
| หน่วง 0.5 + คิว 0.45 วิ → คลิกหาย | ขยายหน้าต่างคิว 0.6 วิ + เทสต์ spam click |
| ก้าวข้าง/ถอยทะลุกำแพง | `Push` อยู่ก่อน `Obstacles.Push` ใน `Simulate` อยู่แล้ว (เหมือน lunge) |
| ค้างซ้ายตอนเปิด panel/เลเวลอัป ค้างสถานะ | ยกเลิกเมื่อ `!canInput` (เทสต์) |
| Knight.cs ใหญ่ขึ้น | ยอมรับ; แยก partial ภายหลังถ้าเกิน |

## 6. ขนาดงาน: **กลาง** (Knight.cs เป็นหลัก ~300–400 บรรทัด + เทสต์; 5 ขั้น)

## 7. จุดที่ design-combo ขัดกับโค้ดปัจจุบัน (ปูส้มตัดสิน)
1. **Net kind**: design "index 5 ขึ้นไป" ใช้เป็น kind ตรงๆ ไม่ได้ — 10..13 คือ skill, 20 คือ dash, และ `src` ใน `Hit` ชนเช่นกัน → เสนอ 30..36 (ผมเลือกไว้ใน tech)
2. **`% Attacks.Length`** เดิมหมุนคอมโบตามความยาวตาราง → ต้องเปลี่ยนเป็น `ComboLen=5`
3. **หน่วง 0.5 วิไม่มีในโค้ดเลย** (status ของ combat-tweaks ไม่เคยทำ) และคลิกค้างเดิม = ไม่มีผล → ทำใหม่ทั้งคู่ในเฟสนี้; ผลข้าง: ความเร็วฟันเดิม (ดาบ Rapier x1.35) ไม่ย่นเวลาระหว่างท่าต่ำกว่า 0.5 วิ
4. **lunge เดิมเป็น m/s** (ท่า "lunge 5" ≈ 0.5 ม.จริง) — design ระบุเมตรเฉพาะ Sidestep/Jab/Retreat; ผมให้ 3 ท่านั้นเป็นเมตรรวมตามที่ design เขียน ส่วน Dash Slash/Cleave/Vault ใช้ `lunge` เดิม
5. **Stun ของ Heavy**: Minotaur `stunResist 0.5` → หยุดจริง 0.25 วิ (ไม่ใช่ 0.5); หุ่น/สไลม์ได้เต็ม และ Minotaur ไม่ถูก stun ตอน `Charging` state ของมันเอง → เสนอคงไว้ (ความต้านบอส) หรือบอกให้ผมส่ง stun 1.0 กับ Minotaur
6. **ชาร์จกับโล่ค้าง**: ถ้ายกโล่อยู่แล้วกดซ้ายค้าง design ไม่ได้บอก → ผมให้ "ไม่ชาร์จ (คลิกปกติตอนปล่อย)" ปูส้มยืนยันได้
7. **ชุบ**: design ไม่ได้พูดถึง — ผมให้ Charging บังการค้าง F ชุบ (เหมือนที่ `IsAttacking` บังอยู่แล้ว)
