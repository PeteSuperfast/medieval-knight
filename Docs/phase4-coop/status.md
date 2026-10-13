# phase4-coop — status

## ทำถึงไหน (2026-10-13)
**developer ทำครบ 11 ขั้นตาม tech.md §6 แล้ว** — build ผ่าน, selftest 0 FAIL, mptest v1 (`-arena`) FAIL=0, mptest-loop FAIL=0, `-menutest` ผ่าน. รอ director ด่าน 2 + พีทลองเล่น. **ยังไม่อัป Drive** (พีทสั่ง; ปูส้มจะถามพีท)

| ขั้น | commit | ผล |
|---|---|---|
| snapshot | `07c035e` | — |
| 1 `-arena` + `mptest.ps1` + Loop/Coop | `e366cfc` | selftest ฐาน 321/1 FAIL เดิม (ดู "เจอระหว่างทำ"), mptest v1 ผ่าน (รอบที่ 3) |
| 2 Pile + Fall + LoopSave pv 2 | `5e0b512` | selftest 329 PASS 0 FAIL |
| 3 หลายกอง (RunLoop / LoopUi) | `8db9bac` | selftest 336 PASS (1 FAIL = ตำแหน่งกองในเทสต์ใหม่ผิด แก้แล้ว) |
| 4+5 Protocol 3, TeamNet, ห้อง / READY / Enter | `2fdb9c9` | mptest v1 ผ่าน 2/2; mptest-loop เคส 1-3 |
| 6 mirror + เก็บ/กิน/ทิ้ง | `5315508` | mptest-loop 24 PASS |
| 7 portal ทีม + G3 + X1 | `2ccfd78` | 35 PASS |
| 8 ล้ม / ชุบ / ตาย / spectate | `e4f2ab0` | 50 PASS |
| 9 wipe + หลายกอง + Take / Piles / Worlds | `151345f` | 68 PASS |
| 10 หลุด / เข้ากลางรอบ / host ปิด / rc / -menutest | `aae925d` | 81 PASS; menutest ผ่าน |
| 11 docs + polish + zip | (commit สุดท้าย) | ดูรายงานส่งงาน |

## ผลทดสอบสุดท้าย (build เดียวกับที่ zip)
- build: Unity `-buildWindows64Player` (เทียบเท่า build.bat) ผ่าน 0 error → `Builds\dev\MedievalKnight.exe`
- selftest เต็ม: **342 PASS / 0 FAIL** (รอบที่ 2; รอบที่ 1 = 340 PASS + 2 FAIL ในเคส `loop prepare: moving into a full carry` / `Enter moves the item under the keyboard cursor` = คลิก UI คลาดเวลา ไม่เกี่ยวโค้ด แล้วผ่านเมื่อรันซ้ำ)
- `mptest.ps1` (v1 `-arena`): H PASS=61 FAIL=0 (รอบที่ 1)
- `mptest-loop.ps1`: H PASS=81 FAIL=0, A 5 / B 5 / C 2 / X 1 / V 1 / D 4 (FAIL ทั้งหมด 0, รอบที่ 1, ~4.5 นาที)
- `-menutest`: failed checks=0

## ตัดสินไปแล้ว / ทำต่างจากเอกสาร
1. **build ลง `Builds\dev\`** ไม่ใช่ `Builds\MedievalKnight.exe`: มีเกมที่เปิดอยู่ (PID 24432, ไม่ใช่ของ developer) ล็อกไฟล์นั้น ห้าม kill → ใช้คำสั่ง Unity เดียวกับ build.bat แต่ `-buildWindows64Player`. ต้องรัน `build.bat` เองเมื่อเกมนั้นปิดแล้ว (เนื้อหาเหมือนกัน)
2. ขั้น 4 กับ 5 รวม commit เดียว (TeamNet / TeamRun เขียนด้วยกัน); ขั้น 2–3 / 6–10 แยกตามแผน
3. `Pack` มี `applied` (u16) เพิ่มจาก tech-net: host ข้าม Pack ที่ยังไม่รับ grant ล่าสุด → mirror ไม่ย้อน ของไม่หายตอนเก็บพร้อมกัน (decisions ข้อ 9)
4. คนหลุดกลางรอบ: ย้ายไป `TeamRun.leavers` (ช่อง pid ว่างให้คนใหม่เข้าได้) ไม่ค้างใน roster; `RunEnd` มีชื่อต่อแถว (pid อาจถูกใช้ซ้ำ)
5. R1: รายชื่อห้องอยู่ที่หัว PREPARE (แทนบรรทัดบนจอที่ถูกแผงกันไว้) + แถวซ้ายบนเมื่อปิด PREPARE; host ไม่มีปุ่ม READY (พร้อมเสมอ) ปุ่ม `NEW WORLD n/m`
6. Hud: party rows ซ่อนตอน Coop (TeamUi วาดแทน); ป้ายโลกเก่าเลื่อนลงใต้รายชื่อ
7. banner ของทีม (`X is down`, `revived`) ไม่ส่งให้คนที่รอที่ฐาน
8. `Worlds` ส่ง ≤ 8 โลก (ทุกกอง) ให้ client ดู
9. ผู้เล่นคนเดียว (host เข้าโลกคนเดียว) ชนะแล้วก็รอ 15 วิก่อนกลับ (ใช้กติกาเดียวกัน)

## เจอระหว่างทำ
- selftest เดิมมี FAIL อยู่ 1 ข้อก่อนเริ่ม (`30 deaths ... 30 different codes`): seed โลกซ้ำเมื่อโลกสลายแล้วเกิดใหม่ในวินาทีเดียวกัน (commit `aa2fe5a` ไม่ได้รันเทสต์) → แก้ด้วย `seedCounter` ใน `Loadout.NewSeed` (ขั้น 2)
- mptest v1 มีเช็คผันผวนตาม CPU 2 ข้อ (`A's walk looks smooth on B/H`, `stands at the same place`): ตกในรอบแรกๆ ผ่านเมื่อรันซ้ำ (ที่ขั้น 1: รอบ 1 FAIL=1, รอบ 2 FAIL=3, รอบ 3-4 FAIL=0)

## สิ่งที่ค้าง / เสนอ
- พีทลองเล่นจริง (2 เครื่อง / LAN) — ยังไม่ครอบคลุมเครือข่ายจริง (latency / packet loss)
- decisions ข้อ 6: คนหลุดเพราะเน็ตก็เสียของ (ชนะ = หายถาวร, wipe = เป็นกอง) **ต้องแจ้งพีท**
- เสนอ (ไม่ได้ทำ): ปุ่ม "ยกเลิกนับกลับ" ไม่จำเป็น; เสียงเตือนตอนเพื่อนล้ม; ให้ host เห็น Worlds ของ client ที่เป็น SP (ตัดออกตาม requirement)
- phase 3 (biome + เกรดอาวุธ) รอพีทสั่ง
