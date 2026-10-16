# tech: นำแมพหิมะเข้าเกมเป็นหนึ่งโลกใน biome สุ่ม (snow-map-models)
อ้างอิง: requirement.md, design.md, status.md (โมเดลอนุมัติแล้ว 2026-10-16), layout.json, tri_report.txt, world-map-preview/decisions.md,
CLAUDE.md ของเกม, reference/{map-and-spawning, loop, online-coop, files, dev-guide}.md, Art_kit/approved.md
ขอบเขต: เฉพาะโค้ด/ข้อมูลฝั่ง Unity (ไม่แก้โมเดล ไม่แตะ NPC/เควสต์/biome อื่น) | ผู้เขียน: architect | วันที่ 2026-10-17

## 1. ภาพรวมแนวทาง
- สร้างแมพหิมะเป็น "ฉากชุดที่สอง" ข้างฉากทุ่งเดิม (ฉากเดิมไม่แตะ): สร้างครั้งแรกที่เข้าโลก (ตอนจอดำ) ด้วย `StaticBatcher` แล้วเปิด/ปิดด้วย `SetActive`
  ฐาน (ลานกลาง) ยังเป็นฉากทุ่งเดิมเสมอ จึงไม่กระทบ PREPARE / selftest / mptest
- ข้อมูลทุกอย่างของแมพมาจากไฟล์ที่ bake ใน Editor ครั้งเดียวแล้วฝังใน build (`Resources/Snow/`): ตำแหน่งชิ้น (จาก layout.json), ตารางความสูงพื้น,
  ตารางพื้นที่ห้ามเดิน, จุดเกิด/รัง — ไม่มีการสุ่มตอนรัน จึงตรงกันทุกเครื่อง (ถ้าต้องสุ่มจริงใช้ `System.Random(11)` เท่านั้น ห้าม `UnityEngine.Random`)
- **ปัญหาใหญ่ที่สุด**: เกมเดิมเดินบนพื้นเรียบ y=0 แต่แมพหิมะสูง 0–95 ม. (หน้าผา ที่ราบสูง ปราสาทบนยอดเขา)
  → เพิ่ม `Ground.Y(x,z)` (ตารางความสูงที่ bake) ให้ตัวละคร/ศัตรู/ของ/กล้องเดินตามความสูง ส่วนการชน/การต่อสู้ยังเป็น 2D แนว XZ เหมือนเดิม
  (โค้ดสู้เกือบทั้งหมดตัด `y=0` อยู่แล้ว) พื้นที่ห้ามเดิน (หน้าผา น้ำ ภูเขาฉากหลัง ปราสาท) แปลงเป็นกล่องใส่ `Obstacles` เดิม ไม่เขียนระบบชนใหม่
- biome เลือกที่ผู้ตัดสิน (host/เล่นคนเดียว) แล้วส่งเป็นค่าใน `Enter` + เก็บในโลกเก่า/เซฟ — client ไม่ต้องคำนวณเองจึงตรงกันแน่นอน (`Biome.FromSeed` มีไว้ให้สุ่ม+เทสต์ซ้ำได้)
- เพิ่ม `Net.Protocol` = 6 (Enter / Worlds / Team เพิ่ม 1 ไบต์ biome)

## 2. ไฟล์ที่สร้างใหม่ / แก้ (สคริปต์อยู่ใต้ `Assets/MedievalKnight/Scripts/` ยกเว้นที่ระบุ)
### สร้างใหม่
| ไฟล์ | เหตุผล |
| --- | --- |
| `Biome.cs` | `enum Biome { Meadow = 0, Snow = 1 }`, `Biome.FromSeed`, `Biome.Roll`, ชื่อแสดง (ข้อ 3) |
| `Ground.cs` | `Ground.Y(x,z)` (bilinear จากตารางความสูงของ biome ปัจจุบัน; Meadow = 0), `Ground.Snap(Vector3)` |
| `SnowLayout.cs` | ข้อมูลแมพหิมะ: โหลด `Resources/Snow/*`, แปลงพิกัด Blender→Unity, โซน/ tier, จุดเกิด, รัง, ตารางห้ามเดิน, ตารางคอลลิเดอร์ต่อ prototype |
| `SnowBuilder.cs` | สร้างฉากหิมะ (ภาพ + `Obstacles`) จาก `SnowLayout` + `Kit`; กังหันหมุน (`Spinner`) |
| `Assets/Editor/SnowImport.cs` | Editor: ตั้งค่า ModelImporter ของ `Assets/Models/Snow/*.fbx`, สร้าง material/prefab, ใส่ลง `Resources/Kit/SnowCatalog.prefab`, scene `Showcase_Snow` + ภาพถ่าย |
| `Assets/Editor/SnowBake.cs` | Editor: อ่าน `Docs/snow-map-models/layout.json` + prefab → เขียน `Resources/Snow/snow_layout.json`, `snow_height.bytes`, `snow_block.bytes`, `snow_meta.json` + รายงานตรวจ |
| `Assets/MedievalKnight/Resources/Snow/…` | ผลลัพธ์ของ bake (commit เข้า git) |
| `Docs/snow-map-models/ingame-report.md` (ตอนจบงาน) | tri/เวลา build/ภาพในเกม สำหรับขออนุมัติ |

