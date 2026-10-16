# phase6-combo — status

## ทำถึงไหน
- 2026-10-13 14:29 UTC: พีทสั่งเริ่ม "ท่าคอมโบ + ชาร์จ" → เขียน requirement.md; design-combo.md + decisions.md เดิมใช้ต่อ (ไม่ต้องส่ง designer ใหม่)

## ตัดสินไปแล้ว
- ใช้ design-combo.md 7 ท่า + decisions.md ของ combat-tweaks (พีท 2026-10-11)
- phase นี้ไม่รวม clash/parry, สกิลตามอาวุธ, อัปเกรดสกิล ฯลฯ

## เหลือ
1. architect (อ่าน requirement, design-combo, decisions, code ปัจจุบัน) → tech.md (ต้องระบุผลต่อออนไลน์/Protocol/mptest)
2. ปูส้มตรวจด่าน 2 + decisions → ขออนุมัติพีท → developer

## รอใคร
- รอ architect

## บันทึกผลตรวจ director
- ด่าน 1 (2026-10-13): อยู่ในเสาหลัก 1; ไม่ชน "ไม่ทำ" (ไม่ใช่อาวุธยิงไกล/class ใหม่); พีทสั่งเองจึงเพิ่มเป็น phase 6 ใน vision; design เดิมเขียนก่อน loop/co-op จึงใส่ข้อกำหนดเพิ่มข้อ 1–4 → ผ่าน

## อัปเดต 2026-10-13 (ปูส้ม)
- architect ส่ง tech.md + tech-net.md (กลาง 5 ขั้น, Protocol 3->4, ไม่มีไฟล์ใหม่ นอกจากเอกสาร) → ด่าน 2 ผ่าน: ตรง requirement/design, ไม่แตะ loop/ปุ่ม, ชี้จุดขัด 7 ข้อ → ปูส้มตัดสินใน decisions.md; แยก §3 เป็น tech-state.md ให้ ≤10KB
- โพสต์ขออนุมัติใน Discord แล้ว

## รอใคร (ปัจจุบัน)
- รอพีท (ID 372330922122674179) อนุมัติ tech/tech-net/decisions → ห้ามส่ง developer จนกว่าจะเห็นข้อความอนุมัติจริง

## อัปเดต 2026-10-13 14:44 UTC
- พีท (ID 372330922122674179) อนุมัติ tech/tech-net/tech-state/decisions ("ok") → ด่าน 1 ผ่าน → ส่ง developer (background) 5 ขั้น; ห้ามอัป Drive รอพีทสั่ง

## รอใคร (ปัจจุบัน)
- รอ developer ส่งรายงาน → director ด่าน 2 (build, selftest 0 FAIL, mptest.ps1 + mptest-loop.ps1 FAIL=0, -menutest, git diff --stat ตรงรายการไฟล์ใน tech.md) → โพสต์ "💻 Developer:"

## Developer 2026-10-13: ขั้น 1 เสร็จ (ข้อมูลท่า)
- AttackDef ฟิลด์ใหม่ + Attacks[5..11] + ComboLen=5 + NetKind/AttackFromNetKind (30..36) + DebugMove + Protocol 3->4 + ClientHit map ชื่อท่า
- build ผ่าน; selftest -looponly 0 FAIL (207 PASS); mptest.ps1 FAIL=0 (H61 A4 B2 C2 X1 V1)
- เหลือ: ขั้น 2 (เลือกท่า+หน่วง+Dash Slash) 3 (charge) 4 (ออนไลน์) 5 (เอกสาร+รันครบ)

## Developer 2026-10-13: ขั้น 2 เสร็จ (เลือกท่า + หน่วง 0.5 + Dash Slash)
- PickAttack (dash > ทิศ > เดิม), nextSwingAt + คิว 1 คลิก (หน้าต่าง 0.6 วิ), Sidestep/Jab/Retreat ก้าวเป็นเมตรรวม, SelfTest เคสใหม่ + โหมด `-p6only` (รันเฉพาะเคส phase 6 ~1 นาที)
- build ผ่าน; selftest -p6only 0 FAIL (26 PASS)
- เหลือ: ขั้น 3 (charge local) 4 (ออนไลน์) 5 (เอกสาร + รันครบ)

