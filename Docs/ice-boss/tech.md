# tech: บอสจอมมารน้ำแข็ง Kryos (ice-boss)
อ้างอิง: requirement.md, design.md, design-moves.md, decisions.md, Boss Design Doc จอมมารน้ำแข็ง.md, CLAUDE.md ของเกม, reference/{files, loop, online-coop, map-and-spawning, progression, weapons-and-tab, sound, dev-guide, controls}.md, snow-map-models/{tech, status}.md
โค้ดที่ตรวจ: `Assets/MedievalKnight/Scripts/` ที่ HEAD `c7e3841` (snow-map ขั้น 0) + working tree วันที่เขียน (2026-10-17) | ผู้เขียน: architect
ขอบเขต: โค้ดฝั่ง Unity ของบอส/ลูกน้อง/ห้อง/สถานะ/HUD/เสียง/เครือข่าย/เทสต์ | ไม่แก้โมเดล Blender (artist ทำ .fbx ตามสัญญาในข้อ 6) | ไม่เขียนโค้ดจริงในเอกสารนี้

## 0. สถานะฐานโค้ดที่ตรวจจริง (ต้องอ่านก่อนลงมือ)
| เรื่อง | ที่เจอจริง | ผลต่อ tech นี้ |
| --- | --- | --- |
| แมพหิมะ | HEAD = ขั้น 0 (import fbx/prefab/Showcase) เท่านั้น; ขั้น 1 bake มีไฟล์ใน working tree (`Resources/Snow/snow_*.json/.bytes`) ยังไม่ commit; **ยังไม่มี** `Biome.cs` `Ground.cs` `SnowLayout.cs` `SnowBuilder.cs`, `MapLayout.Lair` ยังเป็น `static readonly (0,0,150)`, `Obstacles` ยังไม่มี `Use(biome)`, ไม่มี `Biome` ใน `Enter`/`Worlds`/`Team`, `Net.cs` clamp y ยัง 0..6 | ทำเฟส capsule→ต้นแบบเต็มบน **โลกทุ่ง** ได้ก่อน (บังคับด้วย `-boss kryos`) แล้วค่อยผูก "โลกหิมะ→Kryos" ตอนท้าย (ขั้น 12) โดยผ่านตะเข็บ `MapLayout.Lair` / `Ground.Y` ที่แมพหิมะสร้าง |
| `Net.Protocol` | **= 6** ที่ HEAD (ค่าจาก knight-rig-slash) | snow-map tech.md เขียนว่า "5→6" และ design.md ของบอสเขียน "เป็น 7" — **ทั้งคู่ล้าสมัย** แมพหิมะต้องเป็น 7 (ถ้าทำก่อน) ส่วนบอสต้องเป็น **ค่าปัจจุบัน + 1 ณ วันลงมือ** (คาดว่า 8) → ก่อนแตะ `Net.cs` ให้ `grep "Protocol ="` ใน `Net.cs` (บรรทัด ~276) ทุกครั้ง บวก 1 ครั้งเดียวทั้งงาน (ข้อ 8.1) แล้วเขียนเหตุผลต่อท้ายคอมเมนต์ค่าคงที่เหมือนรอบก่อน |
| ผลลัพธ์ bake ลาน | `snow_meta.json`: `lair=(135,81)` `lairRadius=21` `lairClearance=21.66` — **น้อยกว่า 22 ม.** ที่ design ตั้งเกณฑ์ไว้ (ห้อง R=24, ไม่ต่ำกว่า 22) | decisions.md ข้อ 4 บอกให้ย่อห้องโดยคงสัดส่วน → ข้อ 4.10 กำหนดสเกลห้อง `k = (lairClearance − 1)/26` (= **0.795** ตามไฟล์ที่มี: กำแพง r≈20.7, ลานเล่น R≈19.1) และใส่ในข้อที่ต้องตัดสิน (ขอ artist เปิดลานเพิ่มได้ แต่ไม่บล็อกงาน) |
| จุดเทเลพอร์ตใน design | "P = r=24×0.7" แต่รูปวาดพิกัด `(±17,±17)` (= ห่างศูนย์กลาง 24 ม. = ชนกำแพง) ขัดกันเอง | ใช้ **รัศมี 0.7×R_กำแพงเล่น = 16.8k** → มุมคือ `(±11.9k, ±11.9k)` (k = สเกลห้อง); เสากลางห้อง r=12k; บัลลังก์ `(0, +21k)` — ใส่ในข้อตัดสิน |
| ระบบเสียง | `Sfx.cs` โหลด **mp3 6 ไฟล์** ด้วยอาร์เรย์ขนาด 6 ตายตัว ไม่มีเสียงสังเคราะห์ใดๆ (decisions.md ข้อ 5 เขียนเหมือนมี "เสียงสังเคราะห์ของเกมที่มี" — **ไม่มี**) | ต้องสร้างตัวสังเคราะห์ใหม่ `SfxSynth` (ข้อ 10) หรือเปลี่ยนแผน — ใส่ในข้อตัดสิน |
| ชื่อชนกัน | `Knight.cs` มีเมธอด private `Vector3 Ground(float y)` (บรรทัด ~1034) | แมพหิมะจะเพิ่ม `static class Ground` → ภายใน `Knight` การเรียก `Ground.Y(...)` จะ **คอมไพล์ไม่ผ่าน** (เมธอดบังชื่อคลาส) ต้องเปลี่ยนชื่อเมธอดเป็น `GroundAt`/`GroundPos` ตอนแมพหิมะแตะ Knight; โค้ดบอสห้ามเรียก `Ground.Y` ตรงๆ ให้ผ่านตะเข็บเดียว `BossRoom.GroundY(x,z)` (ตอนนี้คืน 0) |
| Random | gameplay เดิมของ Minotaur/Game ใช้ `UnityEngine.Random` ฝั่ง host (ไม่ซิงก์) ส่วนฉากใช้ `System.Random(11)` | โค้ดบอส: AI ฝั่ง host ใช้ `System.Random` เท่านั้น (เพื่อเทสต์ซ้ำได้); ภาพฝั่ง client คำนวณจากสูตรตายตัว ไม่สุ่มเลย |
| ท่าทางผู้เล่น | กลิ้ง `Rolling` = `rollInvulnUntil` 0.5 วิ ผ่านทุก `TakeDamage` ("DODGE"); parry/clash เฉพาะ `by != null && !heavy`; โล่ 80%+; `Invulnerable` = invuln/Glowing/Rolling | ข้อ 5 |
| โครงสร้างทั่วไป | ศัตรู = `Enemy : MonoBehaviour` มี `NetType` (0-2 สไลม์, 3 Minotaur, 4 หุ่น), snapshot ≤255 ตัว, `Enemy.All` ลงทะเบียนใน OnEnable/OnDisable (ปิด GameObject = หายจาก HitArea และ snapshot), `HitArea` ตีทุก `Enemy` ในรัศมี, ส่ง `Game.HitEnemy` → `TakeHit` | ข้อ 4 ใช้ "ตัวแทน Enemy" ให้ก้อนน้ำแข็ง/แกนหลัง/กำแพงน้ำแข็ง/Golem ถูกตีด้วยท่อเดิม ไม่เขียนระบบตีใหม่ |

## 1. ภาพรวมแนวทาง
- เขียนบอสเป็น `BossController : Enemy` (NetType 5) ตามแพทเทิร์น `Minotaur`: AI รันที่ host/เล่นคนเดียว (`sim`), client เป็น puppet ที่อ่านสถานะ+เวลาจาก snapshot แล้วเล่น telegraph/VFX/anim เองจากสูตรตายตัว; ค่าท่าทั้งหมดอยู่ตารางค่าคงที่ใน `BossData` (แทน ScriptableObject), เคลื่อนด้วยเทเลพอร์ต+ไถลช้าผ่าน `Obstacles.Push` (แทน NavMeshAgent), ท่าทางด้วยคีย์เฟรมโค้ด (แทน Animator), telegraph ด้วย `Fx.MakeDecal` (แทน Decal)
- ของที่ต้อง "ถูกตี/ขวาง" ทำเป็น `Enemy` ตัวแทนเล็กๆ (ก้อนน้ำแข็ง, แกนหลัง, ชิ้นกำแพงน้ำแข็ง, Ice Golem) เพื่อใช้ `HitArea`/`Hit` message เดิม; สิ่งกีดขวางแบบเปิด-ปิดได้ (กำแพงวงห้อง, เสา, กำแพงน้ำแข็ง, หนามหาง) เพิ่ม API "dynamic" เล็กๆ ใน `Obstacles`
- สถานะ Chill/Frozen อยู่ใน `StatusEffects` ของ `Knight` แต่ละตัว และ **เครื่องเจ้าของเป็นผู้ตัดสิน** (เพราะกลิ้ง/บล็อก/parry ตัดสินที่เครื่องเจ้าของอยู่แล้ว) host รู้ผ่านไบต์สถานะที่ติดไปกับ state ของอัศวิน
- ต้นแบบก่อน: ขั้น 4 = capsule + กล่องชี้ทิศ + แท่งแทนหาง + telegraph สีเดียว + แถบ HP/poise จริง เล่นเฟส 1 ให้สนุกก่อน (พีทเล่นเอง = ด่านที่ 1) แล้วค่อยเพิ่มเฟส 2/3 → ซิงก์ co-op → เสียง → โมเดล → ขัดเกลา → ผูกกับโลกหิมะ
- ใช้ของเดิมให้มากที่สุด: `Game.DamageKnight`, `Knight.TakeDamage` (parry/clash/block/roll), `Enemy.TakeHit/Deflect`, `Fx`, `Prims`, `Hud.Popup/Banner`, `RunLoop.OnBossKilled` (portal/VICTORY), `Game.RollDrop`, `ModelLib`
- ขนาดงาน: **ใหญ่มาก** (ข้อ 16)

## 2. ไฟล์ที่สร้างใหม่ / แก้ (สคริปต์ใต้ `Assets/MedievalKnight/Scripts/` ถ้าไม่ระบุ)
### 2.1 สร้างใหม่
| ไฟล์ | หน้าที่ / เหตุผล |
| --- | --- |
| `BossData.cs` | ตารางค่าทั้งหมด (ท่า 10, เฟส, HP/poise, Judgment, Chill/Frozen, Golem, สเกลห้อง) + ฟังก์ชันล้วน (`MaxHp`, `PoiseMax`, `PhaseOf`) — ไม่มี UnityEngine state จึงเทสต์ headless ได้ |
| `BossController.cs` | `BossController : Enemy` state machine + เป้า + poise + host AI + puppet mirror + `WriteExtra/ReadExtra` |
| `BossMoves.cs` | `partial class BossController`: การทำงานของทีละท่า (host: หาเหยื่อ/ตัดสินดาเมจ, ทุกเครื่อง: ภาพจาก `(move, t)`) — แยกไฟล์เพราะท่า 10 ท่ายาวเกินไฟล์เดียว (ใช้ `partial` ครั้งแรกในโปรเจกต์ ยอมรับได้) |
| `BossJudgment.cs` | `partial`: Frozen Judgment (6 วิ, ขั้นย่อย 0-5) + Absolute Zero (ท่าที่มี timeline ซับซ้อน) |
| `TargetSelector.cs` | static ล้วน เลือกเป้า: ไกลสุด / ใกล้ในระยะ / โดดเดี่ยวสุด (Judgment ไม่ซ้ำคน) / จุดเทเลพอร์ตที่เหมาะ / ถ่วงน้ำหนักคนที่เพิ่งโดนเลือก |
| `TelegraphSpawner.cs` | สร้าง/อัปเดต telegraph บนพื้น (วงกลม พัด วงแหวน เส้น เส้นโค้ง) เป็นเมชแบน 2 ชั้น (ขอบเข้ม+ไส้) ผ่าน `Fx.MakeDecal`; พูลได้ ≤12 อัน |
| `StatusEffects.cs` | Chill (3 ชั้น) / Frozen / Judgment-frozen / อมตะหลังหลุด / mash / ล็อกฉากเปิดตัว; คลาสธรรมดาที่ `Knight` ถือ (`knight.status`) |
| `OrbitingCrystals.cs` | ผลึกลอย 3/2/1 ชิ้น หมุนรอบบอส, แตกตามเฟส, ถูกปล่อยเป็นกระสุน Crystal Orbit |
| `BossShots.cs` | กระสุน Frost Bolt + เส้นทางผลึก Orbit: เมชเล็กๆ พูลได้; host ตรวจโดน, client วาดอย่างเดียวจากสูตร `Trace(origin, dir, …)` |
| `IceGolem.cs` | `IceGolem : Enemy` (NetType 6) AI ยืนขวาง + ทุบพื้น + พื้นลื่นตอนตาย (`SlickZone`) |
| `BossProxies.cs` | ตัวแทน `Enemy` เล็กๆ: `IceBlock` (7: ก้อนแช่แข็งของอัศวิน), `BossCore` (8: แกนหลังตอน Judgment), `IceWallPiece` (9: ชิ้นกำแพง Ice Wall) |
| `BossRoom.cs` | ห้องบอส: ภาพ (พื้นวงเวทย์ เสา บัลลังก์ วงเทเลพอร์ต 5 จุด), สิ่งกีดขวางแบบ dynamic (กำแพงวง 8 ส่วน, เสา 4 ต้น), สเกล `k`, ตะเข็บ `GroundY`, ดึงผู้เล่นนอกวง, `Teardown` |
| `BossBody.cs` | ร่างกายบอส: `BossBody` abstract + `FileBody` (.fbx) / `CodedBody2` (Prims) / `CapsuleBody` (ต้นแบบ) ตามแพทเทิร์น `KnightBody`; `BossAnim` ตารางคีย์เฟรม 16 คลิป (แพทเทิร์น `KnightAnim`) |
| `BossUi.cs` | UI ของตัวเอง (canvas แยกแบบ `MapUi`/`LoopUi`): แถบ HP+poise+ขีดเฟส+ไอคอนผลึก, ข้อความ Judgment, แถบ mash, ไอคอน Chill/Frozen ข้างแถบ stamina, ขอบจอวูบ |
| `SfxSynth.cs` | สร้าง `AudioClip` เสียงบอส 23 อันด้วยโค้ด (ข้อ 10) |
| `BossSelfTest.cs` | เคสทดสอบบอส (ข้อ 12) — แยกไฟล์ เพราะ `SelfTest.cs` ยาว ~4,200 บรรทัดและแมพหิมะก็เพิ่มเคส; ต้องเปลี่ยน `class SelfTest` เป็น `partial` (แก้คำเดียว) |
| `Assets/Editor/BossImport.cs` (ขั้น 10) | ตั้งค่า ModelImporter ของ `Resources/Models/Boss/*.fbx` + ตรวจรายชื่อ node/tri ตามสัญญาข้อ 6 (ใช้ helper ใน `AssetImport.cs`/`SnowImport.cs` ซ้ำ) |
| `Assets/MedievalKnight/Resources/Models/Boss/*.fbx` | โมเดลจาก artist (ข้อ 6) |

