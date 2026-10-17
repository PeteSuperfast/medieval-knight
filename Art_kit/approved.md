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

## Snow map (โมเดลแมพหิมะ)

เจ้าของอนุมัติ 2026-10-16 (peatengkrub) — ทั้งชุด 123 ไฟล์ รวม 73,730 tris (3.97 MB) ที่ `Assets/Models/Snow/` สร้างจาก `Docs/snow-map-models/make_snow_map.py -- --exportall` (ตรวจไฟล์ด้วย `verify_snow_fbx.py`)

ตั้งชื่อ `snow_<หมวด>_<ชื่อ>_<vNN>.fbx` | ภูมิประเทศ chunk 4x4 (95x95 ม.) | ชิ้นซ้ำ export 1 ชิ้น วางตาม `Docs/snow-map-models/layout.json` | กังหันแยกใบพัด (pivot = แกนใบพัด)

**แกน Blender -> Unity** (ตรวจจากไฟล์ .fbx จริง): vertex เก็บแกน Blender ดิบ + root Lcl Rotation (-90,0,0), UnitScale 100 -> หลัง import (x,y,z)_blender = (-x, z, -y)_unity (X กลับด้านเหมือนกระท่อม/รั้วเดิม) | ตำแหน่งโลก (E,N,Z)_blender = (E,Z,N)_unity | ใส่ FBX ใน child หมุน Y=180 (เหมือน FrontYaw) | yaw Unity = -rz | รายละเอียดเต็มใน `layout.json` > `unity_axis_mapping`

material = สีล้วนตามจานสี (ไม่มี texture/UV), pivot = กลางฐาน (chunk = กลาง chunk ที่ z=-12 ม.; ไฟล์ world mesh/merged = pivot ที่ origin โลก 0,0,0), flat shading

