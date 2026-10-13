# phase2-old-world — status

## ทำถึงไหน
- 2026-10-13 (ปูส้ม): พีทสั่ง "ดำเนินการ phase 2" → ถามข้อค้าง (พีทตอบ 1.a / 2.ใช่) → requirement → designer → architect → ปูส้มตัดสิน decisions ข้อ 7–14 → พีทอนุมัติ ("ok" 09:36 UTC)
- 2026-10-13 (developer): ทำครบ 7 ขั้นของ tech §9 — build ผ่านทุกขั้น, selftest ที่เกี่ยวข้องผ่าน, commit แยกต่อขั้น (ผู้ขอ peatengkrub)
  1. `OldWorld.cs` + `Loadout` (Die / Survive / take / Sanitize) + เคสข้อมูล
  2. `LoopSave` DTO โลกเก่า (Version ยัง 1; เซฟ phase 1 เปิดได้) + เคสเซฟ
  3. ตัวคูณใน `Game` (HP ×1.5, ดาเมจ ×1.25, แพ็ค ×1.5; armor ไม่คูณ) + เคสสเกล
  4. `RunLoop` ตาย / ABANDONED / Victory + autosave ตำแหน่ง + G2 (บล็อก OLD WORLD ในหน้าสรุป)
  5. PREPARE NEW WORLD / OLD WORLDS, แถบแดงสลาย, หน้ารายการ, เข้าโลกเก่า (ถามอาหาร, banner, ป้าย, objective 2)
  6. `LootPile` + ลูกศร/ป้าย + หน้าต่างเก็บของ (เก็บ / Take all / เต็ม / ทิ้ง) + เซฟทุกครั้งที่เก็บ
  7. ตรวจรวม + docs (loop.md, files.md, controls.md, CLAUDE.md, vision.md) + zip

## ผลตรวจ (ก่อนส่งงาน)
- build ผ่าน; selftest เต็ม **315 PASS / 0 FAIL**; `-looponly` 180 PASS / 0 FAIL (เคสใหม่ 58 เคส: `old world ...`)
- mptest: รอบ 1 H FAIL=1, รอบ 2 H FAIL=2 (เช็ค "stands at the same place on every screen" คลาดเคลื่อน 0.15-0.3 ม. ตัวผู้เล่นคนละคนกัน ไม่เกี่ยวกับโลกเก่า), รอบ 3 **FAIL=0** (H 61 PASS, A 4, B 2, C 2, X 1, V 1) — flake ด้านเวลา ไม่ได้แตะ Net
- `-menutest` ผ่านทั้งหมด (failed checks=0)

## ตัดสิน / ทำต่างจากแผน
- armor ไม่คูณ (decisions ข้อ 9): เพิ่ม `Game.EnemyArmorScale` แก้ 1 บรรทัดใน `Slime.cs` / `Minotaur.cs` (tech §6 ว่า "ไม่แก้ไฟล์เหล่านั้น" แต่ decisions ชนะ)
- ป้ายโลกเก่า (D2) วางซ้ายใต้กล่อง HP/LV แทนมุมขวาบน เพราะแผงปุ่มควบคุม (F1) ของ HUD อยู่มุมขวาบนและทับ
- ล้อเมาส์เลื่อนรายการไม่ทำ (ไม่แตะ InputX): เลื่อนด้วยลูกศร ขึ้น/ลง (ลง backlog)
- แถวในหน้ารายการแสดงไอคอนไอเท็ม 4 ชิ้น + `+N` ตาม design; ตัวอักษรม่วงใช้โทนสว่างกว่า #5E5878 (อ่านบนพื้นมืดได้) กรอบใช้ #5E5878 ตามเดิม
- ไม่ทำ: ฟอกหมอกม่วงเทา (decisions ข้อ 6), co-op, biome, Soul

## เหลือ
1. director ด่าน 2 → โพสต์ "💻 Developer:" แท็กพีท + zip ขึ้น Drive (ทำแล้วตอน developer ส่งงาน)
2. พีทลองเล่นจริง: ตายในโลกใหม่ → OLD WORLDS → เก็บคืน → ฆ่าบอส → กลับฐาน 3 ครั้ง ดูการสลาย
3. ข้อที่ต้องแจ้งพีท (decisions ข้อ 10, 13): กองสูงสุด 48 ชิ้น / โลกเก่า 99 โลกตอนโหลดเซฟ; เซฟตำแหน่งทุก ~10 วิ + ตอนปิดเกม (ผ่อนกฎ phase 1 "ปิดเกมไม่แตะเซฟ")
4. design.md เกิน 10KB (13 KB) — ไม่ใช่ของ developer ตัดตอนปิดงาน

## บันทึกผลตรวจ director (ด่าน 2 หลัง developer ส่งงาน, 2026-10-13)
- ผ่าน: ไฟล์ที่เปลี่ยนตรง tech.md (+ Slime.cs/Minotaur.cs 1 บรรทัดต่อไฟล์ ตาม decisions ข้อ 9), 8 commit แยกตามขั้น, zip 38,679,847 B อยู่บน Drive, ไม่แตะ InputX/Net/Menu, ไม่มีของแถม
- ข้อสังเกต: mptest ไม่ผ่านใน 2 รอบแรก (H FAIL 1 และ 2) ผ่านรอบที่ 3 — ยังไม่ได้เทียบกับ commit ก่อนหน้าเพื่อยืนยันว่าเป็น flake; ยังติดตามต่อ
- design.md 13 KB เกินเพดาน 10 KB ของ vision (ยังไม่ได้ตัด)