### 2.2 แก้ของเดิม
| ไฟล์ | แก้อะไร / ทำไม |
| --- | --- |
| `Enemy.cs` | เพิ่ม `virtual bool IsWorldBoss` (ค่าเริ่ม false), `virtual bool AllowsClash` (ค่าเริ่ม true); อัปเดตคอมเมนต์ `NetType` (5 Kryos, 6 IceGolem, 7 IceBlock, 8 BossCore, 9 IceWallPiece); `PuppetMove` เพิ่มตะเข็บ y (ตอนนี้ y=0) — แพทเทิร์นเดียวกับที่แมพหิมะจะทำ ต้องรวมกัน |
| `Minotaur.cs` | `override IsWorldBoss => true` (พฤติกรรมเดิมทุกอย่าง) |
| `Game.cs` | (1) ฟิลด์ `public BossController boss;` + ตัวช่วย `WorldBoss` (Enemy) (2) `SpawnActors`: เลือกชนิดบอส `Minotaur` หรือ `BossController` ตาม `BossKind` (ข้อ 7) (3) `SpawnPuppet` รองรับ type 5-9 (4) แทน `e is Minotaur` ที่เป็น "บอสโลก" เป็น `e.IsWorldBoss`: `OnEnemyKilled` (บรรทัด ~768), `OnPuppetKilled` (~803), `RollDrop` (~902, ~931), และจุดที่ใช้ตัวแปร `minotaur` ตรงๆ (~395, ~737, ~1281, ~1300, ~1478) (5) `DamageKnight(...)` เพิ่มพารามิเตอร์ `HitFx fx` (ธงหนัก/พื้นที่/chill/แช่แข็ง/ประหาร ข้อ 5.2) (6) `UpdatePacks`: ห้ามเกิดสไลม์ใน r=34k รอบห้องบอสตอนบอสตื่น + ลบสไลม์ที่อยู่ในห้องเงียบๆ ตอนตื่น (7) `ClearWorld`: `BossRoom.Teardown()` (8) `PushEnemiesFrom`/`OnPartyWipe`: ข้ามบอส Kryos (wipe = จบรอบอยู่แล้ว) |
| `Knight.cs` | (1) `public readonly StatusEffects status` (2) `Update`: `canInput` ปิดเมื่อ `status.Locked`; นับ mash (3) `ReadInput`: `move=0` ตอนล็อก (4) `Simulate`: ความเร็วเดิน×`status.MoveMult`, พื้นลื่น (`SlickZone`) ผสมความเร่ง, ยกเลิก action ตอนโดนแช่ (`CancelAction`+`CancelEat`) (5) `TakeDamage(..., HitFx fx)`: ลำดับตามข้อ 5.1 (6) `Deflected`/clash: เคารพ `by.AllowsClash` (7) `NetFlags` ไม่แตะ (บิต 128 ว่างแต่เก็บไว้) — ส่ง **ไบต์สถานะใหม่** ใน `WriteKnight/ReadKnight` (8) เมธอด `Ground(float)` เปลี่ยนชื่อ (ข้อ 0) (9) เปิด `public const float BaseMoveSpeed = MoveSpeed` ให้บอสคิด 70% |
| `Progress.cs` | ไม่แก้ (ใช้ `MoveSpeedMult`/`DamageTakenMult`/`BlockReduction` เดิม) |
| `World.cs` | `Obstacles`: เพิ่ม API dynamic ที่ถอดได้ (ข้อ 4.8) — เล็กมาก (ฟิลด์ `on` ใน `Obs`, `AddCircleDyn/AddBoxDyn → id`, `SetOn(id,bool)`, `Push` ข้ามอันที่ปิด, `Clear` ล้างทั้งหมด); **ต้องรวมกับการแก้ `Obstacles.Use(biome)` ของแมพหิมะ** (id เป็นของชุดปัจจุบัน; บอสสร้างห้องหลัง `SetBiome` เสมอ) |
| `Fx.cs` | `MakeDecal` ใช้ได้เลย; ตัวช่วยตั้งตำแหน่ง/เอียง decal ตามพื้นเก็บใน `TelegraphSpawner` (ผ่าน `BossRoom.GroundY`) — ไม่จำเป็นต้องแก้ `Fx` |
| `Prims.cs` | ไม่แก้ (ใช้ `Lathe(6)` ทำผลึก 6 เหลี่ยม, `Strip` ทำเส้นโค้ง, `Arc`, `Lane`) |
| `ModelLib.cs` | `Recolor` เพิ่มพารามิเตอร์ตัวเลือก "ชื่อ material ลงท้าย `_glow` → `Prims.Glow`"; เพิ่ม `FindNodes(inst, names, out missing)` สำหรับตรวจสัญญา node; ไม่เปลี่ยนพฤติกรรมเดิมของดาบ |
| `Net.cs` | `Protocol` +1 (ข้อ 0, 8.1); `enum Msg` เพิ่ม `Boss` ต่อท้าย (หลัง `RunEnd`); `WriteKnight/ReadKnight` +1 ไบต์สถานะ; `SendKnightHit` เปลี่ยนไบต์ `heavy` เป็น `fx flags`; handler `Msg.Boss` ทั้งสองทิศ; hook หน่วง `-netlag` ใน `Poll` (ข้อ 12); clamp y ตามที่แมพหิมะแก้ (ห้ามทับกัน) |
| `TeamRun.cs`, `TeamNet.cs` | แทบไม่แตะ: `OnPortal` ใช้เดิม; ตรวจว่า wipe/ABANDONED/ออกกลางรอบเรียก `ClearWorld` แล้ว `BossRoom.Teardown` ครบทุกเส้นทาง; ถ้าแมพหิมะเพิ่ม biome ใน `Enter` บอสไม่ต้องแก้เพิ่ม |
| `RunLoop.cs` | ข้อความ "Defeat the Minotaur" (บรรทัด ~393) / "THE MINOTAUR HAS FALLEN…" (~1110) → ชื่อบอสจาก `BossData.NameOf(kind)`; ส่ง seed โลกให้บอส (`WorldInPlay`); `OnBossKilled` ใช้ต่อเดิม |
| `LoopUi.cs` | ข้อความ ~333 และ ~1338 ("Minotaur …") → ชื่อบอส |
| `Hud.cs` | แถบบอสเดิมซ่อนเมื่อบอสเป็น Kryos (`bossPanel`) และเรียก `BossUi` แทน; ไม่ย้ายแถบ stamina |
| `Sfx.cs` | enum `Snd` ต่อท้าย 23 ค่า; อาร์เรย์ `clips/played/lastTime/BaseVolume/PitchVar/MinGap` ขยายตามจำนวน enum (ลำดับต้องตรงกัน); เสียงใหม่ดึงจาก `SfxSynth` แบบ lazy |
| `Args.cs` | `-boss kryos|minotaur`, `-bossphase 1..3`, `-bosshp <คูณ>`, `-bossmodel capsule|coded|file`, `-netlag <ms>` (scripted/selftest/mptest ไม่ใส่ = Minotaur เหมือนเดิมเสมอ) |
| `SelfTest.cs` | `partial` + เรียก `BossTests()`; เพิ่มสวิตช์ `-bossonly`; แก้เคสที่ผูก "Sfx 6 คลิป" ถ้ามี (ต้อง grep `clips.Length`/`Files.Length`) |
| `MpLoopTest.cs`, `mptest-loop.ps1` | ส่วนที่ 11 "บอสน้ำแข็ง" + สวิตช์ `-Boss kryos` / `-NetLag 150` (ข้อ 12) |
| Docs (ตอนจบ) | `reference/{files, online-coop, sound, controls, dev-guide, loop, map-and-spawning}.md`, `CLAUDE.md` ของเกม, `Docs/backlog.md` (เสียงพูด+เพลง 3 layer รอไฟล์จากพีท ตาม decisions ข้อ 5), `ice-boss/status.md` |

## 3. ลำดับคลาสโดยย่อ (ใครเรียกใคร)
```
Game ── SpawnActors ──> BossController (Enemy, NetType 5) ── owns ──> BossBody (Capsule/Coded/File) + BossAnim
  │                          │  ├─ OrbitingCrystals ── BossShots (Bolt / Orbit path)
  │                          │  ├─ TelegraphSpawner (decal pool)
  │                          │  ├─ TargetSelector (static, host) ── ใช้ Game.players
  │                          │  ├─ BossData (static tables)           IceGolem (Enemy 6) ── SlickZone
  │                          │  └─ proxies: IceBlock(7) BossCore(8) IceWallPiece(9)
  │                          └──> Game.DamageKnight(k, dmg, from, src, knock, by, HitFx) ──> Knight.TakeDamage ──> StatusEffects
  ├── BossRoom (room visuals + dynamic Obstacles + pull-in + GroundY seam)
  └── BossUi (HP / poise / phase / Judgment / mash / chill icons)   Sfx ── SfxSynth
```

## 4. คลาส หน้าที่ และรายละเอียด
### 4.1 `BossData` (ตารางค่า ไม่ใช่ ScriptableObject)
- `enum Move { FrostBolt, CrystalOrbit, StaffSweep, Blink, SummonGuardian, FrostNova, IceWall, FrozenJudgment, AbsoluteZero, TailWhip }` (ลำดับ = เลขท่า 1-10 ของ design; ใช้เป็น id ในเครือข่าย 0-9)
- `struct MoveDef`: `phaseMask`, `tele`, `hitAt`, `end` (เวลารวมท่า), `cd`, `kind` (Light/Heavy/Area/Special), `pct` (% MaxHp ผู้เล่น), `chill`, `knockM` (เมตร), `rangeMin/Max` (ระยะเหยื่อที่ใช้ได้), `rollDemand` (จำนวนครั้งที่ต้องกลิ้งถ้าไม่ใช้ทางอื่น), `weight`
- ค่าตาม design-moves ข้อ 2/5: Bolt tele 0.6 hit 0.6 cd 4 pct 12 chill 1 | Orbit 0.8/0.8 cd 8 pct 18 chill 1 (2 จังหวะ: ออก 0.7 วิ → พัก 0.3 → กลับ 0.7; จังหวะที่สองเริ่มห่าง ≥1.0 วิ) | Sweep 0.8/0.8 cd 5 pct 20 knock 3 m | Blink 0.5 cd 6 | Summon 1.5 cd 30 | Nova 1.0/1.0 cd 10 pct 15 chill 2 | Wall 1.2/1.2 cd 15 pct 10 | Judgment 1.5 (timeline ข้อ 4.5) 1 ครั้ง/เฟส เริ่มได้หลังเข้าเฟส 20 วิ | AZ 3.0/3.0 cd 35 pct 40 + Frozen | Tail 0.8 (+0.9 ครั้งที่สอง) cd 7 pct 18 knock 4 m
- ฟังก์ชันล้วน: `MaxHp(players, worldHpMul) = DpsRef(20) × 300 × (1 + Game.CoopHpPerPlayer×(n−1)) × worldHpMul` (ได้ 6,000/9,600/13,200/16,800 ที่ n=1..4 ตรง design); `PoiseMax(n) = 20×4×1.6×(1+0.6(n−1))` (= 128/205/282/358); `PhaseOf(hp/maxHp)` (เกณฑ์ 60% / 25%); `RestAfter(phase)` = 1.5/1.0/0.7; `GolemCap(phase)` = 1/2/2; `GolemHp = 8%×MaxHp`; ความเร็วบอส `= 0.7 × Knight.BaseMoveSpeed` (= 4.34 ม./วิ ไม่คูณ Chill)
- ดาเมจท่า = `pct/100 × เหยื่อ.MaxHp × worldDmgMul` (โลกเก่า ×1.25 ใช้ได้ตามเดิม; **ห้ามใช้** `EnemyHpScale`/`EnemyDamageScale` ตามเหตุผลใน design ข้อ 4) — host ใช้ `Knight.MaxHp` ของ puppet ผู้เล่น (ซิงก์ผ่าน `WriteKnight` อยู่แล้ว)
- ค่า Judgment/Chill/Frozen/Golem/ห้อง เป็น `const` ชื่อชัดเจน (`JudgeNeedPoints=24`, `JudgeFriendHit=6`, `JudgeSoloPress=1.5`, `FrozenSeconds=2`, `MashCut=0.15`, `FrozenImmune=4`, `ChillDecay=3`, `ChillSlow=0.15`, ฯลฯ) เพื่อจูนได้ที่เดียว
- `NameOf(kind)` ("Kryos - Frost Lord" / "Minotaur") ใช้ใน banner/objective

