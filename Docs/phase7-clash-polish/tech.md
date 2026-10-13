# Phase 7 — clash/parry + เลขสี + scroll กล้อง + แผนที่ M: tech
> Architect | คู่กับ `tech-net.md` (เครือข่าย+เทสต์) `tech-ui.md` (เลข/กล้อง/แผนที่) | ตรวจโค้ดจริง 2026-10-14

## 1. แนวทาง
- clash/parry ตัดสินใน `Knight.TakeDamage` บนเครื่องเจ้าของตัว (บล็อก/dash ตัดสินที่นั่นอยู่แล้ว) แล้วรายงานให้ host ทำให้ศัตรูกระเด็น/สตัน
- flag "หนัก" ผู้เล่น: host รู้จาก `Hit.src == 36` อยู่แล้ว ส่งต่อใน `EnemyHit.flags` บิต 8 (0 byte เพิ่ม)
- `KnightHit` เพิ่ม attackerId + flags → **bump `Net.Protocol` 4 → 5** ไม่เพิ่ม `Msg`
- แผนที่ M = uGUI ไฟล์ใหม่ วาดจาก `MapLayout.ZoneAt` (แมพเหมือนกันทุกโลก/seed) ไม่ส่งข้อมูลเพิ่ม
- ไม่แตะ loop/โลกเก่า/เซฟ/กฎ F ชุบ; ปุ่มใหม่มีแค่ F2 + ล้อเมาส์

## 2. ไฟล์ (Scripts/)
| ไฟล์ | | หน้าที่ |
| --- | --- | --- |
| `Knight.cs` | แก้หลัก | หน้าต่าง parry/clash, `TakeDamage`, โล่แฟลช, counter ทดสอบ |
| `Enemy.cs` | แก้ | `TakeHit`+heavy, `Deflect`/`NetDeflect`, `NumberStyle`, glow |
| `Slime.cs` `Minotaur.cs` | แก้ | ส่ง `by`/`heavy`, สัญญาณเตือน, `Deflect` ของบอส |
| `Game.cs` | แก้ | `DamageKnight`/`HitEnemy`/`ClientHit`/`ReportDeflect`, กล้อง, M/F2/Esc |
| `Net.cs` | แก้ | `Protocol=5`, `KnightHit`, `EnemyHit`, `Hit.src` 37/38 |
| `InputX.cs` `Hud.cs` `RunLoop.cs` | แก้เล็ก | F2+`Scroll()` / ข้อความ F1 / `PileMarks()` |
| `MapUi.cs` | **ใหม่** | panel แผนที่ (canvas ตัวเอง ตามแพทเทิร์น `TeamUi`) |
| `SelfTest.cs` `MpTest.cs` | แก้ | เคสใหม่ + `Tap(Key.M)`→F2 |
| Docs: controls, online-coop, files, dev-guide, CLAUDE.md | แก้ตอนจบ | ปุ่ม, Protocol 5 |

## 3. คลาส / ความสัมพันธ์
**Knight** — `parryUntil`, `lastPressAt`, `clashFrom/To`; counter `ParryCount/ClashCount/HeavyBlockCount`
- กดโล่ขึ้นใหม่: ถ้า `now-lastPressAt >= 0.5` → `parryUntil=now+0.2`; อัปเดต `lastPressAt` ทุกครั้ง (กดรัวไม่ได้ parry)
- `AttackRoutine` เมื่อ `!a.heavy`: `clashFrom`=จบ windup−0.1, `clashTo`=จบ strike+0.1; ล้างใน `ClearActionFlags`/`StopAct`; Heavy Strike ไม่เปิดหน้าต่าง
- `TakeDamage(..., float knockback=6, Enemy by=null, bool heavy=false)` ลำดับ: dash > Invulnerable (เดิม) > **parry** (`by!=null`, IsBlocking, มุมโล่ <75°, `!heavy`, `now<parryUntil`) > **clash** (`by!=null`, `!IsBlocking`, `!heavy`, ในหน้าต่าง, ศัตรู ±60° ≤5.5 ม.) > บล็อกเดิม (heavy → popup `HEAVY`) > โดน
- parry/clash: ไม่แตะ hp/armor/`LastDamageSource`; fx+`Snd.Clash` (pitch 1.35/0.9)+popup+Shake+`HitStop`; clash `knockVel=-srcDir*10` ไม่ยกเลิกท่า; แล้ว `Game.ReportDeflect`
- `by==null` (ปุ่ม H, เทสต์เดิม) = พฤติกรรมเดิม 100%; โล่แฟลช = `Tint` บน `shieldNode` ตอน `ParryOpen` (remote แฟลชตอน NetBlocking ขึ้น)

