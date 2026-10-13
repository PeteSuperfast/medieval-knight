# Phase 4 Design (UX/UI) - co-op ของ loop ใหม่
อ้าง requirement.md (พีทยืนยัน) + vision + phase2 design | ข้อความในเกมเป็นอังกฤษ | ต่อยอด PREPARE/OLD WORLDS/LOOT/G เดิม

## 1. เป้าหมายผู้เล่น
รู้ตลอดว่าเพื่อนแต่ละคนสถานะไหน (พร้อม/ล้ม/ตาย) ต้องช่วยใครตอนไหน; ล้มแล้วมีโอกาสถูกชุบ ตายแล้วดูเพื่อนสู้ต่อได้
จบรอบเห็นชัดว่าทีมรอดกี่คน ของใครกลับบ้าน/ตกที่ไหน และกองไหนต้องกลับไปเก็บ

## 2. หน้าจอ/องค์ประกอบ
| รหัส | ชื่อ |
|---|---|
| R1 | Party panel ที่ฐาน (Ready/Waiting) |
| R2 | PREPARE แถวล่างแบบทีม: READY / ENTER WORLD / Force start (ปรับ C2) |
| R3 | OLD WORLDS แบบ client อ่านอย่างเดียว + "n piles" (ปรับ H) |
| R4 | Waiting overlay (คนเข้ากลางรอบ) |
| K1 | สถานะล้ม: จอคนล้ม/วงนับ 60 วิเหนือหัว/จอคนชุบ |
| S1 | Spectate (ตายแล้วดูเพื่อน) |
| T1 | Party list ในโลก: ป้ายสถานะต่อคน |
| X1 | แบนเนอร์นับถอยหลังกลับฐาน 15 วิ |
| G3 | สรุปแบบทีม (ตารางต่อคน) ต่อจาก G/G2 |
| P2 | หลายกอง: ลูกศร/ป้าย/LOOT เปิดพร้อมกันหลายคน (ปรับ P/L) |

## 3. Wireframe
**R1+R2: ฐาน/PREPARE (host เห็น)**
```
HOSTING 192.168.1.5:7777        PLAYERS 3/4
+ Peat (host)  READY            <- R1 มุมซ้ายบน ใต้กล่องสถานะ
+ Alice        READY            (เหลือง "packing..." = กำลังจัดของ)
+ Bob          packing...       (เทา "WAITING" = รอรอบหน้า)
...STASH | CARRY... (เดิม)
|[READY v]  [ENTER NEW WORLD 2/3]  [OLD WORLDS (2)]       Esc: Back|
```
client: แถวล่าง `[READY] [OLD WORLDS (2) view]` + `Waiting for host to enter a world`
host กดเข้าโลกตอนมีคนไม่ Ready -> `Bob is not ready. Start without waiting? [START] [CANCEL]`

**K1: คนล้ม (จอตัวเอง)** vignette แดง
```
            YOU ARE DOWN          <- กลางบน 40px แดง
        Help is needed: 42 s
   (มีคนชุบ: "Alice is reviving you..." เขียว + แถบ)
```
เหนือหัวคนล้ม (ทุกคนเห็น): วงแหวนลดทวนเข็ม + ตัวเลขวินาที; นอกจอ = ลูกศรแดงเล็กขอบจอ
คนชุบ (<= 3 ม.): `Hold F: Revive Bob` ใต้ตัว; ค้าง 2 วิ แถบเขียว (ถูกตี = รีเซ็ต+กระพริบแดง)

**S1: ตายแล้ว** กล้องตามเพื่อนที่รอด
```
 SPECTATING  Alice  (HP bar)          <- กลางบน
 You fell. Waiting for the team...    <- ล่างกลาง
 [Q/E or Left/Right: Switch]
```
**T1 party list** แถวละคน สีผู้เล่น+ชื่อ+Lv+HP; ล้ม = `DOWN 42s` แดงกระพริบ; ตาย = เทา `FELL`; หลุด = `LEFT`
**X1** กลางบน กรอบฟ้า (สี portal)
```
  PORTAL OPEN - returning in 15
  Everyone returns together     (คนไกล: ลูกศรฟ้าชี้ portal)
```
**G3: สรุปทีม** (เหนือบล็อกตัวเองจาก G/G2 เดิม รวม LEFT BEHIND)
```
VICTORY - TEAM RETURNS HOME        | ALL FELL - OLD WORLD CREATED #A3F2
 Peat   SURVIVED   5 items home    |  3 piles, 11 items. Crumbles in 3 returns
 Bob    FELL       2 items home    |  Peat  FELL  4 items dropped
 Cara   LEFT       lost            |  Bob   FELL  2 items dropped
 OLD WORLDS  #A3F2  3 -> 2 left    |  OLD WORLDS  #B71C 2 -> 1 left
 [ RETURN TO BASE ]                |  [ RETURN TO BASE ]
```
**P2 หลายกอง** กองละลำแสงทอง + ป้าย `LOOT x5` (ไม่มีชื่อเจ้าของ); ลูกศรทอง 1 อัน = กองใกล้สุดที่ยังมีของ `Loot 142 m (3 piles)`
**L ในทีม**: หน้าเดิม เกมไม่หยุด; ชิ้นที่คนอื่นเก็บก่อนหายจากรายการ + แถบเหลือง `Taken by Alice`