### 4.2 `BossController` — state machine
```
Dormant ─(ผู้เล่นเป็นๆ ≤16k·ม. จากบัลลังก์ หรือถูกตี)─> Intro (3.5 วิ) ──> Choose
Choose ─(เลือกท่า + หมุนหาเป้า ล็อกทิศ/จุดเมื่อเริ่ม telegraph)─> [Glide] ─> Telegraph(tele) ─> Strike(hitAt..end) ─> Recover(พักตามเฟส) ─> Choose
Telegraph/Recover ──(poise เต็ม)──> Stagger (2 วิ, ยกเลิกท่า) ──> Recover
ทุก state ยกเว้น Judgment/AbsZero/Death ──(hp ข้ามเกณฑ์ 60%/25%)──> Transition (อมตะ 3 วิ ผลึกแตก 1 ชิ้น ล้าง Chill ทุกคน) ──> Choose
Judgment/AbsZero ──(hp ข้ามเกณฑ์)──> รอจบท่าก่อน (hp ถูกล็อกไม่ต่ำกว่าเกณฑ์) แล้วเข้า Transition
ทุก state ──(hp = 0 เฟส 3)──> Death (3 วิ ร่างแตก) ─> Dead (Enemy.Dead → Game.OnEnemyKilled)
```
- รัน 2 โหมดจากเมธอดเดียวแบบ `Minotaur` (`bool sim = !puppet`): sim ตัดสินใจ/นับเวลา/ทำดาเมจ; puppet อ่าน `(State, move, t, args)` จาก snapshot (`ReadExtra`) แล้วเรียก "ภาพของท่า" เดียวกัน (`TickMoveVisual(move, t)`) — ห้ามแยกสูตร 2 ชุด
- ฟิลด์หลัก: `State`, `Phase` (1-3), `curMove`, `t`, `moveSerial` (เพิ่มทุกครั้งที่เริ่มท่า ให้ client รู้ว่าท่าใหม่แม้ State เดิม), `aimYaw`, `pointA/pointB` (จุดล็อก: ปลายทางเทเลพอร์ต/ปลาย Orbit/เส้นกำแพง/วง AZ), `targetPid`, `victimPid`, `cds[]`, `lastMoves` (ห้ามท่าเดิมติดกัน >2), `poise` (ข้อ 4.2.1), `immuneUntil`, `hpFloor`
- **เลือกท่า (host)**: (1) กรองเฟส+cooldown (2) เลือกเป้าตามกฎของท่า (`TargetSelector`) (3) กรองระยะเหยื่อ (`rangeMin/Max`) และเงื่อนไขพิเศษ — Tail: มีผู้เล่นอยู่ในพัดหลัง (>100° จากทิศหน้า) ≤7 ม. เกิน 1.0 วิ, ไม่ติดกัน 2 ครั้ง, ไม่ใช้ตอนกำลังเข้าท่า Sweep (หลัง Sweep ได้) ; Judgment: เฟส ≥2, 1 ครั้ง/เฟส, ≥20 วิหลังเข้าเฟส, เหยื่อต้องไม่ซ้ำคนเดิม; AZ: เฟส 3, cd 35, ครั้งแรก ≥8 วิหลังเข้าเฟส; Summon: golem ยังไม่เต็ม `GolemCap`; Blink: เมื่อมีคนอยู่ ≤3 ม. เกิน 3 วิ หรือ cd ครบและไม่มีท่าอื่นเข้าเกณฑ์ (4) สุ่มถ่วงน้ำหนักด้วย `System.Random(seedโลก ^ 0x4B525950)` ลดน้ำหนักท่าที่เพิ่งใช้ ×0.35 (5) ถ้าไม่มีท่าใดผ่าน → ไถลเข้าหาเป้า 0.3 วิแล้วเลือกใหม่
- **ตื่น/เปิดตัว**: ตื่นที่ host เมื่อมีอัศวินเป็นๆ ≤16k ม. จากบัลลังก์ → `BossRoom.CloseDoor()` (กำแพงวงผุด) + **ดึงอัศวินที่อยู่นอกวงเข้าห้อง** (เทเลพอร์ตไปจุดทางเข้าด้านในกำแพง — ข้อตัดสิน) → ทุกเครื่องเห็น state=Intro จึงล็อกอัศวินตัวเอง 3.5 วิ (`status.IntroLock` + `Protect(3.5)` อมตะ; ไม่ pause) → banner "KRYOS - FROST LORD"
- **ความเร็ว/ตำแหน่ง**: ไม่วิ่งไล่ ไถลช้า (`Glide`) 4.34 ม./วิ ผ่าน `Obstacles.Push(…, Obstacles.Enemies)` ใน Choose/Recover ถ้าเหยื่ออยู่ไกลกว่าระยะที่ท่าถัดไปต้องการ; เทเลพอร์ต Blink ย้ายทันทีที่ปลายท่า; บอส `solid=true radius 1.2` (ผู้เล่นเดินทะลุไม่ได้ ใช้ pushout เดิมใน `Knight.Simulate`); `knockResist=0` (ไม่กระเด็น)
- **เกราะ**: 0 (`InitStats(maxHp, 0)`); ไม่มี `BuildBar` (ใช้ `BossUi` แทน)

#### 4.2.1 ดาเมจเข้าบอส / poise / ชะงัก (override `TakeHit`)
ลำดับใน `TakeHit(damage, …)` (เฉพาะ sim):
1. Dormant → ตื่น; Intro/Transition/Death → **ไม่รับดาเมจ** (ไม่ลด HP, ขึ้น popup "IMMUNE" ได้ ไม่เกิน 1 ครั้ง/วิ)
2. ตัวคูณ: ชะงักปกติ ×1.25; ชะงักหลังทีมหักล้างท่าประหาร ×2 ตลอด 3 วิ (ข้อ 4.5); ตีโดน `BossCore` (ผ่าน proxy) ×2 (แยกบัญชีแกน)
3. ล็อก HP ไม่ต่ำกว่าเกณฑ์เฟส (`hpFloor` = 60%/25%×Max ขณะอยู่เฟส 1/2) — ตัดดาเมจส่วนเกินทิ้ง (กันข้ามเฟสด้วยการตีครั้งเดียว) แล้วตั้ง `pendingTransition`
4. เรียก `base.TakeHit(...)` ด้วยดาเมจที่ปรับแล้ว (ได้ `EnemyHit` broadcast + ตัวเลข + SFX เดิม) ; เสร็จแล้วเติม **poise**: บัฟเฟอร์ (เวลา, ดาเมจ) ย้อน 4 วิ; ผลรวม ≥ `PoiseMax(n)` → ชะงัก (ถ้าอยู่ Telegraph ยกเลิกท่า, ถ้า Recover ชะงักปกติ) 2 วิ แล้วล็อกเกจ 8 วิ ; ไม่ชะงักตอน Judgment (3.0-6.0 super armor — แต่ 0-1.5 เกจเต็ม = ยกเลิกท่าตาม design), Transition, Blink ระหว่างหาย, Intro/Death
5. `Deflect(parry, from, by)` (override): parry ท่า **Sweep/Tail** สำเร็จ = poise +40% ของ `PoiseMax` (ตรวจ `curMove`), ไม่ชะงักถ้าเกจไม่เต็ม, ไม่มีแรงสะท้อน (`DeflectStun=0`, `knockResist=0`); parry ท่าอื่น (Bolt/Orbit) = ปัดดาเมจอย่างเดียว ไม่ให้ poise (กันฟาร์ม — ข้อตัดสิน); clash ปิด (`AllowsClash=false`)
6. เครือข่าย: ใช้ `Net.BroadcastEnemyHit` เดิมทั้งหมด (HP ไป client ทาง `WriteNet` อยู่แล้ว)

