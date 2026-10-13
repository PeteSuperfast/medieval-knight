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
