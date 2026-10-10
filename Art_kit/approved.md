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
