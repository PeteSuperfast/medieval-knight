# core-v2 — status
(ติดตาม phase ตาม Docs/vision.md)

## Phase 0 — เสร็จ 2026-10-12
- ทำถึงไหน: แก้ mptest "A's walk looks smooth on B/H" ที่ตกมาตั้งแต่ 4b146ba
- สาเหตุ: `NetInterp.offset` ใช้ค่ามากสุดที่เคยเห็น คลายช้ามาก (0.4 ms/ตัวอย่าง ≈ 6 ms/วินาที) ตัวอย่างที่ส่งถึงเร็วผิดปกติครั้งเดียวทำให้เวลาที่วาด "เร็วเกินจริง" ~0.15–0.2 วินาที → buffer หมด (margin ติดลบ) ค้าง/กระโดด ตกสลับจอ H/B ในแต่ละรอบ
- แก้: offset = ค่ามากสุดของ 40 ตัวอย่างล่าสุด (~2 วินาที) — ใช้กับศัตรูฝั่ง client ด้วย
- ผล: mptest 3 รอบติด FAIL=0 jumps 0 stalls ~12 จาก ~360 เฟรม; selftest 0 FAIL (รอบแรกมี FAIL 5 ข้อเรื่องเก็บดาบ Banana Dao ไม่ทัน เป็น timing flake ไม่เกี่ยว รันซ้ำผ่าน 0 FAIL)
- ตัดสินใจ: ไม่ปรับ KnightDelay (0.13) เพราะ margin ต่ำสุด 0.03 s ยังบวก
- เหลือ: phase 1 ต้องคุยให้ชัด + requirement.md ก่อน (ดูหัวข้อรอตัดสินใน vision.md)
- รอใคร: พีท (phase 1 requirement)

## Director review (ด่าน 2, phase 0)
- ขอบเขต: แตะเฉพาะ NetInterp.cs (+ CLAUDE.md/vision.md/status) ✔ ไม่มีของแถม ✔ build ผ่าน ✔ ไม่นอกโฟลเดอร์ ✔