## Developer 2026-10-13: ขั้น 3 เสร็จ (charge local)
- คลิกตัดสินตอนปล่อย (TestHoldSeconds 0.15), ชาร์จเต็ม 1 วิ นับจากกด, Heavy Strike, เดินช้า 50%, วง+ดาบเรือง, ยกเลิกด้วยคลิกขวา/dash/สกิล/ล้ม, TeamRun ไม่ชุบขณะชาร์จ
- selftest เต็ม 0 FAIL (382 PASS) รวมเคสชาร์จ/ล้มกลางชาร์จ/loop smoke; `-p6only` ใช้รันเฉพาะเคส phase 6
- หมายเหตุ: บิต NetCharging ฝั่ง Knight ถูกใส่ใน build เดียวกัน; เคสเครือข่าย (MpTest) commit ในขั้น 4

## Developer 2026-10-13: ขั้น 4 เสร็จ (ออนไลน์)
- NetCharging (bit 32) ใน NetFlags, remote pose/glow, Act kind 30..36, Protocol 4; MpTest: holdon/holdoff, chg<pid>, เคส Vault Chop + Heavy + ยกเลิกชาร์จ + ล้มกลางชาร์จ
- mptest.ps1 (-arena) FAIL=0: H70 A4 B2 C2 X1 V1

## Developer 2026-10-13: ขั้น 5 เสร็จ (เอกสาร + รันครบ) — ปิด phase 6
- selftest เต็ม 0 FAIL (382 PASS); mptest.ps1 H70 A4 B2 C2 X1 V1 FAIL=0; mptest-loop.ps1 H81 A5 B5 C2 X1 V1 D4 FAIL=0; -menutest failed checks=0
- เอกสารอัปเดต: CLAUDE.md, controls, dev-guide, online-coop, files (ทุกไฟล์ <= 10KB)
- สร้าง medieval-knight-win64.zip ในโฟลเดอร์เกม (ไม่ได้อัป Drive ตามสั่ง)
- รอ: พีทลองเล่นว่ารู้สึกดีไหม

## ผลตรวจ director ด่าน 2 (developer phase 6)
- 5 ขั้น (898275d..fbacb38); git diff --stat 12 ไฟล์ ตรงรายการใน tech.md (Knight/Net/Game/TeamRun/SelfTest/MpTest + docs) ไม่มีไฟล์นอกแผน
- ผลเทสที่ developer รายงาน: build ผ่าน, selftest เต็ม 382 PASS / 0 FAIL, mptest.ps1 -arena FAIL=0, mptest-loop FAIL=0, -menutest 0
- ทำเพิ่มเล็กน้อย: โหมด -p6only + hook เทสต์ (ไม่กระทบเกมเพลย์) | ต่างจากแผน: ชาร์จนับ 1 วิจากตอนกด (ตรง design-combo)
- ข้อสังเกต: ยังไม่ได้ดูเอฟเฟกต์ด้วยตา; ยังไม่อัป Drive; Heavy ต่อคอมโบยาก (ชาร์จ ≥1 วิ เกินหน้าต่าง 0.9 วิ) ให้พีทตัดสินหลังลองเล่น
- ปูส้ม: ผ่าน -> โพสต์ให้พีท

## รอใคร (ปัจจุบัน)
- รอพีทลองเล่น + สั่งอัป Drive

## ด่าน 1 (2026-10-15): test_disk ขอ "เหลือท่าฟันเดียวตาม Standing_Melee_Attack_Downward.fbx"
- ยังไม่เริ่ม: (1) ขัด phase 6 ที่พีทสั่ง (7 ท่าคอมโบ + Heavy) ซึ่งรอพีทลองเล่น (2) เกมไม่มีโมเดล/skeleton ท่าฟันเป็นโค้ด จึงใช้ fbx ตรงๆ ไม่ได้ (3) กระทบ Net kind 30..36 / Protocol / selftest / mptest
- รอพีท (ID 372330922122674179) ตัดสินใจ
