# phase7-clash-polish — status

## ทำถึงไหน
- 2026-10-13 17:02 UTC: พีทสั่ง "ทำ a b c" → ปูส้มเขียน requirement.md (A clash/parry + B1 ของเล็ก); B2 (กระเป๋า 8 ช่อง/คีย์ลัด/เมนู Esc) ตัดออกเพราะชน loop แจ้งพีทแล้ว; C เก็บกวาดเอกสารส่งให้ agent แยก

## ตัดสินไปแล้ว
- ใช้ที่ test_disk ยืนยันไว้ (parry 0.2 วิ, ท่าหนักทะลุ, clash ไม่เสียหายทั้งคู่)

## เหลือ
1. designer → design.md (feedback, ท่าไหนของศัตรูเป็นท่าหนัก, UI แผนที่ M, สีตัวเลข)
2. architect → tech.md (host/client authority, Protocol)
3. ปูส้มตรวจ → พีทอนุมัติ → developer

## รอใคร
- รอ designer

## บันทึกผลตรวจ director
- ด่าน 1: เสาหลัก 1; ไม่ชน "ไม่ทำ"; ข้อ B2 ชน loop/vision จึงตัดและแจ้งพีท; พีทสั่งโดยตรง → ผ่าน

## อัปเดต 2026-10-13 (ปูส้ม)
- designer ส่ง design.md + design-ui.md → ด่าน 2 ผ่าน: ตรง requirement ข้อ 1–8, ไม่มีของที่ไม่ได้สั่ง (ข้อเสนอเกินอยู่ในคำถาม ปูส้มตัดสินใน decisions.md), ไม่แตะ B2; แยก design.md เป็น design-enemy.md ให้ ≤10KB
- ส่ง architect (background) ทำ tech.md

## รอใคร (ปัจจุบัน)
- รอ architect

## งาน C (เก็บกวาดเอกสาร) 2026-10-13
- agent ตัด/แยกเอกสาร 54 ไฟล์ (commit 3df4c3c, เฉพาะ .md, lossless, ดัชนีที่ชื่อเดิม) → ทุก .md ในเกมนอก Art_kit ≤10KB; ปูส้มแก้ files.md Protocol 3→4
- ยังเกิน: Art_kit/approved.md 11.9KB (ถูกอ้างชื่อในขั้นตอนนำ asset เข้าเกม ไม่แตะ)

## อัปเดต (ปูส้ม)
- architect ส่ง tech.md + tech-net.md + tech-ui.md (กลาง 7 ขั้น, Protocol 4→5, ไม่เพิ่ม Msg) → ด่าน 2 ผ่าน: ตรง requirement/design/decisions, parry/clash ตัดสินที่เจ้าของตัว + รายงาน host ด้วย Hit src, แผนที่ใช้ข้อมูลเดิมไม่เพิ่ม Msg; ปูส้มตัดสิน decisions ข้อ 10–14; เพิ่มแถว phase 7 ใน vision
- โพสต์ขออนุมัติใน Discord แล้ว

## รอใคร (ปัจจุบัน)
- รอพีท (ID 372330922122674179) อนุมัติ → ห้ามส่ง developer จนกว่าจะเห็นข้อความอนุมัติจริง

## อัปเดต 2026-10-13 17:25 UTC
- พีท (ID 372330922122674179) อนุมัติ ("ok") → ด่าน 1 ผ่าน → ส่ง developer (background) 7 ขั้น; ห้ามอัป Drive/push รอพีทสั่ง

## รอใคร (ปัจจุบัน)
- รอ developer → ด่าน 2 (build, selftest 0 FAIL, mptest.ps1 + mptest-loop.ps1 FAIL=0, -menutest, git diff --stat ตรง tech.md) → โพสต์ "💻 Developer:"