| ไฟล์ (`Assets/Models/Snow/`) | tris | ขนาด กว้าง x ลึก x สูง (ม.) | อนุมัติ | ภาพ |
| --- | --- | --- | --- | --- |
| snow_bridge_s1_v01.fbx | 1140 | 18.7 x 4.9 x 3.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_bridge_s2_v01.fbx | 1052 | 14.7 x 4.9 x 3.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_bridge_s3_v01.fbx | 1072 | 10.7 x 4.9 x 3.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_building_house_v01.fbx | 1095 | 8.9 x 8.0 x 13.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_building_house_v02.fbx | 1167 | 10.4 x 8.0 x 13.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_building_house_v03.fbx | 1129 | 8.9 x 8.0 x 13.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_building_windmillbody_v01.fbx | 649 | 6.8 x 7.5 x 14.7 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_building_windmillsails_v01.fbx | 474 | 14.4 x 1.2 x 14.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_castle_base_v01.fbx | 64 | 39.7 x 40.6 x 9.5 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_castle_keep_v01.fbx | 1141 | 32.4 x 36.7 x 38.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_castle_walls_v01.fbx | 1724 | 35.3 x 37.0 x 21.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_campfire_v01.fbx | 301 | 5.1 x 5.4 x 2.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_chains_v01.fbx | 780 | 42.9 x 26.6 x 13.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_deco_crystal_v01.fbx | 80 | 4.7 x 4.1 x 6.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_crystal_v02.fbx | 65 | 3.4 x 3.8 x 5.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_crystal_v03.fbx | 50 | 3.4 x 2.5 x 4.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_crystal_v04.fbx | 65 | 5.2 x 3.9 x 7.3 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_crystal_v05.fbx | 80 | 2.9 x 3.5 x 3.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_icecave_v01.fbx | 439 | 10.8 x 10.9 x 7.3 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_icecave_v02.fbx | 411 | 10.8 x 10.8 x 7.3 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_icelake_v01.fbx | 440 | 64.4 x 44.8 x 2.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_deco_igloo_v01.fbx | 363 | 7.0 x 8.3 x 3.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_igloo_v02.fbx | 363 | 7.8 x 9.3 x 3.7 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_igloo_v03.fbx | 329 | 6.3 x 7.3 x 3.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_sled_v01.fbx | 316 | 3.4 x 1.7 x 1.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_deco_snowman_v01.fbx | 414 | 4.2 x 1.9 x 4.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_path_icechunks_v01.fbx | 337 | 88.3 x 105.4 x 43.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_path_icesteps_v01.fbx | 2260 | 93.5 x 105.9 x 45.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_path_stones_v01.fbx | 642 | 200.8 x 192.6 x 33.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_path_surface_v01.fbx | 3200 | 219.3 x 208.7 x 37.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_plant_bush_v01.fbx | 114 | 3.2 x 2.3 x 1.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_plant_flower_purple_v01.fbx | 15 | 0.7 x 0.7 x 0.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_plant_flower_red_v01.fbx | 15 | 0.7 x 0.7 x 0.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_plant_flower_white_v01.fbx | 15 | 0.7 x 0.7 x 0.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_plant_flower_yellow_v01.fbx | 15 | 0.7 x 0.7 x 0.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_plant_mushroom_v01.fbx | 22 | 0.7 x 0.5 x 0.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_plant_tuft_v01.fbx | 15 | 0.4 x 0.4 x 0.7 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_banner_v01.fbx | 58 | 1.7 x 0.9 x 5.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_barrel_v01.fbx | 112 | 1.1 x 1.1 x 0.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_chainpost_v01.fbx | 48 | 0.5 x 0.5 x 1.3 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_crate_v01.fbx | 72 | 0.9 x 0.9 x 0.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_dock_v01.fbx | 412 | 8.3 x 2.7 x 3.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_fence_v01.fbx | 108 | 4.1 x 0.3 x 1.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_fountain_v01.fbx | 298 | 8.0 x 8.0 x 5.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_icelamp_v01.fbx | 111 | 1.0 x 0.8 x 3.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_lamp_v01.fbx | 142 | 1.4 x 0.6 x 3.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_signcastle_v01.fbx | 134 | 5.0 x 1.5 x 8.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_signmountain_v01.fbx | 95 | 5.0 x 1.2 x 7.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_signpine_v01.fbx | 134 | 5.0 x 2.2 x 7.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_signsnow_v01.fbx | 200 | 5.0 x 1.2 x 7.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_slabarc_v01.fbx | 72 | 1.6 x 3.2 x 0.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_stall_v01.fbx | 120 | 3.8 x 2.2 x 3.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_stall_v02.fbx | 120 | 3.8 x 2.2 x 3.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_prop_stall_v03.fbx | 120 | 3.8 x 2.2 x 3.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_pillar_v01.fbx | 101 | 6.4 x 6.0 x 12.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_pillar_v02.fbx | 101 | 6.2 x 6.4 x 12.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_pillar_v03.fbx | 101 | 6.5 x 6.2 x 13.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_pillar_v04.fbx | 101 | 6.2 x 6.4 x 13.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_pillarsnow_v01.fbx | 116 | 6.4 x 6.0 x 12.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_pillarsnow_v02.fbx | 116 | 6.2 x 6.4 x 12.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_pillarsnow_v03.fbx | 116 | 6.5 x 6.2 x 13.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_pillarsnow_v04.fbx | 116 | 6.2 x 6.4 x 13.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_small_v01.fbx | 46 | 1.3 x 1.1 x 0.7 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_small_v02.fbx | 54 | 1.8 x 1.4 x 0.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_small_v03.fbx | 51 | 2.6 x 1.9 x 1.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_small_v04.fbx | 54 | 2.8 x 2.4 x 1.3 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_small_v05.fbx | 46 | 3.3 x 2.8 x 1.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_small_v06.fbx | 54 | 4.0 x 3.4 x 1.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_smallsnow_v01.fbx | 46 | 1.3 x 1.1 x 0.7 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_smallsnow_v02.fbx | 54 | 1.8 x 1.4 x 0.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_smallsnow_v03.fbx | 46 | 2.4 x 1.9 x 1.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_smallsnow_v04.fbx | 54 | 2.8 x 2.4 x 1.3 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_smallsnow_v05.fbx | 46 | 3.3 x 2.8 x 1.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_rock_smallsnow_v06.fbx | 54 | 4.0 x 3.4 x 1.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_scene_cloud_v01.fbx | 128 | 182.8 x 118.2 x 21.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_scene_mist_v01.fbx | 240 | 76.7 x 66.6 x 17.3 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r0c0_v01.fbx | 2870 | 95.0 x 95.0 x 17.7 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r0c1_v01.fbx | 2660 | 95.0 x 95.0 x 25.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r0c2_v01.fbx | 2660 | 95.0 x 95.0 x 36.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r0c3_v01.fbx | 2870 | 95.0 x 95.0 x 39.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r1c0_v01.fbx | 2660 | 95.0 x 95.0 x 33.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r1c1_v01.fbx | 2450 | 95.0 x 95.0 x 18.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r1c2_v01.fbx | 2450 | 95.0 x 95.0 x 15.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r1c3_v01.fbx | 2660 | 95.0 x 95.0 x 42.1 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r2c0_v01.fbx | 2660 | 95.0 x 95.0 x 61.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r2c1_v01.fbx | 2450 | 95.0 x 95.0 x 31.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r2c2_v01.fbx | 2452 | 380.0 x 380.0 x 64.9 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r2c3_v01.fbx | 2660 | 95.0 x 95.0 x 63.5 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r3c0_v01.fbx | 2870 | 95.0 x 95.0 x 84.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r3c1_v01.fbx | 2660 | 95.0 x 95.0 x 81.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r3c2_v01.fbx | 2660 | 95.0 x 95.0 x 74.7 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_chunk_r3c3_v01.fbx | 2870 | 95.0 x 95.0 x 92.5 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_drifts_v01.fbx | 1560 | 172.0 x 357.6 x 49.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_terrain_mountain_v01.fbx | 250 | 2.1 x 1.9 x 1.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_terrain_mountain_v02.fbx | 250 | 2.2 x 1.9 x 1.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_terrain_mountain_v03.fbx | 250 | 2.1 x 1.8 x 1.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_terrain_mountain_v04.fbx | 250 | 2.2 x 2.0 x 1.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_bare_v01.fbx | 90 | 3.5 x 2.6 x 5.7 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_blossom_v01.fbx | 113 | 4.9 x 4.1 x 6.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_log_v01.fbx | 40 | 1.6 x 0.5 x 0.5 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_pine_v01.fbx | 138 | 6.5 x 6.5 x 10.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_pine_v02.fbx | 114 | 7.6 x 7.3 x 8.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_pine_v03.fbx | 101 | 4.9 x 4.8 x 7.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_pine_v04.fbx | 138 | 5.6 x 5.5 x 8.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_pinesnow_v01.fbx | 135 | 6.4 x 6.6 x 10.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_pinesnow_v02.fbx | 111 | 7.4 x 7.4 x 8.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_pinesnow_v03.fbx | 99 | 5.0 x 4.8 x 7.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_pinesnow_v04.fbx | 135 | 5.6 x 5.5 x 8.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_round_v01.fbx | 113 | 4.9 x 4.0 x 6.2 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_stump_v01.fbx | 76 | 1.1 x 1.1 x 0.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_tree_tall_v01.fbx | 113 | 3.2 x 3.0 x 9.6 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_objects.png` |
| snow_village_lane_v01.fbx | 32 | 6.2 x 22.0 x 3.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_village_plazapaving_v01.fbx | 120 | 18.0 x 18.0 x 0.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_branch_v01.fbx | 54 | 20.9 x 48.9 x 0.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_foam_v01.fbx | 260 | 17.7 x 22.2 x 21.4 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_lakeoutlet_v01.fbx | 4 | 14.2 x 12.1 x 0.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_pool_v01.fbx | 8 | 16.4 x 14.0 x 0.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_river_v01.fbx | 140 | 157.5 x 120.5 x 0.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_stream_v01.fbx | 64 | 81.6 x 55.0 x 0.0 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_waterfall0_v01.fbx | 90 | 14.0 x 13.5 x 20.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_waterfall1_v01.fbx | 90 | 12.2 x 13.5 x 20.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_waterfall2_v01.fbx | 90 | 10.4 x 13.5 x 20.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |
| snow_water_waterfall3_v01.fbx | 90 | 8.6 x 13.5 x 20.8 | peatengkrub 2026-10-16 | `Docs/snow-map-models/preview_top.png` |

หมายเหตุ: ภาพรวมแมพ `Docs/snow-map-models/preview.png`; ขนาดของไฟล์ world mesh (น้ำ/ทาง/ลานหมู่บ้าน/หิมะทับสัน) = ขอบเขตทั้งแมพ ไม่ใช่ขนาดชิ้นเดียว; `snow_deco_chains_v01`/`snow_scene_mist_v01`/`snow_scene_cloud_v01` = รวมหลายก้อนเป็นไฟล์เดียว (13/5/8 ก้อน) ตำแหน่งในพิกัดโลก; `Tree_Round_NNN` ใน layout ใช้ mesh เดียวกับ `snow_tree_round_v01`; ภูมิประเทศหลักไม่มีไฟล์ `Terrain_Ground` เดี่ยว (แบ่งเป็น chunk 16 ไฟล์ r0c0..r3c3)

## Snow map in game (นำเข้าเกมแล้ว 2026-10-17)

`Assets/Models/Snow/*.fbx` ทั้ง 123 ไฟล์ถูกนำเข้าเป็น prefab (`Assets/Prefabs/Snow`, material `Assets/Materials/Snow`, `Resources/Kit/SnowCatalog.prefab`) ด้วย Editor `SnowImport` (ตั้ง ModelImporter: Read/Write, ไม่มี animation; ไม่แก้ไฟล์ .fbx). หน้าที่หันเข้าด้านในของหลังคา/ผ้าใบ/ผิวน้ำแข็งทะเลสาบแก้ด้วย mesh สองหน้าใน `Assets/Prefabs/Snow/Meshes`. การวางใช้ `Docs/snow-map-models/layout.json` ผ่าน `SnowBake` -> `Resources/Snow`. Tree_Round 12 ต้นยังไม่มีตำแหน่งใน layout.json. รายละเอียด: `Docs/snow-map-models/ingame-report.md`
