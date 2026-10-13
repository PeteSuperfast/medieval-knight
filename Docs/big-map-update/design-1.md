# Design: big-map-update (medieval-knight)
> ส่วน 1/6 ของ `design.md` (ดัชนี: `design.md`) · ต่อที่ `design-2.md`

ผู้ออกแบบ: Designer ทีมปูส้ม | อ้างอิงโค้ด branch `main` (`76fe974`) | ข้อความในเกมเป็นภาษาอังกฤษ ส่วนเอกสารนี้เป็นภาษาไทย
แก้ไขรอบ 2: ปรับให้ตรงกับ tech.md (สไลม์เกิดเป็นแพ็ค, dash, อาหาร 2 ชนิด, เพดานแต้ม 10, ตัด minimap ออกจาก layout หลัก ฯลฯ)
**[เสนอเพิ่ม]** = ข้อเสนอนอก requirement (สรุปรวมท้ายเอกสาร) ที่เหลือคือสิ่งที่ requirement ขอหรือ layout หลัก

---

## 1. เป้าหมายของผู้เล่น

- ออกผจญภัยในโลกกว้าง: เริ่มที่เมือง วิ่งข้ามทุ่งและป่า ฆ่าสไลม์สะสม EXP / Soul / อาหาร / ดาบ จนแข็งแรงพอไปท้าบอส Minotaur ที่รังทางเหนือ
- ต่อสู้เป็นจังหวะ: วิ่งเข้าหา ฟัน ยกโล่ แล้ว dash หลบตอนศัตรูง้างท่า (stamina = ทรัพยากรของการหลบเท่านั้น)
- ตายแล้วเสียความแข็งแกร่ง (กลับ Lv 1) แต่ไม่เสียของสะสม (ดาบ, Soul) ต้องรู้สึกว่า "เสียดาย แต่ไม่ถึงกับเริ่มใหม่หมด"

---

## 2. รายการหน้าจอ / องค์ประกอบ UI

| # | องค์ประกอบ | สถานะ | หมายเหตุ |
| --- | --- | --- | --- |
| 1 | HUD หลัก (เลือด / เกราะ / stamina / เลเวล+EXP / Soul) | แก้ของเดิม | เอาแผงค่าสถานะออก |
| 2 | ไอคอนสกิล Q E R F | แก้เล็กน้อย | ตัดตัวเลข stamina ออกจากป้าย |
| 3 | หน้าต่าง Tab (ช่องเก็บของ + ค่าสถานะ) | ใหม่ แทนหน้าต่าง I | ดาบเป็นรูป |
| 4 | หน้าจอตาย + ตอนฟื้น | ใหม่ | แทน banner บรรทัดเดียวเดิม |
| 5 | ตัวนับ Soul + popup "+N Soul" | ใหม่ | |
| 6 | ไอเทมอาหารบนพื้น (Apple / Roast Meat) + popup "+N HP" | ใหม่ | |
| 7 | Feedback ของ dash | ใหม่ | ใช้ Fx เดิม + เสียงเดิม |
| 8 | Party list ออนไลน์ | แก้เล็กน้อย | |
| 9 | แผงปุ่มควบคุม (F1) | แก้ข้อความ | อยู่มุมขวาบนที่เดิม |
| 10 | ลูกศรชี้รังพร้อมระยะ ("Lair 143 m") | ใหม่ (HUD หลัก) | ตัวช่วยหาทางขั้นต่ำ แทน minimap |
| 10b | **[เสนอเพิ่ม]** Minimap | ตัวเลือก ไม่อยู่ใน layout หลัก | งานเพิ่มระดับกลาง |
| 11 | **[เสนอเพิ่ม]** ป้ายชื่อโซนตอนเดินเข้า | ใหม่ | ใช้ Banner เดิม |
| 12 | ป้ายบอกทางในโลก + ถนนดิน + landmark | ส่วนของแมพ | ไม่ใช่ UI |
| 13 | แถบบอส Minotaur, banner, notice, ตัวเลขดาเมจ | ของเดิม | แถบบอสแสดงเมื่อบอสอยู่ใน 45 ม. |

---

## 3. แมพ

### 3.1 ขนาดและพิกัด

- ของเดิม: `ArenaHalf = 19` → สนาม 38 x 38 ม. | ของใหม่: ด้านละ 10 เท่า → **380 x 380 ม.** ครึ่งด้าน = 190 ม.
- ระบบพิกัด: ศูนย์กลางแมพ = (0,0), X ไปตะวันออก, Z ไปเหนือ; เหนืออยู่ด้านบนจอ
- ขอบสุด +-190 ม. = +-19,000 ซม. อยู่ในช่วง i16 ของ protocol ปัจจุบัน
- ความรู้สึกที่ต้องการ: เดินจากเมืองไปรังบอสด้วยความเร็ววิ่งราว 45-60 วินาที ระหว่างทางมีศัตรูและของให้ทำ

### 3.2 ผังโซน (1 ช่อง = ราว 10 ม. กว้าง x 20 ม. สูง; เหนืออยู่ด้านบน)

```
 X: -190      -100        -50     0      50       100      190
Z+190 +--------------------------------------------------------+
      |MMMMMMMMMMMMMM  ..  ruins ruins ..  PPPPPPPPPPPPPPPPPPPPP|
      |MMM MOUNTAIN MMM   [ LAIR  ]       PPP DARK PINE PPPPPPPP|
Z+150 |MMMMMMMMMMMM        ( M )  <- Minotaur (0,+150)  PPPPPPPPP|
      |MMMMMMM            ruined colonnade             PPPPPPPPPP|
Z+110 |AAAAAAA            ||road||                      PPPPPPPPP|
      |AAA AUTUMN AAA      ||  ||    [T] watchtower    PPPPPPPPP|
Z+60  |AAAAAAAAAA   ggggggggg||gggggggggg  ggggg   PPPPPPPPPPPPPP|
      |AAAAAAAAA  gggg MEADOW gggg||gggggggg ggg   PPPPPPPPPPPPP|
Z+0   |AAAAAAAAA   ggggggggggggg ||gggggggggg      PPPPPPPPPPPPPP|
      |AAAAAAAA    ggg  [+] crossroads  ggg   pond  PPPPPPPPPPP |
Z-40  |AAAAAAA        gggggggg||gggggggggggg     fffffffff  PPP  |
      |~~~~~~~~        =====|| TOWN WALL ===      fffFARMffffffff|
Z-100 |~~LAKE~~~  |  wall  [GATE] wall  |        ff[windmill]fff|
      |~~~dock~~  |  houses  ||  houses |        ffffffffffffff |
Z-150 |~~~~~~~~~  |  houses [WELL*]     |  [yard D]   fffffffff |
      |~~~~~~~~   |  (0,-150) = SPAWN   |  dummy (+25,-140)     |
Z-190 +--------------------------------------------------------+
   legend: M=mountain/cliff  A=autumn forest  P=dark pine forest  g=meadow
           f=farmland  ~=lake (ผ่านไม่ได้)  ||=dirt road  [T]=landmark  *=spawn
```

