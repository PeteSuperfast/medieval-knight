# Approved assets (Art kit)

เฉพาะ asset ที่เจ้าของอนุมัติแล้ว developer เอาไปใช้ในเกมได้
สร้างจาก `Art_kit/style_kit.py` (`blender -b -P Art_kit/style_kit.py -- <asset>`) → FBX ที่ `Assets/Models/`

| Asset | ฟังก์ชัน | FBX | tris | สูง | อนุมัติ | ภาพ |
| --- | --- | --- | --- | --- | --- | --- |
| Tree_Pine ต้นสน (ลำต้น 6 เหลี่ยม + ใบ 4 ชั้นเอียง สลับ #465820 / #6C8535) | `make_pine()` | `Assets/Models/Tree_Pine.fbx` | 92 | 3.998 ม. | peatengkrub 2026-10-10 | `Art_kit/previews/Tree_Pine.png` |

หมายเหตุ: origin = กลางฐานลำต้น, Z up ใน Blender, material สีล้วน (MK_<ชื่อสี>) ไม่มี texture/UV, flat shading
งานต้นฉบับ/ภาพเทียบอ้างอิง: `Docs/pine-tree/`
