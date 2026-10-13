> ส่วน 10/14 ของ `tech.md` (ดัชนี: `tech.md`) · ต่อจาก `tech-9.md` · ต่อที่ `tech-11.md`

| รายการ | ตอนนี้ | ใหม่ (งบ) |
| --- | --- | --- |
| ต้นไม้ | ~100 | ~2,500 รวม (ป่าส้ม/สนหนาแน่น ~7-8 ม. ต่อต้น; ทุ่ง/ไร่โปร่ง; ทะเลสาบ/ภูเขา/เมืองน้อย) |
| บ้าน | 0 | ~30-40 หลัง (box ~25 ชิ้น/หลัง) + หอ/กังหัน/ป้าย |
| renderer แบบเดิม `Prims.Part` ต่อชิ้น | ~500 | ~15,000+ → **ห้ามทำแบบนี้** |
| หลังรวมต่อ chunk/material | – | ≤ ~1,200 renderer ทั้งแมพ ที่วาดจริงต่อเฟรมหลัง culling ≈ 4-6 chunk |
| draw call ฉากที่เห็น | ~500 | ~100-150 (+ เงา cascade 2 ~100-200) |
| vertex | ต่ำ | ต้นไม้ ~70 tri x 2,500 ≈ 175k tri / 525k vertex ≈ 17 MB; รวมทั้งแมพ ~25 MB; วาดจริง ≲ 60k tri/เฟรม |
| `farClipPlane` | 200 | **120** |
| shadow distance / cascade / map | 55 / 2 / 4096 | คงเดิม |

**วิธีคุม performance**:
1. **chunk 38 ม.** (380/10): ทุก prop `StaticBatcher.Add` ตาม `floor((x + 190) / 38)`; key = (cx, cz, material, castShadow); `Build` ใช้ `Mesh.CombineMeshes(instances, true, true)` + `indexFormat = UInt32` → bounds ต่อ chunk ให้ Unity cull เอง
2. source mesh สร้างครั้งเดียวต่อชนิด (`Cyl`, `Cone`, `Ball`, `Cube` cache ใน `Prims.meshes` อยู่แล้ว) + `Matrix4x4` ต่อ instance (ประมาณการ < 1 วิที่ `Awake` ก่อนเมนูแสดง)
3. material `Prims.Lit(color)` cache ตาม key อยู่แล้ว; ตรวจ SRP Batcher เปิดอยู่ (ค่าเริ่มต้น URP) สำหรับ actor (อัศวิน ~45 renderer, สไลม์ ~7 + แถบ HP 3 x 48)
4. เงา: ของเล็ก (ดอกไม้ กรวด แพตช์ น้ำ) `shadows = false`
5. actor: cap `MaxSlimes = 48`; ลบสไลม์ไกลทุกคน > 70 ม.; ไม่ต้อง LOD
6. CPU: `SimulateWorld` แยกศัตรู O(n²) ที่ n ≤ ~50 ≈ 1,250 คู่; `Enemy.Find` O(n) ใน `ReadSnapshot` O(n²) ที่ n ≤ 50 รับได้
7. SelfTest วัด `Time.unscaledDeltaTime` เฉลี่ย 3 วิ ที่กลางเมืองและกลางป่า (log เท่านั้น)

### 9.3 การเกิดของสไลม์ และ Minotaur

**สไลม์ — "แพ็คต่อผู้เล่น" แทนเวฟเดียวทั้งสนาม** (`wave`/`slimesAlive` ยังอยู่ในโค้ด/snapshot แต่ **ไม่ประกาศ "Wave n" ใน HUD/Banner**, Tab แสดง Kills เท่านั้น)