### แก้ของเดิม
| ไฟล์ | แก้อะไร / ทำไม |
| --- | --- |
| `World.cs` | `MapLayout`: ฟิลด์ `static readonly` (`TownSpawn` `BasePoint` `Lair` `DummyPos` `Gate` ฯลฯ) → property ชื่อเดิมคืนค่าตาม biome ปัจจุบัน (โค้ดอ่านชื่อเดิมได้ไม่ต้องแก้); `ZoneAt` / `SpawnTier` / `Walkable` / `InTown` แยกกิ่ง Snow; `Obstacles` เพิ่ม "ชุด" ต่อ biome (`Obstacles.Use(biome)`) เพราะ `Clear()` ตอนนี้ล้างทั้งหมด; `WorldBuilder.Build` เรียกใต้ชุด Meadow เหมือนเดิม |
| `Game.cs` | `SetBiome(Biome)` (สลับ root ฉาก + ชุด Obstacles + กล้อง/ lighting ถ้าตัดสินใจ), `MinotaurHome` / `KnightSpawn` / `DummyPos` ที่เป็น `static readonly` → property; `SpawnActors` ใช้ตำแหน่งตาม biome; ใส่ `y = Ground.Y` ที่ `PlaceCamera`/`camFocus` (บรรทัด ~1508 `target.y = 0f`), `SpawnPack` (y ของศัตรู), `SpawnPointFor`; `EndSession` กลับ Meadow |
| `Knight.cs` | บรรทัด ~605/630: `p.y = 0` / `p.y = jumpY` → บวก `Ground.Y(p.x,p.z)`; ปุ่ม dash/ leap ใช้ y พื้นปลายทาง; puppet ใช้ y จากเครือข่ายตามเดิม |
| `Enemy.cs`, `Slime.cs`, `Minotaur.cs`, `Dummy.cs` | y ตามพื้นตอนเคลื่อน/กระโดด (ฐานโค้งกระโดด = พื้น) ; `Minotaur` leash/ `ResetToLair` ใช้ `MapLayout.Lair` ที่เป็น property แล้ว |
| `Weapon.cs` (`Pickup`), `LootPile.cs`, `Portal.cs`, `Fx.cs` | วางที่ `Ground.Y` ตอนสร้าง; decal พื้น (`Fx.MakeDecal`) ยกตามความชัน (ดูความเสี่ยง) |
| `RunLoop.cs` | `StartWorld`: `g.SetBiome(runBiome)` ตอนจอดำก่อน `SpawnActors`; 3 จุดที่ `ResetForRun(BasePoint)` (บรรทัด ~84, 467, 855) เรียก `g.SetBiome(Biome.Meadow)` ก่อน; `EnterWorld` (host/SP) ตัดสิน biome; `Walkable` ของกอง (บรรทัด ~806) ผ่าน property ตาม biome ของโลกนั้น; `DropPortal` ใช้ y พื้น; banner ชื่อ biome |
| `Loadout.cs` | `runBiome` (+ `BeginRun` รับ biome), `Fall`/`Die` สร้าง `OldWorld` พร้อม `biome` ของรอบ; `Sanitize` ตัด biome ที่ไม่รู้จัก → Meadow, ตรวจตำแหน่งกองตาม biome |
| `OldWorld.cs` | field `public Biome biome;` (ค่าเริ่ม Meadow) |
| `LoopSave.cs` | DTO: `WorldDto.bm` (int, ไม่มี = 0), ระดับ root `rb` (biome ของรอบที่เปิดอยู่) — Version ยัง 1 (เพิ่มฟิลด์ทางเลือก) |
| `TeamRun.cs`, `TeamNet.cs` | `worldBiome`; `Enter(seed,left,biome,pids)`, `Worlds` ต่อโลกเพิ่ม `U8 biome`, `Team` เพิ่ม `U8 biome`; `HostWipe/HostAbandon` ใช้ biome ของรอบ |
| `Net.cs` | `Protocol` 5 → 6; clamp y ตอนรับตำแหน่ง (บรรทัด ~714 `Mathf.Clamp(p.y,0,6)`) → `0..110`; (ตำแหน่ง i16 cm รับ 95 ม. ได้) |
| `MapUi.cs` | texture พื้นหลังต่อ biome (แคช 2 ใบ) สร้างจาก `ZoneAt` + สีชุดหิมะ; มาร์กเกอร์ town/lair ใช้ property |
| `Hud.cs` | ลูกศรชี้รังใช้ `MapLayout.Lair` อยู่แล้ว (ไม่ต้องแก้ถ้าเป็น property) |
| `LoopUi.cs` | แถว OLD WORLDS แสดงป้าย biome (`SNOW`) ; banner เข้าโลก |
| `Kit` (ใน `KitCatalog.cs`) | `Load()` โหลด `Kit/SnowCatalog` ด้วย (ชื่อ prefab ขึ้นต้น `snow_` ไม่ชนของเดิม) |
| `Args.cs` | `-biome meadow|snow` (บังคับ biome ของรอบใหม่; **เกมที่ scripted/selftest/mptest ไม่ใส่ = Meadow เสมอ ห้ามสุ่ม**) |
| `SelfTest.cs`, `MpLoopTest.cs`, `mptest-loop.ps1` | เคสใหม่ (ข้อ 8) |
| `Assets/Editor/AssetImport.cs` | แค่ใช้ `Hex/EnsureDir/GetKitMaterial` ซ้ำ (ทำ internal/แชร์) ไม่เปลี่ยนพฤติกรรม |
| Docs (ตอนจบ): `reference/{files,map-and-spawning,loop,online-coop}.md`, `CLAUDE.md` ของเกม, `Art_kit/approved.md` | อัปเดตตามจริง (protocol 6, biome, Ground) |

