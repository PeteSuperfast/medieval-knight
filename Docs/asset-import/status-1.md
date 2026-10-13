# สถานะ: นำ asset เข้าเกม (asset-import)
> ส่วน 1/2 ของ `status.md` (ดัชนี: `status.md`) · ต่อที่ `status-2.md`

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

