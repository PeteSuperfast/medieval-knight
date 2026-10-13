# Phase 2 Design (UX/UI) — โลกเก่า
> ส่วน 1/2 ของ `design.md` (ดัชนี: `design.md`) · ต่อที่ `design-2.md`
อ้าง requirement.md + vision.md + phase1 design.md / loop.md | ข้อความในเกมเป็นอังกฤษ | ต่อยอดหน้า PREPARE / HUD / G เดิม ไม่รื้อ

## 1. เป้าหมายผู้เล่น
ตายแล้วรู้ทันทีว่า "ของยังอยู่ที่ไหน เหลือเวลาอีกกี่ครั้ง" แล้วตัดสินใจเองว่าจะกลับไปเสี่ยงเก็บคืนหรือไม่
ในโลกเก่าเห็นกองของและรู้ทางเสมอ เก็บเท่าที่ช่องพกรับได้ เห็นชัดว่าโลกไหนกำลังจะสลาย

## 2. หน้าจอ/องค์ประกอบใหม่
| รหัส | ชื่อ | ที่มา |
|---|---|---|
| C2 | PREPARE ปุ่มล่างเป็น 2 ปุ่ม + แถบแจ้งสลาย | ปรับ C เดิม |
| H | หน้ารายการโลกเก่า (OLD WORLDS) | ใหม่ ซ้อนบน C |
| P | กองของ (LOOT PILE) ในโลก + ลูกศรชี้ทาง | ใหม่ |
| L | หน้าต่างเก็บของจากกอง (LOOT) | ใหม่ ใช้หน้าตา Carry เดิม |
| D2 | HUD โลกเก่า (ป้ายสถานะ + objective บรรทัดสอง) | ปรับ D เดิม |
| G2 | หน้าสรุป: เพิ่มบล็อก OLD WORLDS / ข้อความสลาย / ตาย-รอดในโลกเก่า | ปรับ G เดิม |

ชื่อโลก = `#` + รหัส 4 ตัวจาก seed (เช่น `#A3F2`) ใช้ทุกที่ให้ผู้เล่นอ้างอิงโลกเดียวกันได้

## 3. Wireframe
**C2: PREPARE** (ส่วนบน/ช่องของเหมือนเดิม; เปลี่ยนแถวล่าง)
```
| ! Old World #B71C has crumbled. 4 items are gone forever. (แดง แสดงครั้งเดียว) |
| ...STASH | CARRY... (เหมือนเดิม)                                               |
| ! No food packed   [ NEW WORLD ]  [ OLD WORLDS (2) ]            Esc: Back   |
```
OLD WORLDS (0) = เทา กดไม่ได้ ชี้เมาส์ขึ้น `No old worlds yet` | ถ้ามีโลกที่เหลือ 1 ครั้ง ปุ่มมีจุดเหลือง

**H: OLD WORLDS** (~1000x560 ซ้อนบน C ฉากหลังมืดลง)
```
+----------------------------------------------------------+
| OLD WORLDS                       Harder: enemies HP x1.5 |
|                                  DMG x1.25, +50% packs   |
| #B71C  Fell Oct 13, 14:32   5 items  [pips o..]  1 left! | <- เลือกอยู่ (กรอบทอง)
| #A3F2  Fell Oct 12, 21:05   2 items  [pips ooo]  3 left  |
| (เรียง: เหลือน้อยสุดก่อน แล้วใหม่สุด; เกิน 5 แถวเลื่อนได้)    |
|----------------------------------------------------------|
| LOOT IN PILE:  [Sword+2][Rapier][Apple][Apple][Rune]     |
| Crumbles after your next return.   Leave room in your    |
| pack to carry loot back.                                 |
| [ ENTER OLD WORLD ]                           Esc: Back  |
+----------------------------------------------------------+
```
แถว: ไอคอนของสูงสุด 4 ชิ้น + `+N`; pips 3 จุด (เต็ม=เหลือ) ; `1 left!` เหลือง
ENTER OLD WORLD ใช้ carry ที่จัดไว้ใน C ตอนนี้ (ถ้าไม่มี Weapon = `Pack a Weapon first` เหมือนเดิม)

**P: ในโลก** กองของ = ถุง/หีบเตี้ย + ด้ามอาวุธโผล่ + แสงทองบางๆ สูง ~12 ม. (เห็นไกล ต่างจาก portal ฟ้า)
```
        YOUR LOOT  x5        <- ป้ายลอยเหนือกอง (ขาว/ทอง)
             |  (ลำแสงทอง)
          [ถุงของ]
ขอบจอ: ลูกศรทอง + `Loot 142 m` (ชี้เมื่อกองอยู่นอกจอ)
```
**L: LOOT** (เดินชนกอง รัศมี ~1.5 ม. → เปิดเอง เกมหยุด)
```
+------------------------------------------------+
| YOUR LOOT #A3F2                    Take all [Q]|
| PILE 5             | YOUR PACK                 |
| [Sword+2][Rapier]  | Weapons 1/2 [Iron][ ]     |
| [Apple][Apple]     | Food 3/3  [x][x][x]       |
| [Rune]             | Supplies 1/4 [ ][ ][ ][ ] |
| Pack full: 2 left in the pile (เหลือง)         |
+------------------------------------------------+
```
**D2: HUD** กลางบนเดิม `Objective: Defeat the Minotaur to return` + บรรทัดสองทอง `Recover your loot` (หายเมื่อกองหมด)
ป้ายมุมขวาบน (~240x56): `OLD WORLD #A3F2` / `HP x1.5 DMG x1.25` / `Crumbles in 3 returns`

**G2: สรุป** (ต่อจาก G เดิม เพิ่มบล็อกล่าง ก่อนปุ่ม RETURN TO BASE)
```
YOU FELL (โลกใหม่)                | VICTORY (ทุกโลก)
 ...LOST เดิม...                  |  ...BROUGHT HOME เดิม...
OLD WORLD CREATED #A3F2 (ม่วง)    | OLD WORLDS
 5 items lie where you fell.      |  #A3F2  3 -> 2 left
 Crumbles after 3 returns.        |  #B71C  1 -> 0  CRUMBLED, 4 items lost forever (แดง)
```
ตายในโลกเก่า: `ADDED TO OLD WORLD #A3F2 — pile now 7 items, 2 returns left (counter not reset)`
รอดจากโลกเก่า: `#A3F2: 2 items still in the pile` | กองหมด = `#A3F2: all loot recovered. World closed.`
หมายเหตุเล็กใน YOU FELL: `Old worlds only fade when you return alive.` (แสดงเมื่อมีโลกเก่า)