## 3. คลาส หน้าที่ ความสัมพันธ์
```
Biome (enum)  ──ใช้โดย──> OldWorld.biome, Loadout.runBiome, TeamRun.worldBiome, Enter/Worlds/Team, Game.SetBiome
Biome.FromSeed(int) : Biome            // ((uint)seed >> 16) % Count  — บริสุทธิ์ ใช้ตอนสุ่มโลกใหม่/เทสต์
Biome.Roll(System.Random) : Biome      // host เท่านั้น ตอนสร้างโลกใหม่ (ข้อ 4)

MapLayout (static)  ── ถาม ──> Current layout  (Meadow: ค่าเดิมทั้งหมด | Snow: SnowLayout)
   TownSpawn / BasePoint / Lair / DummyPos / ZoneAt / SpawnTier / Walkable / InTown

Ground (static)     Y(x,z) | Snap(v)   // Meadow=0; Snow=ตารางความสูง bilinear
SnowLayout (static) Load(), Spawn, Lair, LairRadius, SafeCenter/SafeRadius, ZoneAt, Blocked(x,z), Instances[], ToUnity(...)
SnowBuilder         Build(Transform parent) : เรียกจาก Game.SetBiome(Snow) ครั้งแรก
Game.SetBiome(b)    สลับ SetActive(rootMeadow/rootSnow) + Obstacles.Use(b) + MapLayout.Current + (ปรับ RenderSettings ถ้าตกลง)
Obstacles           ชุด[Biome] -> items/cells  (Clear() ล้างเฉพาะชุดที่เรียก)
```
ความสัมพันธ์กับ loop เดิม: ฐาน = ฉาก Meadow เสมอ (SetBiome(Meadow) ตอนกลับฐาน) → ตอนเข้าโลก `RunLoop.StartWorld` เรียก `SetBiome(runBiome)` ก่อน
`ResetForRun(SpawnPointFor(pid))` → `SpawnActors` (เฉพาะ Authority) → Minotaur อยู่ `MapLayout.Lair` (property) → portal เกิดที่จุดตายบอส (y จากพื้น)
ไม่มีโค้ด gameplay ไหนถามชื่อ biome ตรงๆ — ทุกอย่างผ่าน `MapLayout.*` / `Ground.*` / `Obstacles` เพื่อให้เพิ่ม biome ต่อไปได้

### 3.1 แปลงพิกัด Blender → Unity (ต้องยืนยันด้วยเทสต์ ข้อ 5 ขั้น 0)
- ตำแหน่ง: Blender (E, N, Z) → Unity (x, y, z) = (E − 190, Z, N − 190) (ตรงกับ design ข้อ 2) ; ลานกลาง (190,190) = (0,0) = จุดฐานเดิม
- การหมุน/สเกล: แปลงด้วย basis change M = (x,y,z)→(x,z,y) แบบ conjugation (R_u = M · R_b · M) ไม่ใช้ Euler ตรงๆ — วิธีง่ายสุด: yaw Unity ≈ −rot_z(Blender) ถ้า FBX import ไม่กลับแกน
- **ไฟล์ import ต้องผ่านการตรวจอัตโนมัติ**: bounds ของ chunk `r0c3` ต้องมีศูนย์ที่ x=142.5, z=−142.5 (= E332.5,N47.5) ถ้าไม่ตรง (กลับแกน X/สเกล 100 เท่า) ให้หยุดและแก้ import ก่อน ห้ามแก้ที่ตำแหน่งชิ้นทีละชิ้น
- ขนาด/หน่วย: `useFileScale` ตั้งให้ 1 หน่วย = 1 ม. ตรวจจาก chunk กว้าง 95 ม.

## 4. การเลือก biome ให้ host/client ตรงกัน
1. **โลกใหม่**: ผู้ตัดสิน (SP หรือ host) ตอน `EnterWorld`: ถ้ามี `Args.biome` → ใช้เลย; ถ้าเป็นการรัน scripted/test → Meadow; นอกนั้น `Biome.Roll` ด้วย `System.Random(seed เวลา+จำนวนโลก)` แบบเดียวกับ `Loadout.NewSeed` (ค่าเริ่ม 50/50 ดูข้อ 10 ข้อ 2) เก็บใน `Loadout.runBiome` + เซฟทันที (`Save()` ก่อน fade อยู่แล้ว)
2. **โลกเก่า**: ใช้ `OldWorld.biome` ที่เก็บตอนสร้างโลก (ตาย/ABANDONED/wipe) — ไม่คำนวณใหม่จาก seed เพราะ seed ของโลกเก่าที่มีอยู่ในเซฟเดิมสุ่มบิตใดก็ได้ ถ้าคำนวณจะทำให้โลกเก่าเปลี่ยนแมพ (กองของไปอยู่ในกำแพง)
3. **ออนไลน์**: host ส่ง `Enter(seed,left,biome,pids)`; client เรียก `SetBiome` จากค่านี้เท่านั้น (ไม่เลือกเอง) ; รายการ `Worlds` (client ดูโลกเก่าของ host) และ `Team` พก biome ด้วยเพื่อป้าย/พิกัดกอง
4. โลกเก่าในเซฟเก่า (ไม่มี `bm`) = Meadow ; โปรโตคอลไม่ตรง (5 vs 6) ถูกปฏิเสธที่ handshake เหมือนทุกครั้งที่เคยเพิ่ม protocol
5. ผู้เล่นคนเดียว / ออนไลน์ใช้ทางเดียวกัน (`RunLoop.EnterWorld`) ; `-arena`/`-selftest` ไม่มี `-loop` = Meadow ล้วน ไม่มีทางเข้า Snow (กันชุดเทสต์ v1 เพี้ยน)

## 5. เลย์เอาต์ตายตัวจาก layout.json และการ bake
ข้อมูลใน layout.json: terrain_chunks 16 ชิ้น (pivot_world), prototypes ~90 (ชื่อ fbx, tris), instances ~1,140 (name, fbx, pos[E,N,Z], rot_deg, scale), non_instanced_world_meshes (พื้น/น้ำ/ทาง ที่ export เป็น fbx world-coordinate ชื่อ `snow_terrain_drifts`, `snow_water_*`, `snow_path_*`, `snow_village_*`, `snow_scene_mist/cloud`)
**ไม่มีใน layout.json**: ความสูงพื้น, พื้นที่ห้ามเดิน, โซน/ tier, จุดเกิด/รัง, รัศมีคอลลิเดอร์ → ต้องสร้างเอง (ข้อ 5.1–5.4)

