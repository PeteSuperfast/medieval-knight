# Phase 1 Tech — ข้อมูล + ระบบเซฟ

## 1. โมเดลข้อมูล
- `Item { kind: None|Weapon|Food; id; plus }` — Weapon.id = index `Weapons.All`, Food.id = index `Foods.All`, plus 0..`Weapons.MaxPlus`(5) อาหารไม่มี plus (อาหาร 1 ชิ้น = 1 ช่อง ไม่ซ้อน)
- `ItemBag(capW, capF, capS)` = `Item[] weapons, foods, supplies` ตำแหน่งช่องคงที่ (ช่องว่าง = kind None)
  - **Stash** 12/8/16, **Carry** 2/3/4 (ค่าคงที่ `Loadout.StashCaps/CarryCaps` เพื่อโตภายหลัง)
- `Loadout`: `stash`, `carry`, `heldSlot` (0/1 = ช่อง HELD ใน carry.weapons), `inRun`, `introShown`
- ในโลก Soul/EXP/เลเวล/การ์ด/สเตตัส ไม่เข้าข้อมูลนี้ (เริ่มใหม่ทุกรอบ = `Knight.ResetForRun`)

## 2. กติกาเต็ม / ย้าย / รวม +N
| กติกา | รายละเอียด |
|---|---|
| ใส่ Weapon | ถ้ามี id เดียวกันใน weapons หรือ supplies ของถุงนั้น → **รวม**; ไม่มี → ช่อง weapons ว่าง → ไม่มี → supplies ว่าง → ไม่มี = Full |
| ใส่ Food | ช่อง foods ว่าง → supplies ว่าง → Full |
| รวม +N | ตีเป็นจำนวน "สำเนา": `plus = min(5, a.plus + b.plus + 1)` (ตรงกับกติกาเดิม: เก็บซ้ำ = +1); เกิน 5 ส่วนเกินหาย (ในโลก: ดาบซ้ำที่ +5 ให้ EXP 15 เหมือน v1 `Weapons.DuplicateExp`) |
| ย้าย | Add ลงปลายทางสำเร็จจึงลบต้นทาง; Full → ไม่ย้าย + `Carry is full` |
| ลำดับ stash | รวมแล้ว id อาวุธไม่ซ้ำใน stash (สูงสุด 6 ชนิด) |
| HELD | `heldSlot` ต้องชี้ช่อง weapons ที่ไม่ว่างเสมอ; ถ้าช่องว่างลง ย้ายไปอีกช่อง; ถ้าไม่เหลือ = เข้าโลกไม่ได้ |
| Drop (ในโลก) | ลบจาก carry → `Pickup` + `plus` + `dropped`; **ห้าม Drop อาวุธชิ้นสุดท้ายใน weapons** (`Can't drop your last Weapon`) — เพิ่มนอก design เพื่อไม่ให้ไม่มีอาวุธกลางโลก (ขอให้ปูส้มรับรอง) |
| Weapon ใน supplies | ถืออาวุธไม่ได้จนกว่าย้ายเข้า weapons (ตาม decisions #4) |

## 3. ผลลัพธ์รอบ
- `Survive()`: วนทุกชิ้นใน carry → `stash.Add`; สำเร็จ = BROUGHT HOME (NEW ถ้า id ไม่เคยอยู่ใน stash/carry ตอนเข้าโลก); ใส่ไม่ลง = LEFT BEHIND (หายถาวร); ล้าง carry
- `Die(abandoned)`: รายการ carry ทั้งหมด = LOST; ล้าง carry; stash ที่ไม่ได้พกอยู่เหมือนเดิม
- ทั้งสองจบด้วย `EnsureIron()`: ถ้า stash+carry ไม่มี Weapon เลย → ใส่ Iron+0 ลง stash (`RunResult.ironRestored`), `inRun=false`
- กลับฐานแล้ว `AutoPack()`: ถ้า carry.weapons ว่าง → ย้าย weapon ชิ้นแรกใน stash เข้า HELD (ครั้งแรก = Iron ตาม design flow 1; ผู้เล่นถอดคืนได้)

## 4. ระบบเซฟ
- **ไฟล์เดียว** `Application.persistentDataPath/medieval-knight-save.json` (เปลี่ยนด้วย `-save <ไฟล์>`)
- รูปแบบ JSON (`JsonUtility`, DTO `[Serializable]`): `{ "v":1, "inRun":false, "intro":true, "held":0, "stash":{"w":[{"k":1,"id":0,"p":0},...],"f":[...],"s":[...]}, "carry":{...} }` ช่องคงตำแหน่ง ช่องว่าง `k=0`
- `carry` เซฟเสมอ (จัดชุดค้างไว้ข้ามการปิดเกมได้ และใช้แสดง LOST ตอน ABANDONED)
- **เวลาเซฟ**: (1) กด ENTER WORLD หลังตั้ง `inRun=true` (2) ตอนจบรอบ — ตาย/ชนะ ทันที ก่อนแสดงสรุป (ปิดเกมที่หน้าสรุปจึงไม่เสียผล) (3) กด RETURN TO BASE (4) ปิด Prepare ถ้ามีการย้ายของ
- **ปิดเกมกลางโลก = ตาย**: เข้าโลกเซฟ `inRun=true` ไว้ก่อน → ถ้าปิดแอป/Esc Esc/crash ค้างไว้ตามนั้น; เปิดเกมใหม่ + กด Single Player → `RunLoop.Begin()` เห็น `inRun` → `Die(abandoned:true)` → เซฟ (flag ล้าง) → หน้า ABANDONED; ล้าง flag เฉพาะตอนจบรอบ (`Survive`/`Die`) เท่านั้น
- **กันไฟล์เสีย**: เขียน `*.tmp` → ถ้ามีไฟล์เดิม copy เป็น `*.bak` → `File.Replace`/Move เข้าที่; โหลด: parse ไม่ได้/`v` ไม่รู้จัก → ลอง `.bak` → ไม่ได้ทั้งคู่ = ย้ายไฟล์เสียเป็น `*.corrupt` แล้วเริ่มใหม่ (stash=Iron)
- **Validate** หลังโหลด: id/plus อยู่ในช่วงตาราง, ความยาวอาร์เรย์ = ความจุ (ตัด/เติม), รวมอาวุธ id ซ้ำ, `held` ชี้ช่องว่าง → แก้, ไม่มี Weapon → `EnsureIron`; ข้อผิดพลาด IO ทั้งหมด try/catch → Hud notice `Could not save` ไม่ให้เกมล่ม
- **เทสต์ไม่เขียนทับเซฟจริง**: `Args.savePath` (`-save`); ถ้า `Args.Scripted` และไม่มี `-save` → `LoopSave.Disabled=true` (อยู่ในหน่วยความจำ ไม่แตะดิสก์); selftest ตั้ง path เองเป็น `<โฟลเดอร์ selftest>\save_test.json`; `-mptest` ไม่เข้า loop จึงไม่แตะเซฟ
- PlayerPrefs (ชื่อ/IP/port ของ Menu) ไม่เกี่ยว ไม่ย้าย

## 5. ผลต่อระบบ save เดิม
ไม่มีระบบเซฟข้ามรอบเดิม (v1 มีแค่ PlayerPrefs ของเมนู) → ไม่ต้องย้ายข้อมูล; `-selftest`/Host/Join ไม่อ่าน/เขียนไฟล์นี้