| กติกา | ค่า |
| --- | --- |
| ทริกเกอร์ | per-player (`packAt[pid]`): สไลม์ที่ยังมีชีวิตในรัศมี 45 ม. รอบผู้เล่น <= 1 และผู้เล่นยังไม่ตาย และ **ไม่ได้ยืนในเขตเมือง (`MapLayout.InTown(x,z)` สี่เหลี่ยมของเมือง)** และโซนที่ยืน `SpawnTier > 0` → ตั้งเวลา +2.5 วิ แล้ว `SpawnPack(anchor)` |
| จำนวน | `min(4 + packs, 10)` โดย `packs` = จำนวนแพ็คของผู้เล่นคนนั้น; co-op +2 ต่อผู้เล่นอื่นที่อยู่ใน 45 ม.; ต่อแพ็คสูงสุด 16; ทั้งโลกไม่เกิน `MaxSlimes = 48` |
| ตำแหน่ง | วงแหวน **15-26 ม.** รอบผู้เล่น (แพ็คแรก 10-16 ม.); สุ่มสูงสุด 20 ครั้งเลี่ยง: ใกล้ผู้เล่น < 4.5 ม., โซน Lake/Mountain/Town (`SpawnTier == 0`), ในกล่อง obstacle, ใกล้ Minotaur < 6 ม.; ไม่ได้ → ข้ามรอบ. (ขอบเขตจอ ~26 x 16 ม. จึงโผล่ขอบจอ) |
| ชนิด | `blue = clamp((p-2)*0.15, 0, 0.5)`, `red = clamp((p-5)*0.12, 0, 0.35)` คูณ `SpawnTier(โซนที่ผู้เล่นยืน)` (Meadow/Farmland 1.0, Autumn 1.5, DarkPine 2.0, Lair 2.0) |
| ระยะไล่ | `Slime.ChaseRange` 17 → **30 ม.**; **ไม่ไล่ผู้เล่นที่ยืนในเมือง** (เมืองปลอดภัย) |
| ลบสไลม์ไกล | ทุก 1 วิ (authority) สไลม์ที่ยังมีชีวิต ไม่สตัน อยู่ห่าง**ทุกผู้เล่น** > 70 ม. → `Destroy` เงียบ (ไม่ EXP/ดรอป/Soul); client ลบ puppet เองเพราะหายจาก snapshot |
| ป้ายประกาศ | **เลิกประกาศแพ็ค** (ไม่มี `WorldBanner("Wave ...")` และไม่เพิ่ม `Net.SendBanner`); banner อื่นของ host (Minotaur returns, wipe) คงเดิม; คำถาม Q6 |
| `OnPartyWipe` | ลบสไลม์ทั้งหมด, รีเซ็ต `packAt`/`packs`, Minotaur กลับรัง |

**Minotaur — รังคงที่ + leash**
- เกิดที่ `MapLayout.Lair` (0,+150) ตอนเริ่ม/F5 และฟื้นที่รัง 20 วิหลังตาย (`MinotaurRespawn`) ไม่เกิดใกล้ผู้เล่น
- `AggroRange 12.5` เดิม (ตื่นเมื่อมีคนเข้าใกล้); design ว่า "ไม่ออกมาเดินนอกรัง" — tech ใช้ leash ที่ **40 ม. จากรัง** (รัศมีรัง 25 + เผื่อไล่ผู้เล่นที่หนี) แทนไม่ให้ออกเลย; เกินระยะ หรือไม่มีผู้เล่นที่ยังไม่ตายภายใน 45 ม. นาน 5 วิ → `ResetToLair(home)` (มีอยู่แล้ว เติมเลือด กลับรัง)
- ผู้เล่นตายไม่ทำให้ Minotaur หาย (ยกเว้น `OnPartyWipe`)

**จุดเกิด/ฟื้นผู้เล่น**: จุดเดียวในเมือง `TownSpawn (0,-150)`; ตายแล้ววิ่งกลับรัง **300 ม. ≈ 32 วิ** (วิ่ง) / 48 วิ (เดิน) — สั้นกว่าที่ design ตั้งใจ (45-60 วิ) ดู 13 ข้อ 12; ไม่มี checkpoint (Q2)

### 9.4 Net กับแมพใหญ่
ตำแหน่ง ±190 ม. อยู่ใน i16 cm (5.1); ฉาก/collision deterministic; ศัตรูเกิดจาก host ส่งผ่าน snapshot (ขนาด 5.4); `ClientHit` 14/9 ม. พอสำหรับ dash; ผู้เล่นคนละฝั่งแมพ: snapshot ส่งศัตรูทั้งหมดให้ทุกคน (cap 48)

