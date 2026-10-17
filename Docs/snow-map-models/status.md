# status
- 2026-10-16 เขียน requirement.md ร่างตามคำตอบพีท รอพีทยืนยัน
- 2026-10-16 designer ส่ง design.md (15.6KB เกิน 10KB ยอมรับเหมือน world-map-preview) | 🎬 ด่าน 2 ผ่าน: ครบขอบเขต ไม่มีโค้ด tri รวม ~182k <= 220k layout คงเดิม สีใหม่ N1-N3 เป็นแค่ข้อเสนอ ยังไม่ใส่ style.md | แก้ถ้อยคำคำถามข้อ 3 | รอพีทอนุมัติ design + ตอบ 4 ข้อ
- 2026-10-16 16:37 UTC พีทตอบ: 1 อนุมัติสีใหม่ N1-N3 ทั้งหมด 2 เพดาน 220k พอ 3 L3 ตกลง 4 ควัน/หมอก/ธงทึบสีซีดได้ -> เพิ่ม 3 สีลง style.md แล้ว ส่ง blender-artist
- 2026-10-16 blender-artist ส่งงาน 165,729 tri | 🎬 ด่าน 2 ผ่าน (ดู preview+before_after) | โพสต์ให้พีทอนุมัติ รอ
- 2026-10-16 17:59 UTC พีท(ID ตรง ห้องหลัก)ตอบ "อนุมัติ" ต่อโพสต์โมเดลแมพ -> อนุมัติโมเดล | ส่ง artist export .fbx ทั้งชุด+approved.md และส่ง architect เขียน tech.md การนำเข้า (ขนานกัน) | ยังไม่ส่ง developer
- 2026-10-17 artist export .fbx 123 ไฟล์ 4MB (tris รวม 73,730) + approved.md + layout.json (แกน Blender->Unity) | 🎬 ด่าน 2 ผ่าน: ตรวจจำนวนไฟล์ 123 ตรง ไม่แตะนอกโฟลเดอร์ | commit e6f7c7a | รอ architect tech.md
- 2026-10-17 architect ส่ง tech.md (45KB, ใหญ่ 9 ขั้น) | 🎬 ด่าน 2 ผ่าน: ครบหัวข้อ ไม่มีโค้ด ข้อ 5 (vision.md) หมดประเด็นแล้ว พีทลบไฟล์แล้ว | พบชนกับงาน knight-rig-slash: แตะ Knight.cs + Net.Protocol 5>6 ทั้งคู่ -> ต้องทำทีละงาน | รอพีทอนุมัติ tech + ตอบ 7 ข้อ + กำหนดลำดับ
- 2026-10-16 18:27 UTC พีท(ID ตรง ห้องหลัก)ตอบ "อนุมัติ" ต่อโพสต์ tech แมพหิมะ -> อนุมัติ tech.md ด้วยค่าแนะนำทุกข้อ (1-7) และลำดับข้อ 8 = ทำอัศวิน rig ก่อน (ค่าแนะนำ) | ยังไม่ส่ง developer: rig tech.md ยังไม่มีข้อความอนุมัติจริง
- 2026-10-16 รอ developer ทำงาน knight-rig-slash เสร็จก่อน แล้วค่อยส่ง developer แมพหิมะ

## สรุปบันทึก (2026-10-17, พีทสั่งพัก + !save)
- ทำถึง: โมเดลอนุมัติแล้ว (พีท 17:59 UTC) export .fbx 123 ไฟล์ commit e6f7c7a; tech.md อนุมัติแล้ว (พีท 18:27 UTC ค่าแนะนำข้อ 1-7)
- ตัดสินแล้ว: ลำดับ = อัศวิน rig ก่อน (เสร็จขั้น 1-8) แล้วแมพหิมะ (ห้ามขนาน เพราะแก้ Knight.cs/Net.Protocol)
- เหลือ: ส่ง developer ทำ tech.md 9 ขั้น (ต้อง build/selftest/mptest ผ่านทีละขั้น) แล้ว zip -> Drive
- รอ: พีทสั่งให้เริ่ม (ตอนนี้พักอยู่); ไม่ต้องขออนุมัติ tech ซ้ำ
- ทำต่อ: "อ่าน Docs\snow-map-models\status.md + tech.md แล้วส่ง developer ขั้น 0"