## 4. Flow
1. Host/Join Game (ฟอร์มเดิม) -> ฐาน loop -> R1 เห็นเพื่อน
2. ทุกคนจัด carry (Tab) -> READY (ล็อก carry; กดซ้ำ = ยกเลิกแก้) ต้องมี Weapon ถึง READY ได้
3. host กด ENTER NEW WORLD (หรือ OLD WORLDS > เลือกแถว > ENTER OLD WORLD) -> fade 0.5 วิ -> ทุกคนเกิดข้างกัน Lv1; แบนเนอร์ `WORLD #XXXX` / `Stay together. Revive with F.`
4. ล้ม: HP 0 -> K1; เพื่อน <= 3 ม. ค้าง F 2 วิ -> ลุก HP 30%+โล่ 3 วิ `Bob revived Alice`; ครบ 60 วิ -> `Alice has fallen` -> S1
5. ทุกคนล้ม/ตาย (wipe) -> G3 ALL FELL; กองแต่ละคนเกิดที่ตำแหน่งสุดท้ายของคนนั้น; RETURN TO BASE -> ฐาน
6. ฆ่า Minotaur -> portal; คนรอดยืน 1.5 วิ -> X1 นับ 15 -> ทุกคน (รวมล้ม/ตาย) กลับพร้อมกัน -> G3 VICTORY -> ฐาน
7. คนเข้ากลางรอบ: ฐาน + R4 `Run in progress - you'll join the next one` จัด PREPARE/READY ได้; ทีมกลับแล้วรวมกัน
8. โลกเก่า: host เลือก; ใครเดินชนกองก็เปิด L; เก็บตามช่องพกตัวเอง
9. หลุด: `Cara disconnected` (เหลือง) แถว LEFT ทีมเล่นต่อ; host หลุด = เมนู `The host closed the game.`

## 5. ปุ่ม
| ที่ | ปุ่ม | เพิ่มใน InputX.Map()? |
|---|---|---|
| ชุบ | ค้าง **F** (`InputX.Held`) | ไม่ต้อง (F มีแล้ว) |
| READY / ENTER / Force start | คลิก หรือ Enter/Space ที่ปุ่มโฟกัส ลูกศร/WASD เลื่อน | ไม่ต้อง |
| Spectate สลับคน | Q/E หรือ ซ้าย/ขวา | ไม่ต้อง |
| LOOT / Take all | เดิม (Q) | ไม่ต้อง |

**F ชนกับ Ground Slam เดิม:** มีเพื่อนล้มใน <= 3 ม. F = ชุบ (ปิด Ground Slam ชั่วคราว) ไม่มี F = สกิลเดิม
คนล้ม: ขยับ/โจมตี/สกิล/ใช้ของไม่ได้ (Esc ได้) ไม่มีจอย

## 6. สี/ขนาด (ชุดเดิม phase 1-2 + style.md)
- พื้น #101418 85% กรอบ #3A4450 เลือก #D9A441; Ready/ชุบ/ได้ #6FD16F; รอ/เตือน #F2C14E; ล้ม/FELL/สลาย #E05050; ตาย/LEFT #958A80
- กอง/ลูกศร #F5C25A; portal/X1 ฟ้าเดิม; โลกเก่า #5E5878
- วงนับล้ม รัศมี 0.9 ม. สูง 2.4 ม. หนา 6px เลข 22px; YOU ARE DOWN 40px ห่างขอบบน 80px
- R1 แถวละ 28px ตัวอักษร 20px; แถบชุบ 220x14px; X1 ~420x72px; G3 ~800px แถวสูง 40px

## 7. ต่างจาก requirement / ตัดสินเพิ่ม
1. F ชนกับ Ground Slam (requirement ไม่ได้เอ่ย) -> ใช้กฎตามระยะข้างบน
2. คนล้มขยับไม่ได้ (ไม่คลาน)
3. นับ 15 วิกลับฐานแล้ว วงนับล้มเดินต่อ แต่ของปลอดภัยทั้งคนล้มและตาย
4. ป้ายกองไม่แสดงชื่อเจ้าของ
5. client ดูโลกเก่าของ host ได้ (อ่านอย่างเดียว) เพื่อจัดช่องพก

## 8. คำถามให้ปูส้มตัดสิน
1. LOOT ในทีมไม่หยุดเกม: ให้โล่ 6 วิตอนเปิดเหมือน LevelUpProtect ไหม (เสนอ: ให้ ครั้งเดียว/การเปิด)
2. host บังคับเริ่ม: คนไม่ READY/ไม่มี Weapon -> รอที่ฐานรอบนี้ (เสนอ) หรือให้ Iron +0 ฟรี
3. ระหว่างนับ 15 วิ คนล้มยังชุบได้ไหม (เสนอ: ได้ ไม่ขยายเวลา)
4. ฐานกับโลกเป็นพื้นที่เดียวกันไหม (R4: คนรอที่ฐานเห็นทีมในโลกไหม — เสนอ: ไม่เห็น มีแค่ panel)
5. แถว OLD WORLDS รวมของทุกกองเป็นรายการเดียว `3 piles` ตกลงไหม
