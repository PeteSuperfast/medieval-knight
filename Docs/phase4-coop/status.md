# phase4-coop — status

## ทำถึงไหน
- 2026-10-13: พีทสั่งข้าม phase 3 มาทำ phase 4 → ปูส้มถามข้อค้างใน vision → พีทตอบ 1.A / ทีมรอด=ไม่มีโลกเก่า คนตายไม่เสียของ / 3.B → เขียน requirement.md (ร่าง) เสร็จ

## ตัดสินไปแล้ว
- ล้มรอ 60 วิ ไม่มีใครชุบ = ตาย; ทุกคนล้ม = จบรอบ ของตกเป็นโลกเก่า (พีท 1.A)
- ทีมรอด = ไม่มีโลกเก่า คนที่ตายไปแล้วไม่เสียของ (พีท)
- กองของ ใครในทีมก็เก็บได้ (พีท 3.B)

## เหลือ
1. พีทยืนยัน requirement.md (รวมตาราง "รอยืนยัน")
2. designer → design.md; architect (อ่าน design.md) → tech.md (ต้องระบุ Net.Protocol ใหม่ และแผน mptest ชุดใหม่)
3. ตรวจ + decisions.md → ขออนุมัติ → developer → build/selftest/mptest
4. กลับไป phase 3 หลังจบ phase 4

## รอใคร
- รอพีทยืนยัน requirement.md

## บันทึกผลตรวจ director
- ด่าน 1 (2026-10-13): phase 4 อยู่ใน vision; ลำดับข้าม phase 3 เป็นคำสั่งเจ้าของ (แก้ vision/CLAUDE.md แล้ว); ไม่ชน "ไม่ทำ"; ไม่ชนงานค้าง (phase 2 รอเทส+อัป Drive) → ผ่าน
- ด่าน 2 (requirement.md ที่ปูส้มเขียนเอง): ครอบคลุมคำตัดสินของพีททั้ง 3 ข้อ + เงื่อนไข vision phase 4, ขอบเขตไม่เกิน → ผ่าน

## อัปเดต 2026-10-13 11:39 UTC
- พีทยืนยัน requirement.md ("ok") รวมกติกา wipe: ของแต่ละคนตกตำแหน่งล่าสุดของคนนั้น (หลายกอง, กองไม่ย้าย — ใช้กับ Single Player ด้วย)
- ส่ง designer (background) ทำ design.md แล้ว

## รอใคร (ปัจจุบัน)
- รอ designer ส่ง design.md → ตรวจด่าน 2 → ส่ง architect

## อัปเดต 2026-10-13 (ปูส้ม)
- designer ส่ง design.md → ด่าน 2 ผ่าน → ปูส้มตัดสิน decisions ข้อ 1–5
- architect ส่ง tech.md + tech-net.md (11 ขั้น ขนาดใหญ่ Protocol 2→3) → ด่าน 2 ผ่าน: ตรง requirement/design, ฐาน=โลกพื้นที่เดียวยืนยันจากโค้ดจริง, v1 ย้ายไป -arena + mptest เดิมต้องผ่านทุกขั้น → ปูส้มตัดสิน decisions ข้อ 6–11
- โพสต์ขออนุมัติใน Discord แล้ว

## รอใคร (ปัจจุบัน)
- รอพีท (ID 372330922122674179) อนุมัติ design/tech/tech-net/decisions → ห้ามส่ง developer จนกว่าจะเห็นข้อความอนุมัติจริง

## อัปเดต 2026-10-13 12:01 UTC
- พีท (ID 372330922122674179) อนุมัติ design/tech/tech-net/decisions ("ok") → ส่ง developer (background) ทำ 11 ขั้นแล้ว (ห้ามอัป Drive; รอพีทสั่ง)

## รอใคร (ปัจจุบัน)
- รอ developer ส่งรายงาน → director ด่าน 2 (build/selftest/mptest ทั้งสองชุด, git diff --stat ตรงรายการไฟล์ใน tech.md) → โพสต์ "💻 Developer:" แท็กพีท
