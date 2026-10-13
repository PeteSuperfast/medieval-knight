# Phase 7 — เครือข่าย + เทสต์ (แยกจาก tech.md)

## 1. ใครตัดสินอะไร (authority)
| เรื่อง | ตัดสินที่ | หมายเหตุ |
| --- | --- | --- |
| ศัตรูตีผู้เล่น | host (AI) | `DamageKnight`: ตัวเอง = `TakeDamage` ตรง; client = `KnightHit` ไปเครื่องเจ้าของ (เดิม) |
| parry / clash / block / dodge | **เครื่องเจ้าของ** | รู้สถานะโล่/ท่าตัวเองแม่นสุด ตามแพทเทิร์นเดิมของ block/dash |
| ผลต่อศัตรู (กระเด็น/สตัน/Recover) | **host** (ถือศัตรู) | client รายงาน `Hit` src 37 (parry) / 38 (clash) |
| ให้ทุกเครื่องเห็นตรงกัน | host broadcast | `EnemyHit` (deflect) + snapshot เดิม (pos/state/stun) |
- ตัวเจ้าของเล่น fx ทันทีใน `TakeDamage`; เครื่องอื่นเล่นตอนรับ `EnemyHit` (`byOther`) → ไม่ซ้ำ; ศัตรูตายก่อนข้อความถึง = `ClientHit` คืน false เงียบ

## 2. ข้อความ → **Protocol 4 → 5** (ไม่เพิ่ม `Msg`)
| ข้อความ | ใหม่ |
| --- | --- |
| `KnightHit` host→เจ้าของ | ต่อท้าย `U16 attackerNetId` (0=ไม่มี) + `U8 flags` (บิต 1 = ท่าหนัก) |
| `Hit` client→host | `src` 37=parry, 38=clash: `id`=ศัตรู, `origin`=ตำแหน่งผู้เล่น, `damage`=1 (ผ่าน validate) ที่เหลือไม่สน; `ClientHit` แยกสาขา: ศัตรูมี/ไม่ตาย/ไม่ puppet/ห่างผู้เล่น ≤ `radius+9` → `e.Deflect` |
| `EnemyHit` flags | เดิม 1 คริ 2 สตัน 4 ตาย; ใหม่ **8 หนัก**, **16 parry**, **32 clash** (16/32: ตัวเลขดาเมจ 0, `by`=pid ผู้ parry → `NetDeflect` แทน `NetHit`) |
- **flag "หนัก"**: client ส่ง `Hit.src=36` (Heavy Strike) อยู่แล้ว → host `heavy = AttackFromNetKind(src)==HeavyIndex` → `TakeHit` → `BroadcastEnemyHit` บิต 8 → ทุกเครื่อง `NumberStyle` สีเดียวกัน; ตัว host ตีหนักใช้ทาง `HitEnemy` เดียวกัน. เรื่องสีอย่างเดียวไม่ต้อง bump (v4 มองข้ามบิต 8) แต่ `KnightHit` ต้อง bump
- v4 เข้า v5: Welcome ปฏิเสธ "version" ตามเดิม; role V ของ mptest (`-netversion 99`) และ `.ps1` ไม่แก้

## 3. หน่วง 0.1 วิที่ client
`KnightHit` ที่มี attacker → รอ `Net.EnemyDelay` (coroutine บน `Game`) ค่อย `TakeDamage` เพราะภาพศัตรูบนจอ client ช้า 0.1 วิ; ก่อน apply เช็ก `g.knight` ยังอยู่/ไม่ Dead; host ไม่หน่วง; ไม่มี attacker (test) = ทันที