### 5.1 `SnowBake` (Editor, รันได้ซ้ำ, ผลเหมือนเดิมทุกครั้ง)
อ่าน layout.json + FBX ที่นำเข้าแล้ว แล้วเขียน
- `snow_layout.json` : instances แปลงเป็นพิกัด Unity แล้ว (index prototype, x, y, z, yaw/แมทริกซ์, scale) — ไฟล์ตายตัว ทุกเครื่องได้ชุดเดียวกัน
- `snow_height.bytes` : ตาราง 1 ม. ขนาด 381×381 (ushort, หน่วย cm) = ยิง ray ลงบน mesh พื้น (terrain chunks + path_surface + village_plazapaving + ดาดฟ้าสะพาน + ท่าไม้) ไม่ใช้ Physics ตอนรัน
- `snow_block.bytes` : ตาราง 1 ม. (บิต) ห้ามเดิน = ความชันเกิน `MaxSlope` (ค่าเริ่ม 40°) ∪ น้ำลึก (river/stream/pool/lake outlet/ทะเลสาบน้ำแข็ง ยกเว้นใต้ดาดฟ้าสะพาน) ∪ ภูเขาฉากหลัง/ขอบแมพ ∪ รอยเท้าปราสาท (rasterize mesh `castle_walls/keep/base` ช่วงสูง 0.5–3 ม. เหนือพื้น ช่องประตูเปิดเอง) ; ขยายขอบ 1 ช่องให้กำแพงไม่บางเกินไป
- `snow_meta.json` : `spawn` (ลานหมู่บ้าน ใกล้ `fountain` instance แล้วเลื่อนไปขอบทาง), `safeCenter/safeRadius` (หมู่บ้าน ~30 ม. = Town ไม่มีสไลม์), `lair` + `lairRadius` (ดูข้อ 5.4), `dummy`, ตารางโซน A/B/C/D (สี่เหลี่ยมใน design ข้อ 3), ตำแหน่งป้ายทาง, ตำแหน่งดาดฟ้าสะพาน (กล่องหมุน)
- **รายงานตรวจ (ต้องไม่ error)**: จำนวน instance = layout.json; spawn เดินถึงรังได้ (BFS บนตาราง 1 ม. รัศมีตัว 0.4); ทุก waypoint ของทางหลัก (design ข้อ 5) เดินได้; สะพาน S1 ต่อสองฝั่ง; ลานกลาง |x|<28, z −42..32 ไม่มีคอลลิเดอร์ (ข้อ 5.3); ขนาดไฟล์; จำนวนกล่อง

### 5.2 คอลลิเดอร์ของชิ้นที่วาง (เป็นตารางในโค้ด `SnowLayout`, ไม่ใช้ MeshCollider)
| กลุ่ม prototype | รูป | ชั้น (`Obstacles` mask) |
| --- | --- | --- |
| ต้นไม้ (pine*, pine_snow*, tree_*, dead_snow) | วงกลมรัศมี ~0.5 ม. (ลำต้น) | Players เท่านั้น (เหมือนต้นไม้เดิม สไลม์ไม่ติด) |
| หินเล็ก rock0–1, พืชพื้น, เห็ด, ตอ, ท่อนไม้, หินแผ่น | ไม่มี | – |
| หินกลาง–ใหญ่ rock2–5 | วงกลม ≈ 0.35 × max(W,D) | Both |
| เสาผา pillar0–3 | วงกลม ≈ 2.4 ม. | Both |
| บ้าน, กังหัน (body), แผงตลาด, ท่าไม้, อิกลู, ถ้ำ, ปราสาท (จากตารางห้ามเดิน) | กล่องหมุนตาม footprint (W×D จาก tri_report ลดมุม/ชายคา ~0.8) | Both |
| น้ำพุ, ผลึกใหญ่, กองไฟ, ตุ๊กตาหิมะ, เลื่อน | วงกลม | Both |
| รั้ว, โคม, เสาโคม, ป้าย, ถัง, ลัง, chain post | กล่อง/วงกลมเล็ก | Players |
| ดาดฟ้าสะพาน | **ปลดบล็อก**แถบดาดฟ้าที่ทับน้ำ ; ราวสะพานเป็นกล่องบาง 2 ข้าง | Players |
ต้นไม้จำนวน ~500 + ของอื่น ~600 = วงกลม/กล่องราว 1,100 รายการ + กล่องจากตารางห้ามเดิน (เป้า ≤ 8,000 รวม; เกินให้เพิ่มเป็นช่อง 2 ม.)

### 5.3 ลานกลางและฐาน
ฐานอยู่ฉากทุ่ง ไม่ใช่ฉากหิมะ จึงไม่มีผลต่อ PREPARE/ selftest. แต่เพื่อรักษากฎ "ห้ามวางของกีดขวางในลานกลาง" ตามที่ design ข้อ 7 ขอ (จุด (190,190) ต้องเว้นไว้) ให้ bake **ตัดคอลลิเดอร์ที่ตกในกล่อง |x|<28, z −42..32 ออก** (ภาพยังอยู่ ไม่กระทบรูปทรงที่อนุมัติ) และพิมพ์รายการ — ถ้าตัดแล้วเห็นเป็นโล่งผิดธรรมชาติให้เสนอ artist (ข้อ 10 ข้อ 4)

