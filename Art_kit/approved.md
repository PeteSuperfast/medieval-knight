# Approved assets (Art kit)

เฉพาะ asset ที่เจ้าของอนุมัติแล้ว developer เอาไปใช้ในเกมได้
สร้างจาก `Art_kit/style_kit.py` (`blender -b -P Art_kit/style_kit.py -- <asset>`) → FBX ที่ `Assets/Models/`

| Asset | ฟังก์ชัน | FBX | tris | สูง | อนุมัติ | ภาพ |
| --- | --- | --- | --- | --- | --- | --- |
| Tree_Pine ต้นสน (ลำต้น 6 เหลี่ยม + ใบ 4 ชั้นเอียง สลับ #465820 / #6C8535) | `make_pine()` | `Assets/Models/Tree_Pine.fbx` | 92 | 3.998 ม. | peatengkrub 2026-10-10 | `Art_kit/previews/Tree_Pine.png` |

หมายเหตุ: origin = กลางฐานลำต้น, Z up ใน Blender, material สีล้วน (MK_<ชื่อสี>) ไม่มี texture/UV, flat shading
งานต้นฉบับ/ภาพเทียบอ้างอิง: `Docs/pine-tree/`

## Rocks (หิน 3 ขนาด x 3 แบบ)

เจ้าของอนุมัติรูปทรง 2026-10-10 (peatengkrub) — สร้างจาก seed ด้วย `make_rock(kind, seed)` ใน `style_kit.py`
รันทั้งชุด: `blender -b -P Art_kit/style_kit.py -- rocks` → `Assets/Models/Rocks/` + `Art_kit/previews/Rocks_variants.png`
อยากได้แบบใหม่: เรียก `make_rock("medium", seed=<เลขอะไรก็ได้>)` (kind เดียวกัน + seed เดียวกัน = หินเหมือนเดิมเสมอ)

| ไฟล์ (`Assets/Models/Rocks/`) | seed | tris (งบ) | สูงเหนือพื้น | ฐาน (กว้าง x ลึก) |
| --- | --- | --- | --- | --- |
| Rock_Small_A / B / C | 3 / 14 / 27 | 20 / 20 / 20 (60) | 0.27 / 0.33 / 0.27 ม. | ~0.5 x 0.5 ม. |
| Rock_Medium_A / B / C | 11 / 42 / 77 | 72 / 72 / 68 (150) | 1.07 / 1.04 / 1.04 ม. | ~1.0-1.3 x 1.1-1.5 ม. |
| Rock_Large_A / B / C (กลุ่ม 2-3 ก้อน) | 21 / 58 / 90 | 212 / 216 / 138 (500) | 2.31 / 2.74 / 2.66 ม. | ~2-3 x 2-3.7 ม. |

หมายเหตุ: ความสูงสุ่ม ±15% ตาม seed, ฐานจมพื้น 12% ของความสูงรวม (origin z=0 = ระดับพื้น), origin xy = กลางฐาน, material 2 ช่อง `MK_rock_top` (#ADABB7 หน้าที่หันขึ้น) / `MK_rock_side` (#958A80)

## Barrel (ถังไม้ 8 เหลี่ยม)

เจ้าของอนุมัติ 2026-10-10 (peatengkrub) หลังปรับแผ่นไม้ต่ำสูง + ห่วงเอียง — `make_barrel(name, seed, lying)` ใน `style_kit.py`
รัน: `blender -b -P Art_kit/style_kit.py -- barrels` → `Assets/Models/Props/` + `Art_kit/previews/Barrels.png`

| ไฟล์ (`Assets/Models/Props/`) | seed | tris | ขนาด |
| --- | --- | --- | --- |
| Barrel_Upright.fbx (ตั้ง) | 5 | 324 (งบ 400) | สูง 0.90 ม. กว้างกลาง ~0.67 ม. กว้างปาก/ฐาน ~0.56 ม. |
| Barrel_Lying.fbx (นอน ด้านแบนวางพื้น) | 9 | 324 (งบ 400) | สูงจากพื้น 0.67 ม. ยาว 0.90 ม. |

หมายเหตุ: origin = กลางฐาน (ถังนอน = กลางรอยวางบนพื้น, ไม่มี yaw — หมุนเองตอนวางในฉาก), 8 แผ่นไม้แยกชิ้น, ห่วงเหล็ก 2 วงเอียง ~4°, ฝายุบ 3.5 ซม. 4 แผ่น
material: `MK_barrel_wood` #A1773D, `MK_barrel_groove` #715332, `MK_barrel_iron` #3A3A3A; ต้นฉบับ/เทียบอ้างอิง `Docs/barrel/`

## Fence (รั้วไม้)

เจ้าของอนุมัติ 2026-10-10 (peatengkrub) — `make_fence_straight()` / `make_fence_corner()` ใน `style_kit.py`
รัน: `blender -b -P Art_kit/style_kit.py -- fence` → `Assets/Models/Props/` + `Art_kit/previews/Fence.png`

| ไฟล์ (`Assets/Models/Props/`) | seed | tris | ขนาด (x y z) |
| --- | --- | --- | --- |
| Fence_Straight_2m.fbx | 21 | 160 | 1.99 x 0.23 x 1.10 ม. (เสา 3 ต้น ราง 2 ชั้น หมุดเหล็ก) |
| Fence_CornerPost.fbx | 34 | 180 | 0.61 x 0.61 x 1.10 ม. (เสา + ราง 2 ชั้น 2 แขน ไปทาง -X และ +Y) |

หมายเหตุ: origin = กลางฐานเสา (รั้วตรง = เสากลาง), ราง/หมุดอยู่ฝั่ง -Y ของรั้วตรง, ไม่มี yaw ใน FBX (เสามุมหมุน -45 องศาเฉพาะภาพ preview)
material: `MK_fence_wood` #A1773D (หน้ากว้าง/ด้านบน), `MK_fence_side` #715332 (ด้านข้าง/รอยบิ่น), `MK_fence_peg` #3A3A3A; ต้นฉบับ `Docs/fence/`

## Small props (ของเล็กๆ)

เจ้าของอนุมัติ 2026-10-10 (peatengkrub) — `make_mushroom_cluster / make_grass_tuft / make_stepping_stone / make_tree_stump` ใน `style_kit.py`
รัน: `blender -b -P Art_kit/style_kit.py -- props` → `Assets/Models/Props/` + `Art_kit/previews/Small_props.png`

| ไฟล์ (`Assets/Models/Props/`) | seed | tris | ขนาด (x y z) |
| --- | --- | --- | --- |
| Mushroom_Red_Cluster.fbx | 21 | 119 | 0.35 x 0.24 x 0.25 ม. (เห็ด 3 ดอก 25/17/10 ซม.) |
| Grass_Tuft.fbx | 33 | 70 | 0.21 x 0.16 x 0.20 ม. |
| Stepping_Stone.fbx | 45 | 40 | 0.35 x 0.31 x 0.06 ม. |
| Tree_Stump.fbx | 57 | 78 | 0.50 x 0.50 x 0.40 ม. |

หมายเหตุ: origin = กลางฐาน (z=0 = พื้น)
material: `MK_mushroom_red` #C8483A, `MK_plaster_cream` #D8CCB0, `MK_grass_mid` #6C8535, `MK_grass_light` #8DB04A, `MK_rock_top` #ADABB7, `MK_rock_side` #958A80, `MK_wood_dark` #715332, `MK_wood_light` #A1773D; ต้นฉบับ `Docs/small-props/`

## Round trees (ต้นไม้พุ่มกลม)

เจ้าของอนุมัติ 2026-10-10 (peatengkrub) — `make_round_tree(kind, seed=None)` kind = `slender` / `wide` ใน `style_kit.py`
รัน: `blender -b -P Art_kit/style_kit.py -- round_trees` → `Assets/Models/` + `Art_kit/previews/Round_trees.png`

| ไฟล์ (`Assets/Models/`) | seed | tris | ขนาด (x y z) |
| --- | --- | --- | --- |
| Tree_Round_Slender.fbx (เพรียว 4 พุ่ม) | 3 | 428 | 3.37 x 2.20 x 4.80 ม. |
| Tree_Round_Wide.fbx (กว้าง 5 พุ่ม) | 8 | 534 | 5.66 x 4.30 x 4.66 ม. |

หมายเหตุ: origin = ฐานลำต้น (ลำต้นเอียง/โค้ง ฐานจมพื้นเล็กน้อย <= 4 ซม.), ลำต้น 6 เหลี่ยม + กิ่ง 5 เหลี่ยม
material: `MK_wood_dark` #715332, `MK_leaf_light` #A0B85B (หน้าที่หันขึ้น), `MK_leaf_under` #6C8535; ต้นฉบับ `Docs/round-trees/`

## Cottage (บ้านกระท่อม)

เจ้าของอนุมัติ 2026-10-10 (peatengkrub) — `make_cottage(seed=7)` ใน `style_kit.py` (ต้นฉบับ `Docs/cottage/make_cottage_detail.py`, ภาพที่อนุมัติ `Docs/cottage/preview_detail_front.png` / `_back.png`)
รัน: `blender -b -P Art_kit/style_kit.py -- cottage` → `Assets/Models/Buildings/Cottage.fbx` (ไฟล์เดียวทั้งหลัง) + `Art_kit/previews/Cottage_front.png` / `Cottage_back.png`
ทั้งหลัง 2,822 tris, สูง 7.40 ม. (ยอดปล่อง), กว้าง x ลึก 5.64 x 4.62 ม. รวมชายคา (ถึงปล่อง 5.97 ม. ใน X), ฐานจมพื้น 5 ซม.

ใน FBX: empty ราก `Cottage` + 6 mesh ลูก (แยก object แต่ละชิ้น)

| object ใน FBX | ฟังก์ชัน | tris | ขนาด (x y z) | material |
| --- | --- | --- | --- | --- |
| Cottage_Plinth ฐานหิน + ก้อนหินฝังด้านข้าง 30 ก้อน (ช่องประตูเจาะแล้ว) | `cottage_plinth()` | 628 | 5.09 x 4.10 x 0.85 ม. (z -0.05..0.80) | rock_side, rock_top, opening_dark |
| Cottage_Walls ผนังปูน + ช่องประตู/หน้าต่าง + หน้าจั่ว + โครงไม้ + หินมุม | `cottage_walls()` | 1,114 | 4.76 x 3.76 x 5.05 ม. (z 0.75..5.80) | plaster_cream, opening_dark, wood_dark, rock_top, rock_side |
| Cottage_Roof หลังคา + ขอบไม้ชายคา/หน้าจั่ว + dormer (รวมกรอบและกระจก dormer) | `cottage_roof()` | 440 | 5.64 x 4.62 x 4.24 ม. (z 2.29..6.53) | roof_purple, wood_dark, plaster_cream, window_glow |
| Cottage_Door บานไม้โค้ง + บานพับเหล็ก + ห่วง + กรอบหิน | `cottage_door()` | 336 | 1.50 x 0.54 x 2.29 ม. | wood_light, wood_dark, barrel_iron, rock_side, rock_top |
| Cottage_Windows หน้าต่างโค้ง 2 บาน: กรอบไม้ + ขอบล่าง + กระจกเรืองแสง + ซี่ไม้ | `cottage_windows()` | 280 | 3.80 x 0.31 x 1.13 ม. | wood_dark, wood_light, window_glow |
| Cottage_Chimney ปล่องหินด้าน +X | `cottage_chimney()` | 24 | 1.03 x 1.01 x 7.40 ม. | rock_side, rock_top |

ค่าที่ทำให้ชิ้นส่วนต่อกันได้ = ค่าคงที่ `COTTAGE_*` ใน `style_kit.py` (ขนาดฐาน/ผนัง/หลังคา, ช่องเปิด `COTTAGE_DOOR` / `COTTAGE_WINS`, seed) — อย่าแก้ตัวเลขโดยไม่ถามเจ้าของ; ชิ้นเดียวเรียกแยกได้ (ผนังเจาะช่องเองด้วยค่าชุดเดียวกัน)
หมายเหตุ:
- origin ราก = กลางฐานระดับพื้น (z=0), Z up ใน Blender, 1 หน่วย = 1 ม., ไม่หมุน (ภาพ preview หมุน -20 / +240 องศาเฉพาะตอน render) ประตูหันไป -Y, ปล่องอยู่ +X
- empty ราก `Cottage` ใน FBX จะมี rotation X = -90 (ธรรมชาติของ export จาก Blender; mesh ลูก bake แล้ว ภาพโลกถูกต้อง) — ถ้าจะหมุนบ้านใน Unity ให้ใส่ GameObject ครอบ อย่าแก้ rotation ของราก
- **emission ไม่ติดมาใน FBX**: ใน Unity ต้องตั้ง Emission สี #F5C25A (ความแรง ~1.6) ที่ material `window_glow` เอง (ใช้กับกระจกหน้าต่างและ dormer)
- material `opening_dark` #3A3A3A (ช่องมืดหลังประตู/หน้าต่าง), `roof_purple` #5E5878, `window_glow` #F5C25A, ที่เหลือใช้สี kit เดิม (`plaster_cream` #D8CCB0, `wood_dark` #715332, `wood_light` #A1773D, `barrel_iron` #3A3A3A, `rock_top` / `rock_side`)
- ของประดับในภาพ preview ไม่รวมใน FBX: ควัน 3 ก้อน (`cottage_smoke()` ไม่ export), ถัง 2 ใบ (`Props/Barrel_*.fbx`), หินแผ่น (`Props/Stepping_Stone.fbx`), เห็ด (`Props/Mushroom_Red_Cluster.fbx`), หญ้า (`Props/Grass_Tuft.fbx`) — ใช้ไฟล์ใน kit วางเอง
