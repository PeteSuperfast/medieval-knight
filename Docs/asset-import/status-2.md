> ส่วน 2/2 ของ `status.md` (ดัชนี: `status.md`) · ต่อจาก `status-1.md`

## ทำอะไรไป
- `Assets\MedievalKnight\Scripts\KitCatalog.cs` (ใหม่): `KitCatalog` (รายการ prefab ใน `Resources\Kit\KitCatalog.prefab`) + `Kit.Get(name)` ดึง mesh/transform/material ของ prefab ตอนรัน; ถ้าไม่มี catalog เกมถอยกลับไปใช้ primitive เดิมทั้งหมด
- `World.cs`: `StaticBatcher.Add` รับ submesh; `WorldBuilder.PutKit` วาง prefab เข้า batcher (ยังรวมเป็น chunk ต่อ material เหมือนเดิม ไม่สร้าง GameObject ต่อชิ้น)
- ของที่แทนที่: ต้นไม้ทุกต้น (Tree_Pine / Round_Slender / Round_Wide), หินใหญ่-เล็ก (Rock_Small/Medium/Large), บ้านในเมือง+ไร่ (Cottage, 22 หลัง; ยุ้งฉางเดิมกว้าง 12 ม. คงเป็น primitive), รั้วลานซ้อมหุ่นฟาง และรั้วรอบแมพ (Fence_Straight_2m)
- ของประกอบใหม่ (ไม่มี collision): หญ้า 691, เห็ด 70, ตอไม้ 28, หินแผ่นหน้าบ้าน 66, ถังไม้ 37 (ตั้ง 26 / นอน 11) รวมทั้งหมด 3,392 ชิ้นจาก kit
- `AssetImport.MakeCatalog`: ตั้ง FBX เป็น Read/Write (import setting ใน .meta เท่านั้น ไม่แตะไฟล์โมเดล) + สร้าง KitCatalog.prefab
- `SelfTest.cs`: เพิ่มภาพถ่ายในเกม 7 จุดหลังวัดเฟรมไทม์ (กล้องถอยหลัง camZoom 2.4) + เพิ่มจุดวัดเฟรมไทม์ 3 จุด; ไม่เปลี่ยนเช็คเดิม

