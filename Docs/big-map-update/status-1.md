# Status: big-map-update
> ส่วน 1/2 ของ `status.md` (ดัชนี: `status.md`) · ต่อที่ `status-2.md`

- เกม: medieval-knight
- คนขอ: test_disk (Discord 449209885138223116)
- บันทึกเมื่อ: 2026-10-11 02:31 (เวลาไทย) โดยปูส้ม ตามคำสั่งพีท
- ห้องงาน: pusom-dev (Discord channel 1558546783880089661)

## ตอนนี้อยู่ขั้นไหน

**ปิดงานแล้ว (พีทยืนยันในห้อง pusom-dev 2026-10-11 05:34 เวลาไทย: "เรื่อง big-map-update จบไปแล้วนิ")** — ข้อความด้านล่างเป็นบันทึกตอนส่งมอบ ปูส้มยังไม่ได้ยืนยันเองว่า push GitHub แล้วหรือยัง และ branch `wip/big-map-update` ยังไม่ได้ลบ

**developer ทำครบ A-G แล้ว (04:20 เวลาไทย 2026-10-11)** — build + selftest ผ่าน, mptest ผ่านยกเว้น 1 เช็คเดิมที่พังมาก่อนงานนี้; zip ขึ้น Drive แล้ว; **ยังไม่ได้ push GitHub**; รอปูส้มตรวจรายงานแล้วโพสต์ "💻 Developer:" แท็ก test_disk

| ขั้นตอนทีม | สถานะ |
| --- | --- |
| 1. คุย requirement | เสร็จ |
| 2. requirement.md + คนขอยืนยัน | เสร็จ — test_disk ยืนยัน 2026-10-11 01:40 ("เริ่มได้เลย เอาทั้งหมด") |
| 3. designer + architect | เสร็จ — design.md, tech.md (รอบนี้ส่งพร้อมกันตามกฎเดิม ก่อนพีทเปลี่ยนเป็นส่ง designer ก่อน) |
| 4. ตรวจ | เสร็จ — ส่งกลับแก้ 1 รอบ (ขัดกันหนัก) ที่เหลือปูส้มตัดสินเองใน decisions.md |
| 5. โพสต์สรุป ขออนุมัติ | เสร็จ |
| 6. อนุมัติ | **เสร็จ — test_disk พิมพ์ "OK" 2026-10-11 02:07** ในห้อง pusom-dev |
| 7. developer ลงมือ | **เสร็จ** (A-G) |
| 8. โพสต์ผล + zip ขึ้น Drive | zip ขึ้น Drive แล้ว; รอปูส้มโพสต์ผลในห้อง |

## งานของ developer (7 ขั้นตาม tech.md หัวข้อ 10)

สถานะนับจาก git ณ เวลาที่บันทึก: ขั้นที่ commit แล้ว = build และเทสผ่านตามกติกาที่สั่งไว้ (ปูส้มยังไม่ได้ตรวจผลเทสเอง รอรายงานของ developer)

| ขั้น | เนื้องาน | สถานะ | commit |
| --- | --- | --- | --- |
| เอกสาร | requirement / design / tech / decisions | commit แล้ว | `4ca65db` |
| A | Shift วิ่ง, Space dash (อมตะ 0.5 วิ, stamina 25), คลิกขวายกโล่, stamina ใช้กับ dash อย่างเดียว, Tab เปิดหน้าต่าง, Esc ปิด Tab ก่อน | commit แล้ว | `7751284` |
| B | เลเวล 25 + ตาราง EXP, ตายแล้วรีเซ็ต, Soul, หน้าจอ YOU DIED, protocol 2 | commit แล้ว; build + selftest รันซ้ำ ผ่าน (109 PASS, 0 FAIL) | `feb415e` |
| C | อาหาร (Apple / Roast Meat) | commit แล้ว; selftest ผ่าน; mptest ผ่านหมดยกเว้น 1 เช็ค "A's walk looks smooth on B" ซึ่งพังเหมือนกันบน `4b146ba` (ก่อนเริ่มงานนี้) = ไม่ใช่ regression | `5013ddd` |
| D | หน้าต่าง Tab รวมช่องเก็บของ + ค่าสถานะ, รูปดาบ | commit แล้ว (เก็บงานค้างของ agent ก่อนหน้า) build + selftest ผ่าน 0 FAIL; ไอคอนดาบ render จริงจากโมเดล (ไม่ต้องใช้แผน B) | `a866701` |
| E | แมพ 380 x 380 ม. แบบ chunk + collision (ใหญ่และเสี่ยงสุด) | commit แล้ว: build + selftest ผ่าน 0 FAIL (เพิ่มเช็คแมพ/ชนกำแพง/ทะเลสาบ/ภูเขา/ต้นไม้/เฟรมไทม์ ~4.2 ms ทุกจุด), mptest ผ่านหมดยกเว้นเช็คการเดินเนียนข้อเดิม + เช็คมุมแมพ 4 มุมผ่าน; ฉากสร้าง 55 ms 1,270 chunk mesh 1,304 obstacle | ดู git log |
| F | ระบบเกิดสไลม์เป็นแพ็ครายคน, leash ของ Minotaur, ลูกศรชี้รัง | commit แล้ว: build + selftest ผ่าน 0 FAIL (เพิ่มเช็คเมืองปลอดภัย / แพ็ค 10-26 ม. / ลบเมื่อไกล 70 ม. / สไลม์ไม่ไล่เข้าเมือง / leash / ลูกศรรัง); mptest ผ่านหมดยกเว้นเช็คการเดินเนียนข้อเดิม (พังเหมือนกันบน 4b146ba) | ดู git log |
| G | อัปเดต CLAUDE.md ของเกม, zip, copy ขึ้น Drive | commit แล้ว: CLAUDE.md เขียนใหม่ตามของจริง, zip `medieval-knight-win64.zip` (38.4 MB, 184 ไฟล์ โครงเดิม MedievalKnight/...) copy ทับใน `G:My DrivePusomGames` แล้ว | ดู git log |

- branch: `main` (นำหน้า `origin/main` หลาย commit ยังไม่ได้ push ตามที่พีทสั่งให้รอ)
- `wip/big-map-update` (`d3303ef`): โค้ดครึ่งๆ กลางๆ จากรอบที่พีทสั่งหยุด **ไม่ได้ใช้** ยังไม่ได้ลบ
- ไฟล์ใน `G:\My Drive\PusomGames\medieval-knight-win64.zip` เป็นเวอร์ชันใหม่ (big-map-update) แล้ว (04:17)

