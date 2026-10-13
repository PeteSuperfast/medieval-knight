# Phase 6 — state machine / ท่า (แยกจาก tech.md §3)
> อ่านคู่กับ tech.md

## 3. คลาส / ฟิลด์ / state machine

### 3.1 `AttackDef` (ฟิลด์ใหม่ ค่าเริ่ม 0 = ไม่มีผล ท่าเดิมไม่เปลี่ยน)
| ฟิลด์ | ใช้กับ | ความหมาย |
| --- | --- | --- |
| `sideStep` (m) | Sidestep | ก้าวไปด้านที่กด (A/D) รวมเท่านี้ตลอด strike |
| `backStep` (m) | Jab, Retreat | ถอยหลังรวมเท่านี้ (ทิศตรงข้ามที่หัน) |
| `shieldJab` (bool) | Jab | ตั้ง `shieldPush` ช่วง wind→strike (ใช้โมเดลโล่เดิม) |
| `hitStop` (s) | Heavy | 0 = กฎเดิม (0.05/0.075) |
| `stun` (s) | Heavy | 0.5; ส่งเข้า `HitArea(... stun ...)` (ปัจจุบัน DoHit ส่ง 0) |
| `heavy` (bool) | Heavy | ไม่นับเป็นท่าคอมโบ (ไม่เข้าตารางเลือกท่า) |
ระยะก้าวคิดเป็นเมตรรวม (`Push(vec * dist * dt*speed/strikeTime)`) เพราะ `lunge` เดิมเป็น m/s ได้แค่ ~0.4–0.6 ม. ต่อท่า Vault ใช้ `hopStrike` สูงขึ้น (ภาพเท่านั้น ไม่ตั้ง `Airborne`)

### 3.2 ตาราง `Knight.Attacks` หลังแก้ (ค่าตาม design-combo ข้อ 5)
`0..4` เดิม (ห้ามแก้ค่า) | `5 Dash Slash` | `6 Advance Cleave` | `7 Sidestep Slash` | `8 Shield Jab` | `9 Vault Chop` | `10 Retreat Slash` | `11 Heavy Strike`
- `public const int ComboLen = 5;` — แทน `% Attacks.Length` ใน `TryStartAttack` (ถ้าไม่แก้ คอมโบจะวน 12 ท่า = บั๊กแน่นอน)
- Net kind: ท่า 0..4 = 0..4 (เดิม); ท่า 5..11 = **kind 30..36** (helper `NetKind(i)` / `FromNetKind(k)`); skill 10..13, dash 20 คงเดิม

### 3.3 เลือกท่าตอนคลิก (`PickAttack(inp)` ใน TryStartAttack; ลำดับ charge > dash > ทิศ > เดิม)
```
n = ComboLen-cycle index (comboIndex, timeout 0.9 วิ เดิม)
if (now - dashStartTime <= 0.35) -> Dash Slash   (นับเป็นคลิก 1: comboIndex=0 -> ต่อไป 1)
elif n==0 or n==2 and moveInput != 0:
     rel = angle(move, forward): <45 W | 45..135 A/D (แยกด้วย sign ของ right·move) | >135 S
     n==0: W->Advance, A/D->Sidestep(side=±1), S->Jab ;  n==2: W->Vault, S->Retreat, A/D->Rising(เดิม)
else Attacks[n]
comboIndex = (n+1) % ComboLen   // ท่าใหม่ใช้ n เดิมของตำแหน่งที่มันแทน
```
ทิศวัดจาก `lastMove` (inp.move ล่าสุด) เทียบ `transform.forward` ณ ตอน**เริ่มฟัน** (ไม่ใช่ตอนกด) ทำให้คิวคลิกใช้ทิศปัจจุบัน

### 3.4 หน่วง 0.5 วิ
- `float nextSwingAt` = เวลาเริ่มฟันล่าสุด + 0.5 (คงที่ ไม่คูณ `SwingSpeed`); Heavy = เวลาปล่อย + 0.5
- ใน `Simulate`: คลิกตอน `IsAttacking || now<nextSwingAt` → ลงคิว 1 คลิก (`queued`, หน้าต่างเพิ่ม 0.45→0.6 วิ); คิวถูกใช้เมื่อ `!IsAttacking && now>=nextSwingAt`
- `TryDash` ล้าง `nextSwingAt=0` + เก็บ `dashStartTime` (Dash Slash ต่อได้ทันที)
- `DebugAttack`/`DebugMove` ข้ามหน่วง (เทสต์เดิมที่เรียก DebugAttack ไม่เปลี่ยนพฤติกรรม)

### 3.5 Charge state machine (local เท่านั้น; remote ใช้ flag)
```
None --กดซ้าย (ไม่ block, canInput)--> Pending(pressT)
Pending --ปล่อย <0.15 วิ--> คลิกปกติ (ใช้ PickAttack ตอนปล่อย)   [HoldSeconds = 0.15]
Pending --ค้าง >=0.15 วิ และ !IsAttacking--> Charging(t=0)   (ถ้ากำลังฟันอยู่ รอจบก่อนแล้วนับ t จากตอนนั้น)
Charging --t>=1.0--> Ready (ring กะพริบ->ขาว, Snd.Ready, ค้างได้ไม่จำกัด)
Charging --ปล่อย t<1--> ฟัน PickAttack ท่าเดิม   | Ready --ปล่อย--> Heavy Strike
ยกเลิก (ไม่ฟัน): คลิกขวา (ยกโล่ + ต้องปล่อยซ้ายก่อนถึงชาร์จใหม่ `chargeLock`), dash, skill Q/E/R/F, !canInput/Paused/panel, ล้ม (DownRoutine/ClearActionFlags)
```
- ระหว่าง Charging: `Charging=true` (public), `IsAttacking=false`, ความเร็วเดิน ×0.5 (`speedMul`), `lockFacing=false`, โดนตีไม่ยกเลิก, ยกโล่ไม่ได้ (ยกเว้นคลิกขวา=ยกเลิก)
- ท่าเงื้อ: `Pose()` ข้าม rest/block lerp เมื่อ `Charging` แล้วตั้ง `swYaw/swPitch/swHand` เป็นท่าเงื้อสองมือ (Heavy.yawWind...) ปล่อยแล้ว `AttackRoutine(Heavy)` เริ่มจากท่านั้น (windupTime สั้น 0.04)
- Visual: วงแหวน `Prims.Cyl` แบนใต้เท้า (สร้างตอน `Build()` เฉพาะ `!remote`, ปิดเมื่อไม่ชาร์จ) รัศมีโต/สี #F5C25A→#D8CCB0; ดาบเรือง = `tint.Set` ตามเดิม (ตัวเอง+remote)
- Heavy ไม่กิน stamina; นับเป็น 1 คลิกในคอมโบ (`comboIndex=(n+1)%ComboLen`, `lastAttackEnd` อัปเดตตามเดิม)

### 3.6 ผลต่อ ล้ม/ชุบ/โล่/F
- ล้ม: `DownRoutine` เรียก `ClearActionFlags()` อยู่แล้ว → เพิ่มรีเซ็ต charge ที่นั่นที่เดียว (+ `CancelAction`)
- ชุบ: โล่ชุบ (`Glowing`) ไม่เกี่ยว; ชาร์จ/ฟันตอน Glowing ได้ตามเดิม
- กฎ F: ไม่แตะ (`ReviveInstead` เดิม); เพียงแต่ Charging ปิดการค้าง F ชุบ (3 ใน ข้อ 2)