## 4. เทสต์เดิมที่ต้องแก้
| ที่ | ปัญหา | แก้ |
| --- | --- | --- |
| SelfTest ~306-312 `Tap(Key.M)`, `Sound: MUTED` | M ไม่ใช่เสียงแล้ว | ใช้ `Key.F2`; เพิ่ม assert กด M ไม่เปลี่ยน volume |
| SelfTest 596 / 608 / 1330 (`TakeDamage` หลังยกโล่ 0.3-0.4 วิ) | หน้าต่าง parry 0.2 วิ | `by=null` → ไม่กระทบ ไม่ต้องแก้ |
| SelfTest 2998 / 3027 `ControlsShown` | ข้อความ M | แก้เฉพาะบรรทัด M; assert เดิมไม่แตะบรรทัดนั้น |
| screenshot (`camZoom`, pitch 56) | `mk_campitch` ในเครื่องค้างค่า | ต้นเทสต์ตั้ง `camPitch=56` + ลบ PlayerPrefs |
| MpTest 525 / MpLoopTest 494 `TakeDamage(99999)` | – | `by=null` ไม่กระทบ |

## 5. เคสใหม่
**SelfTest** `Phase7(d)` + สวิตช์ `-p7only` (~1 นาที); `g.packsOff`, ใช้ `DebugStrike(slime, k, heavy)`:
1. parry: block 0.05 วิ → strike เบา: hp/armor เท่าเดิม, `ParryCount`+1, สไลม์ `Stunned` ห่าง ≥3 ม., `Snd.Clash`+1
2. block 0.35 วิ → armor −4 (20×0.2), `ParryCount` คงเดิม
3. กันสแปม: ยกโล่→ปล่อย→ยกใหม่ ≤0.5 วิ + strike = บล็อกปกติ; รอ ≥0.5 วิ = parry
4. heavy (สไลม์แดง) ในหน้าต่างเปิด = บล็อกปกติ + `HeavyBlockCount`+1
5. clash: `DebugAttack(0)` รอหน้าต่าง (`ClashOpen`) → strike: ไม่เสียหาย, `ClashCount`+1, ท่าไม่ถูกตัด; ศัตรูอยู่หลัง / ฟัน Heavy Strike / นอกหน้าต่าง / `by=null` = โดนปกติ
6. dash ในหน้าต่าง parry = DODGE (ไม่นับ parry)
7. Minotaur: `DebugState(SweepStrike)` + clash → Recover ≈0.5 วิ ไม่สตัน; parry → Stun ≈0.5 วิ
8. สีเลข / กล้อง / แผนที่ → `tech-ui.md`

**MpTest** (`-arena` ต่อท้ายเคส 3b ~915): สไลม์ 1 ตัว (`EnsureSlimes`); คำสั่งใหม่ `strike <pid> <heavy>` (ที่ H เรียก `DebugStrike`) และ `parryprobe` (A ยกโล่แล้วส่ง `Net.SendTest("strike|..")` ทันที); summary key ใหม่ `par<pid>` `cla<pid>` `sldef` `slst` `dnum`:
- a) A parry: hp A เท่าเดิม, `par`+1; สไลม์ `slst`=1 และ `sldef`+1 ที่ H B A เท่ากัน
- b) strike หนักตอน A ยกโล่ → บล็อกปกติ ไม่มี deflect
- c) clash: A `atk 0` + strike ในหน้าต่าง → `cla`+1, hp A เท่าเดิม
- d) A Heavy Strike ใส่หุ่น (crit 0/2): `dnum` เท่ากันทุกเครื่อง (หนัก / คริ+หนัก)
- e) host parry ตัวเอง: B เห็น `sldef`+1
**MpLoopTest**: ไม่เพิ่ม ต้องผ่านเดิมทั้งชุด (บอส/สไลม์ตี client ผ่าน `KnightHit` รูปใหม่อยู่แล้วใน bait/wipe)

## 6. เกณฑ์ผ่านรวม
build ผ่าน; selftest 0 FAIL; `mptest.ps1` + `mptest-loop.ps1` FAIL=0 (เช็กผันผวนตาม CPU รันซ้ำได้); `-menutest` ผ่าน; เอกสาร ≤10KB/ไฟล์