## ตัดสินใจเอง (เหตุผล: เบาที่สุด/ไม่กระทบ gameplay)
1. **ไม่เปลี่ยน collision แม้แต่วงเดียว**: จำนวน obstacle = 1,304 เท่าเดิม, ลำดับ/ค่าของ `System.Random(11)` หลักเท่าเดิมทุกครั้งที่เรียก (ที่เก็บ rng ที่ kit ไม่ใช้ ยังเรียกทิ้งไว้ให้ครบ); ของที่ kit สุ่มเอง (หมุน/ของประกอบ) ใช้ `System.Random(11)` อีกสายแยก. เหตุผลหลัก: build เก่า/ใหม่ต้องมี collision เหมือนกัน ไม่ต้องเพิ่ม `Net.Protocol` (ยังเท่ากับ 2)
2. ต้นไม้บล็อกเฉพาะผู้เล่นเหมือนเดิม (วงกลม 0.5 x scale) แต่ภาพใช้ prefab; หินที่ s>=1.4 บล็อกเหมือนเดิม จึงใช้ Rock_Medium/Large; หินที่ s<1.4 (เดิมไม่บล็อก) ใช้ Rock_Small ขยายให้เตี้ย เดินทับได้
3. ถังไม้/ตอไม้/หินแผ่น/หญ้า/เห็ด **ไม่บล็อก** (ตามข้อ 1) — วางถังชิดผนังบ้าน/แผงตลาดเพื่อไม่ให้เห็นผู้เล่นเดินทะลุ
4. ป่าใบส้มคงสีใบส้ม/แดง/เหลืองเดิมของโซน (override material `leaf_light`/`leaf_under`) เพราะเป็นตัวบอกโซน; ทรงและลำต้นเป็นของ kit. หลังคาบ้านใช้ม่วงเทา (kit) และแดงอิฐ #B5523B (จานสี) สลับกับที่เดิมเป็นหลังคาแดง
5. หน้าบ้านหันเข้าลานเหมือนเดิม (door +Z เดิม = หน้า -Z ของ prefab, หมุน +180 ตอนวาง); Cottage ขยาย 1.25 ให้พอดีกล่อง collision เดิม (7 x 6 ม.)
6. **ไม่เปิด post-processing/SSAO ในเกมจริง**: เกมไม่มี post-processing (BuildScript ปิดไว้เพื่อ performance) การเปิดต้องเพิ่ม renderer data + ผลต่อเฟรมไทม์ยังไม่วัด — เหตุผลเบาที่สุดคือคงเดิม; หน้าต่างบ้านใช้สี emission (ไม่มี bloom). ถ้าต้องการให้บอก จะทำเป็นงานแยก
7. Material ของ kit รวมตามสี (21 prefab ใช้ 13 material) เพื่อลดจำนวน chunk mesh; ใช้ material ตัวเดียวกับ prefab (URP Lit, instancing)
8. ภาพในเกมถ่ายโดย selftest (ไม่ต้องมี script ถ่ายภาพแยก); ที่ `Docs\asset-import\ingame\`

## ตัวเลข
| | ก่อน | หลัง |
| --- | --- | --- |
| ชิ้นที่ merge ใน StaticBatcher | 8,548 | 12,016 |
| chunk mesh | 1,270 | 1,627 |
| obstacle | 1,304 | 1,304 |
| เวลาสร้างฉาก (WORLD built) | 46 ms | 94 ms |
| frame time (selftest 1280x720, ทุกจุด) | 4.2 ms (worst 5-6) | 4.2 ms (worst 5-7) |
| ขนาด player (Unity build result) | 102,860,558 B | 103,587,122 B |

หมายเหตุ frame time: ค่าเฉลี่ย 4.2 ms เท่ากันทุกจุดทั้งก่อนและหลัง แปลว่าถูกจำกัดที่เฟรมเรตของจอ/vsync ในหน้าต่างเทส ไม่ใช่ขีดความสามารถ GPU จึงบอกได้แค่ว่า "ยังไม่ตกจากเดิม"; รวมสามเหลี่ยมฉากเพิ่มขึ้น (ต้นสน ~890 x 92, ต้นกลม 710 x ~480, รั้ว 825 x 160 tris, บ้าน 22 x 2,822)

## ผลเทส
- build.bat ผ่าน (errors=0)
- selftest: failed checks=0 (ไม่มี FAIL)
- mptest: ผ่านทุกกลุ่ม H/A/B/C/X/V/N FAIL=0 exceptions=0 (เช็ค "A's walk looks smooth on B" ที่เคยตกก็ผ่านรอบนี้)

## ภาพในเกม (`Docs\asset-import\ingame\`)
03_asset_town, 04_asset_meadow, 05_asset_autumn_forest, 06_asset_pine_forest, 07_asset_town_houses, 08_asset_farm, 09_asset_gate (.png)

## เหลืออะไร / รอใคร
- ขั้น 6 zip ขึ้น Drive: ดูส่วนท้ายถ้าทำแล้ว
- ถ้าอยากให้ถังไม้/ตอไม้บล็อกผู้เล่น หรือเปิด bloom/SSAO ในเกม ต้องให้พีทตัดสิน (ข้อ 1 และ 6)

## ขั้น 6 เสร็จ (2026-10-11)
- zip `medieval-knight-win64.zip` (โครง `MedievalKnight/...` เดิม, 184 ไฟล์, 38,590,019 bytes) copy ทับใน `G:\My Drive\PusomGames\` แล้ว จาก build ที่ผ่าน selftest + mptest
- ไม่ได้ push GitHub

## Banana-sword.fbx ผูกกับดาบเล่มที่ 6 (2026-10-12)
- สำเนาไฟล์ใช้ใน player build: `Assets/MedievalKnight/Resources/Models/Banana-sword.fbx` (ต้นฉบับยังอยู่ที่ `Assets/Models/Weapons/`) โหลดด้วย `Resources.Load<GameObject>("Models/Banana-sword")`
- แกนหลัง Unity import: ใบมีดยาวตาม -Z (ปลาย z -1.39, โค้งไป -X), มี Camera/Light ติดมาใน FBX (ถูกถอดตอนสร้างโมเดลในเกม) ในเกมหมุน yaw 180 ให้ +Z = มือไปปลาย ดู `Docs/combat-tweaks/status.md`