- 2026-10-17 พีทแจ้ง: ไฟล์ Docs\"Boss Design Doc จอมมารน้ำแข็ง.md" (ไฟล์ของพีท) เตรียมไว้เป็นบอสของแมพหิมะ -> architect/developer ของแมพหิมะต้องอ่านก่อนวางบอส (tech.md ข้อ 3 รัง/บอส) และต้องให้พีทอนุมัติ/ยืนยันขอบเขตบอสก่อนลงมือ
- 2026-10-17 พีท(ID ตรง) สั่ง "ทำ 2 ต่อเลย" -> ส่ง developer ขั้น 0-8 (บอสเดิม Minotaur; จอมมารน้ำแข็งแยกงาน) | อัศวิน rig ขั้น 1-8 เสร็จแล้ว ไม่ชน

## developer (2026-10-17) ขั้น 0 เสร็จ
- SnowImport.cs: ตั้ง ModelImporter 123 ไฟล์ (Read/Write, ไม่มี animation/camera/light), material 28 สี (Assets/Materials/Snow), prefab 123 (Assets/Prefabs/Snow, ตัวห่อ Pivot หมุน Y180), SnowCatalog.prefab, scene Showcase_Snow, ภาพ Docs/snow-map-models/ingame/snow_*.png
- ตรวจแกน: chunk ทั้ง 16 ตรงตำแหน่ง (r0c3 = 332.5,47.5), castle (340,340) ตรง
- พบ: บางหน้าหลังคา/ผ้าใบ (roof_red/roof_purple/red_deep, plaster ของ stall/sails/banner) winding กลับด้าน -> สร้าง mesh คู่ (521 tri) เป็น asset แยกใน Assets/Prefabs/Snow/Meshes (ไม่แก้ .fbx)
- พบ: chunk r2c2 มีแผ่นรองขนาดทั้งแมพ 380x380 (2 tri); Tree_Round 12 ต้น (non_instanced) ไม่มีตำแหน่งใน layout.json -> ยังไม่วาง (ขอ artist ส่งตำแหน่ง)
- พบ: ทะเลสาบน้ำแข็ง (SM7_IceLake) ถูกพื้นทับบางส่วน ต้องตรวจความสูงในขั้น 1
- build ผ่าน, selftest -looponly 211 pass / 0 FAIL

## developer ขั้น 1 เสร็จ (Bake)
- SnowBake.cs -> Resources/Snow/{snow_layout.json (1180 ชิ้น + 2342 obstacle), snow_height.bytes (381x381 cm), snow_block.bytes, snow_meta.json}; รายงาน Docs/snow-map-models/bake_report.txt
- ตรวจ: ไม่มี error; จุดเกิด (25,25)=game(-165,-165) clearance 4.8 ม.; waypoint 0-14 เดินถึงได้จากจุดเกิด (สะพาน S1 ต่อสองฝั่งแล้ว); waypoint 15 (ประตูปราสาท) อยู่หลังกำแพง/ซี่ประตู ไม่ต่อกับพื้นที่เดินได้ (ไม่ต้องใช้ — รังอยู่นอกประตู)
- รัง Minotaur = (E325,N271) game(135,81) y=50.4 บนลานหิมะข้างทะเลสาบน้ำแข็ง ห่างประตู ~64 ม.; วงโล่งใหญ่สุดในรัศมี 70 ม. จากประตู = 21.7 ม. (เป้า 22: ต่ำกว่า 0.3 ม. ใช้ lairRadius=21)
- ลานกลาง: ตัดคอลลิเดอร์ 45 ชิ้นตามข้อ 5.3 (รายการใน bake_report.txt); ภูมิประเทศลานกลางมีช่องชัน/ห้ามเดิน 1,197 ช่อง (หน้าผาจริงของแมพ ไม่ใช่ obstacle)
- เทอร์เรนเหนือขอบเหนือของทะเลสาบน้ำแข็งสูงกว่าน้ำแข็งถึง 2.4 ม. ที่ 17% ของพื้นที่ (ธรรมชาติของโมเดล) ไม่ยกทะเลสาบ
- build ผ่าน, selftest -looponly 211/0

