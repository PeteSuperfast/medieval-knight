# สถานะ: นำ asset เข้าเกม (asset-import)

คนขอ: peatengkrub (เจ้าของ) — อัปเดตล่าสุด 2026-10-11

## ทำถึงไหน
| ขั้น | สถานะ |
| --- | --- |
| 1 ตรวจ approved.md กับ Assets\Models\ (21 ไฟล์) | เสร็จ (ปูส้มตรวจ) |
| 2 prefab ทุก asset (URP Lit, GPU instancing, collider, static) | เสร็จ 21 prefab ใน `Assets\Prefabs\{Trees,Rocks,Props,Buildings}` |
| 3 แสงและ post-processing ตาม Docs\style.md | เสร็จ (เฉพาะฉาก Showcase) |
| 4 scene Showcase + ภาพ render | เสร็จ รูปอยู่ `Docs\asset-import\showcase\` **รออนุมัติจากพีท** |
| 5 แทน placeholder ในแมพ + กระจายของประกอบ + build | ยังไม่ทำ (ต้องรออนุมัติขั้น 4) |
| 6 zip ขึ้น Drive | ยังไม่ทำ |

## สิ่งที่สร้าง
- `Assets\Editor\AssetImport.cs` — editor script สร้าง material/prefab/ฉาก/ภาพ (รันซ้ำได้: `Unity -batchmode -projectPath . -executeMethod AssetImport.RunAll` ไม่ต้องใส่ -nographics/-quit)
- `Assets\Materials\Kit\MK_*.mat` — URP Lit สีตามจานสี ใช้ร่วมกันทุก prefab, enableInstancing; `MK_window_glow` มี Emission #F5C25A x1.6
- `Assets\Prefabs\...` — static, collider ตามประเภท (ตารางข้างล่าง)
- `Assets\Scenes\Showcase.unity` — แยกจาก Main.unity ไม่อยู่ใน Build Settings
- `Assets\Showcase\` — pipeline asset + renderer แยก (post-processing + SSAO), volume profile, sky gradient shader/material

## Collider ต่อประเภท
| ประเภท | collider |
| --- | --- |
| Tree_Pine / Round_Slender / Round_Wide | CapsuleCollider เฉพาะลำต้น (รัศมีจากจุดยอดใกล้พื้น, สูง <= 2.2 ม.) ไม่คลุมพุ่มใบ |
| Rock_* (9) | MeshCollider convex |
| Barrel_Upright | CapsuleCollider |
| Barrel_Lying, Fence_Straight_2m, Fence_CornerPost, Tree_Stump | BoxCollider |
| Cottage | BoxCollider ตัวบ้าน (ฐาน+ผนัง) + BoxCollider ปล่อง; หลังคา/ประตู/หน้าต่างไม่มี collider |
| Mushroom_Red_Cluster, Grass_Tuft, Stepping_Stone | ไม่มี (ของตกแต่ง เดินทับได้) |

## ตัดสินใจไปแล้ว (developer ตัดสินเอง ขออนุมัติ/แก้ได้)
1. Cottage / Fence_Straight_2m / Fence_CornerPost: ใน prefab มี GameObject `Pivot` หมุน Y 180 ครอบ FBX เพราะ FBX เข้า Unity แล้ว "ด้านหน้า" (ประตู/ราวรั้ว, -Y ใน Blender) หันไป +Z; หลังหมุน ด้านหน้าหันไป -Z (หันหากล้องเกม). รากของ FBX (Cottage X -90) ไม่ได้แก้. ถ้าไม่ต้องการ ให้ตั้ง `FrontYaw = 0` ใน AssetImport.cs แล้วรันใหม่
2. Emission ความแรง 1.6 = คูณสี #F5C25A (linear) 1.6 เท่า (ไม่ใช่ EV 1.6)
3. แสง Showcase: Directional สี #FFEBCB ความแรง 1.45 หมุน (38, 62, 0) เงานุ่ม; ambient แบบ Trilight จาก #B6C4DE / #D5BFC3 / #8DB04A; ท้องฟ้า gradient #B6C4DE -> #D5BFC3
4. Post-processing: Bloom (threshold 1.0 ให้เฉพาะหน้าต่างเรือง), Color Adjustments อุ่นเล็กน้อย, SSAO; **ไม่ใช้ Tonemapping** (ลองแล้วสีหญ้าเพี้ยนจากจานสี)
5. post-processing/SSAO อยู่ใน pipeline asset แยก `UniversalRP_Showcase.asset` ใช้เฉพาะตอน render Showcase — ไม่แตะ `UniversalRP.asset` / `Renderer3D.asset` ของเกม (ตอนนี้เกมไม่มี post-processing; ถ้าจะเปิดในเกมจริงเป็นงานขั้น 5 ที่ต้องถามอีกที)
6. ไม่ได้เปลี่ยน import setting ของ FBX (มีแค่ .meta ใหม่)

## ตรวจแล้วว่าเกมเดิมไม่พัง
- build.bat ผ่าน ขนาด player เท่าเดิมเป๊ะ (102,860,558 bytes) — prefab/model/material ใหม่ไม่ถูกรวมเข้าเกมเพราะไม่มีอะไรอ้างถึง
- selftest ผ่าน `failed checks=0`
- ไม่แตะโค้ดใน `Assets\MedievalKnight\Scripts\`, ไม่มีผลกับ System.Random(11) / Net.Protocol

## เหลืออะไร / รอใคร
- รอพีทอนุมัติภาพ Showcase เทียบกับ `Art_kit\previews\*.png`
- หลังอนุมัติ: ขั้น 5 (แทน placeholder, กระจายของประกอบด้วย seed, build, รออนุมัติ) และขั้น 6 (zip + Drive)
- ข้อควรระวังขั้น 5: ห้ามวางของกีดขวางในลานกลาง (|x|<28, Z -42..32); ต้องใช้ System.Random(11) ตามเดิมถ้ามี collision; Net.Protocol ต้องเพิ่มถ้าซิงก์ของใหม่

---

# อัปเดต: ขั้น 5-6 (2026-10-11) — แทน placeholder ในเกม + zip ขึ้น Drive

เจ้าของอนุมัติภาพ Showcase (ข้อความ "approve" 05:41) และสั่งให้ทำต่อจนอัป Drive ได้เลย (05:42)

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