### 5.4 จุดเกิดผู้เล่น/ศัตรู/บอส/portal บนแมพหิมะ
| รายการ | ค่า | ผ่าน loop เดิมอย่างไร |
| --- | --- | --- |
| จุดเกิดผู้เล่น (`TownSpawn`) | ลานหมู่บ้าน SW ราว Blender (25,25) = Unity (−165, −165) ; `SpawnPointFor(pid)` เลื่อน ±1.8 ม. ตามเดิม + y จากพื้น | `ResetForRun(SpawnPointFor(pid))` ไม่เปลี่ยน |
| เขตปลอดภัย | วงกลมหมู่บ้าน r≈30 ม. = `Zone.Town` (`SpawnTier`=0, `InTown`=true) | `UpdatePacks`, `Slime` ใช้ `InTown` เดิม |
| หุ่นฟาง | วางที่ลานหมู่บ้าน (ใกล้ spawn ~10 ม.) เพื่อไม่ให้ `dummy == null` ทำโค้ด/เทสต์เดิมพัง | `SpawnActors` |
| สไลม์ | `SpawnTier` ตามโซน: A ทุ่ง 1.0, B ป่าสน 1.5, C ทางขึ้นเขา 2.0, D1/D2 2.0, D3 รัง 2.0 ; น้ำ/ผา = 0 ; ต้อง `Walkable` และไม่ติด `Obstacles` (โค้ดเดิม) | `SpawnPack` ไม่แก้ตรรกะ แก้แค่ y |
| บอส Minotaur (`Lair`) | ลานโล่งหน้าประตูปราสาท (ใกล้ waypoint 15, Blender ~(340,335)) ; bake เลือก "วงกลมโล่งที่ใหญ่สุด" ภายใน 70 ม. จากประตู (ต้อง r ≥ 22 ม.) เก็บใน meta | `MinotaurHome`/`Lair`, leash 40 ม., `ResetToLair` ใช้ property |
| Portal | จุดตายบอส (clamp ±186 เดิม) + y พื้น | `RunLoop.DropPortal` |
| กองโลกเก่า | จุดตาย (x,z) ตาม biome ของโลก ; ตรวจ `Walkable` ของ biome นั้น | `BuildPileViews`, `PilePoint` |
ระยะเดินจาก spawn ถึงรัง ≈ 550–650 ม. (เดิม ~300 ม.) — ดูข้อ 10 ข้อ 6

## 6. Collider ภูมิประเทศ / สิ่งกีดขวาง และ y
- การชนผู้เล่น/ศัตรูยังเป็น 2D ผ่าน `Obstacles.Push` เหมือนเดิม; ตัวละครไม่มี Rigidbody/Collider ใหม่; ไม่ใช้ `Physics` (จึงตรงกันทุกเครื่อง)
- `Ground.Y` เป็นข้อมูลภาพเท่านั้น (ตัวละครยืนบนพื้น) ไม่เข้า logic ดาเมจ/ระยะ (ระยะคิดเฉพาะ XZ อยู่แล้ว) ; ออนไลน์: y ของทุกตัวส่งใน `State`/`Snapshot` อยู่แล้ว (i16 cm) แค่ขยาย clamp
- กล้อง: `camFocus.y` = `Ground.Y` ของเป้า (lerp ~0.15 วิ) ; ตรวจไม่ให้กล้องจมเนิน (ยก `camY ≥ Ground.Y(camPos) + 2`)
- กระโดด/ leap/ dash: y = พื้น ณ ตำแหน่งปัจจุบัน + โค้งเดิม (ไม่ล็อกพื้นปลายทาง)
- ตกหน้าผา: ไม่มี (ตารางห้ามเดิน + กล่องกันที่ลาดเกิน 40°) ; ทางลาดที่ artist เจาะผ่านหน้าผาต้องไม่ถูกบล็อก (ตรวจใน BFS ของ bake)