## developer ขั้น 2-3 เสร็จ
- ขั้น 2 refactor (ebd18e1): Biome/Biomes, MapLayout property + Current, Ground.Y=0, Obstacles.Use(biome); selftest เต็ม 441/0, mptest 80/0
- ขั้น 3 เซฟ/เครือข่าย: OldWorld.biome, Loadout.runBiome (BeginRun รับ biome, โลกเก่าใช้ biome ตัวเอง), LoopSave bm/rb (เซฟเก่า = Meadow, ค่าแปลก = Meadow), Enter/Worlds/Team +1 ไบต์ biome, Net.Protocol 6 -> 7 (tech.md เขียน 5->6 แต่โค้ดจริงเป็น 6 อยู่แล้วจาก knight-rig-slash), clamp y -2..110, Args -biome, RunLoop.PickBiome (scripted = Meadow เสมอ, RandomBiomes=false จนขั้น 8); เคสใหม่ SelfTestSnow.cs 11 ข้อ
- ผล: selftest -looponly 222/0, mptest-loop 103/0 (protocol 7)

## developer ขั้น 4 เสร็จ (SnowBuilder ภาพ + y-follow)
- SnowLayout.cs (โหลดข้อมูล/GroundY/BlockAt/โซน), SnowBuilder.cs (1180 ชิ้น -> 892 chunk mesh + 36 renderer ตรง + กังหันหมุน, 165,383 tri, สร้าง 31 ms), Game.SetBiome (สลับ root ทุ่ง/หิมะ + MapLayout.Current + Obstacles.Use), MapLayout ตอบตาม biome (TownSpawn/Lair/Dummy/InTown/ZoneAt/SpawnTier/Walkable/Signs), Ground.Y/Top, Knight y = Ground.Y, กล้องตามความสูง (+กันจมเนิน), AimPoint ใช้ระนาบที่เท้า, Fx วงแหวน/สตรีค/ฝุ่นยกตามพื้น, Kit โหลด SnowCatalog
- เข้าด้วย -biome snow เท่านั้น (สุ่มยังปิด); ภาพในเกม Docs/snow-map-models/ingame/game/
- ผล: selftest เต็ม (ทุ่ง) 452/0 (441 เดิม + 11 เคสใหม่), -snowonly 20/0

## developer ขั้น 5 เสร็จ (การชน)
- SnowBuilder.MakeObstacles: 2,342 obstacle (643 วงกลม + 42 กล่องจากชิ้นวาง + 1,657 กล่องจากตารางห้ามเดิน) hash 410339541 เท่ากันเมื่อสร้างซ้ำ (งบ <= 8,000); ชุดแยกต่อ biome (Obstacles.Use), เมธอดทดสอบ CountOf/HashOf/CentersIn/Overlaps
- เทส -snowonly (32/0): waypoint 0-14 เดินถึงจากจุดเกิด (BFS 94,454 ช่อง), รัง/จุดเกิด/หุ่นฟางเป็นพื้นโล่ง, สะพาน S1 เดินได้ตลอด (ก้าวความสูงสูงสุด 0.08 ม.), เดินชนแม่น้ำ/ภูเขา/กำแพงปราสาท/ขอบแมพ + เดินทแยง 8 จุดไม่หลุดเข้าช่องห้ามเดิน
- selftest -looponly 222/0
