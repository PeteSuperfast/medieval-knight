# phase1-minimal-loop — status

## ทำถึงไหน
**เสร็จทั้ง 10 ขั้นของ tech.md §5 (2026-10-12)** — developer ทำตามแผนที่พีทอนุมัติ ทุกขั้น build ผ่านและ commit แยก

| ขั้น | เนื้อหา | ผล |
|---|---|---|
| 1 | `Args` -loop/-save, `InputX` C, `MapLayout.BasePoint` | build ผ่าน |
| 2 | `Loadout.cs` + เคสข้อมูล | ★ selftest ผ่าน |
| 3 | `LoopSave.cs` + เคสเซฟ/โหลด/ไฟล์เสีย | ★ ผ่าน |
| 4 | `Game.Loop`, `StartSession(loop)`, `RunLoop` โครง, `Menu.StartSingle` | ★ ผ่าน |
| 5 | PREPARE เต็ม (ย้าย / HELD / เตือนไม่มีอาหาร / ENTER WORLD) | เคส prepare ผ่าน |
| 6 | เข้าโลก (เซฟ inRun, fade, เลเวล 1, HELD), ปุ่ม 1/2, Esc เตือน | เคส world ผ่าน |
| 7 | เก็บของเข้า carry, Pack full, กิน C, หน้าต่าง Carry, Drop | ★ ผ่าน |
| 8 | ตาย = จบรอบ, YOU FELL / ABANDONED, Iron restored | ★ ผ่าน |
| 9 | บอสไม่ respawn, portal, VICTORY, LEFT BEHIND | ★ ผ่าน |
| 10 | ซ่อน Soul, ปิด F5, ข้อความ F1, แก้ `-menutest`, เอกสาร | build + selftest + mptest + menutest |

## ผลทดสอบตอนจบ
- build: ผ่าน (errors=0)
- selftest เต็ม: **239 PASS / 0 FAIL** (เคส loop 104 ข้อ ท้ายไฟล์; `-looponly` รันเฉพาะชุดนี้ ~100 วิ)
- mptest.ps1: H PASS=61, A 4, B 2, C 2, X 1, V 1 — **FAIL=0 ทุก role**, exceptions=0
- `-menutest`: PASS 16 / FAIL 1 — ส่วน Single Player (เข้าฐาน, PREPARE, เดิน, Esc Esc) ผ่านทั้งหมด; FAIL เดียว = "Start Hosting ... Enemy.All.Count >= 5" (เช็คเก่าก่อน big-map-update F: เมืองเป็นเขตปลอดภัย ไม่มีสไลม์ตอนเริ่ม จึงเหลือแค่หุ่น + Minotaur) ไม่เกี่ยวกับ loop ไม่ได้แก้ (นอก scope; ขอพีทตัดสินว่าจะลดเป็น >= 2 ไหม)
- flake ที่เจอ: selftest เคส v1 สุ่มดรอป Banana Dao (1 ครั้ง 5 FAIL) และ "ready voice line" (1 ครั้ง) รันซ้ำผ่านทั้งคู่

## ทำต่างจากเอกสาร (เหตุผล)
1. **ช่องคลัง (stash) ใน PREPARE ขนาด 54 px** (design 88 px) — 12 + 8 + 16 ช่องต้องอยู่ในจอเดียว; ช่อง carry 88 px ตามเดิม
2. **หน้าต่าง Tab ในโลกเป็น LoopUi (Carry window)** ไม่ได้ปรับ panel เดิมของ `Hud` (tech.md §3 บอกให้แก้ Hud) — แยกไฟล์ใหม่ ไม่แตะ v1; ผู้เล่นเห็น: ช่อง Weapons 2 / Food 3 / Supplies 4 + สเตตัสตัวละคร (ไม่มีตาราง Skills)
3. **ลูกศรชี้ portal ใช้ลูกศรบนกลางจอเดิม** (ที่เดียวกับลูกศรรัง) ไม่ได้ทำลูกศรที่ขอบจอ; ป้าย RETURN PORTAL เป็นตัวหนังสือบนจอที่ตามตำแหน่ง portal
4. **วงโหลด portal เป็น 24 ชิ้นบนพื้นรอบวง** (ไม่ใช่วง UI)
5. แบนเนอร์ portal แสดงหลัง "MINOTAUR SLAIN!" และหลังเลือกการ์ดเลเวลอัปเสร็จ (การ์ดเลเวลอัปล้างแบนเนอร์)
6. ตายที่ฐานด้วย `H` (test hit) = ลุกที่ฐานไม่เสียอะไร (ไม่มีในเอกสาร; กันค้าง)
7. สวิตช์เทสต์ `-looponly` (SelfTest) เพิ่ม — รันเฉพาะเคส loop เร็วขึ้น ไม่มีผลต่อเกม

## ตัดสินระหว่างทำ
- decisions.md ทั้ง 15 ข้อยึดตามเดิม; Drop (คลิกขวา) ตาม #1; ห้ามทิ้งอาวุธชิ้นสุดท้ายในช่อง Weapons (#8)
- Weapon ใน Supplies ถือไม่ได้จนกว่าย้ายเข้า Weapons (คลิกในหน้าต่าง Carry) ตาม #4
- รวม +N: `min(5, a + b + 1)` ตาม #9; stash เต็มตอนรอด = LEFT BEHIND ตาม #10

## เหลือ / รอ
- **รอพีทเล่นมือ**: รอด 1 รอบ + ตาย 1 รอบ + ปิดเกมกลางโลก (ภาพหน้าจอของ selftest ไม่ถูกเขียนในสภาพแวดล้อมนี้ จึงยังไม่ได้เทียบหน้าตา PREPARE / สรุปกับ design §3 ด้วยตา)
- phase 2 (โลกเก่า) ยังไม่เริ่ม; หัวข้อ "รอพีทตัดสิน" ใน vision.md ที่เกี่ยวข้องต้องตอบก่อน
- เอกสาร: `Docs/reference/loop.md` (ใหม่), `files.md`, `controls.md`, `weapons-and-tab.md`, `map-and-spawning.md`, `CLAUDE.md` อัปเดตแล้ว

## ส่งขึ้น Drive
- zip `medieval-knight-win64.zip` (185 ไฟล์ โครง `MedievalKnight/...` เดิม, 38,656,107 bytes ≈ 36.9 MiB) copy ทับใน `G:\My Drive\PusomGames\` แล้ว จาก build ที่ผ่าน selftest 239 PASS + mptest FAIL=0
- 2026-10-13 พีทลองเล่นมือจริงแล้วถือว่าดี → Phase 1 ปิด; menutest host เกณฑ์ลดเป็น >= 2 (พีทสั่ง) menutest 17 PASS / 0 FAIL
- 2026-10-13 แก้บั๊ก (พีทแจ้ง): ฆ่าบอสแล้วมี level-up card เด้งตอนยืนใน portal → หน้า VICTORY ทับ card กดไม่ได้. สาเหตุ: portalHold นับด้วย unscaledDeltaTime ทั้งที่เวลาหยุดตอนเปิด card. แก้: วงโหลด portal ไม่เดินเมื่อ PanelOpen หรือ pendingChoices>0 (RunLoop.TickPortal) + เคส selftest ใหม่ loop cases 0 FAIL