## 7. Static batching / instancing และงบประสิทธิภาพ
- ชิ้นซ้ำ (ต้นไม้ หิน พืช ของประกอบ บ้าน ปราสาท ฯลฯ ~1,140 ชิ้น) → `StaticBatcher.Add` (chunk 38 ม. × material) เหมือนทุ่ง ; materials ผ่าน `Kit.Canonical` (รวมตามสี ~25 สี)
- พื้น/น้ำ/ทาง/หิมะทับ/ภูเขา/หมอก (~60 mesh ที่ artist export แล้ว) → MeshRenderer ตรง (ไม่ผ่าน batcher) เพื่อคง cull ต่อ chunk 95 ม. ; พื้นไม่ cast shadow, ต้นไม้/อาคารเป็น cast
- กังหัน: ใบพัดเป็น GameObject แยกที่ hub (offset (0,−3.3,8.3) จาก body) + `Spinner` (ภาพล้วน ไม่ซิงก์) ; ธง/ควัน/หมอกเป็น mesh ทึบ ไม่มี alpha (ตามที่พีทอนุมัติ)
- FBX ต้องตั้ง **Read/Write = true** (ให้ CombineMeshes ใน player ได้), ไม่สร้าง collider, ไม่ import material/animation, normals จาก FBX (flat)
- emission เปิดที่ material `window_glow`/`glow`/`ember` (#F5C25A) ผ่าน Editor ตามที่ approved.md บันทึกว่า FBX ไม่พก emission
- **งบ** (วัดและบันทึกในรายงาน): tri ในฉากหิมะ ≤ 220,000 (artist ส่ง 165,729) ; mesh/renderer ที่สร้าง ≤ 1,500 ; เวลาสร้างฉากครั้งแรก ≤ 0.8 วินาที (ทำตอนจอดำ) ; เฟรมไทม์เฉลี่ย ≤ 8 ms ที่ 1280×720 (ทุ่งเดิม ~4 ms) ; หน่วยความจำเพิ่ม ≤ 60 MB ; ฉากทุ่งตอนอยู่ในหิมะ `SetActive(false)` (ไม่วาด ไม่เสียเฟรม)
- Lighting: ใช้แสง/โพสต์เดิมของเกม (`Game.SetupLight` + Renderer3D) ก่อน ; ปรับสี ambient/พื้นหลังของ biome เป็น "ตัดสินใจ" (ข้อ 10 ข้อ 5) — ไม่ต้องทำใน build ผ่านขั้นแรก

## 8. เซฟ / โลกเก่า / ออนไลน์ ต้องไม่พัง
### ข้อมูลที่ต้องเก็บ
| ข้อมูล | ที่เก็บ | หมายเหตุ |
| --- | --- | --- |
| `OldWorld.biome` | เซฟ `ow[].bm` (int, ไม่มี=0) | ใช้ตอนเข้าโลกเก่า + ตำแหน่งกอง |
| `Loadout.runBiome` | เซฟ `rb` | รอบที่เปิดค้าง (ปิดเกม/แครช) → ABANDONED ได้ biome ถูก (ถ้าไม่เก็บ กองจะไปอยู่พิกัดหิมะในฉากทุ่ง) |
| ค่า biome ใน Enter/Worlds/Team | เครือข่ายเท่านั้น | ไม่เก็บถาวรฝั่ง client |
- ไม่เปลี่ยน `LoopSave.Version` (1) เพราะเป็นฟิลด์ทางเลือก ; อ่านเซฟเก่า → ทุกอย่าง Meadow ; เขียนไฟล์ใหม่ที่ exe เก่าเปิด = ฟิลด์ที่ไม่รู้จักถูกข้าม (โลกหิมะดูเป็นทุ่ง — ยอมรับได้ ไม่ใช่เส้นทางใช้งานปกติ)
- `Sanitize`: `bm` ∉ {0,1} → 0 ; ตำแหน่งกอง clamp ±186 เหมือนเดิม และถ้าไม่ `Walkable` ใน biome นั้น → ย้ายไป `TownSpawn` ของ biome นั้น
- โลกเก่าเดิมทั้งหมดไม่เปลี่ยนพฤติกรรม ; ความยากโลกเก่า (HP×1.5 ฯลฯ) ใช้กับทุก biome เท่าเดิม
### ออนไลน์
- สร้างฉากหิมะ/ชุด Obstacles ทำบนทุกเครื่องจากข้อมูลเดียวกัน (ไม่อยู่หลัง `Net.Authority`) ; spawn ศัตรู/ดรอป/EXP ยังอยู่หลัง `Net.Authority` ตามเดิม ; ไม่มีการหยุดเวลา
- client ที่เข้ากลางรอบอยู่ที่ฐาน (ทุ่ง) ไม่สร้างหิมะจนรอบถัดไป
- `Net.Protocol` = 6 ; ไฟล์ที่ต้องแก้ให้ตรงกัน: `Net.cs` (ค่า+clamp y), `TeamNet.cs` (Enter/Worlds/Team ทั้งเข้ารหัสและถอดรหัส), `TeamRun.cs` ; อัปเดต `online-coop.md` (ตาราง protocol)
- เกม `-arena` (ชุด mptest v1) ไม่ส่ง biome ใดๆ ใช้ Meadow ; ไม่กระทบ

## 9. แผนเทส
| ชุด | สิ่งที่เพิ่ม/ตรวจ |
| --- | --- |
| **selftest เต็ม** (~7 นาที) | ต้องผ่านเหมือนเดิมทุกขั้น (Meadow ล้วน) |
| **selftest `-snowonly` (ใหม่ ~2 นาที)** | โหลดข้อมูล: instance ตรง layout.json, chunk r0c3 อยู่ถูกที่ (แมปแกน) ; สร้างฉากหิมะ 2 ครั้งได้ hash คอลลิเดอร์เท่ากัน (`SNOW obstacles=N hash=…`) ; ไม่มีคอลลิเดอร์ในลานกลาง ; spawn/waypoint ทั้ง 16 จุดเดินได้ และ BFS spawn→รัง ผ่าน ; เดินข้ามสะพาน S1 y ต่อเนื่อง (ต่างเฟรมต่อเฟรม ≤ 0.5 ม.) ; ไต่ทางขึ้นเขา y ตาม `Ground.Y` ; `Knight.y == Ground.Y(+jump)` ; แพ็คสไลม์เกิดบนที่เดินได้ ; บอสเกิดที่รัง ตายแล้ว portal เกิด y ถูก ; ตายในหิมะ → OldWorld.biome=Snow → เซฟ/โหลด → เข้าโลกเก่าได้ กองอยู่ตำแหน่งเดิมและ `Walkable` ; เซฟเก่าไม่มี `bm` → Meadow ; `Biome.FromSeed` คงที่ ; ไม่มีการสุ่มเมื่อ scripted ; วัดเฟรมไทม์/mesh/tri |
| **mptest-loop (ขยาย)** | เพิ่มเคส biome: host `-biome snow` ; client A/B/C ต้องรายงาน `biome=Snow`, `obstacle hash` เท่า host, spawn ตรงกัน ; ฆ่าบอส/ wipe/ ชุบ ใน biome หิมะผ่าน ; wipe → โลกเก่า `bm=1` ถูกส่งผ่าน `Worlds` ; ผู้เข้ากลางรอบ (D) ยังอยู่ฐานทุ่ง ; เคสเดิมทั้งหมดต้องผ่านเหมือนเดิม |
| **mptest.ps1** (v1 arena) | รันซ้ำยืนยันไม่เปลี่ยน |
| `-menutest` | ผ่านเหมือนเดิม |
| มือ/ภาพ | Showcase_Snow ถ่ายเทียบ preview Blender (ขั้น 0, ขออนุมัติ) ; ในเกม 6 มุม (หมู่บ้าน, สะพาน S1, ป่าสน, ทางขึ้นเขา, ทะเลสาบน้ำแข็ง, ปราสาท) เทียบ `preview.png` (ขั้น 7, ขออนุมัติ) |
เทสต์เดิมที่ผูก `MapLayout.TownSpawn/Lair` (SelfTest ~20 จุด) ต้องยังได้ค่าทุ่งเมื่อ biome = Meadow

## 10. ลำดับขั้นการทำ (build ผ่านได้ทีละขั้น; ทุกขั้นจบด้วย build.bat + selftest `-looponly` อย่างน้อย)
0. **Import + Showcase** (`SnowImport`): ตั้ง ModelImporter, material/prefab/`SnowCatalog`, ตรวจแมปแกนด้วย chunk r0c3, scene `Showcase_Snow` ถ่ายภาพเทียบ Blender → **ส่งพีทอนุมัติ** (ตาม pipeline "นำ asset เข้าเกม" ข้อ 4). ยังไม่แตะ gameplay. build ผ่าน
1. **Bake** (`SnowBake`): สร้าง `Resources/Snow/*` + รายงานตรวจ (ข้อ 5.1) ; ถ้ารายงาน error (ไม่มีทางเดินถึงรัง ฯลฯ) หยุดและแจ้ง. build ผ่าน (ไม่มีโค้ดรันเกม)
2. **Refactor ไม่เปลี่ยนพฤติกรรม**: `Biome` enum, `MapLayout` เป็น property + `Current`, `Ground.Y` คืน 0, `Obstacles.Use(biome)` ; ใช้ Meadow อย่างเดียว. selftest เต็ม + `mptest.ps1` ต้องผ่านเหมือนเดิม (จุดที่ regress ง่ายสุด)
3. **เซฟ/เครือข่าย**: `OldWorld.biome`, `Loadout.runBiome`, `LoopSave bm/rb`, `Enter/Worlds/Team` + Protocol 6, `Args.biome` ; ค่ายังเป็น Meadow ตลอด (ยังไม่สร้างหิมะ). เทสต์: เซฟเก่าโหลดได้, roundtrip, selftest `-looponly`, `mptest-loop.ps1` ผ่านด้วย protocol 6
4. **SnowBuilder ภาพ + y-follow (ไม่มีชน)**: `SnowLayout.Load`, `SnowBuilder` (พื้น น้ำ ทาง + ชิ้นวาง), `Game.SetBiome`, กล้อง/Knight y ตาม `Ground.Y` ; เข้าได้ด้วย `-biome snow` เท่านั้น ; ถ่ายภาพเทียบ. selftest เต็ม (Meadow) ต้องไม่เปลี่ยน
5. **การชน**: คอลลิเดอร์ชิ้นวาง + กล่องจากตารางห้ามเดิน + สะพาน ; `Walkable/ZoneAt/SpawnTier` ของ Snow ; เทสต์ `-snowonly` ส่วนเดินได้/BFS/ลานกลาง/hash
6. **ผู้เล่น/ศัตรู/บอส/portal บนหิมะ**: `SpawnPointFor`, หุ่นฟาง, `SpawnPack` y, Slime/Enemy/Minotaur y, Pickup/LootPile/Portal/Fx y, กองโลกเก่า ; ตายแล้วเข้าโลกเก่าหิมะได้ ; เทสต์ `-snowonly` ที่เหลือ
7. **UI/แผนที่/ป้าย**: MapUi ต่อ biome, ป้าย OLD WORLDS (`SNOW`), banner, ป้ายทางจาก `sign_*` (ถ้ามีเวลา) ; เทสต์ mptest-loop เคส biome ; วัดงบ ; ถ่ายภาพในเกม → **ส่งพีทอนุมัติ** (ขั้น 5 ของ pipeline)
8. **เปิดสุ่มจริง** (`Biome.Roll`, ค่าเริ่ม 50/50) หลังพีทอนุมัติ ; selftest เต็ม + mptest + mptest-loop ครบ ; อัปเดต docs, zip `medieval-knight-win64.zip` ขึ้น Drive ตามกฎเกม, อัปเดต `status.md`
ถ้าขั้น 4–6 ตัดสินว่าใหญ่เกินรอบเดียว แยก PR/commit ต่อขั้น (commit ก่อนแก้ทุกครั้งตามกฎ)

## 11. ความเสี่ยง / สิ่งที่อาจพังของเดิม
| ความเสี่ยง | ผล | ทางกัน |
| --- | --- | --- |
| เกมเดิมสมมุติ y=0 ทั่วระบบ (Knight, Enemy, Slime, Minotaur, Fx decal, Pickup, กล้อง, Hud ที่ฉาย world→screen) | ของลอย/จมพื้นบนเนิน, เอฟเฟกต์พื้น (วงเตือนบอส, เส้นฟัน) ฝังใต้ดิน | ทำ y-follow ผ่านจุดเดียว (`Ground.Snap`) ; decal ยกตามความชัน ; ขั้น 4 ตรวจด้วยภาพ ; Meadow คืน 0 จึงไม่เปลี่ยนของเดิม |
| FBX แกนกลับ/สเกลผิด (แมพกลับด้านหรือใหญ่ 100×) | ทั้งแมพผิด | assert chunk r0c3 + ตรวจ castle (340,340)→(150,150) ก่อนไปขั้นถัดไป |
| ความสูงพื้นจาก ray ผิด (สะพาน ราว หลังคา ทับ) | เดินจมสะพานหรือลอยบนราว | ray เฉพาะ mesh พื้น/ดาดฟ้า ; ถ้า FBX สะพานไม่แยกดาดฟ้า ขอ artist แยก object `*_deck` (ข้อเสนอ) ; เทสต์เดินข้ามสะพาน |
| ตารางห้ามเดิน 1 ม. สร้างกล่องมาก / ทางแคบ 4 ม. กลายเป็นตัน | Push ช้า หรือเดินไม่ได้ / ทะลุกำแพงบางตอน dash | จำกัด ≤ 8,000 กล่อง ; ขยายขอบ 1 ช่อง ; BFS ยืนยันทางเดิน ; ดูรายงาน |
| รังบอสอยู่บนไหล่เขา/หน้าปราสาท พื้นที่ไม่พอ Minotaur ติด (Charge/ClampToArena) | บอสติด/ ล็อกลาน | bake หา "วงโล่งใหญ่สุด" r ≥ 22 ; ทดสอบ Charge ในเทสต์; ถ้าไม่มีที่ → ตัดสินใจข้อ 10 ข้อ 3 |
| ลานกลางห้ามมีของ (กฎเกม) vs เส้นทาง/ป่า B ผ่านจุด (190,190) | ขัด design ที่ล็อก | ตัดคอลลิเดอร์ใน bake (ข้อ 5.3) |
| `Obstacles.Clear()` ล้างทั้งโลก ; `MapLayout` เป็น static readonly ใช้ใน static init ของ `Game` | ชุดเทสต์/ บูตพังถ้าลำดับ init เปลี่ยน | ขั้น 2 แยก refactor อย่างเดียว ; ทำ property + ชุด Obstacles ; รัน selftest เต็ม |
| โลกเก่าถูกอ่านด้วย biome ผิด (เซฟเก่า/รอบค้าง/ client) | กองในกำแพงหรือหายไป | `bm/rb` ในเซฟ + Sanitize + เคสทดสอบแครชกลางรอบในหิมะ |
| Protocol 6: exe เก่าเข้าห้องใหม่ไม่ได้ | ผู้เล่นต้องอัปเดตพร้อมกัน | เหมือน phase ก่อนหน้า ; แจ้งใน release note |
| แครช/ช้าตอนสร้างฉากครั้งแรก ( ~1,140 ชิ้น + 60 mesh) | กระตุกตอนเข้าโลก | สร้างตอนจอดำ (หลัง fade) ; วัดใน log `SNOWWORLD built in … ms` ; ถ้า > 0.8 วินาที สร้างตอน PREPARE แบบเฟรมละส่วน |
| เทสต์สุ่ม biome ทำให้ชุดเทสต์เดิม flake | selftest/mptest ล้ม 50% | scripted = Meadow เสมอ ; `-biome` ต้องระบุเอง |
| CLAUDE.md (เกม) ระบุ "3 biome เพิ่ม พักไว้ รอพีทตัดสิน" | ขัดกรอบถ้านับเป็น phase 3 | requirement ของพีทลงวันที่ 2026-10-16 เป็นคำสั่งชัด ; ปูส้มเช็ค vision.md หัวข้อ "รอพีทตัดสิน" ก่อนเริ่มขั้น 3 |
| ภาพ/แสง: กล้อง 56° กับหน้าผาสูง 45 ม. บังตัวละคร | มองไม่เห็นตัวเองบางจุด | ยกกล้องตามพื้น + ตรวจ 6 มุมในเกม ; ถ้าแย่ค่อยเสนอ occlusion fade (นอกขอบเขต) |

## 12. ข้อที่ต้องตัดสิน (ค่าเริ่มต้นที่แนะนำ ถ้าไม่ตอบ)
1. **ความสูงพื้น**: ให้ตัวละครเดินตามความสูงจริง (`Ground.Y`, แนะนำ — งานใหญ่ขึ้นแต่แมพตรงที่อนุมัติ) หรือบีบแมพให้ราบ (ต้องกลับไปให้ artist ปรับ ไม่แนะนำ)
2. **สัดส่วน biome** ของโลกใหม่: Meadow : Snow = 50 : 50 (แนะนำ) หรืออื่น ; โลกเก่าคง biome เดิมเสมอ
3. **ที่ตั้งรัง/บอส**: ให้ bake เลือกลานโล่งใหญ่สุดใกล้ประตูปราสาท (แนะนำ) หรือให้ artist ระบุจุดเอง; ถ้าไม่มีลาน r ≥ 22 ม. ขอ artist เปิดลานหน้าประตู
4. **ลานกลาง**: ตัดคอลลิเดอร์ในกล่องลานกลางของแมพหิมะ (แนะนำ) หรือยกเว้นกฎนี้เฉพาะ biome หิมะ (เพราะฐานไม่อยู่ในฉากหิมะ)
5. **บรรยากาศ**: ใช้แสง/ท้องฟ้าเดียวกับทุ่ง (แนะนำสำหรับรอบนี้) หรือปรับสี ambient/พื้นหลังเป็นโทนหิมะ (ต้องดูสีใน style.md ก่อน)
6. **ระยะทาง**: เกิดที่หมู่บ้านต้นทางตามที่อนุมัติ (ใช้เวลาวิ่งถึงรังนานราว 2 เท่าของทุ่ง) หรือย้ายจุดเกิดไปกลางทางเพื่อให้รอบใกล้เคียงเดิม
7. ขอ artist เพิ่ม (ไม่บังคับ): แยก object ดาดฟ้าสะพาน `*_deck`; ถ้า bake ขาดข้อมูลทางลาด/ประตูปราสาท อาจขอ mesh proxy ง่ายๆ ของปราสาท

## 13. ประมาณขนาดงาน: **ใหญ่**
เหตุผล: เกมเดิมไม่มีความสูงพื้น (กระทบ Knight/Enemy/Fx/กล้อง/เครือข่าย), ต้องมี tool bake ใหม่ 2 ตัว, เพิ่ม biome เข้าเซฟ + Protocol 6, สร้างเทสต์ใหม่ทั้งชุดเดี่ยวและออนไลน์ ; ส่วนที่ "ตรงๆ" คือการวางชิ้น 1,140 ชิ้นด้วย `StaticBatcher` ตามแพทเทิร์นเดิม
