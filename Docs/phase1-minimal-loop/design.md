# Phase 1 Design (UX/UI)
อ้าง requirement.md + vision.md | ข้อความในเกมเป็นอังกฤษ คำรวม = "Weapon"

## 1. เป้าหมายผู้เล่น
เตรียมของ เลือกว่าจะเสี่ยงอะไร เข้าโลกสู้ถึง Minotaur แล้วหอบของรอดกลับ
ตายแล้วเจ็บ (ของที่พกหาย) แต่ไม่ตัน (มี Iron Sword เสมอ) และรู้ทุกครั้งว่าได้/เสียอะไร

## 2. หน้าจอ
A เมนู Single Player (เดิม) | B ฐาน (ลานกลางเดิม ไม่มี NPC) | C หน้า PREPARE (จัดของ)
D HUD ในโลก | E Tab window (ปรับ) | F Portal | G หน้าสรุปรอบ VICTORY / YOU FELL / ABANDONED

## 3. Wireframe
**C: PREPARE** (กลางจอ ~1000x560)
```
+----------------------------------------------------------+
| PREPARE                                                  |
| STASH                     | CARRY INTO WORLD             |
| Weapons 5/12              | Weapons 1/2 [HELD Iron+2][ ] |
| [Iron+2][Rapier][ ][ ]... | Food 2/3   [Apple][Meat][ ]  |
| Food 3/8 [Apple][Meat]... | Supplies 0/4  [ ][ ][ ][ ]  |
| Supplies 0/16 [ ]...      | Info: DMG SPD CRIT REACH     |
|----------------------------------------------------------|
| ! No food packed        [ ENTER WORLD ]       Esc: Back  |
+----------------------------------------------------------+
```
**D: HUD** กลางบน `Objective: Defeat the Minotaur to return` | ซ้ายล่าง `[1 Wpn][2 Wpn] [C Food x2]` | ข้อความเหนือแถบสกิล (ที่เดิม) `Pack full! Can't pick up <name>`

**E: Tab** กรอบเดิม: ซ้าย EQUIPPED เหมือนเดิม; ช่องอาวุธ 6 → 2 ช่องใหญ่ + แถว FOOD 3 + SUPPLIES 4; ขวาเหมือนเดิมแต่ตัด SOUL

**F: Portal** วงแหวนฟ้า-ขาวสูง ~3 ม. + ลำแสง ที่จุด Minotaur ตาย ป้ายลอย `RETURN PORTAL`

**G: สรุปรอบ** (~700x480)
```
VICTORY                         | YOU FELL
Minotaur defeated!              | Everything you carried is lost.
BROUGHT HOME (เขียว)            | LOST (แดง)
 Iron Sword +2, Horned King's   |  Iron Sword +2, Apple x2
 Blade NEW, Apple x2            | SAFE IN STASH
Stash: 6/12 W  4/8 F            |  Swift Rapier, Apple x1
[ RETURN TO BASE ]              | [ RETURN TO BASE ]
```

## 4. Flow
1. A กด Single Player → ฐาน → C เปิดเอง (ครั้งแรก Stash มี Iron Sword เล่มเดียว และถูกใส่ HELD ให้)
2. จัดของ → ENTER WORLD → เซฟ → fade 0.5 วิ → เกิดในโลก เลเวล 1 ถือ Weapon ช่อง HELD
3. เก็บของ: เข้าช่องตามชนิด ช่องหมวดเต็มลง Supplies หมดทั้งคู่ = เก็บไม่ได้ ของค้างพื้น + ข้อความ Pack full
4. Minotaur ตาย → แถบกลางจอ 4 วิ `THE MINOTAUR HAS FALLEN. Portal open. Collect your loot, then step in.` Objective เป็น `Step into the Portal` + ลูกศรชี้ที่ขอบจอถ้า portal อยู่นอกจอ
5. เดินเข้า portal ค้าง 1.5 วิ (วงโหลดรอบวง ขยับออก = ยกเลิก) → G VICTORY → RETURN TO BASE → ฐาน (เซฟ)
6. ตาย → ไม่เกิดใหม่ จอสลัว 1.5 วิ → G YOU FELL → RETURN TO BASE → ฐานมือเปล่า (เซฟ) → C เปิดเอง
7. ไม่เหลือ Weapon เลย → Stash ได้ Iron Sword (G บอก `Iron Sword restored`)