**Enemy** — `TakeHit(..., Knight by=null, bool heavy=false)` (เทสต์เดิม positional ยังผ่าน)
- `Deflect(kind, from, by)` ที่ host/SP: knock×`knockResist` + stun×`stunResist` (parry 4 ม./0.6 วิ, clash 3 ม./0.3 วิ) → host broadcast; ถ้า `by.remote` เล่น `ShowDeflect`
- `NetDeflect` ที่ client เล่น fx เฉพาะ `byOther`; `DeflectCount` ให้เทสต์
- glow `glowColor/glowAmt` รวมกับ `flash` ใน `BaseTick` (ตอนนี้ flash เขียนทับ `tint` ทุกเฟรม)

**Slime** `kind==2` = หนัก; Squash ของแดง: วงหด+glow+popup `!` ใน `PuppetPose` ด้วย (ทุกเครื่องเห็น)
**Minotaur** Sweep เบา (decal แดง→ทอง `ShowDecal` รับสี), Smash/Charge หนัก (decal กะพริบ 4 Hz, `Tint(axePivot)`, popup `!` ใน `Begin()`); `Deflect`: parry stun base 1.0×0.5, clash ไม่สตัน `Recover(0.5)`
**Game** `DamageKnight(..., Enemy by=null, bool heavy=false)` (ผู้เรียก Slime 1, Minotaur 3); `ReportDeflect`: host/SP → `by.Deflect`, client → `Net.SendHit` src 37/38; `DebugStrike` hook เทสต์

## 4. ข้อมูล / เซฟ
เซฟ loop/โลกเก่า **ไม่มีผล** (state ชั่วคราวทั้งหมด) | `PlayerPrefs` `mk_campitch` (float) เครื่องเดียว | เครือข่าย → Protocol 5

## 5. ลำดับขั้น (build ผ่าน + selftest ย่อย + commit แยก)
1. **เลขสี**: `NumberStyle`, heavy ใน `TakeHit`/`HitEnemy`/`ClientHit`, `EnemyHit` บิต 8, hook สี (ยังไม่ bump)
2. **parry/clash โลคัล**: `Knight` + `Deflect` + `DebugStrike` + เคส SelfTest
3. **ศัตรูส่ง `by/heavy`** + สัญญาณเตือน + `Minotaur.Deflect`
4. **ออนไลน์**: `Protocol=5`, `KnightHit`, `Hit.src` 37/38, `EnemyHit` deflect, หน่วง 0.1 วิ + เคส MpTest
5. **กล้อง scroll + F2**
6. **MapUi + M/Esc** + `PileMarks`
7. เอกสาร + รันครบ: selftest, `mptest.ps1`, `mptest-loop.ps1`, `-menutest`

## 6. ความเสี่ยง
| ความเสี่ยง | ทางกัน |
| --- | --- |
| client เห็นศัตรูช้า 0.1 วิ แต่ `KnightHit` มาก่อนภาพ → parry ต้องกดก่อนภาพ ≥0.1 วิ | หน่วง apply 0.1 วิ (ข้อ 8.1) |
| ปุ่ม M เปลี่ยนความหมาย | เทสต์เสียงย้ายเป็น F2; F1 แก้ 2 ที่ |
| Esc ตอนแผนที่เปิด ถูกนับ "Esc ออกโลก = ตาย" | เช็ก `MapOpen` ก่อน `LeaveKey` (ลำดับเดียวกับ `InventoryOpen`) |
| `src` 37/38 ชน kind อื่น | ใช้อยู่: 0..4, 10..13, 20, 30..36 → ว่าง |
| `tint` ชน flash | รวมจุดเดียวใน `BaseTick` |

## 7. ขนาดงาน: **กลาง** (~500–700 บรรทัด + เทสต์ ~300; 7 ขั้น)

## 8. design ขัดโค้ด (ให้ปูส้มตัดสิน)
1. **หน่วง `KnightHit` ที่ client 0.1 วิ** กระทบดาเมจทุกชนิดที่ client — ทางเลือก: ไม่หน่วง (parry ที่ client ยากกว่า host ~0.1 วิ)
2. สไลม์ **ไม่มีสัญญาณเตือนพื้นอยู่เดิม** — วงหดของแดงเป็นของใหม่ (design เขียนเหมือนมีอยู่)
3. hit-stop "แช่เฉพาะผู้เล่น+ศัตรู" ทำไม่ได้ถ้าไม่ใช้ timeScale → SP แช่ทั้งโลก 0.08 วิ ออนไลน์ไม่มี
4. บอส parry "stun 0.5 ตาม stunResist": ตั้ง base 1.0 ×0.5 = 0.5 วิจริง
5. ปุ่ม H ไม่ parry/clash (by=null); `vision.md` ยังไม่มีแถว phase 7
