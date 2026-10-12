# Phase 1 Tech — แผนทดสอบ

## 1. แนวทาง
- ส่วนใหม่ต่อท้าย `SelfTest.cs` (หลังเคส F5 เดิม ก่อน `Application.Quit`): ตั้ง `LoopSave.PathOverride = <dir>\save_test.json` ลบไฟล์ แล้ว `g.LeaveToMenu(); Net.StartSingle("Test"); g.StartSession(true)` — เคสเดิมทั้งหมดรันใน v1 ก่อน จึงไม่ถูกกระทบ
- ใช้ hook ตรง (`TakeDamage`, `TakeHit`, `SpawnPickup`, เรียก `run.*`) เหมือนเคสเดิม; เคส UI ใช้ `Tap`/`Click` จริงอย่างน้อย 1 ตัว (Tab, ENTER WORLD)

## 2. เคสใหม่ใน selftest (ทุกข้อ `Check(...)`)
| กลุ่ม | เคส |
|---|---|
| ข้อมูล (ล้วน) | stash เริ่มต้นมี Iron เล่มเดียว; ความจุ 12/8/16 และ 2/3/4; ใส่หมวดตรงก่อนแล้วลง supplies; เต็มทั้งคู่ = Full ไม่เสียของ; รวม +N: (+0)+(+0)=+1, (+2)+(+0)=+3, เกิน +5 ถูกตัด; ย้าย stash↔carry; ไม่เหลือ Weapon → `EnsureIron` |
| เซฟ/โหลด | เขียน→โหลด ได้ค่าเท่าเดิมทุกช่อง; ไฟล์ขยะ → เริ่มใหม่ + มี `.corrupt`; main เสียแต่ `.bak` ดี → ใช้ `.bak`; id/plus นอกช่วง → ถูกแก้; path ที่ใช้ไม่ใช่ `persistentDataPath` และไฟล์เซฟจริง (ถ้ามี) เวลาแก้ไม่เปลี่ยน |
| เข้าฐาน | Single จากเมนู → `State==Base`, ไม่มี enemy, Prepare เปิด, Carry มี Iron (AutoPack), ไม่มีการเกิดของ Minotaur ใน 5 วิ |
| เข้าโลก | ENTER WORLD → `State==World`, level 1, `k.Weapon.id` = ของช่อง HELD, Minotaur มี, ไฟล์เซฟ `inRun=true`, ไม่มี Weapon ใน Carry = เข้าไม่ได้, ไม่มี Food = ต้องยืนยัน |
| รอด | เติมของใน carry (อาวุธ+อาหาร+supplies) → ฆ่า Minotaur ด้วย `TakeHit` → portal เกิด, `minotaur==null` หลังผ่าน >`MinotaurRespawn`+2 วิ ไม่เกิดใหม่; เก็บ Horned King's Blade (เข้า Carry); เดินเข้า portal 1.5s → VICTORY; stash มีของครบ + `+N` ติด; ออกจากรัศมีก่อนครบ = ยกเลิก; Stash เต็ม → LEFT BEHIND |
| ตาย | `TakeDamage(9999)` → `k.Dead` ยังเป็น true หลัง 5 วิ (ไม่ revive), ไม่มีหน้า YOU DIED เดิม, Summary YOU FELL, carry ว่าง, ของใน stash ที่ไม่ได้พกอยู่ครบ; พกเล่มเดียวแล้วตาย → Iron restored |
| ปิดกลางโลก | เข้าโลก → `LeaveToMenu()` → `StartSession(true)` → Summary ABANDONED, carry หาย, ไฟล์ `inRun=false` |
| Pack full | carry เต็ม + `SpawnPickup` ใต้เท้า → ไม่ถูกเก็บ, pickup ยังอยู่, `lastNotice`/Hud มี `Pack full`; เก็บของซ้ำชนิดเดียวกัน (รวม +N) ได้แม้ช่องเต็ม |
| Drop | Drop ดาบ +2 → pickup `plus==2` ยืนบนมันไม่ถูกเก็บ; เดินพ้นรัศมีแล้วกลับ → เก็บกลับได้ +2; Drop อาวุธชิ้นสุดท้ายถูกปฏิเสธ |
| อาหาร/ปุ่ม | C กินช่องแรก HP ลดลงจึงกินได้ / HP เต็ม = ไม่กินและไม่หมด; ปุ่ม 1/2 เปลี่ยนอาวุธในมือ; F5 ใน loop ไม่ล้างอะไร |
| แยกโหมด | หลังจบเคส loop: `-single`/selftest เดิม `g.Loop==false`; Host (`-host`) ไม่เคยมี `Loop` (เช็คใน mptest log ผ่านบรรทัด SESSION — ไม่ต้องแก้ mptest) |

## 3. ที่ต้องรันก่อนส่งงาน
1. `build.bat` ผ่าน (ค้น error + 30 บรรทัดท้ายตามกฎ)
2. selftest: 0 FAIL (เคสเดิมไม่แก้ความคาดหวัง)
3. `mptest.ps1`: **ผ่านเหมือนเดิมโดยไม่แก้สคริปต์/ความคาดหวัง** — โค้ดที่ mptest แตะ (pickup อาหารทันที ช่วง 8b, F5 host ช่วง 9, Tab overlay ไม่หยุดเวลา, Net.Protocol 2) ถูกเกตด้วย `Loop` จึงไม่ควรเปลี่ยน
4. `-menutest`: ส่วน single player ต้องแก้เล็กน้อย (Prepare เปิดเอง → Esc ปิดก่อนเดิน; Tab เปิด Prepare แล้ว `InventoryOpen && timeScale==0` ยังจริงถ้า `InventoryOpen` ครอบ Prepare) — เป็นการเปลี่ยนพฤติกรรมของเมนู Single Player ที่ requirement สั่ง
5. เล่นมือ 1 รอบรอด + 1 รอบตาย + ปิดเกมกลางโลก (ดู screenshot เทียบ design §3)