### 4.3 ท่าทั้ง 10 (ตาม `design-moves.md` ข้อ 2; ตัวเลขใน `BossData`)
ทุกท่า: host ล็อก `aimYaw/pointA/pointB` ตอนเข้า Telegraph แล้วเผยแพร่ใน `WriteExtra`; ทุกเครื่องวาด telegraph จาก `(move, t, args)`; host ตัดสินดาเมจที่ `hitAt` (ผู้เล่นฝั่ง host ทันที / ผู้เล่นระยะไกลเผื่อ `ResolveDelay` ข้อ 8.4) แล้วเรียก `Game.DamageKnight(…, fx)`.
| # | ท่า | host ตัดสิน | ภาพ (ทุกเครื่อง) | ปะทะผู้เล่น |
| --- | --- | --- | --- | --- |
| 1 | Frost Bolt | ที่ `hitAt` ปล่อย 3 นัดที่ yaw−18°/0/+18° จาก `Staff_Tip`; ทุกเฟรมเลื่อน 16 ม./วิ ไกล 30 ม. (`BossShots.Trace` หาจุดหยุดจากเสา/กำแพงวง/กำแพงน้ำแข็งก่อน) ตรวจวงกลมผู้เล่น r 0.6 ม.; โดน → `DamageKnight(12%, chill 1, light)` แล้วกระสุนหาย | เส้น 3 เส้นขึ้นพื้นตอน tele; กระสุนผลึกคำนวณตำแหน่ง = origin + dir×16×(t−hitAt) | กลิ้ง=ผ่าน, บล็อก=ลดดาเมจ ไม่ติด Chill, parry=ปัดนัดนั้น (ไม่ให้ poise) |
| 2 | Crystal Orbit | `OrbitingCrystals.Launch(i)` ปล่อย 1 ชิ้นตามเส้นโค้ง Bézier ไป `pointA` (ปลายทางล็อกที่เหยื่อไกลสุด) แล้ววนกลับ; ตรวจผลึก r 0.8 ม. กวาดเฉพาะจังหวะออก [0.8–1.5] และกลับ [1.8–2.5]; ผู้เล่น 1 คน/จังหวะโดน 1 ครั้ง (18%, chill 1) | เส้นโค้งประบนพื้น 2 ช่วง; ผลึกพุ่งโค้ง | กลิ้ง 2 ครั้ง/อ้อม; บล็อกได้ |
| 3 | Staff Sweep | `hitAt` 0.8: ผู้เล่นในพัด 120° r 5.5 ด้านหน้า (หรือ <1.6 ม.) → 20%, ผลัก 3 ม. | พัดไส้ฟ้าโตขึ้น; แขนกางข้าง | **parry ได้** (ให้ poise +40%), บล็อกได้, clash ไม่ได้ |
| 4 | Blink | จุดปลายทางจาก `TargetSelector.BlinkPoint` (1 ใน 5 จุด); tele 0.5 วิ วงสว่าง; ย้ายตำแหน่งตอน `hitAt` (หายตัว 0.2 วิ) ไม่มีดาเมจ | วงเวทย์จุดเทเลพอร์ต (ติดอ่อนตลอดสว่างตอนเป็นปลายทาง), เอฟเฟกต์หาย/โผล่ | บอส `solid` ดันผู้เล่นที่ยืนทับ |
| 5 | Summon Guardian | ที่ `hitAt` 1.5 วิ spawn `IceGolem` 1 ตัว ห่างบอส 4 ม. ฝั่งเหยื่อ (ถ้ายังไม่เกิน `GolemCap`) ; ชะงักก่อนเวลา = ยกเลิก ไม่เกิด | ชูคทา; วงที่จุดเกิด | ขัดด้วย poise |
| 6 | Frost Nova | `hitAt` 1.0: ผู้เล่นใน r 6.5 รอบบอส → 15% + chill 2, flags **หนัก+พื้นที่** | วงกลมขอบแดง `#C8483A` + `!` | โล่ไม่ช่วย, parry/clash ไม่ได้, ต้องกลิ้ง/ออกวง |
| 7 | Ice Wall | เฟส ≥2; เส้นยาว 24 กว้าง 1.2 (ตัดกับวงห้อง); วาง `center` = กึ่งกลางเหยื่อกับศูนย์กลางทีม (คนเดียว = ที่ผู้เล่น) หมุนตั้งฉากแกนนั้น; ที่ `hitAt` 1.2 ผู้เล่นบนเส้น → 10% (พื้นที่) แล้ว spawn กำแพง: 1 กล่อง dynamic (Both) + `IceWallPiece` 8 ชิ้นห่าง 3 ม. (ตัวรับการตี) ใช้ **HP ร่วม 3% HP บอส** (ข้อตัดสิน) ; หายเอง 12 วิ | เส้นฟ้ากว้าง 1.2; กำแพงงอก/แตก | พื้นที่ โล่ไม่ช่วย; ตีกำแพงแตกได้ |
| 8 | Frozen Judgment | ข้อ 4.5 | ข้อ 4.5 | ข้อ 4.5 |
| 9 | Absolute Zero | ข้อ 4.5 | ข้อ 4.5 | ข้อ 4.5 |
| 10 | Tail Whip | พัด 200° r 7 ม. ด้านหลังบอส; ตัดสินเป็น "แถบหมุนกว้าง 1.6 ม." ผ่านช่วง 0.35 วิ (`hitAt` 0.8): ตรวจมุมสัมพัทธ์ของผู้เล่นกับ [มุมหางเฟรมก่อน, มุมหางเฟรมนี้] ± `asin(0.8/r)` → โดนครั้งเดียว/การฟาด (18%, ผลัก 4 ม., ไม่ Chill); เฟส ≥2 ฟาดซ้ำอีกครั้งสวนทิศที่ +0.9 วิ; เฟส 3 หนามผลึก 3 อัน r 0.6 ม. ตกตามแนวฟาด (dynamic circle ไม่ดาเมจ 4 วิ) | พัดหลังขึ้นพื้นโตขึ้น (ขอบ #1B2A4E ไส้ #6FB8D6); หางยก→ม้วน "C"→กวาด→ล้มพื้น | **parry ได้** (+40% poise); บล็อกได้; กลิ้งผ่าน |
- ท่าเบา (1,2,3,10) = `heavy=false` (ปัดได้); Nova = หนัก+พื้นที่; Wall/AZ/คลื่น/Judgment = พื้นที่ (ไม่ใช่ heavy popup แต่โล่ไม่ช่วย)
- กฎ stamina: ผลรวม `rollDemand` ในหน้าต่าง 4 วิ ≤3 — ตรวจด้วยเทสต์จำลองตารางเวลา (ข้อ 12 เคส B7); ถ้า AI เลือกท่าซ้อนจนเกิน ให้ `Choose` หน่วงด้วย Recover เพิ่ม

### 4.4 `TelegraphSpawner`
- API คร่าวๆ: `Telegraph Begin(Shape s, Vector3 center, float yaw, Vector4 size, float duration, bool heavy)` คืนตัวจับ; `tg.Update(float t01)`; `tg.End()`; รูปทรง: `Circle(r)`, `Fan(r, halfDeg)`, `Ring(r0,r1)`, `Line(len,width)`, `Curve(Vector3[] pts, width)`
- แต่ละอัน = 2 decal (`Fx.MakeDecal`): ขอบเข้ม `#1B2A4E` (ใหญ่กว่า 0.15 ม.) + ไส้ `#6FB8D6` → กระพริบ/สว่างเป็น `#5FD8F0` ในครึ่งหลังของเวลา (อัลฟา 0.35→0.8); ท่าหนัก: ขอบ `#C8483A` + `Hud.Popup("!")` ครั้งเดียวตอนเริ่ม; พัด/วงโตจาก 0→เต็มตาม `t01`
- ยกตามพื้น: `BossRoom.GroundY` (ตอนนี้ 0) → ตำแหน่ง y = พื้น+0.05; ถ้า Ground.Y ไม่เรียบใน footprint ให้เอียงตามเวกเตอร์ปกติจาก 4 จุดสุ่ม ±3 ม. (ข้อ 14 ความเสี่ยง "ลานชัน")
- `Curve` สร้างเมชใหม่ต่อการใช้งาน (ไม่แคช) แล้ว `Destroy` ตอนจบ (≤4 ครั้ง/วิ)
- Prototype (ขั้น 4): สีเดียวกึ่งโปร่ง ไม่มีขอบ

### 4.5 ท่าเต็ม: Frozen Judgment และ Absolute Zero (`BossJudgment.cs`)
**Judgment** — timeline `jt` (วินาทีจากเริ่มท่า) บนเครื่อง host; ทุกเครื่องวาดจาก `(jt, victimPid, pointA)`
| jt | host ทำ | ผู้เล่น |
| --- | --- | --- |
| 0–1.5 | `victim = TargetSelector.Isolated(excludeLast)` (คนห่างเพื่อนสุด, ไม่ซ้ำ; คนเดียว=ตัวเอง); วง r 2.2 ตามเหยื่อ 0.9 วิแล้วล็อก `pointA` ; ส่งเสียง `JudgmentWarn` ทุกเครื่อง | เหยื่อกลิ้งพ้นวง/กลิ้งอมตะตรง 1.5 → ยกเลิกทั้งท่า บอสเสียจังหวะ 1.0 วิ; poise เต็ม/ดาเมจ → ยกเลิก |
| 1.5 | ถ้าเหยื่ออยู่ในวง (ตำแหน่งล่าสุด + ผ่อน `ResolveDelay`) → ส่ง `DamageKnight(0, fx=JudgmentFreeze)` ให้เจ้าของ; เจ้าของตอบ `Msg.Boss.FreezeAck(ok)` ตัดสินที่เขา (กลิ้ง/อมตะ = ok=0 → ยกเลิกท่า); ไม่ตอบใน 0.35 วิ ถือว่า ok | ok → เหยื่อเป็นก้อนน้ำแข็ง (ขยับ/ฟัน/โล่/กลิ้งไม่ได้) ; host spawn `IceBlock(forPid)` |
| 1.5–2.0 | บอสหาย (Blink out) → โผล่ที่ `victim + normalize(victim − bossเดิม)×3` หันหน้าเหยื่อ (หลังบอส=แกนอยู่ไกลเหยื่อ) clamp ในห้อง | – |
| 2.0–3.0 | (ถ้า N≥2) คลื่นวง r 0→14 จากบอส; ผู้เล่นอื่น (ไม่ใช่เหยื่อ) ที่ยังไม่ผ่านขอบวงคลื่น (r_คลื่น ≥ ระยะ) → `fx=Area|Freeze 2.5s`; หลบด้วยกลิ้งอมตะ/หลังเสา (เสาบังคลื่น: ตรวจเส้นตรงบอส→ผู้เล่นตัดวงกลมเสา); คนเดียวข้ามขั้นนี้ | กลิ้ง/หลบหลังเสา |
| 3.0–6.0 | บอส Clench, **super armor** (poise ปิด), เปิด `BossCore` (proxy ที่ด้านหลังบอสห่าง 1.4 ม. r 1.0) + ลูกศรทอง `#F5C25A`; น้ำแข็งเหยื่อหนาขึ้น | ทางรอด: ดูด้านล่าง |
| 6.0 | ไม่มีใครขัด → ทำลายก้อน; เหยื่อรับ `fx=Execute`: เฟส 1-2 ดาเมจ 75% MaxHp **ไม่ลด 30% จาก Frozen** (ข้อตัดสิน) ; เฟส 3 → HP 0 ทันที (เล่นคนเดียว=จบรอบ; co-op=Down ชุบได้) | – |
ทางรอด (ตัวเลขใน `BossData`):
1. **หลุดน้ำแข็ง** คะแนน ≥ **24**: เหยื่อกดรัว (Space/คลิกซ้าย/J) 1 แต้ม/ครั้ง (**คนเดียว 1.5**); เพื่อนฟันโดน `IceBlock` 1 ครั้ง = 6 แต้ม (4 ครั้ง=หลุด). เจ้าของนับแต้มของตัวเอง + รับ `Boss.Unfreeze(points)` ที่ host ส่งเมื่อ `IceBlock.TakeHit` ; ครบ → ปลดเองทันที แล้วส่ง `Boss.Mash(total, released)` ให้ host; ผล: ไม่ดาเมจ, อมตะ 1 วิ, บอสชะงัก 0.5 วิ
2. **ทีมตีแกน**: `BossCore.TakeHit` ส่งดาเมจ×2 เข้าบอสและสะสมในบัญชีแกน; ครบ `DpsRef×3.6×(N−1)` (72/144/216; N≥2) → ปล่อยเหยื่อ, บอสชะงัก 3 วิ, **ดาเมจเข้าบอส ×2 ตลอดชะงัก**
3. **หลบวงแรก** (ตารางบรรทัด 0–1.5)
- ขัดจังหวะสำเร็จ (ชะงัก/หลุด) → เหยื่ออมตะ 1 วิ; หลังโดนท่า เหยื่อ Frozen อมตะ-ซ้ำ 4 วิ (กฎ Frozen)
- คลื่นกับ Frozen อื่น ห่างกัน ≥4 วิ (การ์ด "ไม่มีจังหวะแช่ทุกคนพร้อมกัน"): ตรวจในเทสต์ B8

**Absolute Zero** — tele 3.0 วิ: บอสลอยขึ้น 2.5 ม., ห้องมืดลง+วงแหวนน้ำแข็งใหญ่; host สุ่มวงปลอดภัย 2 วง r 5 ม. (ศูนย์กลางห่างกันตามมุม θ ที่ล็อก ระยะ ≥12 ม.; ส่ง `pointA/pointB`), 3 วิ→ย่อเหลือ r 3 ใน 1 วินาทีสุดท้าย; `hitAt` 3.0 (+ผ่อน): ผู้เล่นนอกทั้งสองวง → 40% + Frozen (ข้ามถ้าอยู่ในช่วงอมตะ-Frozen); ครั้งแรกในเฟส 3 ทำลายเสา 4 ต้น (ปิด dynamic obstacle + เอฟเฟกต์แตก, บิตมาสก์ใน extra) ; เสียงชาร์จ 3 วิ

### 4.6 `StatusEffects` (บนอัศวิน; เครื่องเจ้าของเป็นผู้ตัดสิน)
- สถานะ: `chill` 0..3, `chillTimer` (ลด 1 ชั้นทุก 3 วิ ถ้าไม่โดนเพิ่ม), `frozen` (วินาทีเหลือ), `judge` (แต้มปัจจุบัน/ต้องการ 24 หรือ 0), `frozenImmune` (4 วิ), `introLock`
- `AddChill(n)`: ชั้น+n; ถึง 3 → `Freeze(2.0)` และ chill=0; ขณะอมตะ-Frozen (4 วิ) ชั้นไม่เกิน 2 (ข้อตัดสิน); `ClearAll()` ตอนเปลี่ยนเฟส/Down/ชุบ/จบรอบ
- `Freeze(seconds)`: `Knight.CancelAction()` + `CancelEat()`; `Locked=true` ปิด `canInput`; ดาเมจที่รับ ×0.7; ภาพ: ก้อน `#BFE6F2` โปร่ง (`Prims.Glow`/Tint) คลุมตัว
- `Mash()`: เรียกจาก `Knight.Update` ตอนล็อกเท่านั้น; นับ **ขอบกด** ของ Space / เมาส์ซ้าย / J (ปุ่มละ edge; รวมไม่เกิน 25 ครั้ง/วิ); Frozen ปกติ −0.15 วิ/ครั้ง, Judgment +1 (คนเดียว +1.5) แต้ม; ส่ง `Boss.Mash` ทุก 0.1 วิ (เฉพาะ Judgment)
- `MoveMult = 1 − 0.15×chill` (ใช้กับเดิน/วิ่งใน `Knight.Simulate` เท่านั้น ไม่รวมกลิ้ง/ก้าวท่าฟัน ตรงกติกา SPEED)
- `NetByte` (ส่งไปกับ state): bit0-1 = chill, bit2 = frozen, bit3 = judgment-frozen/marked, bit4 = ล็อกเปิดตัว → ผู้ชมวาดเกล็ด/ก้อน; ผู้ชมไม่คำนวณเอง
- ไม่เซฟ ไม่ข้ามรอบ

### 4.7 `OrbitingCrystals`, `BossShots`, `TargetSelector`, `IceGolem`
- **OrbitingCrystals**: ผลึก 6 เหลี่ยม (`Prims.Lathe` 6 ด้าน) r 2 ม. สูง 2 ม. มุม `a_i = base + 60°/วิ×t + i×120°` (ฟังก์ชันล้วนของเวลา → ตรงทุกเครื่อง); `Count` = 3/2/1 ตามเฟส (`Break(i)` ใน Transition); Orbit ดึงชิ้นออก
- **BossShots**: พูลเมช ≤24; static `Trace(origin, dir, maxLen, circles) → length` (ใช้ทั้ง host ตัดสินและ client วาด)
- **TargetSelector**: ฟังก์ชัน static ล้วนรับ `IList<Knight>`/พิกัด: `Farthest(from)`, `NearestInRange(from, r)`, `Isolated(excludePid)` (ระยะรวมไปหาเพื่อนมากสุด), `BlinkPoint(points, players, bossPos)` (ไกลผู้เล่นส่วนใหญ่/หนีการรุม), `Weighted(candidates, recent)` — ใช้ `System.Random` ที่ส่งเข้ามา; คนที่ `Dead` (Down/Fallen) ถูกข้ามเสมอ
- **IceGolem** (`Enemy`, NetType 6): HP 8% HP บอส, ความเร็ว 60% ผู้เล่น, AI: ยืนบนเส้นระหว่างผู้เล่นที่ใกล้ที่สุดกับบอส ห่างบอส 3 ม.; ท่าทุบพื้นวง r 2.5 ม. tele 1.0 วิ ดาเมจ 20% (หนัก ปัดไม่ได้ บล็อกได้); `expReward=0`, `soulReward=0`; ตาย → เศษน้ำแข็ง + `SlickZone` r 2.2 ม. 5 วิ (ลงทะเบียนในลิสต์ static ทุกเครื่องจากเหตุการณ์ตาย — ตำแหน่งเดียวกัน เวลาเดียวกัน ±100 ms ไม่ต้องส่งเพิ่ม); ใช้ `Obstacles.Enemies`

### 4.8 `BossRoom` และ `Obstacles` dynamic
- ศูนย์ C = `MapLayout.Lair`; สเกล `k = min(1, (clearance − 1)/26)` โดย clearance จาก bake (`snow_meta.lairClearance`; ทุ่งทดสอบใช้ค่าคงที่ 21.66 เพื่อให้ตัวเลขเท่าของจริง); ผังตาม design ข้อ 3 คูณ k (กำแพงวง r=26k, เสา r=12k สูง 4 ม. วงชน r 1.4 ม. (**ไม่สเกล**), เทเลพอร์ต P0 กลาง + 4 มุม r=16.8k, บัลลังก์ `(0,+21k)` ด้านบนจอ)
- สร้างครั้งเดียวเมื่อ `BossController.Awake` ของ **ทุกเครื่อง** (host จริง/client puppet) เรียก `BossRoom.Ensure(center)` ตั้งแต่ spawn — ไม่ขึ้นกับ biome ที่ client จึงใช้กับ `-boss kryos` บนทุ่งได้ และทุกเครื่องได้ผังเดียวกันจากค่าคงที่
- ของที่ชน: เสา 4 (วงกลม dynamic Both) ; กำแพงวง 8 ส่วน (กล่องหมุน dynamic, ชั้น Both) เปิดเมื่อ `State >= Intro` ปิดเมื่อบอสตาย/ABANDONED/wipe/`Teardown`; กำแพงน้ำแข็ง (กล่อง dynamic 1 อัน); หนามหาง (วงกลม dynamic) — สถานะเปิด/ปิดอ่านจากฟิลด์ใน `ReadExtra` (บิตมาสก์เสา, `wallUp`, wall center/yaw/เวลาเกิด) ไม่ส่งแยก
- `Obstacles` dynamic: เพิ่ม `AddCircleDyn/AddBoxDyn` คืน id (index ใน `items`), `SetOn(id, bool)`; `Push`/`NearestCircle` ข้ามที่ปิด; การเก็บใน `cells` เหมือนเดิม; **ห้ามเปลี่ยนความหมายของ `Add*` เดิม**
- `GroundY(x,z)`: ตะเข็บเดียว (ตอนนี้ 0; ขั้น 12 เรียก `Ground.Y`)
- พื้น: ใช้พื้นของ bake/ทุ่ง; เพิ่มวงเวทย์ ลายพื้น เป็น flat mesh ทึบ (ไม่ alpha ตามกติกาแมพ)
- ลบสไลม์: ตอนตื่น ลบสไลม์ในห้องเงียบๆ และ `UpdatePacks` ถือว่า "ปลอดภัย" ในรัศมี 34k รอบ C ขณะบอสตื่น
- ทุ่งทดสอบ: ลานรัง Meadow มีเสาโบราณ r=21 (วงกลมชน 14 ต้น) อยู่ภายในห้อง — ยอมรับเป็นสิ่งกีดขวางเพิ่มในต้นแบบ (ไม่แก้ฉากทุ่ง)

### 4.9 `BossUi` / HUD
- canvas แยก (sortingOrder สูงกว่า HUD, แบบ `MapUi`), อ้างอิง 1280×720 ตาม design ข้อ 7: กรอบ `#0E1224` ขอบ `#3A2A5E`; แถบ HP 560×18 px y≈58 ไส้ `#8FD0E4` ghost 0.5 วิ `#C9D8E8` (เทาเมื่ออมตะ `#6E6A78`); poise 560×4 `#F5C25A` กะพริบเมื่อหมดล็อก; ขีดเฟส 60%/25%; ไอคอนผลึก 3 ดวง 12 px (`#5FD8F0` อยู่ / `#6E6A78` แตก); ชื่อ "KRYOS - FROST LORD"
- โชว์เมื่อผู้เล่นใกล้บอส ≤45 ม. หรือบอสตื่นแล้ว; Judgment: ข้อความกลางจอสูง "FROZEN JUDGMENT" 40 px `#5FD8F0` + ขอบจอวูบ + "<ชื่อ> IS MARKED" ; แถบ mash เหยื่อ 320×16 y≈560 "MASH SPACE!" (ซ่อนเมื่อหลุด); เกล็ด Chill 3 ช่องข้างแถบ stamina (ของตัวเอง) + เกล็ดลอยเหนือหัวทุกคน (วาดตามไบต์สถานะ); Frozen = ก้อนคลุมตัว + แถบเวลาเล็ก
- banner: ใช้ `Hud.Banner` เดิม (เปิดตัว, "PHASE 2/3", ตาย) ; popup `!` ผ่าน `Hud.Popup`

### 4.10 สเกลห้องกับลานที่ bake ได้
- ค่าปัจจุบัน clearance 21.66 → `k=0.795`: กำแพงวง r 20.7, ลานเล่น R 19.1, เสา r 9.5, เทเลพอร์ตมุม (±9.5, ±9.5), บัลลังก์ (0,+16.7), Nova 6.5/Tail 7/คลื่น 14 ม. **ไม่สเกล** (ตามการเล่น) ; AZ วงปลอดภัยต้องอยู่ในลานเล่นพร้อมขอบ 1 ม. (วางศูนย์กลางที่ระยะ ≤ R−6)
- ถ้า artist เปิดลานได้ r ≥ 27 ม. → `k=1` ไม่ต้องแก้โค้ด (สูตรเดียว)

### 4.11 `BossBody` / `BossAnim`
- Fallback chain: `FileBody` (ถ้ามี `Models/Boss/kryos` และ node ครบตามสัญญาข้อ 6) → `CodedBody2` (ประกอบจาก `Prims.Box/Lathe/Cone`: ชายเสื้อคลุมกรวย, ลำตัว, หัว+เขา+มงกุฎ, แขน, คทา, แกนหลัง, ผลึก, หาง 7 ท่อนกล่อง; ~600 tri พอให้เงาอ่านได้) → `CapsuleBody` (ขั้น 4: แคปซูลสูง 3.6 ม. + ลูกบาศก์ชี้ทิศ + แท่งยาวด้านหลัง)
- ทุก body เปิดชิ้นส่วนเป็น `BossRig` (Transform ที่อาจ null: `Root, Body, Head, ArmL, ArmR, StaffMount, Core, Tail[7], RobeBack[3], Hair[3], CrystalMount`) โค้ดอนิเมชันเขียนแบบ null-safe → ใช้ชุดคลิปเดียวกับทุก body (capsule ใช้แค่ Root/Body)
- `BossAnim`: ตาราง `Clip{ name, length, loop, Key[] }`, `Key{ t, ease, PoseKey }` โดย `BossPose` = ค่าไม่กี่ตัว (lean, hover, headYaw, armR pitch/yaw, armL pitch/yaw, robeFlare, tailCurl, coreOpen, scale) ; 16 คลิปตาม design (Intro 3.5, IdleHover loop, Glide loop, CastShoot, Sweep, RaiseStaff, Nova, Clench loop, BlinkOut, BlinkIn, AbsoluteZero, Stagger, PhaseTransition, Death 3.0, TailWindup, TailLashRecover) ; ประเมินด้วยฟังก์ชันล้วน `Eval(clip, t)` (เทสต์ได้); หาง/ผม/ชายเสื้อโบกด้วยสปริงตามชิ้นก่อน (ไม่สุ่ม)
- `hitAt` ใช้จาก `BossData` ไม่ใช่ Animation Event ; ไม่มี Animator/rig/skinning (ชิ้นแยกขยับด้วยโค้ด)
- ตัวแทนอื่น (ก้อนน้ำแข็ง เสา กำแพง Golem ผลึก): แต่ละอันมี "ร่างโค้ด" (Prims) เป็นค่าเริ่ม และสลับเป็น .fbx เมื่อไฟล์ถูกส่งมา

## 5. ปะทะกับผู้เล่น: กลิ้ง / parry / clash / ฟัน / โล่
### 5.1 ลำดับใน `Knight.TakeDamage(damage, from, source, knockback, by, heavy)` ที่แก้ (เพิ่มพารามิเตอร์ `HitFx fx` ค่าเริ่ม `None`)
1. `Rolling` → ผ่านทุกท่า (เดิม "DODGE") — **ไม่ติด Chill/Frozen** ; `Invulnerable`/Glowing → ผ่าน
2. คำนวณ `facing`
3. parry/clash เดิม **เฉพาะ** `by != null && !heavy && !fx.Area`; clash เพิ่มเงื่อนไข `by.AllowsClash`
4. บล็อก: **ข้ามถ้า `fx.Area`** (โล่ไม่ช่วยท่าพื้นที่); มิฉะนั้นเดิม (80%+ ตัดดาเมจ, stamina 10)
5. ลดดาเมจ `RESISTANCE`, Frozen ×0.7 (ยกเว้น `fx.Execute`) ก่อน armor
6. ถ้า `!blocked` และ `fx.chill > 0` → `status.AddChill(chill)`; ถ้า `fx.Freeze` (AZ/คลื่น) และไม่อยู่ใน `frozenImmune` → `status.Freeze(sec)`; `fx.Execute` เฟส 3 → `hp = 0`
7. แรงผลัก = `sqrt(60 × เมตร)` ม./วิ (3 ม.→13.4, 4 ม.→15.5) เพราะ `knockVel` ลดที่ 30 ม./วิ² ; บล็อกคูณ 0.35 (เดิม)
### 5.2 `HitFx` (struct ธง ไบต์เดียวไปกับ `KnightHit`): bit0 หนัก (เดิม), bit1 พื้นที่, bit2-3 chill 0-3, bit4 Freeze ปกติ, bit5 Judgment-freeze, bit6 Execute; ค่า Freeze วินาทีคิดจากชนิด (AZ=2.0 ผ่านกฎ Frozen, คลื่น=2.5) ไม่ส่งเป็นตัวเลข
### 5.3 ผู้เล่นตีบอส
- ฟัน 3 ท่าเดิมผ่าน `HitArea` → `Game.HitEnemy` → `BossController.TakeHit` (host) / `Net.SendHit` (client) โดยไม่แก้ `HitArea`; ก้อนน้ำแข็ง/แกน/กำแพง/Golem ก็ถูกตีด้วยทางนี้เพราะเป็น `Enemy`
- `ClientHit` ตรวจ `b.magnitude − e.radius > 9` ระยะ → ตัวแทนที่ยาว (กำแพง) ต้อง radius พอเหมาะ (8 ชิ้น r 1.7) และ `BossCore` ต้องอยู่ ≤9 ม. จากจุดตี
- ผู้เล่นไม่ตีกระสุนบอส (ไม่ทำ)
### 5.4 สรุปรับมือต่อท่า (ตรวจในเทสต์)
| ท่า | กลิ้งอมตะ | บล็อก | parry | clash | หมายเหตุ |
| --- | --- | --- | --- | --- | --- |
| Bolt / Orbit | ผ่าน | ลดดาเมจ ไม่ติด Chill | ปัดนัดนั้น ไม่ให้ poise | ไม่ได้ | |
| Sweep / Tail | ผ่าน | ลดดาเมจ | ได้ + poise 40% | ไม่ได้ | Tail ฟาด 2 ครั้ง = 2 หน้าต่าง |
| Nova | ผ่าน | **ไม่ช่วย** | ไม่ได้ (หนัก) | ไม่ได้ | ออกวง |
| Wall / AZ / คลื่น / Judgment | ผ่าน (ถ้ากลิ้งตรงจังหวะ) | ไม่ช่วย | ไม่ได้ | ไม่ได้ | |
| Golem ทุบ | ผ่าน | ได้ | ไม่ได้ (หนัก) | – | |

## 6. ระบบโหลดโมเดล .fbx ผ่าน `ModelLib` + สัญญา node
- วางที่ `Assets/MedievalKnight/Resources/Models/Boss/` : `kryos.fbx`, `kryos_tail.fbx`, `kryos_staff.fbx`, `kryos_crystal.fbx`, `ice_golem.fbx` ; (ไม่บังคับ) `ice_pillar.fbx`, `ice_wall.fbx`, `ice_block.fbx`
- โหลดด้วย `ModelLib.Spawn("Models/Boss/kryos", parent)` (ได้ `Strip` คอลลิเดอร์/สคริปต์ให้) → ตรวจ node ด้วย `ModelLib.FindNodes` → ถ้า **ไฟล์ไม่มี หรือ node ขาด หรือ tri เกินเพดาน** ให้ log เตือนครั้งเดียว (`MODELLIB:` เดิม) แล้วถอยไป body ถัดไปทั้งตัว (ไม่ผสมครึ่งๆ) ; ไม่ throw
- สี: ไม่มี texture; ชื่อ material ใน .fbx เป็นรหัสสี → `ModelLib.Recolor(inst, picker, fallback)` แปลงเป็น `Prims.Lit`; ชื่อลงท้าย `_glow` → `Prims.Glow` (emission ตา/ผลึก/แกน) — พาเลตต์ตาม style.md: `kryos_robe #1B2A4E`, `kryos_robe_trim #3A2A5E`, `kryos_armor #0E1224`, `kryos_armor_pattern #3A2A5E`, `kryos_fur #6E6A78`, `kryos_fur_tip #C9D8E8`, `kryos_skin #6E6A78`, `kryos_hair #8FD0E4`, `kryos_crown #6FB8D6`, `kryos_horn #0E1224`, `kryos_horn_tip #3A2A5E`, `kryos_ice #8FD0E4`, `kryos_ice_tip #BFE6F2`, `kryos_gem_glow/kryos_eye_glow/kryos_core_glow #5FD8F0`; หาง `tail_scale_a #1B2A4E`, `tail_scale_b #3A2A5E`, `tail_spike #8FD0E4`, `tail_spike_tip #BFE6F2`, `tail_groove_glow #5FD8F0`; คทา `staff_wood #3A2A5E`, `staff_claw #6FB8D6`, `staff_crystal_glow #5FD8F0`; Golem `golem_stone #6E6A78`, `golem_stone_b #958A80`, `golem_crystal #8FD0E4`, `golem_eye_glow #5FD8F0`
- **สัญญา node (ชื่อต้องตรง ตัวพิมพ์ตามนี้; หน่วย 1 = 1 ม., หันหน้า +Z, pivot ราก "ใต้ชายเสื้อ" บนพื้น)**
  - `kryos.fbx` ราก `Kryos`: `Body` (pivot เอว ~y1.2 ใช้เอียง), `Robe_Front`, `Robe_Back_0/1/2` (pivot บานพับบนสุด), `Head` (pivot คอ), `Hair_0/1/2`, `Shoulder_L/R`, `Arm_L_Upper/Arm_L_Lower/Hand_L`, `Arm_R_Upper/Arm_R_Lower/Hand_R` (เป็นลูกต่อกันตามไหล่→มือ), `StaffMount` (empty ที่มือขวา, +Y ตามไม้), `BackCore` (+ลูก `BackCore_Ring`), `TailMount` (empty ที่เอวหลัง), `CrystalMount` (empty กลางตัวสูง ~2 ม.), `EyeGlow` ; ตัวมงกุฎ+เขาอยู่ใต้ `Head`
  - `kryos_tail.fbx` ราก `Tail`: `Tail_0` … `Tail_6` ซ้อนเป็นลูกต่อกัน (pivot ที่ข้อต่อด้านใกล้โคน, เหยียดตรงไปทาง **−Z**), `Tail_Blade` เป็นลูกของ `Tail_6`
  - `kryos_staff.fbx` ราก `Staff`: จุดจับที่ origin, +Y ตามไม้, `Staff_Tip` (empty ปลาย = จุดปล่อยกระสุน), `Staff_Crystal`
  - `kryos_crystal.fbx` ราก `Crystal` pivot กลางชิ้น (ใช้ซ้ำ 3 ชิ้น + กระสุน)
  - `ice_golem.fbx` ราก `Golem` pivot ใต้เท้า: `Golem_Body`, `Golem_Head`, `Golem_Arm_L/R`, `Golem_Fore_L/R`
- เพดาน tri (นับด้วย `ModelLib.Triangles`, เทสต์ B14): บอสทั้งชุด (ตัว+หาง+คทา) ≤ 7,500; ผลึก ≤ 200; Golem ≤ 1,000; ไม่มีกล้อง/ไฟ/แอนิเมชันในไฟล์
- ไม่ใช้ `generate_3d` (artist ทำใน Blender ตาม requirement ข้อ 5)

## 7. แทน Minotaur เฉพาะโลกหิมะ (บอสโลก: ดรอป / portal / VICTORY ตาม loop เดิม)
- `BossKind { Minotaur, Kryos }` ตัดสินที่จุดเดียว `Game.BossKindFor(world)`: (ก) `Args.boss` ถ้าระบุ (ข) scripted/selftest/mptest ไม่ระบุ = Minotaur เสมอ (ค) ขั้น 12: ตาม `Biome` ของโลก (Snow→Kryos) — ใช้ค่า `runBiome` ที่แมพหิมะเก็บ/ส่งอยู่แล้ว (ผ่าน `Enter`) จึงไม่มีฟิลด์ใหม่ในเซฟ
- `SpawnActors` (เฉพาะ Authority): Kryos → `BossController.Spawn(MapLayout.Lair, actors, seed)`; จำนวนผู้เล่น n = `players.Count` ตอน spawn (หลัง `SpawnTeammates` เหมือน Minotaur); HP ผ่าน `BossData.MaxHp(n, worldHpMul)`; client ได้ puppet จาก snapshot (`SpawnPuppet` type 5)
- ตอนบอสตาย: `Enemy.TakeHit` → `Game.OnEnemyKilled(e)` → `e.IsWorldBoss` → `boss=null`, `minoRespawnAt=float.MaxValue`, `run.OnBossKilled(e.transform.position)` (portal ที่จุดตาย, ยืน 1.5 วิ = VICTORY; ออนไลน์นับ 15 วิ ผ่าน `Msg.Portal`/`Reached`/`HomeIn` เดิม) — ไม่แก้ `TeamRun`
- ดรอป/EXP: ชุดเดียวกับ Minotaur ตาม decisions ข้อ 2 — `expReward = 250 × Game.EnemyExpScale`, `soulReward = 50`, `RollDrop` เปลี่ยนเงื่อนไข `e is Minotaur` → `e.IsWorldBoss` (Horned King's Blade 100%, co-op หนึ่งเล่ม/คน เป็นวง; Meat ×2); Golem/proxy: EXP 0
- Death 3 วิ เล่นต่อหลัง portal เกิดทันที (เหมือน Minotaur ที่ portal เกิดตอนตาย) ; กำแพงวงปลดตอน `OnDeath`
- ข้อความ: objective "Defeat Kryos to return", portal "KRYOS HAS FALLEN. Portal open…", banner ตาย "KRYOS SLAIN!" ; โลกทุ่งยังใช้ข้อความ Minotaur เดิม
- `PushEnemiesFrom`/`OnPartyWipe`: ไม่เกี่ยว (ใน loop wipe=จบรอบ); โหมด `-arena` (สนาม v1) ไม่มี Kryos
- โลกเก่า: Kryos ใหม่เต็ม HP ทุกครั้งที่เข้า, `worldHpMul` 1.5 และ `worldDmgMul` 1.25 ใช้ตามข้อ 4.1

## 8. ซิงก์ co-op (host ถือ AI)
### 8.1 Protocol
- `Net.Protocol` ปัจจุบัน = 6 → ก่อนลงมือขั้น 8 **ให้เปิด `Net.cs` ดูค่าจริง** (หากแมพหิมะสำเร็จแล้วจะเป็น 7) แล้วบวก 1 (คาด 8); แก้ที่ `Net.cs` บรรทัดค่าคงที่ + คอมเมนต์ท้ายบรรทัดอธิบายการเปลี่ยนแปลง (เหมือน p5/p6) ; ห้ามบวกหลายรอบในงานเดียว ; `Args.netVersion` ตามค่าอัตโนมัติ
- รูปแบบที่เปลี่ยน (ทั้งหมดในครั้งเดียว): (1) `WriteKnight/ReadKnight` +1 ไบต์ (ไบต์สถานะ `StatusEffects.NetByte`) ท้ายสุด (2) `KnightHit` ไบต์ท้าย `heavy` → `fx` (bit0 เท่าเดิม) (3) `Msg.Boss` ใหม่ (4) `Enemy` type 5-9 ใน snapshot (ไม่เปลี่ยนโครง เพิ่มค่า `NetType` และ `WriteExtra` ของตัวเอง)
### 8.2 Snapshot extra ของบอส (`WriteExtra`/`ReadExtra`, ~40 ไบต์ × 15 Hz)
`State u8 | Phase u8 | move u8 | moveSerial u8 | t f32 | aimYaw yaw | pointA (x,z pos) | pointB (x,z pos) | arg f32 (เช่น รัศมีวง/มุม) | targetPid u8 | victimPid u8 | crystals u8 | pillarMask u8 | flags u8 (อมตะ, wallUp, coreOpen, poiseLocked, hover) | poise u8 (0-255) | wallCenter/yaw/spawnAge`
- client ที่เห็น `moveSerial` เปลี่ยน → เริ่มท่านั้นที่ `t` ที่ host ส่ง (ปรับ `t` ถ้าคลาดเกิน 0.2 วิ เหมือน Minotaur) ; ภาพของท่าเกิดจาก `(move, t, args)` เท่านั้น
- ตัวแทน `Enemy` (7-9) และ `IceGolem` (6) ใช้ `Enemy.WriteNet` มาตรฐาน + extra ของตัวเอง (`IceBlock`: forPid + แต้ม; `BossCore`: เปิด/ปิด; `IceWallPiece`: HP ร่วม%, หมายเลขชิ้น)
### 8.3 `Msg.Boss` (ไบต์แรก = subtype)
| ทิศ | subtype | ข้อมูล | ใช้เมื่อ |
| --- | --- | --- | --- |
| C→H | `Mash` | total f32, released u8 | เหยื่อ Judgment ส่งแต้มรวมทุก 0.1 วิ + ธงหลุดแล้ว |
| C→H | `FreezeAck` | ok u8 | เหยื่อตอบ ณ 1.5 วิ ว่าโดนแช่จริงหรือกลิ้งพ้น |
| H→C | `Unfreeze` | amount f32 (วินาที หรือแต้ม) | เพื่อนตี `IceBlock` → เจ้าของรับ (Frozen ปกติ −0.6 วิ/ครั้ง; Judgment +6 แต้ม) |
| H→C | `Release` | – | host สั่งปลด (ทีมตีแกนสำเร็จ/ท่าถูกขัด) |
(ใช้ `Net.ToAll`/`ConnOf(pid).Send` เหมือนข้อความ loop อื่น; ใส่ rate-limit ฝั่ง host: `Mash` ≤ 20/วิ/คน)
### 8.4 หลักการเวลาและความยุติธรรม (ping 150 ms)
- ภาพบอสบน client ถูกวาดย้อน `Net.EnemyDelay` (0.1 วิ) เหมือน Minotaur และ `KnightHit` ที่ระบุผู้ตี ถูกหน่วง 0.1 วิ (`Game.ApplyKnightHit`) → telegraph กับดาเมจบน client เลื่อนเท่ากัน ผู้เล่นเห็น telegraph ครบเวลา
- ฝั่ง host ตรวจรูปทรงของผู้เล่น **ระยะไกล** ที่ `hitAt + ResolveDelay` โดย `ResolveDelay = 0.1 + rtt/2` (rtt จาก ping ต่อ connection ถ้า `Net` มีให้ ไม่มีใช้ 0.1+0.075; เพดาน 0.25); ผู้เล่นฝั่ง host ตัดสิน ณ `hitAt` เลย; ค่านี้จูนได้ใน `BossData` (ถ้าเทสต์ ping 150 พบ "โดนทั้งที่กลิ้งทัน" > 10% ให้เพิ่ม)
- กลิ้ง/บล็อก/parry ตัดสินที่เครื่องเจ้าของเสมอ ; ไม่มี `UnityEngine.Random` ในเส้นทางภาพ; ของสุ่ม (เลือกท่า, ตำแหน่ง AZ) อยู่ host และส่งผลเป็นค่า
- ไม่ pause ออนไลน์: เปิดตัว/เปลี่ยนเฟสใช้ "อมตะ+ล็อกการเคลื่อนที่" ไม่ใช้ `Game.Paused`
### 8.5 ผู้ชม / เพื่อนล้ม
- เหยื่อ Judgment ที่ถูกฆ่า (Down) → `ClearAll()` สถานะ; `IceBlock` ถูกทำลายโดย host เมื่อ forPid ไม่ Frozen แล้ว/ตาย/ออก; Judgment ที่เหยื่อออกจากเกม → ยกเลิกท่า; ผู้เล่นหลุดกลางท่า → `TargetSelector` ข้ามคนที่ไม่อยู่ใน `players`
- wipe (ทุกคน Down/Fallen) = `HostWipe` เดิม → `ClearWorld` → `BossRoom.Teardown`; ผู้เข้ากลางรอบอยู่ฐาน ไม่เห็นบอส

## 9. ปุ่ม / UI
- **ไม่เพิ่มปุ่มใหม่** ใน `InputX.Map()` : ตอนแช่ใช้ Space / คลิกซ้าย / J เดิม (อ่านผ่าน `InputX` แบบ edge ใน `StatusEffects.Mash`), เมนู/Tab/M/F2 เหมือนเดิม; ตอน Frozen ปุ่มอื่นไม่มีผล
- ข้อความช่วยบนจอ: บรรทัดควบคุมใน `Hud` เพิ่ม 1 บรรทัด "Frozen: mash Space / Click / J" (ข้อความอังกฤษ) ตอนแช่ ; `reference/controls.md` อัปเดตตอนจบ
- รายการ UI อื่นตามข้อ 4.9; ไม่มีเมนู/ตั้งค่าเพิ่ม

## 10. เสียง (ต้องเพิ่มใน `enum Snd` + `Sfx.cs`)
- ปัญหา: `Sfx` เล่นได้แต่ mp3 → เพิ่มตัวสังเคราะห์ `SfxSynth` (ระบุเป็นข้อตัดสิน) : `AudioClip.Create` + `SetData` ที่ 44.1 kHz โมโน; สูตรต่อเสียง = ลำดับ (ออสซิลเลเตอร์ sine/saw/สี่เหลี่ยม + noise จาก `System.Random(เมล็ดคงที่ต่อเสียง)` + envelope ADSR + ฟิลเตอร์ low-pass ง่ายๆ + sweep ความถี่) ดังนั้นสร้างซ้ำได้ตรงกันทุกเครื่อง; สร้างแบบ lazy ครั้งแรกที่เล่น (AZ 3 วิ ~130k ตัวอย่าง ใช้เวลาไม่กี่ ms) และมี hook เทสต์ `SfxSynth.Hash(snd)`
- enum ต่อท้ายตามลำดับ (ต้องตรงกับตาราง `Files/BaseVolume/PitchVar/MinGap`): `BossRoar, BossPhase, BossDeath, BossBolt, BossOrbit, BossSweep, BossTailWhip, BossBlinkOut, BossBlinkIn, BossSummon, BossNova, BossWallRise, BossWallBreak, BossAbsZero, JudgmentWarn, FreezeOn, FreezeBreak, MashTick, ChillApply, GolemSlam, GolemShatter, CoreHit, PoiseBreak` (23 ค่า; `Files[i]` = null → ใช้ synth)
- โครงสร้างที่ต้องแก้ใน `Sfx.cs`: ขนาดอาร์เรย์ตามจำนวน enum; `lastTime` เติม −10 ด้วยลูป; `clips` หลังช่อง 6 โหลดจาก synth ; `Play` เหมือนเดิม
- สูตรคร่าวๆ (จูนทีหลัง): Roar เสียงต่ำ saw 70→45 Hz 1.2 วิ+noise; Phase คอร์ดสามเสียง 0.9 วิ; Death ลงต่ำ 2 วิ+noise แตก; Bolt/Orbit/Sweep/TailWhip = noise band-pass sweep 0.25–0.5 วิ (TailWhip มี thud ต่ำท้าย); BlinkOut/In = sine sweep ลง/ขึ้น 0.3 วิ; Summon swell 1 วิ; Nova boom 0.6 วิ; WallRise/Break noise กรอบ; AbsZero ชาร์จไต่ขึ้น 3 วิ; **JudgmentWarn = 2 โทนไล่ขึ้นเด่นชัด (เช่น 660→880 Hz) ดังไม่ขึ้นกับระยะ**; FreezeOn/Break กรอบแตก; MashTick คลิกสั้น 0.05 วิ; CoreHit chime สีทอง; PoiseBreak 0.35 วิ
- ใช้ซ้ำของเดิม: parry/block = `Snd.Clash` (pitch เดิม), ฟัน/โดนบอส = `Slice/Swing`
- เสียงบอสใช้ `volume` เต็มทุกเครื่อง (ห้องเล็ก) ยกเว้นเสียงของเพื่อนใช้ `RemoteVolume` เดิม ; ตัดเสียงพูดและเพลง 3 layer ออก (decisions ข้อ 5) → ลง `Docs/backlog.md` ว่ารอไฟล์จากพีท
- ถ้าพีทไม่เอา synth: ทางเลือกคือใช้ 6 mp3 เดิมปรับ pitch/volume (คุณภาพต่ำกว่ามาก) — ดูข้อตัดสิน

## 11. เซฟ / โลกเก่า ต้องไม่พัง
- **ไม่มีข้อมูลบอสในเซฟ**: HP/เฟส/สถานะไม่ถูกบันทึก (บอสเกิดใหม่เต็มทุกครั้งที่เข้าโลก) ; `LoopSave.Version` ยัง 1; ไม่เพิ่มฟิลด์; เซฟเก่า/ใหม่เปิดข้ามกันได้เหมือนเดิม
- ผลต่อโลกเก่า: ตายกลางสู้ Kryos → ของที่พกเป็นกอง ณ จุดตายเหมือนเดิม (ในห้องบอส: ตำแหน่งกองต้อง `PileSpotFree` ซึ่งตรวจ `Obstacles.Push` — กอง **ต้องไม่เกิดทับเสา/กำแพงน้ำแข็ง/หนาม** : `PilePoint` จะหาจุดว่างใกล้เคียง; หลัง `Teardown` obstacle dynamic ถูกลบ จุดกองในห้องเข้าได้) ; กองที่อยู่นอกกำแพงวงยังเก็บได้เมื่อกำแพงเปิดใหม่ (กำแพงปิดเฉพาะตอนบอสตื่น)
- โลกเก่าหิมะที่ตายใน Kryos: เข้าใหม่ → Kryos เต็ม HP ×1.5 ดาเมจ ×1.25 (ไม่มี "ครึ่งทาง")
- ABANDONED (ปิดเกม/Esc กลางสู้): กำแพงปลดเมื่อเริ่มโลกใหม่ (ฉากถูกทำลาย) — ไม่มีสถานะค้างข้ามเซสชัน
- โลกเก่าเดิม (ทุ่ง) ไม่เปลี่ยน; `Sanitize` ไม่ต้องแก้
- ตัวเลขใหม่ทั้งหมดเป็นค่าคงที่ในโค้ด (ตามแนวเกม ไม่มี config)

## 12. แผนเทส
### 12.1 selftest ใหม่ `-bossonly` (~3 นาที) ใน `BossSelfTest.cs` (เฉพาะ logic + ฉากบอสแบบสคริปต์ ไม่ต้องมีคนเล่น)
| รหัส | ตรวจ |
| --- | --- |
| B1 ข้อมูล | `MaxHp` n=1..4 = 6000/9600/13200/16800; `PoiseMax` = 128/205/282/358; โลกเก่า ×1.5; เกณฑ์เฟส 60/25; ท่า 10 ท่าครบ `hitAt<=end`; telegraph ท่าที่ต้องหลบ ≥0.6 วิ; rest 1.5/1.0/0.7 |
| B2 TargetSelector | ไกลสุด/ใกล้สุด/โดดเดี่ยว (ไม่ซ้ำคน) / ข้ามคน Down / คนเดียวเลือกตัวเอง / ทำซ้ำด้วย seed เท่ากันได้ลำดับเท่ากัน |
| B3 StatusEffects | Chill ซ้อน 3 → Frozen; ลด 1 ชั้น/3 วิ; MoveMult; mash −0.15 วิ; อมตะ 4 วิ (ชั้นไม่เกิน 2); ล้างตอนเฟส; Frozen ล็อก `canInput`; ดาเมจ ×0.7; Judgment 24 แต้ม (คนเดียว 1.5/ครั้ง) |
| B4 AI ล้วน | จำลอง 20 นาทีเวลาเกม (`DebugStep(dt)`) กับผู้เล่นตัวแทน: ไม่มีท่าเดิมติด >2; Tail ไม่ติด 2 ครั้ง; Judgment ≤1/เฟส ไม่ซ้ำเหยื่อ ≥20 วิหลังเข้าเฟส; Summon ไม่เกิน cap; ท่าไม่ซ้อนกัน |
| B5 ปะทะ | สั่ง `DebugMove` ทีละท่าใส่อัศวินทดสอบ: ดาเมจ % ตรง; กลิ้งผ่านทุกท่า; บล็อกลดแต่ **Nova/Wall/AZ โล่ไม่ช่วย**; Bolt บล็อกไม่ติด Chill; parry Sweep/Tail → poise +40%; clash กับบอส = ไม่เกิด; Judgment เฟส 3 ประหาร hp=0; Frozen ไม่โดน Chill ซ้ำ |
| B6 poise/ชะงัก | เกจเต็มตอน telegraph = ยกเลิกท่า ชะงัก 2 วิ ×1.25; ล็อก 8 วิ; ไม่ชะงักตอน Judgment/Transition; hp ถูกล็อกที่เกณฑ์เฟส (ตีทีเดียวไม่ข้ามเฟส) |
| B7 stamina | จำลองตารางท่า: ในหน้าต่าง 4 วิ ผลรวม `rollDemand` ≤3 |
| B8 Judgment | ครบ 3 ทางรอด: กลิ้งพ้นวง=ยกเลิก; เพื่อนตีก้อน 4 ครั้ง (×6=24) หลุด; ทีมตีแกนถึง 72/144/216 → ชะงัก 3 วิ ×2; ไม่มีใครขัด → 75% / HP0; คนเดียวข้ามคลื่น; คลื่น+Frozen ห่าง ≥4 วิ; เหยื่อไม่ซ้ำ |
| B9 AZ | วง 2 อัน ห่าง ≥12 ม.; ย่อเหลือ 3 ม.; นอกวง 40%+Frozen ในวง 0; เสาแตกครั้งแรก |
| B10 ห้อง | ผังตามสเกล k; กำแพงวงปิด/เปิด (ผู้เล่นไม่ผ่าน/ผ่านได้); ดึงคนนอกวงเข้า; เสา/กำแพงน้ำแข็ง/หนามเป็นสิ่งกีดขวางและถูกถอด; `Teardown` คืน `Obstacles.Count` เท่าก่อนสร้าง; ไม่มีสไลม์เกิดในห้อง |
| B11 loop | ฆ่า Kryos → ดรอป Horned King's Blade + Meat ×2, EXP 250×scale, Soul 50, portal เกิด, 1.5 วิ = VICTORY, ตายกลางสู้ = YOU FELL + OldWorld ที่จุดตาย; **ฆ่า Minotaur ทุ่งยังเหมือนเดิม** (เคส loop เดิมทั้งหมดผ่าน) |
| B12 co-op ใน process เดียว | puppet ที่ `ReadExtra` จากไบต์ของ host ได้ state/เฟส/telegraph เหมือน (เทียบ hash ของ `(move,t,args)`) |
| B13 เสียง | ทุก `Snd` ใหม่มีคลิป ยาว >0 มีสัญญาณ (RMS>0) ซ้ำแล้ว hash เท่าเดิม; ลำดับ enum = ตาราง; 6 คลิปเดิมโหลดได้ |
| B14 โมเดล | `-bossmodel capsule/coded/file`: สร้างได้ไม่ throw; ไม่มีไฟล์ → fallback + เตือนครั้งเดียว; tri ≤ เพดาน; node ขาด → fallback ทั้งตัว; ชิ้น null-safe ในทุกคลิป |
| B15 การ์ดข้างเคียง | เมื่อบอสเป็น Minotaur ไม่มี Kryos/BossRoom/ไฟล์ใหม่ใดทำงาน (ตัวเลข Minotaur เดิมทุกค่า) |
- selftest เต็ม (~7 นาที) + `-looponly` + `-slashonly` + `-p7only` ต้องผ่านเหมือนเดิมหลังขั้น 2, 3 และก่อนปิดงาน (ขั้นที่แก้ `Knight/Game/Enemy/Sfx` ถือว่าเสี่ยง regress)
### 12.2 ออนไลน์
- `mptest.ps1` (v1 arena) + `mptest-loop.ps1` เดิมต้องผ่าน (protocol ใหม่)
- `mptest-loop` เพิ่มส่วนที่ 11 "ice boss" (host `-boss kryos`, A B C เข้าร่วม ~60–90 วิ): ทุกโปรเซสรายงาน state/เฟส/ผลึก/เสาเท่ากัน; client เริ่ม telegraph ภายใน ±0.25 วิ ของ host; host สั่ง Judgment → client เหยื่อเห็น MARKED/แถบ mash, `Mash`→host ครบ 24 แล้วหลุด; B ตีก้อน 4 ครั้งของ A → A หลุด; ทีมตีแกน → ชะงัก; AZ: คนนอกวงโดน Frozen, คนในวงไม่โดน; เพื่อนชุบ F ระหว่างสู้; ฆ่าบอส → portal ทุกเครื่อง + ดรอปต่อคน; wipe → OldWorld; D เข้ากลางรอบอยู่ฐาน ไม่เห็นบอส ; สวิตช์ `-Boss kryos` ใน `.ps1` (ค่าเริ่ม Minotaur)
- **ทดสอบ ping**: เพิ่ม `-netlag <ms>` (Args) → ใน `Net.Poll` เก็บ `EvMessage` ไว้ในคิวหน่วงจนครบ `lag/2` ทั้งสองฝั่ง (RTT = lag) ใช้เฉพาะ scripted; รัน `mptest-loop.ps1 -Boss kryos -NetLag 150` ตรวจ: telegraph เห็นก่อนดาเมจ ≥ (tele − 0.1 วิ); อัตรา "โดนทั้งที่กลิ้งทัน" ≤10%; ไม่ desync ของเสา/กำแพง/เฟส; ผลึกในมือผู้ชมตรง ±1 ชิ้น
### 12.3 มือ/ตา (ด่านพีท)
- ด่าน A (หลังขั้น 4): เฟส 1 บน capsule เล่นคนเดียวจบใน ~2 นาที อ่าน telegraph ได้ ไม่ตายถ้าหลบถูก — ตามเช็กลิสต์ design-moves ข้อ 8.3 (ส่ง zip + บันทึกสิ่งที่ต้องจูน)
- ด่าน B (หลังขั้น 7): ครบ 3 เฟสคนเดียว เวลาสู้ 4–6 นาที
- ด่าน C (หลังขั้น 8): ออนไลน์ 2–4 คน ที่ ping 150
- ภาพ: ถ่ายห้องบอส/ท่าสำคัญ (Nova, Wall, Judgment, AZ) เทียบ design ในขั้น 11 ส่งพีทอนุมัติ

## 13. ลำดับขั้นการทำ (build ผ่านได้ทีละขั้น; commit ก่อนแก้ทุกครั้ง; ทุกขั้นจบด้วย `build.bat` + `-bossonly`/`-looponly` ตามที่ระบุ)
**ก่อนเริ่มทุกขั้น**: อ่าน `snow-map-models/status.md` ว่าแมพหิมะทำถึงขั้นไหน, `grep "Protocol ="`, ดู `git status` สะอาด, ตรวจว่าไฟล์ที่จะแก้ไม่ถูกงานอื่นแก้ค้างอยู่ (Knight/Game/Net/World/Enemy/Hud/Sfx ถูกแมพหิมะแก้เช่นกัน — **ห้ามทำขนาน** ไฟล์เหล่านี้; ขั้น 1 สร้างไฟล์ใหม่ล้วนจึงทำขนานได้)
0. **เช็กก่อนลงมือ** (ไม่มีโค้ด): ยืนยันข้อตัดสินข้อ 15, อ่านสถานะแมพหิมะ, ยืนยันชื่อชนกัน `Ground`
1. **ข้อมูลล้วน**: `BossData`, `TargetSelector`, `StatusEffects` (ยังไม่ผูก Knight), `BossSelfTest` เคส B1-B3 + `partial SelfTest` + `-bossonly`. build ผ่าน, ไม่มีพฤติกรรมเกมเปลี่ยน
2. **ตะเข็บทั่วไป (ไม่เปลี่ยนพฤติกรรม)**: `Enemy.IsWorldBoss/AllowsClash`, `Minotaur` override, `Game` เปลี่ยน `e is Minotaur` → `e.IsWorldBoss` + `Game.boss`/`BossKind` (ยังคืน Minotaur เสมอ), `Args -boss`, `Obstacles` dynamic API, ข้อความ objective ผ่านชื่อบอส. **selftest เต็ม + mptest.ps1 + mptest-loop.ps1 ต้องผ่านเหมือนเดิม** (จุด regress ง่ายสุด)
3. **อัศวินรองรับสถานะ**: `Knight.status`, `canInput`/mash/MoveMult/พื้นลื่น, `TakeDamage(fx)` + `Game.DamageKnight(fx)`, `AllowsClash` ใน clash, เปลี่ยนชื่อเมธอด `Ground`. ยังไม่มีบอสจึงไม่มีใครใส่สถานะ ; เคส B3 ผูก Knight จริง (Chill→Frozen ด้วยคำสั่งทดสอบ) + ผ่าน `-slashonly`/`-p7only`/`-looponly`
4. **ต้นแบบ capsule เฟส 1 คนเดียว** (`-boss kryos -single -loop` บนทุ่ง): `BossController` + `CapsuleBody` + `BossRoom` (ผัง k=0.795, กำแพง/เสาแบบกล่อง-กระบอก) + `TelegraphSpawner` (สีเดียว) + ท่า Bolt, Sweep, Blink, Nova, Tail Whip + poise/ชะงัก + `BossUi` (HP + poise + ชื่อ) + Intro/ตาย/portal/ดรอป. 4a: Bolt, Sweep, Nova + HP/UI; 4b: Blink, Tail + poise. เคส B4(บางส่วน) B5 B6 B10. **ด่าน A: พีทเล่นสนุกไหม** (ส่ง zip ตามกฎเกม)
5. **เฟส 1 ส่วนที่เหลือ**: Crystal Orbit + `OrbitingCrystals` + `BossShots` path, Summon + `IceGolem` + `SlickZone`
6. **เฟส 2**: Ice Wall (`IceWallPiece`) + Frozen Judgment ครบ (`IceBlock`, `BossCore`, mash, คลื่น, ทางรอด 3) ; Transition เฟส (อมตะ 3 วิ ผลึกแตก ล้าง Chill) ; เคส B8
7. **เฟส 3**: Absolute Zero + เสาแตก + Judgment ประหาร + เฟส 3 ปรับ (Tail 2 ครั้ง+หนาม, ท่าเร็วขึ้น) ; Death ; เคส B9 B11 → **ด่าน B: ครบ 3 เฟสคนเดียว**
8. **co-op**: Protocol +1 (เช็คค่าปัจจุบันก่อน), `WriteKnight` ไบต์สถานะ, `KnightHit` fx, `Msg.Boss`, `WriteExtra/ReadExtra`, `SpawnPuppet` 5-9, puppet ภาพของทุกท่า, `-netlag`, `MpLoopTest` ส่วน 11. `mptest.ps1` + `mptest-loop.ps1` (เดิมและ `-Boss kryos`) ผ่าน; รัน `-NetLag 150` → **ด่าน C**
9. **เสียง**: ขยาย `Snd`/`Sfx`, `SfxSynth`, ผูกจุดเล่นเสียงทั้ง 23 ; เคส B13 (ถ้าพีทตัดสินไม่ทำ synth ให้ข้ามและเสนอทางเลือก)
10. **โมเดล/anim**: `CodedBody2` + `BossAnim` 16 คลิป → `FileBody` และตัวโหลดไฟล์ทั้ง 5 (ทำคู่ขนานกับขั้น 9 ได้เมื่อ artist ส่ง .fbx; ขั้นนี้ไม่บล็อกขั้นอื่น) + `BossImport` + เคส B14
11. **ขัดเกลา VFX**: telegraph 2 ชั้นสี/ขอบแดง, เอฟเฟกต์สถานะ (เกล็ด ก้อนน้ำแข็ง), ลูกศรทองแกน, หมอกเย็น, hit flash, banner, กล้องเปิดตัว (ถ้าอยากได้); ถ่ายภาพเทียบ design → **ส่งพีทอนุมัติ**
12. **ผูกกับโลกหิมะ** (ต้องรอแมพหิมะอย่างน้อยถึงขั้น 6 ของ snow tech.md คือ `MapLayout.Lair` เป็น property, `Ground.Y`, `Obstacles.Use`, biome ใน `Enter`): `BossKindFor` ตาม `Biome`; `BossRoom.GroundY` → `Ground.Y`; `k` จาก `lairClearance`; ตรวจพื้นใน footprint (ความต่างสูง ≤1 ม.) ; telegraph เอียงตามพื้น; MapUi/รังชี้ห้องบอส; ปรับ `-boss` ให้เป็นตัวบังคับทดสอบเท่านั้น; selftest เต็ม + mptest + mptest-loop ครบ ; อัปเดต docs/status/backlog; zip `medieval-knight-win64.zip` ขึ้น Drive ตามกฎเกม
- ถ้าขั้นใดใหญ่เกินรอบเดียว แยก commit ย่อยต่อท่า (4a/4b …) ; build ไม่ผ่าน แก้ได้ 3 รอบ แล้วแจ้ง error ตามกฎเกม

## 14. ความเสี่ยง / สิ่งที่อาจพังของเดิม
| ความเสี่ยง | ผล | ทางกัน |
| --- | --- | --- |
| แก้ไฟล์ร่วมกับแมพหิมะ (Knight, Game, Net, World, Enemy, Hud) และ `Net.Protocol` | merge ชน / เลข protocol ซ้ำ | ทำหลัง/สลับกับแมพหิมะ ไม่ขนาน; เช็คเลข protocol ทุกครั้ง; commit ย่อย |
| `e is Minotaur` กระจายหลายจุดใน `Game.cs` | ลืมจุดหนึ่ง → Kryos ไม่ปล่อย portal/ดรอป หรือ Minotaur พัง | ขั้น 2 เปลี่ยนทั้งชุดโดย grep `Minotaur`/`minotaur` แล้วเคส B11 + loop เดิม |
| `Knight.TakeDamage` เปลี่ยนลำดับ (พื้นที่/Frozen/Execute) | parry/clash/โล่เดิมของ Minotaur/สไลม์เพี้ยน | พารามิเตอร์ใหม่มีค่าเริ่มเท่าพฤติกรรมเดิม; เคส `-p7only`/`-slashonly` ต้องผ่าน; เพิ่มเคสเทียบ before/after |
| `Sfx` เปลี่ยนจาก 6 → 29 ช่อง | selftest เดิมที่ผูกจำนวนคลิปพัง / เสียงเดิมเพี้ยน | grep ก่อน; ขั้น 9 แยก commit; เคส B13 |
| ห้องบอสเล็กกว่า design (k≈0.8) | ท่า r 7/14 ม. กินห้อง, AZ วงปลอดภัยแน่น, หลบยาก | ไม่สเกลท่า; ปรับสเกลวง AZ/ตำแหน่งเสา; รายงานในด่าน A; ขอ artist เปิดลานถ้าเล่นแล้วแน่น |
| ลานชัน/ไม่เรียบ (แมพหิมะ) | telegraph จมพื้น/ลอย | เอียงตามพื้น + ยก 0.05-0.15 ม.; วัดความต่างสูงตอน bake (ขั้น 12); ถ้า >1 ม. ขอ artist ปรับพื้นรัง |
| ตัวแทน `Enemy` หลายตัว (8 กำแพง + Golem + block + core) | snapshot ใหญ่, HitArea ชนผิดตัว, ตัวเลขดาเมจเด้งบนก้อนน้ำแข็ง | จำกัดชิ้น; override `ShowHit` ลดเลข; ปิด `GameObject` เมื่อไม่ใช้ (หายจาก `Enemy.All`); ตรวจ cap 255 |
| ผลลัพธ์ตัดสินที่เครื่องเจ้าของ (กลิ้ง/บล็อก) vs host ตัดสินรูปทรง | Judgment ack ช้า → ยืดท่า, ผลต่างระหว่างเครื่อง | `FreezeAck` timeout 0.35 วิ; ยอมรับโดยออกแบบ (ข้อ 8.4); เทสต์ ping 150 |
| แช่แข็งแล้วอินพุตค้าง (ปุ่มติด, `leftWas`, การกินค้าง) | อัศวินกดไม่ได้หลังหลุด | `Locked` ปิดผ่าน `canInput` เดียว; ตอนหลุดเคลียร์ flag เหมือน Down; เคสทดสอบหลุดแล้วฟันได้ทันที |
| หลายผู้เล่นตายพร้อมแช่ | คนเหลือเป็นศูนย์ → wipe ผ่านทางเดิม | `ClearAll` ตอน Down; ตรวจ `Fallen` ไม่ถูกนับเป็นเหยื่อ |
| Judgment "ตายทันที" ตอนเล่นคนเดียวโหด | ผู้เล่นรู้สึกถูกโกง | ทำตาม decisions ข้อ 3 (กดรัวทันก็รอด 24 แต้ม/4.5 วิ); ปรับ 1.5 แต้ม/ครั้งหลังเล่นจริง |
| `Time.time`/hit-stop/หยุดเกมในโหมดคนเดียว | state machine หยุดค้างหรือเดินเร็วตอนเปิด Tab | ใช้ `Time.deltaTime` เหมือน Minotaur (หยุดตาม timescale); ออนไลน์ไม่หยุดจึงต้อง "ยก" อมตะ/ล็อกจากเวลาเกม ไม่ใช่ `unscaledTime` |
| synth เสียงฟังไม่เข้ากับ mp3 เดิม | คุณภาพเสียงไม่ผ่านใจพีท | ตั้งเป็นค่าเริ่ม ปรับได้ใน `SfxSynth`; ข้อตัดสินข้อ 7 |
| สัญญา node .fbx ไม่ตรง | โมเดลไม่ขึ้น | fallback ทั้งตัว + เตือน; `BossImport` ตรวจและรายงานก่อนรวม |
| เวลาเฟรมจากโค้ดภาพหาง/ผม (สปริง) + เมชหลายชิ้น | เฟรมตกใกล้บอส | ~35 renderer ยอมรับได้ (บอสตัวเดียว); วัดตอนขั้น 11 (เป้าไม่เกินที่แมพหิมะวัดไว้ +0.5 ms) |

## 15. ข้อที่ต้องตัดสิน (ค่าที่แนะนำถ้าไม่ตอบ)
1. **ลำดับกับแมพหิมะ**: ขั้น 1 ทำขนานได้ (ไฟล์ใหม่ล้วน); ขั้น 2–11 รอให้แมพหิมะ commit ถึงขั้น 6 ก่อน (แนะนำ) หรือรอจบทั้งงาน; ขั้น 12 หลังแมพหิมะขั้น 8
2. **ห้องย่อ**: ใช้ `k=0.795` (R 19 ม.) ตามผล bake 21.66 (แนะนำ ทำงานได้เลย) หรือขอ artist เปิดลานหน้าประตูให้ r ≥ 27 ม. (ได้ห้องเต็มขนาด; ไม่บล็อกงาน)
3. **พิกัดจุดเทเลพอร์ต/เสา** ที่ design ขัดกันเอง: ใช้รัศมี 16.8k / 12k (แนะนำ)
4. **ดึงผู้เล่นนอกวงเข้า** ตอนบอสตื่น: ทำ (แนะนำ; ไม่งั้นเพื่อนที่ช้าถูกขังนอกกำแพง) หรือรอให้ทุกคนเข้า ≤ 28k ม. ก่อนตื่น
5. **parry Bolt/Orbit**: ปัดทิ้งได้แต่ไม่ให้ poise (แนะนำ) หรือให้ poise 20%
6. **HP กำแพงน้ำแข็ง**: HP ร่วม 3% ของบอสทั้ง 8 ชิ้น (แนะนำ) หรือแยกชิ้น
7. **เสียง**: สร้าง `SfxSynth` (แนะนำ แต่เพิ่มงาน ~1 ขั้น และฟังเป็นเสียงสังเคราะห์) / ยืม 6 mp3 ปรับ pitch / รอไฟล์เสียงจากพีท (decisions ข้อ 5 เขียนเหมือนเกมมีเสียงสังเคราะห์อยู่ — ที่จริงไม่มี)
8. **Judgment ไม่ลดดาเมจ 30% ของ Frozen** (75% เต็ม) (แนะนำ) หรือให้ลดตามกฎ
9. **Chill ขณะอมตะ-Frozen 4 วิ**: จำกัดไม่เกิน 2 ชั้น (แนะนำ) หรือไม่สะสมเลย
10. **ก้อนน้ำแข็ง Frozen ปกติ**: เพื่อนฟันลดเวลา 0.6 วิ/ครั้ง (แนะนำ) หรือเท่ากับกดรัว 0.15
11. **ด่านพีท** A/B/C ตามข้อ 12.3 (ต้องมีคนเล่นจริง ไม่ใช่เทสต์อัตโนมัติ)
12. **กล้องเปิดตัว** (เลื่อนไปที่บอสระหว่าง Intro 3.5 วิ): ทำในขั้น 11 ถ้ามีเวลา (ไม่บังคับ)

## 16. ประมาณขนาดงาน: **ใหญ่มาก**
เหตุผล: ไฟล์ใหม่ ~15 ไฟล์ (โค้ดใหม่ราว 6,000–7,500 บรรทัด + เทสต์ ~1,500) ; แตะไฟล์แกนกลางที่เสี่ยงทั้งหมด (`Knight`, `Game`, `Enemy`, `Net`, `Sfx`, `Obstacles`) ; ต้องเพิ่ม protocol + ข้อความใหม่ + ตัวหน่วง ping ; ระบบเสียงสังเคราะห์ใหม่ ; แอนิเมชันโค้ด 16 คลิป ; เทสต์ออนไลน์หลายโปรเซส ; มีด่านให้พีทเล่น 3 ด่าน. ส่วน "ตรงๆ" คือสถาปัตยกรรมใช้แพทเทิร์นเดิม (Enemy + snapshot + DamageKnight + ModelLib) จึงไม่ต้องรื้อ
แบ่งส่งงานได้ 4 ก้อนประเมิน: ขั้น 1–3 (กลาง) | ขั้น 4–7 (ใหญ่ ก้อนหลัก) | ขั้น 8 (กลาง–ใหญ่) | ขั้น 9–12 (กลาง)