## Developer 2026-10-14
- ขั้น 1 เสร็จ: เลขสี (`Enemy.NumberStyle`, `lastNumberKind`, heavy ใน `TakeHit`/`HitEnemy`/`ClientHit`, `EnemyHit` บิต 8); build ผ่าน; `-p7only` ผ่าน (ยังไม่ bump protocol)
- ขั้น 2 เสร็จ: parry/clash โลคัล (`Knight.TakeDamage(by, heavy)`, หน้าต่าง parry 0.2 วิ + กันสแปม 0.5 วิ, โล่แฟลช, `Enemy.Deflect`, `Minotaur.Deflect`, `Game.DebugStrike`); `-p7only` ผ่านทุกเคส (ส่ง by/heavy จาก Slime/Minotaur ทำไปในขั้นนี้ด้วย เพราะเทสต์ต้องใช้)
- ขั้น 3 เสร็จ: สัญญาณเตือน (สไลม์แดง: วงหด+เรือง+`!`; Minotaur Sweep ทอง, Smash/Charge แดงกะพริบ 4 Hz + ขวาน/เขาเรือง + `!`), `Enemy.glowAmt` รวมกับ flash ใน `BaseTick`, `HeavyWarn`; build ผ่าน `-p7only` ผ่าน
- ขั้น 4-6 เสร็จ (commit รวม เพราะแก้ไฟล์เดียวกัน): ออนไลน์ `Net.Protocol` 4→5 (`KnightHit` + attackerNetId/flags, `Hit.src` 37/38, `EnemyHit` 8/16/32, หน่วง 0.1 วิที่ client), กล้อง scroll (`mk_campitch`) + F2 เสียง, `MapUi.cs` แผนที่ M + `RunLoop.PileMarks` + Esc ปิดแผนที่ก่อน; เคส SelfTest `Phase7` + MpTest 3c + MpLoopTest mapportal
- ขั้น 7 เสร็จ: เอกสาร (controls, online-coop, files, sound, dev-guide, CLAUDE.md ≤10KB); ผลเทสต์ (build ผ่าน): selftest เต็ม 416 PASS / 0 FAIL; `mptest.ps1` H=77 PASS FAIL=0; `mptest-loop.ps1` H=82 PASS FAIL=0 (A,B,C,D,X,V ไม่มี FAIL); `-menutest` 19 PASS FAIL=0
- หมายเหตุ: รันแรกของ mptest มี 2 FAIL (เคส 3c ตัวเองใหม่) เพราะสไลม์จริงตีในหน้าต่าง parry → แก้เทสต์ให้ ParkSlimes + วางผู้เล่นไกลมุม; เคสต่อมาผ่านทุกรอบ. selftest รอบแรก 19 FAIL (moveOverride ค้างจากเคสใหม่ของผมเอง) + 1 FAIL (สไลม์จริงกวน) → แก้เทสต์แล้วผ่าน 0 FAIL
- ยังไม่ทำ (ตามสั่ง): ไม่ push, ไม่ copy zip ขึ้น Drive; `medieval-knight-win64.zip` อยู่ในโฟลเดอร์เกม

## รอใคร (ปัจจุบัน)
- รอปูส้มตรวจด่าน 2 → พีทลองเล่น (parry/clash ที่จังหวะจริง, ความรู้สึกหน้าต่าง 0.2 วิ, แผนที่ M)

## ผลตรวจ director ด่าน 2 (developer phase 7)
- ครบ 7 ขั้น (commit c5f72c3..4c0263b); git diff --stat 21 ไฟล์ ตรงรายการ tech.md (ใหม่ MapUi.cs + สคริปต์/เอกสารตามแผน) ไม่มีไฟล์นอกแผน; ไม่แตะ B2
- ผลเทสที่ developer รายงาน: build ผ่าน, selftest 416/0, -p7only 30 PASS, mptest -arena FAIL=0, mptest-loop FAIL=0, -menutest 19/0
- ต่างจากแผน: commit ขั้น 4-6 รวมเป็น b53e83a; แผงแผนที่ 566x646; มี test hook เล็กๆ (ไม่กระทบเกม)
- ปูส้มตัด vision.md / design-ui.md ให้ ≤10KB แล้ว
- ยังไม่อัป Drive / ไม่ push (รอพีทสั่ง); พีทควรลองเล่น: หน้าต่าง parry 0.2 วิ, clash ±0.1 วิ, แผนที่ M ทับแผง F1, เสียง parry/clash ใช้ตัวเดียวต่าง pitch
- ปูส้ม: ผ่าน -> โพสต์ให้พีท
