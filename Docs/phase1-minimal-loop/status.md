# phase1-minimal-loop — status
- ทำถึงไหน: เขียน requirement.md (ร่าง) จากคำตอบของพีท 2026-10-12 ครบ 3 ข้อ
- ตัดสินแล้ว: Iron Sword เล่มเดียวตอนเริ่ม / เลือกของที่พกได้ N ชิ้น / 3 แบบช่อง (อาวุธ อาหาร สัมภาระ) / ตายของหายยังไม่มีเก็บคืน / แจก Iron Sword ฟรีเสมอ / เก็บข้ามรอบเฉพาะอาวุธในคลัง
- เหลือ: พีทอนุมัติ → developer (10 ขั้นใน tech.md §5)
- รอใคร: พีท (อนุมัติ design.md + tech*.md)
- 2026-10-12 13:36 พีทยืนยัน requirement (ok) → ส่ง designer แบบ background
- 2026-10-12 designer เสร็จ design.md ผ่านด่าน 2 → decisions.md (7 ข้อ) → ส่ง architect
- 2026-10-12 architect เสร็จ tech.md + tech-data-save/flow/test.md ผ่านด่าน 2 → decisions #8-15 → โพสต์ขออนุมัติ
- 2026-10-12 developer เริ่ม (พีทอนุมัติเอกสารแล้ว): ขั้น 1 เสร็จ (Args -loop/-save, InputX C, MapLayout.BasePoint) build ผ่าน
- ขั้น 2 เสร็จ: Loadout.cs (Item/ItemBag/Loadout/RunResult) + selftest เคสข้อมูล 13 เคส; build ผ่าน selftest 153 PASS / 0 FAIL