## 5. ย้ายของในหน้า C
- คลิกซ้ายของใน Stash = ไป Carry (หมวดตรงก่อน ไม่ว่างลง Supplies); คลิกของใน Carry = คืน Stash
- เต็มหมด: ช่องสั่นแดง + `Carry is full`
- คลิกขวา Weapon ใน Carry = ตั้งเป็น HELD (สลับช่อง)
- คีย์บอร์ด: ลูกศร/WASD ย้ายเคอร์เซอร์, Enter/Space ย้ายชิ้น, Q/E สลับฝั่ง, Tab/Esc ปิดหน้า (เดินฐานต่อได้)
- ENTER WORLD เทา ถ้า Weapons ใน Carry = 0 `Pack a Weapon first`
- Food 0 = เตือนเหลือง `! No food packed` กดแล้วถามยืนยัน `Enter without food? [Enter] Go [Esc] Back` (ไม่บล็อก)
- ชี้เมาส์ = ช่อง Info แสดงสเตตัส

## 6. ปุ่ม (เพิ่มใน InputX.Map())
| ปุ่ม | ทำอะไร |
|---|---|
| Tab ที่ฐาน | เปิด/ปิด PREPARE (ในโลก = Tab window เดิม) |
| 1 / 2 ในโลก (ไม่มีหน้าต่างเปิด) | สลับ Weapon ช่อง 1/2 (ใหม่; 1-3 เดิมใช้เฉพาะการ์ดเลเวลอัป) |
| C | กินอาหารช่องแรก (HP เต็ม = `HP full` ไม่กิน) |
| คลิกช่อง Food ใน Tab | กินชิ้นนั้น |
| คลิกขวาช่องใน Tab | Drop ลงพื้น (ดูคำถาม 1) |
| Esc ครั้งแรกในโลก | `Leaving now counts as a death. Press Esc again.` (ใช้ Esc สองครั้งเดิม) |
| Enter/Space/ลูกศร/Q/E | นำทาง C และ G เฉพาะตอนหน้าเปิด |
เมาส์คลิกได้ทุกปุ่ม; เกมเดิมไม่มีจอย จึงไม่ออกแบบ

## 7. สภาวะพิเศษ
- **ครั้งแรก**: ป้ายครั้งเดียวบน C `Everything you carry is lost if you fall. Defeat the Minotaur to return.`
- **ไม่เหลือ Weapon**: แจก Iron Sword ตอนกลับฐานและตอนเปิดเกม
- **ปิดเกมกลางโลก**: เปิดใหม่ → ฐาน + G แบบ ABANDONED `You left the world mid-run. Everything you carried was lost.` รายการ LOST เหมือนตาย
- **Stash เต็มตอนรอด**: G แสดงเหลือง `LEFT BEHIND (stash full)` ไม่เงียบ
- ของบนพื้นที่ไม่ได้เก็บ ไม่ถือว่ากลับ ไม่แสดงใน G

## 8. สี/ขนาด
- พื้น C/G ดำ #101418 โปร่ง 85% กรอบ #3A4450; หัวข้อขาว 36 px; ช่องไอเทม 88x88 px ห่าง 8; กรอบช่องตามความหายากเดิม
- ได้ = เขียว #6FD16F, เสีย = แดง #E05050, เตือน = เหลือง #F2C14E, ปุ่มหลัก = ทอง #D9A441 (ปิด #555)
- Portal ฟ้า #6FC8FF + ขาว; ข้อความบนจอ 22-28 px; ไอคอน/ฟอนต์ใช้ของเดิม

## 9. คำถาม/ข้อที่ต้องตัดสิน
1. Pack เต็มแล้วเก็บ Horned King's Blade ไม่ได้ → ผู้เล่นต้อง Drop ของอื่นก่อน จึงเสนอปุ่ม Drop (ใหม่ นอก requirement ต้องอนุมัติ)
2. อาหาร 1 ชิ้น = 1 ช่อง (ไม่ซ้อน) ใช่ไหม
3. Weapon ชนิดเดียวกันรวมเป็น +N ชิ้นเดียว → Stash มีได้สูงสุด 6 ชนิด ไม่เต็ม 12 ตอนนี้
4. Weapon ใน Supplies ต้องย้ายเข้าช่อง Weapons ก่อนถึงถือได้ (ผ่านคลิกใน Tab) ตกลงไหม
5. F5 รีเซ็ตเดิม ล้างคลัง/ขัดกติกา เสนอปิดในเกมจริง เหลือเฉพาะ dev
6. เดินทับอาหารเดิม = กินทันที → ใหม่ = เก็บเข้าช่อง (เปลี่ยนตาม requirement)
7. Soul ซ่อนจาก Tab (ตาม requirement)
ไม่ขัด vision (ไม่มี NPC/ร้านค้า/crafting/บทสนทนา)
