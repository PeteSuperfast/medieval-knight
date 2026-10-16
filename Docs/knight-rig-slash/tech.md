# tech: อัศวินมี rig + ท่าฟัน 3 ท่า + กลิ้ง + parry motion + กินอาหาร + การ์ดสเตตัส 5 ใบ + ระบบรับ .fbx
ผู้ขอ: test_disk | อ้างอิง: requirement.md, design.md (รอบ 2), decisions.md (รวมที่ปูส้มตัดสิน), reference/*.md | Unity 6000.6.5f1 URP | เขียนโดย architect 2026-10-16
ตรวจโค้ดจริงแล้ว: Knight.cs (1,511 บรรทัด), Progress.cs, InputX.cs, Net.cs, Game.cs, Hud.cs, Weapon.cs, Prims.cs, TeamRun.cs, RunLoop.cs, SelfTest.cs, MpTest.cs, MpLoopTest.cs, LoopSave.cs

## 0. สิ่งที่ตรวจพบจากโค้ดจริง (มีผลต่อแผน อ่านก่อน)
1. **ในโหมด loop (โหมดหลักของเกม) อาหารบนพื้นไม่ได้ถูกกินทันที** `Game.UpdatePickups` ส่งเข้า `RunLoop.TryCollect` ลง carry; การกินคือ `RunLoop.Eat/EatFirst` (ปุ่ม C หรือคลิกใน Tab) ส่วน "เดินทับกินทันที" มีเฉพาะสนาม v1 (`-arena/-single/-selftest`, `!loop`) → ท่ากินอาหาร 2 วิต้องผูกกับ "อาหารใน carry" เป็นหลัก (ดูข้อ 5.4 และข้อที่ต้องตัดสิน D1)
2. เซฟ loop (`LoopSave.cs`, `Loadout.cs`, `OldWorld.cs`) **ไม่เก็บ** `Progress`/สกิล/การ์ด/คอมโบเลย (เลเวลเริ่มใหม่ทุกรอบ) → ไม่มีฟิลด์เซฟที่เกี่ยวกับสกิลให้ migrate; รูปแบบเซฟไม่เปลี่ยน (Version 1 เดิม)
3. Knight เป็น "ตัวเดียวกันทั้ง local และ remote": remote เล่นท่าซ้ำจาก `Msg.Act` ผ่าน `PlayRemoteAct` + coroutine เดียวกับ local (`remote` guard กันไม่ให้โดนศัตรู) → ใช้แพทเทิร์นนี้ต่อ ไม่ต้องสร้างระบบซิงก์ anim ใหม่
4. การแสดงผลตัวอัศวินตอนนี้ผูกกับตัวแปร `swYaw/swPitch/swHand/rigLean/rigHop/rigSpin` ที่ `LateUpdate` เขียนลง transform (โมเดลโค้ดล้วน ไม่มี Animator) → ต้องแยกชั้น "สถานะ/เวลา" ออกจาก "การวางท่า" ก่อน แล้วค่อยเสียบ fbx (ข้อ 3)
5. ตัวแปรที่ SelfTest/MpTest พึ่ง: `LastAction`, `ActionsSeen`, `SwordModel` (+โหนดลูก `Tip`, `File`), `DodgeCount`, `ParryCount/ClashCount`, `DebugAttack/DebugMove/DebugSkill`, `attackHeldOverride`, `Knight.Attacks/Skills` → ต้องรักษา/แทนที่ชื่อเหล่านี้อย่างตั้งใจ (ข้อ 8)
6. `Airborne` ถูก Minotaur/Slime/Game ใช้ (กัน AoE ตอนกระโดด Ground Slam) → **คงพร็อพเพอร์ตี้ไว้ (เป็น false ตลอด)** ไม่แตะโค้ดศัตรูตามที่ requirement สั่ง
7. **ผลบาลานซ์ที่ต้องรู้**: ถอดการ์ด Damage/Speed/Crit (`Progress.dmg/spd/crit`) แล้ว ดาเมจผู้เล่นโตจาก "อาวุธ" อย่างเดียว แต่ศัตรูยังโตตามเลเวล (`Game.EnemyHpScale` = 1+0.45/เลเวล) → ถึงเลเวล 10 ศัตรู HP ~5.5 เท่า ดาเมจผู้เล่นเท่าเดิม = สู้ยากมาก (ดู D3)
8. ไฟล์ .fbx อยู่ใน Git LFS (`.gitattributes` มี `*.fbx lfs`) และ precedent การโหลด fbx มีแล้วใน `Weapon.cs` (`BuildFileModel`: `Resources.Load<GameObject>` + ลบ Camera/Light/Collider + ทาสีทับตามชื่อ material) → ใช้แพทเทิร์นเดียวกัน

## 1. ภาพรวมแนวทาง
- แยก Knight ออกเป็น 3 ชั้น: **(ก) สถานะ+เวลาของท่า** (`ActClock`: ท่าอะไร จังหวะไหน ความคืบหน้า 0..1 — คำนวณจาก Time + speed ที่เดียว ใช้ทั้ง local และ remote), **(ข) ตรรกะเกม** (hitbox, stamina, damage, net — อยู่ใน Knight.cs เดิม), **(ค) ตัวแสดงผล `KnightBody`** มี 2 แบบ: `CodedBody` (โมเดลโค้ดเดิม ย้ายมาแทบไม่แก้) กับ `FileBody` (.fbx + animation) → fallback คือสลับเป็น CodedBody อัตโนมัติ
- animation ของ fbx **ไม่ใช้ Animator Controller**: ใช้ Playables (`PlayableGraph` + `AnimationClipPlayable`) แล้ว "ตั้งเวลา clip เอง" ทุกเฟรมตามจังหวะจาก ActClock (แมป 4 จังหวะ → ช่วงของ clip ด้วยตาราง marks) จึงได้ timing ตรงตารางดีไซน์และ hitbox เป๊ะ ไม่ต้องใช้ animation event และไม่ต้องแก้ clip
- เอา 5 คอมโบ + phase 6 (7 ท่า+Heavy) + สกิล Q/E/R/F ออกทั้งหมด แทนด้วย `SwingDef[3]` (เฉียง/กวาด/แทง) วนอัตโนมัติ 1>2>3; Space = กลิ้ง (ใช้ตัวแปร dash เดิมปรับค่า); ปุ่ม F = ชุบเพื่อน หรือค้างกินอาหาร
- เครือข่าย: Act kind ใหม่ (0..2 ท่าฟัน, 20 กลิ้ง, 21 เริ่มกิน, 22 parry motion) + flag NetEating ใน state เดิม → **Net.Protocol 5 → 6**; ไม่มีข้อความใหม่ ไม่มี host lock (อาหาร carry เป็นของเครื่องเจ้าตัวอยู่แล้ว)
- การ์ดเลเวลอัปเป็นขั้นเดียว 5 ใบ; `Progress` ลดเหลือ HP/DEF/STAMINA/SPEED/RESISTANCE

## 2. ไฟล์ (ทุกไฟล์อยู่ใต้ `Assets/MedievalKnight/Scripts/` ยกเว้นที่ระบุ)

### 2.1 สร้างใหม่
| ไฟล์ | เหตุผล |
|---|---|
| `KnightAnim.cs` | ข้อมูล+เวลาของท่าทั้งหมด (ล้วน ไม่แตะ Unity scene เทสได้ง่าย): `enum Act`, `SwingDef`/`RollDef`/`ParryDef`/`EatDef`, `PoseKey`, `struct BodyState`, `class ActClock` |
| `KnightBody.cs` | `abstract class KnightBody` + `CodedBody` (ย้ายโค้ด `Build()`/`Pose()`/`LateUpdate()` เดิม) + `KnightGear` (ตัวสร้างโล่ที่ใช้ร่วมสอง body) |
| `KnightFileBody.cs` | `FileBody`: instantiate fbx, หา socket มือขวา/แขนซ้าย, PlayableGraph, ทาสีตามจานสี |
| `ModelLib.cs` | ระบบรับ .fbx ทั่วไป: `ModelLib.Model(path)` (cache + คืน null เมื่อไม่มี ไม่ throw), `ModelLib.Clip(path)`, `ModelLib.Recolor(...)`, `ModelLib.Strip(...)` (ลบ Camera/Light/Collider/Script ยกมาจาก `Weapon.BuildFileModel`), และ `EffectLib.Play(name, pos, rot)` (โหลด prefab จาก `Resources/Effects/`; ไม่มีไฟล์ = ข้ามเงียบ) |
| `Resources/Models/Knight/knight.fbx` (+`.meta`) | โมเดลอัศวิน rig (Generic, ~15–20 กระดูก, ≤5,000 tri, ≤5 MB) — **ต้องได้รับยืนยัน license/ขนาดจากผู้ขอก่อน commit** |
| `Resources/Models/Knight/Anims/<ชื่อ>.fbx` | clip แยกไฟล์ตามชื่อ: `idle walk run slash1 slash2 thrust roll block parry eat down` (+`hit` ถ้ามี) — Mixamo ตั้งชื่อ take ว่า "mixamo.com" ทุกไฟล์ จึงโหลดทีละไฟล์ด้วยชื่อไฟล์ ไม่ต้องมี importer script |
| `Docs/knight-rig-slash/asset-sources.md` | บันทึกแหล่ง/license/วันที่/ขนาด/tri ของทุกไฟล์ (Mixamo ไม่ใช่ CC0/CC-BY: ห้ามแจกไฟล์ดิบ เก็บหน้าเงื่อนไขไว้) |

ไม่สร้างโฟลเดอร์ `Resources/Effects/` เปล่า (Unity จะสร้าง .meta ให้โฟลเดอร์เปล่าแล้วเกิดขยะใน git); สร้างเมื่อมี prefab ตัวแรก — `EffectLib` รองรับกรณีไม่มีโฟลเดอร์

### 2.2 แก้
| ไฟล์ | สิ่งที่แก้ |
|---|---|
| `Knight.cs` | **ใหญ่สุด** ดูข้อ 3–6: ลบ `AttackDef` เดิม 12 ท่า, `Skills`, `SkillDef`, `NetKind/AttackFromNetKind`, `UpdateHold/Charging/ring/StartHeavy`, `PickAttack`, `SwingDelay`, `DashSlashWindow`, `TrySkill`, `Whirlwind/DashThrust/ShieldBash/GroundSlam`, `cd/cdMax`, `SkillRequest/ComboRequest/HeavyRequest`; เพิ่ม swing/roll/parry/eat routine; ย้ายโค้ดวาดตัวไป `KnightBody.cs`; `TakeDamage` เพิ่มกฎ block-stamina + resistance; `Simulate` ใช้ speed stat |
| `Progress.cs` | ลบ `StatKind`, `UpgradeOffer`, `dmg/spd/crit[]`, `MakeOffers`, `Apply`, `Describe`, `Take`; `enum Attr {Hp, Def, Stamina, Speed, Resist}` (ข้อ 7) |
| `Game.cs` | `OpenLevelUp/OpenStatStep/ChooseUpgrade/ChooseStat` → ขั้นเดียว; `HitEnemy` ตัด `HeavyIndex`; `ClientHit` ตัด src 10..13/30..36; `OnKnightDown`; `Update` (`LevelUpInput(5)`); scope ของ `FoodFullHints` |
| `Hud.cs` | ลบ `SlotUi/slots/MakeSlot` + อัปเดตช่องสกิล; การ์ด 3→5 ใบ; Tab: ลบกล่อง SKILLS รวมเป็นกล่อง STATS 5 แถว; ข้อความปุ่มช่วยเหลือ 2 จุด (บรรทัด ~247 และ ~1086); `Knight.DashCost` → `RollCost`; แถบกิน (world-follow); เลิก "TAB !" กระพริบ (statPoints ถูกใช้ทันที) |
| `InputX.cs` | เพิ่ม `Keypad4/Keypad5` ใน `Map()` (Alpha4/5 มีแล้ว); Q/E ต้อง **คง**ไว้ (โหมดดูเพื่อนของ `TeamRun` ใช้) |
| `Net.cs` | `Protocol = 6` + แก้คอมเมนต์ kind/flags ที่บรรทัด ~16, 276, 999; ไม่มีเปลี่ยนโครง `WriteKnight/ReadKnight/Msg.Act` |
| `TeamRun.cs` | บรรทัด 344 ตัด `!me.Charging` เพิ่ม `!me.Eating`-aware (ชุบเพื่อนชนะกินเสมอ) |
| `RunLoop.cs` | `WorldKeys` ปุ่ม C → `knight.TryBeginEat`; แยก `EatNow` (กินทันที ใช้ในเทสต์/ตอนครบ 2 วิ) กับ `PeekFood(out Section, out int)`; `EatFirst()` คงเป็น instant เพื่อไม่ทำให้ `MpLoopTest` พัง |
| `LoopUi.cs` | บรรทัด ~1249 (C ในหน้า Carry) และคลิกอาหารใน Carry → เรียก `TryBeginEat` แล้วปิดหน้าต่าง (กัน exploit กิน instant ผ่าน Tab ซึ่งออนไลน์ให้โล่ 6 วิ) |
| `Fx.cs` | อาจเพิ่ม 1 เมธอดเส้นฟันเฉียง (ใช้ `Arc/Streak` เดิมได้ก่อน) — ทำเมื่อภาพจริงไม่พอ |
| `Weapon.cs` | ไม่แก้ตรรกะ; ถ้าดึง `Strip/Recolor` ไปไว้ `ModelLib` ให้ `BuildFileModel` เรียกใช้ต่อ (ไม่บังคับ — ไม่ refactor ถ้าไม่จำเป็น) |
| `SelfTest.cs` | ลบ Phase6* ทั้งชุด + เคสคอมโบ/สกิล/dash/การ์ด/Heavy ของ v1; เพิ่มเคสใหม่ (ข้อ 9) |
| `MpTest.cs` | แทนหัวข้อ 3 (skills), 3b (charge/Heavy), 3c-e (heavy number) ด้วยเคสใหม่; ลบคำสั่ง `skill`/`holdon`/`holdoff`; คง `strike/probe/clashprobe/hostparry` |
| `MpLoopTest.cs` | เพิ่มคำสั่ง/เคส 1–2 ข้อทดสอบกินแบบจับเวลาจริง (ข้อ 9.3); เคสเดิมที่ใช้ `EatFirst` ไม่เปลี่ยน |
| `Args.cs`/`SelfTest.cs` | แทน `-p6only` ด้วย `-slashonly` (รันเฉพาะเคสใหม่ ~2 นาที), คง `-p7only` `-looponly` |
| `Assets/Editor/BuildScript.cs` | ไม่แก้ (ตรวจแล้ว: Resources รวมเข้าบิลด์เอง) |
| `CLAUDE.md`(เกม), `Docs/reference/{controls,files,progression,weapons-and-tab,online-coop,dev-guide}.md` | developer อัปเดตตอนจบ (protocol 6, ปุ่ม, ไฟล์ใหม่, ตารางสเตตัส, วิธีเพิ่มท่า) — **ไม่ใช่งานของ architect รอบนี้** |

### 2.3 ลบ
ไม่ลบไฟล์ทั้งไฟล์ (มี `.meta`/ลิงก์ในซีนไม่เกี่ยว) — ลบเฉพาะโค้ด/เคสในไฟล์ข้างบน

## 3. โครงสร้างคลาสและความสัมพันธ์

```
Knight (MonoBehaviour)  -- ตรรกะเกม: input, stamina, hitbox, damage, net, สถานะ
  ├─ ActClock clock            เวลาของท่าปัจจุบัน (act, swing, phase, t, speed)
  ├─ KnightBody body           ตัวแสดงผล (CodedBody | FileBody)   ← สลับตอน Init
  ├─ Progress progress         5 สเตตัส + EXP
  └─ Inventory inventory       (เดิม) อาวุธ
KnightBody (abstract)
  ├─ Transform SwordMount / ShieldMount     ที่ให้ Weapons.BuildModel / KnightGear ไปเกาะ
  ├─ void Apply(in BodyState s, float dt)   วางท่า (เรียกจาก Knight.LateUpdate)
  ├─ void SetTint(Color c, float k)         เดิม: Tint ของ rig + โล่
  └─ Transform Root, string Kind ("coded"|"file")
CodedBody : KnightBody  = โค้ด Build()/Pose()/LateUpdate เดิมย้ายมา + PoseKey interpolate
FileBody  : KnightBody  = fbx + PlayableGraph (mixer 2 ช่อง crossfade 0.1 วิ) + ClipSet
ModelLib / EffectLib    = static, cache, ไม่ throw
```

### 3.1 `KnightAnim.cs` (ข้อมูลที่ dev ต้องทำตามนี้)
```csharp
enum Act { Idle, Walk, Run, Swing, Roll, Parry, Eat, Down }          // ท่าที่ body แสดง
struct BodyState { Act act; int index; int phase; float t; float speed;  // index = ท่าฟัน 0..2; phase 0..3; t 0..1 ในจังหวะนั้น
                   bool blocking; bool dead; float moveSpeed; }          // moveSpeed ใช้เลือก walk/run + จังหวะก้าว
class SwingDef { string name; float[] phaseTime /*ก่อนฟัน,ฟัน,หลังฟัน,ดึงกลับ*/; bool[] scaledBySpeed /*3 ตัวแรก true, ดึงกลับ false*/;
                 float damageMul; float reach, halfArc; bool capsule; float width, length; float lunge;
                 PoseKey[] keys /*5 key สำหรับ CodedBody*/; float[] clipMarks /*5 ค่า 0..1 ของ clip สำหรับ FileBody*/; Color fx; }
```
- ตารางค่าเริ่มต้น = design.md ข้อ 3 ทุกตัวเลข (ก่อนฟัน/ฟัน/หลังฟัน/ดึงกลับ 0.20/0.12/0.15/0.25, 0.18/0.16/0.17/0.25, 0.25/0.08/0.15/0.30; hitbox พัด 100° ระยะ 2.2 / 150° ระยะ 2.0 / แคปซูล 0.8×2.8; ตัวคูณดาเมจ 1.0/0.9/1.2; ถลา 0.4 / 0 / 1.0 ม.) — **เป็นค่าคงที่ในโค้ดตามแพทเทิร์นโปรเจกต์ ไม่มี config ไฟล์**
- `ActClock`: `Begin(act, index, speed)`, `Tick(dt)` → อัปเดต phase/t จาก `phaseTime[i] / (scaled ? speed : 1)`; `Skip(toPhase)` (คลิกต่อใน "หลังฟัน" ข้ามดึงกลับ); ไม่มี coroutine ของตัวเอง — Knight เรียกใน routine
- การคำนวณเวลาเป็นฟังก์ชันล้วน (`static BodyState At(def, speed, elapsed)`) → selftest เช็คตัวเลขได้โดยไม่ต้องรอเฟรมจริง

### 3.2 การแมป 4 จังหวะ → clip (FileBody)
- ต่อ clip ท่าฟัน 1 ชุด `clipMarks = {0, a, b, c, 1}` = เวลา (normalized) ในไฟล์ clip ที่ตรงกับ "จุดเริ่มก่อนฟัน / เริ่มฟัน / จบฟัน / จบหลังฟัน / จบดึงกลับ" (dev ดูจาก clip แล้วใส่ตัวเลข; ทำ debug log ความยาว clip ตอนโหลด)
- ทุกเฟรม: `clipTime = Lerp(marks[phase], marks[phase+1], t) * clip.length` → `playable.SetTime(...)`; playable speed = 0; ตั้ง graph เป็น `DirectorUpdateMode.Manual` แล้ว `Evaluate()` เองหลัง `SetTime` (ต้องพิสูจน์ใน spike ขั้น 7)
- idle/walk/run/block/eat/down: เล่นวน/ค้างด้วย `time = (elapsed * rate) % length` (down clamp ที่ท้าย clip) — จึงไม่ต้องตั้ง loop flag ที่ importer
- เปลี่ยน act = crossfade 0.1 วิด้วย mixer 2 ช่อง (จบไม่ติดสะดุด); ท่าฟันต่อท่า (chain) crossfade 0.05 วิ
- all-or-nothing: ขาด clip บังคับ (`idle walk run slash1 slash2 thrust roll block parry eat down`) หรือหา socket มือ/แขนไม่เจอ → `Debug.LogWarning` ครั้งเดียว + ใช้ `CodedBody` ทั้งตัว (ไม่ผสมครึ่งๆ)

### 3.3 Socket และของถือ
- ดาบ: `Weapons.BuildModel(swordSocket, Weapon)` เดิม (+Z = มือ→ปลายดาบ, มีโหนด `Tip`) — socket = โหนดเปล่าลูกของกระดูกมือขวา (หาด้วยชื่อที่มี `RightHand`) พร้อม `localEuler/offset` ค่าคงที่ที่ dev ปรับครั้งเดียวต่อโมเดล; ต้องคง `SwordModel` + ชื่อโหนด `Model/File/Tip` ให้ตรงกับที่ SelfTest เช็ค
- โล่: โค้ดสร้างเดิมย้ายไป `KnightGear.BuildShield(parent, color)` ใช้กับกระดูกแขนซ้ายล่าง (`LeftForeArm`) ของ FileBody และ `shieldNode` ของ CodedBody — ไม่ต้องหาโมเดลโล่จากไฟล์
- ปรับขนาด: คำนวณ bounds ตอนโหลดแล้วสเกลให้สูงเท่าอัศวินเดิม (~1.9 ม., ตัวแปร `TargetHeight`) จุดหมุนที่เท้า; ปิด root motion (`animator.applyRootMotion=false`) และถ้า Mixamo clip ไหนเลื่อน Hips ไปข้างหน้าให้ใช้ "In Place" ตอนโหลดจาก Mixamo
- ทาสี: `ModelLib.Recolor` แทน material ทุก slot ด้วย `Prims.Lit(สี)` ตามตารางชื่อ material → จานสี `style.md`: เกราะ #958A80, ข้อต่อ/รองเท้า/ถุงมือ #3A3A3A, หนัง #715332, เข็มขัด/ขอบ #A1773D, ผ้าคลุม (ข้อ D2); slot ที่ไม่รู้จัก → #958A80 + log ชื่อ; `Tint` เดิมใช้ได้กับ SkinnedMeshRenderer

### 3.4 ระบบรับ .fbx / เอฟเฟกต์ (`ModelLib`)
- ลายเซ็น: `static GameObject Model(string resPath)` (cache, null ถ้าไม่มี, log เตือนครั้งเดียวต่อ path), `static AnimationClip Clip(string resPath)` (เลือก clip แรกที่ชื่อไม่ขึ้นต้น `__preview`), `static GameObject Spawn(string resPath, Transform parent)` (Instantiate + `Strip`), `static void Recolor(GameObject, Func<string,Color>)`
- `EffectLib.Play(string name, Vector3 pos, Quaternion rot, float life = 2f)`: `Resources.Load<GameObject>("Effects/"+name)`; null = return (เงียบ); มี = Instantiate แล้ว `Destroy(go, life)`; ใช้ที่จุดริ้วดาบ/ประกายตอนโดน/ปัดโล่ — **ถ้าไม่มีไฟล์ ยังใช้ `Fx.I.*` เดิม** (ริ้ว #EEF3F8 0.15 วิ, ประกาย #F5C25A) ดังนั้นเกมสมบูรณ์โดยไม่มี Effects/
- ทุกทางออกของการโหลดห้าม throw ออกมาที่เกมเพลย์ (try/catch รอบ Instantiate/สร้าง graph)

## 4. ท่าฟัน 3 ท่า (Knight.cs)

### 4.1 อินพุต
- คลิก = **ขอบขาขึ้นของปุ่ม** (`attackHeld && !leftWas`) ทันทีที่กด — เลิก "ตัดสินตอนปล่อย ≤0.15 วิ" และ `TestHoldSeconds`; ยังอ่าน `attackHeld` ตอน panel เปิดเพื่อให้การกดที่เริ่มใน panel ไม่นับ (พฤติกรรมเดิมของ `leftWas`)
- `KnightInput.request`/`SkillRequest` ลบ; เหลือ `bool click` (edge), `block`, `run`, `roll` (Space), `eatHeld` (F ค้าง)
- hook เทสต์ `attackHeldOverride` คงไว้ (ขอบขาขึ้นจาก override ก็นับเป็นคลิก)

### 4.2 สถานะ combo
- ตัวแปร: `swingIndex` (ท่าถัดไป 0..2), `queuedClick`, `lastSwingEnd`
- `Click()`: ถ้า `inp.block` (ยกโล่ค้าง) = ไม่ฟัน; ถ้ากำลังกิน → ยกเลิกกิน แล้วฟัน; ถ้ากำลังกลิ้ง/ล้ม = ทิ้ง; ถ้าว่าง → `Time.time - lastSwingEnd > 0.4` ให้ `swingIndex = 0` (รีเซ็ต) แล้วเริ่มท่า `swingIndex`; ถ้าอยู่ในท่า: ก่อนฟัน/ฟัน → `queuedClick = true` (คิวสูงสุด 1 คลิก); หลังฟัน → ข้ามดึงกลับ เข้าท่าถัดไปทันที (ก่อนฟันสั้นลง 30%); ดึงกลับ → เข้าคิว (เริ่มเมื่อดึงกลับจบ ภายในหน้าต่าง 0.4 วิ)
- **ข้อตัดสินของ architect** (design ไม่ระบุ): คิวคลิกที่ได้ระหว่างก่อนฟัน/ฟัน จะถูกใช้ **เมื่อ "หลังฟัน" จบ** (ท่ายังเล่นครบ 3 จังหวะแรก; ไม่เร่งข้าม) เพื่อให้รู้สึกหนักแน่นตามเป้า
- การเปลี่ยนท่าใช้ `swingIndex = (i+1)%3` ตอน "เริ่มท่า" (ไม่ใช่ตอนจบ) เหมือน `comboIndex` เดิม

### 4.3 Routine
`IEnumerator SwingRoutine(SwingDef d, float speed)` แพทเทิร์นเดียวกับ `AttackRoutine` เดิม (เรียกด้วย `StartCoroutine`, ใช้ร่วมกับ remote ผ่าน `RemoteAct`) แต่ขับด้วย `ActClock`:
1. ตั้ง `IsAttacking=true`, `lockFacing=true`, ล็อกทิศ ณ ตอนเริ่ม (หันตามเคอร์เซอร์ก่อนเริ่ม), ยิง `Net.SendAct(index, speed, Vector3.zero)` (kind 0..2), `Game.I.hud.Action(...)`, `LastAction=d.name`, `ActionsSeen++`
2. clash window: `clashFrom = start + pre/speed − ClashPad`, `clashTo = start + (pre+strike)/speed + ClashPad` (กฎ phase 7 เดิม ใช้ช่วง "ฟัน" แทนท่าเดิม ตามดีไซน์)
3. จังหวะ "ฟัน": ตอนเข้า spawn ริ้ว (`Fx.I.Arc` สำหรับเฉียง/กวาด ใช้ `yaw` เริ่ม/จบจาก keys, `Fx.I.Streak` สำหรับแทง) + `SwingSound()` + grunt; ถลาด้วย `Push(dir * lunge/strikeTime * dt)` (แทง 1.0 ม. ทั้งช่วง)
4. **hitbox**: ช่วง t ∈ [0.25, 0.85] ของจังหวะ "ฟัน" เรียก `HitArea(...)` ทุกเฟรมโดยส่ง `HashSet<Enemy> once` ตัวเดียวต่อท่า (มี param `once` อยู่แล้ว) → ศัตรู 1 ตัวโดน 1 ครั้ง/ท่า; ท่าแทงใช้ `HitCapsule` ใหม่ (วัด `Vector3` ระยะตามแกนหน้า 0..length×Weapon.reach และระยะตั้งฉาก ≤ width/2 + e.radius) ทะลุหลายตัว — แยกเป็นเมธอดข้างๆ `HitArea` ใช้ตัวนับ/เสียง/`Game.HitEnemy` ชุดเดียวกัน (อย่า copy logic เสียง/จัดอันดับ armor ซ้ำ ดึงเป็น helper)
5. ดาเมจ = `baseDamage × d.damageMul` ผ่าน `RollDamage(-1, ...)` (ตัดพารามิเตอร์ `skill` ออกทั้งหมด → `RollDamage(float, out bool)`); `baseDamage` ค่าคงที่ตั้งต้น = ดู D4; `Reach(d)` = `d.reach × Weapon.reach`; speed = `SwingSpeed` (= `clamp(Weapon.speed, 0.4, 4)` เพราะไม่มีอัปเกรดความเร็วแล้ว)
6. จบ: `IsAttacking=false`, `lastSwingEnd=Time.time`; ถ้าคิวคลิกค้าง → เริ่มท่าถัดไปทันที
7. `Juice` (shake/hit-stop) ยกจาก `DoHit` เดิม (ค่าตามท่า: เฉียง 0.10/0.05, กวาด 0.08, แทง 0.15)
- ตัด: หน่วง 0.5 วิ, ชาร์จ, ทิศ WASD เลือกท่า, Dash Slash, Spin/Chop (โหมด Chop slam ground + dust ทิ้ง)
- `DebugAttack(int)` คงชื่อไว้ (ใช้ใน mptest/selftest) = เริ่มท่า `index` 0..2 ทันที; ลบ `DebugMove`/`DebugSkill`

### 4.4 Remote
`PlayRemoteAct(kind, speed, target)`: kind 0..2 → `RemoteAct` รอ `interp.delay` แล้ว `SwingRoutine` (hitbox ถูก guard ด้วย `remote` อยู่แล้วใน HitArea → ไม่มีผลต่อศัตรู); ใช้ `ActClock` เดียวกัน จึงเห็นท่าตรง ±0.1 วิ

## 5. กลิ้ง / parry motion / กินอาหาร

### 5.1 กลิ้ง (Space)
- ปรับค่าคงที่ใน `Knight.cs`: `DashMoveTime 0.20 → 0.40` (เปลี่ยนชื่อเป็น `Roll*`), `DashDistance 6`, `DashInvuln 0.5`, `DashCost 25 → RollCost`, `DashCooldown 0.8` — ตรรกะ `TryDash` ใช้ต่อได้เกือบทั้งหมด (ตัด `dashStartTime/Dash Slash`, `nextSwingAt`); เพิ่ม `CancelEat`, reset `swingIndex=0`
- `Dashing` → `Rolling` (อมตะ 0.5 วิ ครอบทั้ง motion; ตัวแปรที่ `Invulnerable` ใช้อยู่) — ค้นแทนที่ทั้งโปรเจกต์ด้วย compiler (ใช้ใน Hud/Game บ้าง)
- คงความเร็วเคลื่อนที่ = 15 ม./วิ (6 ม./0.4 วิ) ตลอดช่วง; ตรวจชนผ่าน `Obstacles.Push` เดิมใน `Simulate`
- motion แบ่ง 20/60/20% (ย่อ/ม้วน/ลุก) ใน `RollDef`; ส่ง `Net.SendAct(20, 1f, goal)` เดิม (kind 20 เปลี่ยนความหมายจาก "dash" เป็น "roll" เท่านั้น); remote เล่น `Act.Roll` ตำแหน่งมาจาก interp เหมือนเดิม
- ข้อความ "DODGE" + `DodgeCount` + stamina แดงกะพริบ (`Deny("Roll")`) คงเดิม

### 5.2 Parry motion
- กฎตัดสิน parry/clash (`TakeDamage`, `ParryWindow` 0.2 วิ, `ParryGap`, `Deflected`) **ไม่เปลี่ยน**; เพิ่มเฉพาะ: ใน `Deflected(parry:true)` เรียก `StartParryMotion()` + `Net.SendAct(22, 1f, Vector3.zero)`
- `ParryRoutine`: 4 จังหวะ 0.15/0.12/0.25/0.20 (`ParryDef`); จังหวะ 2 ยิงประกาย #F5C25A (`Fx.I.Burst` มีอยู่แล้วใน Deflected → ย้ายเวลาให้ตรงจังหวะ 2); ใช้ปุ่มคลิกขวาค้างเดิมเป็นสถานะ `IsBlocking`
- แค่ยกกัน (ไม่ parry) = `Act.Idle + blocking` ค้างท่า `block` (จังหวะ 1 ค้าง) ปล่อยปุ่ม = ดึงกลับ 0.2 วิ (ฝั่ง body เฟดเอง)
- **ข้อตัดสินของ architect** (design ไม่ระบุ): ระหว่าง parry motion จังหวะ 1–2 (0.27 วิ) ตัดไม่ได้; จังหวะ 3–4 ตัดได้ด้วยฟัน/กลิ้ง (เป็นช่องโต้ตอบที่ดีไซน์ต้องการ) — ถ้าไม่เห็นด้วยปรับที่ค่าคงที่ `ParryLockPhases`
- remote: kind 22 → `PlayRemoteParry()`; ถ้าไม่ได้รับ (หลุด) body ก็ไม่ค้างเพราะ ActClock จบเอง

### 5.3 โล่ + stamina (จาก decisions Q1)
ใน `TakeDamage` ช่วง `IsBlocking && facing`: ค่าใช้จ่าย = `BlockStaminaCost(10) × (1 − 0.04 × DEF)`; ถ้า `stamina < cost` → ไม่บล็อก รับเต็ม (ข้อความ/เสียง `GUARD BROKEN` สั้นๆ ใช้ `Sfx` เดิม); ถ้าพอ → หัก stamina + `lastSpendTime` (หน่วงการฟื้น 0.6 วิเหมือนกลิ้ง). Parry/clash ไม่กิน stamina. เทสต์ที่พึ่ง "โล่ฟรี" ต้องเติม stamina ก่อน

### 5.4 กินอาหาร
- ตัวแปร: `Eating` (bool), `EatProgress` (0..1), `eatStart`, `eatSource` (struct: loop = `{Section, index}`)
- **แหล่งอาหาร (loop)**: `RunLoop.PeekFood(out Section s, out int i)` = อาหารชิ้นแรกใน carry (ลำดับเดียวกับ `EatFirst`: Foods แล้ว Supplies); เริ่มกินเมื่อ: `F` ค้าง + ไม่มีเพื่อนล้มใน 3 ม. (`ReviveInstead` false) + `hp < MaxHp − 0.5` + มีอาหาร + `canInput` + ไม่กำลังฟัน/กลิ้ง/ยกโล่ + ไม่ได้ขยับ; ปุ่ม C และคลิกอาหารใน Carry window (แล้วปิดหน้าต่าง) เข้าทางเดียวกัน
- ระหว่างกิน: `speedMul = 0`; ขัดจังหวะ (ความคืบหน้าหาย **อาหารไม่หาย** — ไม่ถอด item จนกว่าจะครบ): ถูกตี (`LastHurtTime` เปลี่ยน รวมโดนโล่ลดดาเมจ — เพิ่ม set ใน `TakeDamage` ทุกเส้นทางที่ไม่ใช่ parry/clash/dodge), `Space`, ปล่อย F, มี input เดิน, `Dead`, panel เปิด; คลิกฟัน/คลิกขวายก = ยกเลิกกินแล้วทำท่านั้นทันที
- ใช้ `Time.unscaledDeltaTime` นับ 2.0 วิ (เหมือนการชุบใน `TeamRun`) ครบ → `RunLoop.EatNow(sec, idx)` (ฟังก์ชัน `Eat` เดิม: ถอด item, `CollectFood` heal, `Save()`, `team.PackChanged()`) — ไม่มีข้อความเครือข่ายใหม่; host เห็นผลผ่าน `Pack` ตามกลไก grant/mirror เดิม
- **ซิงก์**: flag `NetEating = 64` ใน `NetFlags` (บิต 32 `NetCharging` ว่าง ห้ามนำกลับมาใช้ในโปรโตคอลนี้) + Act kind 21 เมื่อเริ่ม (เพื่อให้ `ActionsSeen`/`LastAction` ของ remote นับได้ในเทสต์); remote เล่น `Act.Eat` ตราบที่ flag ขึ้น; ไม่ส่ง progress (แถบ "เหนือหัว" โชว์เฉพาะเครื่องตัวเอง)
- แถบความคืบหน้า: `Hud` สร้าง `eatBar` (Panel เล็ก 1.2 ม.-เทียบ ~96×10 px) ตามหัวผ่าน world→screen แพทเทิร์นเดียวกับ `Popup` + ข้อความ `EATING`/`Hold F: Eat` (สี #5FA85A ซึ่ง**ไม่อยู่ในจานสี** → ใช้ "ใบไม้กลาง" #6C8535 หรือ "หญ้าสว่าง" #8DB04A ตามที่ design อนุญาต — ดู D5)
- **สนาม v1 (`!Loop`)**: คงพฤติกรรมเดิม (เดินทับกินทันที) — เหตุผลและข้อตัดสินดู D1

## 6. การถอดของเดิม (Net kind / ตัวแปร / คอมโบ / สกิล)
| ของเดิม | ที่อยู่ | ทำอย่างไร |
|---|---|---|
| คอมโบ 5 ท่า + phase 6 ท่า 5..11 + Heavy | `Knight.Attacks[12]`, `ComboLen`, `HeavyIndex`, `NetKind`, `AttackFromNetKind`, `PickAttack` | แทนด้วย `SwingDef[3]`; kind 0..2 |
| สกิล Q/E/R/F | `Knight.Skills`, `TrySkill`, 4 coroutine, `cd/cdMax`, `Progress` skill arrays | ลบ; `ReadInput` ตัดแต่ Q/E/R; F ไม่ส่งสกิล |
| ชาร์จ | `Charging`, `ChargeSeconds`, `ChargeFull`, `ShowsCharge`, `netCharging`, `NetCharging(32)`, `ring`, `ChargeTime`, glow ใน `Pose` | ลบ (`TeamRun:344` อ้าง `Charging` → ตัด) |
| Heavy flag ของฝั่งผู้เล่น | `Game.HitEnemy/ClientHit` ส่ง `HeavyIndex`, `EnemyHit flags 8` | ส่ง `false` เสมอ (บิต 8 คงไว้ในรูปแบบ ไม่ใช้ — กฎ `KnightHit flags 1` ของศัตรูไม่เกี่ยว ห้ามแตะ) |
| Hit.src | `Net.SendHit`/`ClientHit` | รับ 0..2, 37 (parry), 38 (clash); อื่นๆ `return false` |
| Act kind | `Net.cs` host relay + `PlayRemoteAct` | 0..2, 20 (roll), 21 (eat), 22 (parry); kind อื่นถูกทิ้งเงียบ (ไม่ throw) |
| `Net.Protocol` | `Net.cs:276` | **5 → 6** ครั้งเดียวที่ขั้นที่ 3 (ทุกขั้นหลังจากนั้นเป็นของ protocol 6; ห้ามส่ง build กลางทางให้เล่นออนไลน์) |
| ปุ่ม | `Knight.ReadInput`, Hud ช่วยเหลือ 2 จุด | Q/E/R ไม่อ่าน; F = ชุบ/กิน; 1–5 การ์ด (ปุ่ม 1/2 สลับอาวุธของ `RunLoop.WorldKeys` ทำงานเมื่อ !PanelOpen เท่านั้น จึงไม่ชนกับการ์ด) |
| `EnemyHit`/`KnightHit` | ไม่แตะ | — |

## 7. ข้อมูลที่ต้องเก็บ / ผลต่อระบบ save
**ไม่มีข้อมูลใหม่ที่ต้องเซฟ.** `Progress` ทั้งก้อน (เลเวล/การ์ด/แต้ม) ไม่ถูกเซฟข้ามรอบอยู่แล้ว; `LoopSave`/`Loadout`/`OldWorld` ไม่อ้างสกิล/การ์ด/คอมโบ → เซฟเดิมโหลดได้เหมือนเดิม **ไม่ต้อง migrate ไม่ต้องขึ้น Version** (ยัง `rc`/`pv:2` เหมือนเดิม)
- ที่ต้องรักษา: `Foods.All[id]`/`Item.Food` เดิม (ไม่เปลี่ยน id/ลำดับ) เพื่อให้ carry/stash ในเซฟเดิมที่มีอาหารยังถูกต้อง
- `PlayerPrefs`: ไม่เพิ่มคีย์ (`mk_campitch` เดิม); ผู้เล่นเก่ามีคีย์สกิล? ตรวจแล้วไม่มี
- ข้อมูลเฉพาะรอบ (RAM): `Progress { level, exp, totalExp, pendingChoices, kills, statPoints, int[5] attr }`
- ค่าสเตตัส (ค่าคงที่ใน `Progress.cs`):
  | การ์ด | ต่อ 1 แต้ม | ที่ใช้ |
  |---|---|---|
  | HP | MaxHp +**20** (`HpPerPoint` เดิม; design wireframe เขียน +10 แต่ข้อความระบุ "ค่าเดิม" → ใช้ 20 ดู D6) + เติมเลือดทันที | `Knight.MaxHp` |
  | DEF | Armor +10, `BlockReduction` +1.2% (ฐาน 80%), `BlockStaminaCost` ×(1−0.04n) | `MaxArmor`, `TakeDamage` |
  | STAMINA | max +15, regen +5% | `MaxStamina`, regen ใน `Update` |
  | SPEED | `MoveSpeed`/`RunMult` ×(1+0.03n) | `Simulate` (เฉพาะเดิน/วิ่ง ไม่รวมกลิ้ง/ถลาท่าฟัน) |
  | RESIST | ดาเมจที่รับ ×(1−0.025n) และ knockback ×(1−0.05n) | `TakeDamage` หลังคิดโล่ก่อนแบ่ง armor/HP |
- `AttrCap` 10 เดิม; `Progress.Spend(Attr)`, `CanSpend`, `AttrName` (HP/DEF/STAMINA/SPEED/RESISTANCE) ปรับตามเดิม; `ForceCrit` (hook เทสต์) คงไว้; crit chance/mult = ค่าอาวุธ + `BaseCritChance 0.10`/`BaseCritMult 1.5` ค่าคงที่ (เดิมมาจาก `crit[]` อยู่ศูนย์) — ตรวจ `weapons-and-tab.md` แล้ว: ตัวคูณ Damage/Speed/Crit มาจากอาวุธเท่านั้น (ตามดีไซน์)
- `Game.OpenLevelUp()`: `LevelStep` เหลือ 0/1 (ลบค่า 2); ถ้า `!progress.CanSpendAny` ปิดเลย; `ChooseStat(index 0..4)` → `knight.SpendPoint` → `pendingChoices--` → ปิด; `AddExp` ยัง `pendingChoices++ / statPoints++` ต่อเลเวล (หลายเลเวลต่อเนื่องเปิดการ์ดซ้ำทีละใบตามที่ `Update` ทำอยู่แล้ว)
- Hud การ์ด: `CardUi[5]`, เรียงแถวเดียว ~164×230 px ระยะห่างตามความกว้างจอ (ข้อความ 5 ใบต้องไม่ล้น: ตัดบรรทัด detail เหลือ 2 บรรทัด), `LevelUpInput(5)`, ข้อความ "keys 1–5"

## 8. ลำดับขั้นการทำ (ทุกขั้น `build.bat` ผ่าน และ selftest ส่วนที่เกี่ยวข้องผ่านก่อนไปขั้นต่อไป)
commit ก่อนแก้ทุกขั้น; เทสต์แบบเต็มทำที่ขั้น 6 และ 10 เท่านั้น (selftest เต็ม ~7 นาที — ระหว่างทางใช้ `-slashonly` / `-p7only` / `-looponly`)

1. **ฐานข้อมูล (ไม่เปลี่ยนพฤติกรรม)**: เพิ่ม `KnightAnim.cs` (ตาราง+`ActClock`+ฟังก์ชันคำนวณจังหวะล้วน), `ModelLib.cs` (ยังไม่มีใครเรียก). เพิ่มเคสเทสต์ pure-math (ข้อ 9.1). build ผ่าน, selftest เดิมผ่านทั้งหมด
2. **แยก CodedBody (refactor ล้วน)**: ย้าย `Build()/Pose()/LateUpdate()/Tint/shield` ไป `KnightBody.cs` + `KnightGear`; Knight เรียกผ่าน `body`; `SwordModel/RebuildSword/Equip` ทำงานเหมือนเดิม. พฤติกรรมเดิม 100% → selftest เต็มชุดเดิม + `mptest.ps1` ต้องผ่านเท่าเดิม (นี่คือจุดพิสูจน์ว่า refactor ไม่พัง)
3. **ท่าฟัน 3 ท่า (ถอดคอมโบ/phase 6/Heavy/ชาร์จ)**: ใส่ `SwingDef[3]`, `SwingRoutine`, `HitCapsule`, อินพุตคลิก-ทันที, combo chain; ลบ `Attacks[12]` เดิม, `Charging*`, `PickAttack`, `NetKind*`; แก้ `Game.HitEnemy/ClientHit`, `TeamRun:344`, `Hud` ข้อความ action; **`Net.Protocol = 6`**; ลบ Phase6* ใน SelfTest + หัวข้อ 3b/3e ใน MpTest; เพิ่มเคส slash (9.2). ยังเหลือสกิลอยู่ชั่วคราว (แปลง `DamageMult(skill)` ให้ยังคอมไพล์)
4. **ถอดสกิล Q/E/R/F**: ลบ `Skills/TrySkill/coroutine 4 ตัว/cd`, `Hud` แถบสกิล+`SlotUi`, `ReadInput` Q/E/R/F, `Net` kind 10..13, `Progress` skill arrays; F = ชุบเพื่อนอย่างเดียว; ลบเคสสกิลใน SelfTest/MpTest; ช่อง SKILLS ใน Tab เว้นว่างไว้ก่อน (ใส่ใน ขั้น 7). `ProgressTests` ที่พึ่ง `MakeOffers` จะถูกแก้ในขั้น 7 → **ในขั้นนี้ให้เปลี่ยน `MakeOffers` ให้คืนอาร์เรย์ว่าง** เพื่อให้ build/เล่นได้ (ไม่มีการ์ดสกิล = ข้ามไปขั้นลงแต้ม; ตรงกับทางที่ `OpenLevelUp` เดิมรองรับ "ทุกการ์ดเต็ม")
5. **กลิ้ง + parry motion**: เปลี่ยนค่าคงที่ dash→roll, `Rolling`, `RollDef`/`ParryDef`, `ParryRoutine`, Act 20/22, `CodedBody` poses ของกลิ้ง/ยกโล่; แก้เคส dash เดิมใน SelfTest (หัวข้อ run/dash) + MpTest 3c (คงเคส parry)
6. **ด่านตรวจกลาง**: selftest เต็ม + `mptest.ps1` + `mptest-loop.ps1` + `-menutest` ต้องผ่าน ก่อนทำเรื่องเสี่ยงสูง (UI/อาหาร/สเตตัส)
7. **การ์ดสเตตัส 5 ใบ**: `Progress` ใหม่, `Knight` ใช้ speed/resist/block-stamina/regen, `Game.OpenLevelUp` ขั้นเดียว, `Hud` การ์ด 5 + กล่อง STATS ใน Tab + ข้อความช่วยเหลือ, `InputX` Keypad4/5; เขียนเคส SelfTest ที่ผูกการ์ดเดิมใหม่ (หัวข้อ level-up 1058/1302/1365/1478/1491 ใน SelfTest)
8. **กินอาหาร**: `Knight` eat state, F hold, `RunLoop.PeekFood/EatNow`, C/Carry-window routing, `Hud` แถบกิน, flag 64 + Act 21, pose กิน (CodedBody), ความสัมพันธ์กับชุบเพื่อน (`TeamRun`); เคส 9.3
9. **FileBody + ระบบรับ fbx** (ต้องมีไฟล์ที่ผู้ขอยืนยัน license แล้ว):
   - 9a spike เล็ก: โหลด `knight.fbx` + 1 clip เล่นผ่าน PlayableGraph ใน `-modeltest` (ซีนทดสอบ ไม่แตะเกม) พิสูจน์: Generic clip จากไฟล์แยกจับกับ rig ได้, Manual evaluate ทำงานบน Unity 6000.6.5f1, Tint ทำงานกับ SkinnedMeshRenderer, สเกล/จุดหมุนที่เท้า
   - 9b `FileBody` เต็ม + `clipMarks` ทั้ง 3 ท่า + socket ดาบ/โล่ + ทาสี + crossfade; fallback ทดสอบโดยเปลี่ยนชื่อไฟล์ชั่วคราว
   - 9c ถ้าจะใช้ effect จากไฟล์: ผูก `EffectLib` กับริ้ว/ประกาย (ไม่บังคับ)
10. **ปิดงาน**: selftest เต็ม, `mptest.ps1`, `mptest-loop.ps1`, `-menutest`; ถ่ายภาพ 3 ท่า/กลิ้ง/ปัด/กิน เทียบ design; วัด frame time (4 ผู้เล่น fbx พร้อมกัน), ขนาดบิลด์/RAM; อัปเดตเอกสาร reference + CLAUDE.md เกม; zip ขึ้น Drive ตามกฎ

ขั้น 3–5 อาจเสร็จก่อนมีไฟล์ fbx → ผู้เล่นเห็นท่าใหม่ครบด้วยโมเดลโค้ด (ลด risk ทีละก้อน) ส่วนขั้น 9 รอ asset

## 9. แผนเทส

### 9.1 SelfTest — pure (ขั้น 1)
- ตาราง `SwingDef` รวม 0.72/0.76/0.78 วิที่ speed 1.0; ที่ speed 2.0 สามจังหวะแรกครึ่งเดียว ดึงกลับคงที่; hitbox window = 25–85% ของจังหวะฟัน; `ActClock.At` ตรงกับตัวเลขที่ปัดเศษ
- `ModelLib.Model("ไม่มีไฟล์")` = null ไม่ throw; `EffectLib.Play("ไม่มี")` ไม่ throw

### 9.2 SelfTest — `-slashonly` (ขั้น 3–5; ~2 นาที)
1. คลิก 3 ครั้ง (mouse เทียม) → `LastAction` = ท่า 1,2,3,1; ไม่คลิก >0.4 วิหลังดึงกลับ → ท่า 1; คลิกใน "หลังฟัน" → ช่องว่างระหว่าง `ActionsSeen` สองท่าสั้นกว่าจบดึงกลับ
2. หุ่นฟางโดน **ครั้งเดียว** ต่อท่า และดาเมจ = base × mul × อาวุธ (ปิด crit ด้วย `Progress.ForceCrit=1`); พัดเฉียง 100°/กวาด 150°: วางเป้าที่มุม ในกรอบ/นอกกรอบ (เช่น 140° ท่าเฉียงไม่โดน ท่ากวาดโดน); แทง: 2 เป้าเรียงแนวหน้าโดนทั้งคู่ เป้าห่างด้านข้าง >0.4 ม. ไม่โดน; ระยะคูณ `Weapon.reach`
3. ยกโล่ค้างตอนคลิก = ไม่ฟัน; กลิ้งกลางท่า → ท่าตัด `IsAttacking=false`, ท่าถัดไปกลับเป็นท่า 1; ล้มกลางท่า → ไม่ค้าง (`IsAttacking`, `lockFacing` ล้าง)
4. ถอดแล้ว: กด Q/E/R ไม่เกิด action (`ActionsSeen` เท่าเดิม), F โดดไม่ได้, ค้างซ้าย 1.5 วิไม่เกิด Heavy, `Space` ไม่เกิด Dash Slash, `Knight.Attacks.Length == 3`
5. กลิ้ง: ระยะ 6±0.3 ม. ใน 0.4±0.05 วิ, stamina −25, คูลดาวน์ 0.8 วิ (กดซ้ำก่อนหมด = ไม่กลิ้ง), stamina ไม่พอ = `StaminaDeniedTime` อัปเดต + ไม่กลิ้ง, อมตะ 0.5 วิ (ยิง `H` ผ่าน `TakeDamage` → `DodgeCount++`), ไม่กดทิศ = ถอยหลัง, หลังกลิ้งฟันได้ทันทีเริ่มท่า 1
6. parry motion: ใช้ `Game.DebugStrike` (hook phase 7 เดิม) ตอนยกโล่ในหน้าต่าง 0.2 วิ → `ParryCount+1`, เฟสเรียง 1→4 ตามเวลา 0.15/0.12/0.25/0.20 (±0.05), ฟันตัดได้เฉพาะหลังจังหวะ 2; ยกโล่เปล่าไม่เกิด motion; เคส phase 7 เดิม (`-p7only`) ผ่านเท่าเดิม
7. block-stamina: ยกโล่รับ 1 ตี −10 stamina, DEF 10 แต้ม −6, stamina 0 = รับเต็ม

### 9.3 SelfTest — กิน/การ์ด/สเตตัส/โมเดล (ขั้น 7–9)
- กิน (loop, ผ่าน `StartLoopWorld(setup)` ให้มี Apple ใน carry): ค้าง F 2.0 วิ (ตั้ง `moveOverride` ว่าง) → HP +15% และ item หาย; ปล่อย F ที่ 1.5 วิ → ไม่เพิ่ม ไม่หาย; โดน `TakeDamage` กลางทาง (ทั้งแบบโล่ลดดาเมจ) → รีเซ็ต; Space/เดิน → รีเซ็ต; HP เต็ม → ไม่เริ่ม + "HP full" (`fullHints++`); มีเพื่อนล้ม ≤3 ม. (ใช้ hook `Down`) → F ชุบไม่กิน; คลิกฟันระหว่างกิน → ยกเลิกกิน + ฟัน; ล้ม → ล้างสถานะ; ตรวจ Pack/`team.PackChanged` ถูกเรียก (ออนไลน์: mptest-loop)
- Tab คลิกอาหาร → เริ่มกินแบบจับเวลา + หน้าต่างปิด; `EatFirst()` ยัง instant (hook เทสต์)
- การ์ด 5 ใบ: ขึ้นเลเวล → เห็น 5 การ์ด `LevelStep==1` เท่านั้น; กด 1..5 (และ Keypad) และคลิกกลางการ์ดที่ 4/5 เลือกได้; ค่าต่อแต้มตรงตารางข้อ 7 (HP/Armor/block%/stamina/regen/ความเร็วเดิน วัดจากระยะที่เดินใน 1 วิ/ลดดาเมจจริงจาก `TakeDamage`); เพดาน 10 แต้ม (เต็ม = การ์ดสีเทา เลือกไม่ได้ ไม่ปิด panel); ขึ้นเลเวลหลายเลเวลติด → เปิดซ้ำตามจำนวน; ไม่มีการ์ดความสามารถ; ตายแล้วรีเซ็ต; `F5` รีเซ็ต
- ค่ารวมสุทธิ: เปรียบ "ดาเมจดาบ" ระหว่างเลเวล 1 กับ 25 ต้องเท่ากัน (ยืนยันว่าอัปเกรด Damage ถูกถอดจริง)
- โมเดล: (ก) ไฟล์ไม่มี → `k.BodyKind=="coded"` เกมเล่นครบทุกท่า; (ข) ไฟล์มี → `"file"`, หา socket เจอ, `SwordModel.name==Weapon.name` + `Find(SwordModel,"Tip")` ได้, tri ≤5,000/ตัว, ทุก slot สีอยู่ในจานสี (เทียบด้วยค่าคลาดเล็กน้อย), ถ่ายภาพ `Shot("rig_idle")` ฯลฯ ใช้เทียบด้วยตา; (ค) สลับอาวุธ (Tab) ดาบเปลี่ยนที่มือ (เคส Banana Dao ที่มี fbx เป็นตัวพิสูจน์ว่าไม่ชนกัน)
- เซฟ: เคส `LoopSaveTests`/`OldWorldSaveTests`/`LoopBaseTests` เดิมต้องผ่านโดยไม่แก้; เพิ่ม fixture เซฟจากบิลด์ก่อนแก้ (commit `HEAD` ก่อนงานนี้) โหลดแล้วอาหารใน stash/carry ครบ

### 9.4 `mptest.ps1` (arena v1) — แก้ MpTest.cs
- หัวข้อ 3: เปลี่ยนเป็น "ท่าฟัน 1/2/3 ของ client ถูกเล่นซ้ำ 1 ครั้งต่อหน้าจอ (`an`, `ac`) + ดาเมจที่ host ตรง base×mul" (ใช้ `atk 0|1|2`), กลิ้ง (`atk` ใหม่หรือคำสั่ง `roll`: kind 20 ถึงทุกหน้าจอ, ตำแหน่งปลายทางตรง), parry motion (host ได้ kind 22 → `pm` นับ), กิน (คำสั่ง `eat` ในโหมด loop เท่านั้น — ใน arena ไม่ทดสอบ เพราะ v1 คง instant), flag `eat` ขึ้น/ลงบนทุกหน้าจอ (`Sum()` เพิ่มคีย์ `eat<pid>`)
- ลบ: 3 (skills), 3b (charge/Heavy/dropped charge), 3c-e (heavy number colours ของ Heavy Strike; ส่วน a–d parry/clash/hostparry **คงไว้**); ตัดคำสั่ง `skill/holdon/holdoff` และคีย์ `chg`
- `hunt` ที่เรียก `DebugAttack(swings++ % 2)` ใช้ได้ต่อ (ท่า 0/1)
- ผ่านเกณฑ์เดิม: เพิ่ม `-expect` ตามจำนวนเช็คใหม่ (แก้ใน `.ps1` ถ้าตัวเลข fixed — ตรวจตอนทำ)

### 9.5 `mptest-loop.ps1` (MpLoopTest.cs)
- เคสเดิมที่เรียก `run.EatFirst()` ยังเป็น instant → ผ่านเท่าเดิม
- เพิ่ม: A "give f Apple" + hp ต่ำ → คำสั่งใหม่ `eat` (ผ่านเส้นทางจับเวลาจริง) → รอ ≥2.1 วิ → carry ของ A ลดลง 1 ชิ้นบน host mirror (`Pack` ไปถึง) และ HP เพิ่ม; ขัดจังหวะ (`TakeDamage` ผ่านคำสั่ง bait/`H`) → mirror ไม่ลด; ยืนยันว่า wipe/กองหลังกินใช้ carry ที่ลดแล้ว (ไม่ทำของซ้ำ)
- เพิ่ม: กินระหว่างเพื่อนล้ม → F เป็นชุบ ไม่ใช่กิน

## 10. ความเสี่ยง / สิ่งที่อาจพังของเดิม
| # | ความเสี่ยง | ผลที่อาจเกิด | วิธีกัน |
|---|---|---|---|
| R1 | **Knight.cs ถูกแตะเกือบทั้งไฟล์** | บั๊กซ่อน ที่ขอบเขตของ refactor (ขั้น 2) | ขั้น 2 เป็น refactor ล้วนพร้อม selftest เต็มก่อนเปลี่ยนพฤติกรรม; commit แยกทุกขั้น |
| R2 | **เสียสมดุลเกม**: ไม่มีการ์ดดาเมจ/ความเร็ว/คริ แต่ศัตรูยังโต +45% HP/เลเวล | เลเวลสูงสู้ไม่ได้ / ตัวเลข "ผ่านใน selftest" แต่เล่นจริงตัน | D3 — เสนอปรับ `EnemyHpScale` หรือให้ผู้ขอทดเล่น; dev เพิ่ม log เวลาฆ่า Slime/Minotaur ต่อเลเวลใน selftest |
| R3 | **เซฟ/loop**: ไม่กระทบ (ไม่มีฟิลด์สกิล) แต่ `Foods`, `Item.Food`, `RunLoop.Eat` ถูกเรียกเส้นทางใหม่ | อาหารหาย/เบิ้ลถ้าขัดจังหวะผิดจังหวะ | ถอด item เฉพาะตอนครบ 2 วิในจุดเดียว (`EatNow`); เคส interrupt ทุกแบบ + mptest-loop |
| R4 | **ออนไลน์**: protocol 6 ทำให้บิลด์เก่าเข้าไม่ได้ (ตั้งใจ); kind 20 เปลี่ยนความหมายแต่เลขเดิม | โปรโตคอลเพี้ยนถ้า dev ลืมแก้ที่ใดที่หนึ่ง (host relay ไม่ validate kind) | ขั้น 3 bump ทันที; `PlayRemoteAct` ทิ้ง kind ที่ไม่รู้จักเงียบ; `mptest-loop` ผ่านก่อน release |
| R5 | **Playables/Generic clip จากไฟล์แยก** อาจจับกระดูกไม่ตรง หรือ Unity 6000.6.5f1 มี API ต่างจากที่คาด | FileBody ใช้ไม่ได้ทั้งตัว | spike 9a ก่อนลงแรง; ทางสำรอง: ตั้ง Humanoid ผ่าน `AssetPostprocessor` ใน `Assets/Editor/` หรือรวม clip ทั้งหมดไว้ใน fbx เดียว; ยังมี fallback CodedBody เสมอ |
| R6 | **Mixamo**: ไม่ใช่ CC0/CC-BY, clip บางท่าไม่มี (ฟันเฉียงบนซ้ายล่างขวา/แทง/กลิ้ง/กิน) และความยาวไม่ตรง | งานปรับ clip เพิ่ม / ติดขัดแหล่ง | ตาราง marks ปรับ timing แทนการตัด clip; ท่าไหนไม่มี → ใช้ CodedBody ทั้งตัวไปก่อน (all-or-nothing); พีทรับทราบ Mixamo แล้ว (decisions ข้อ 4) แต่ต้องรายงานแหล่ง/ขนาด/tri ให้ผู้ขอยืนยันก่อน commit fbx; อย่าวาง fbx ใน repo สาธารณะ |
| R7 | **ประสิทธิภาพ**: 4 SkinnedMesh + 4 PlayableGraph บน build ที่มี `StaticBatcher` + enemies | เฟรมตก | จำกัด ≤5,000 tri/ตัว; อัปเดต graph เฉพาะที่มองเห็น/ใกล้กล้อง; วัดใน ขั้น 10 (selftest มี frame-time เดิมที่ข้อ "the big map") |
| R8 | **เทสต์ที่ผูกพฤติกรรมเก่า** (SelfTest ~3,857 บรรทัด; ราว 1,000 บรรทัดเป็น phase 6, เคส stamina/dash/ block/ level-up/ drop ที่พึ่งโล่ฟรี-ดาเมจอัปเกรด) | FAIL เพราะ test ล้าสมัย ไม่ใช่บั๊ก | แก้เทสต์ในขั้นเดียวกับที่เปลี่ยนพฤติกรรม; ห้ามลบเช็คที่ยังมีความหมาย (เช่น reach, crit, armor) — แค่ปรับค่าที่คาด |
| R9 | **ชุบเพื่อน vs กิน** ชน F เดียวกัน; และกิน vs `canInput` (Tab/level-up panel) | กินขณะเปิด panel หรือชุบไม่ติด | `TeamRun` เป็นผู้ชนะเสมอ (`ReviveInstead` ตรวจก่อน); `canInput` false = ยกเลิกกิน; เคส 9.3 |
| R10 | **สีผู้เล่น 4 คน** (เดิม tabard/โล่/พุ่ม/ผ้าคลุมต่างกันตาม pid) vs ข้อกำหนดจานสี | ออนไลน์แยกคนยากขึ้น | D2; ชื่อเหนือหัว/ป้าย DOWN (`TeamUi`) ช่วยอยู่แล้ว |
| R11 | **hitbox ต่อเนื่องหลายเฟรม + ส่ง `Msg.Hit` ต่อศัตรู** | ส่งซ้ำ/เกิน `MaxHitsPerSecond` | `once` set ต่อท่า; ตรวจจำนวน hit ต่อท่าใน selftest (1/ตัว) |
| R12 | hit-stop/`Time.timeScale` ตอนฟัน | กิน/กลิ้งใช้เวลาผิด | ใช้ `unscaledDeltaTime` สำหรับกิน; `Time.time` สำหรับท่า (เหมือนเดิม) |
| R13 | `Airborne` ไม่มีผู้ตั้งค่า | โค้ดศัตรูยังเช็คอยู่ (ไม่พัง) | คงพร็อพเพอร์ตี้; ไม่แตะศัตรูตาม requirement; ใส่ใน backlog ให้ลบทีหลัง |

## 11. สิ่งที่ต้องให้ปูส้ม/ผู้ขอตัดสิน (ก่อนส่ง developer)
- **D1 กินอาหาร**: แนะนำให้ใช้ "ค้าง F 2 วิ กินอาหารจาก carry" (loop) และให้สนาม v1 (`-arena/-single`) คง "เดินทับกินทันที" เพราะเป็นสนามเทสต์ ไม่ใช่เกมหลัก และการล็อกอาหารบนพื้นข้ามเครื่องต้องเพิ่มข้อความเครือข่ายใหม่ (เสี่ยง/ขนาดงานเพิ่มชัดเจน) — design ข้อ 6 เขียนเป็น "อาหารบนพื้น ≤1.2 ม." ซึ่งไม่ตรงกับกลไก loop จริง ถ้าต้องการให้กินจากพื้นใน loop ด้วย ต้องขยาย (เพิ่ม host lock ~ขนาดงานกลาง→ใหญ่)
- **D2 สีผู้เล่น**: จานสีมีผ้าคลุมให้เลือก #B5523B/#5E5878 เท่านั้น แต่มีผู้เล่น 4 คน — เสนอใช้ 4 สีจากจานสี: #1594BB (น้ำ), #B5523B, #6C8535, #5E5878 ตาม pid กับผ้าคลุม+ผ้าตกแต่งโล่
- **D3 บาลานซ์**: ผู้เล่นไม่มีการ์ดดาเมจ แต่ศัตรูโตตามเลเวล — เสนอปรับ `Game.EnemyHpScale` (0.45→ประมาณ 0.15–0.20 ต่อเลเวล) หรือรับความเสี่ยงแล้วค่อยปรับหลังเล่นจริง (นอกขอบเขต requirement จึงไม่ลงมือเอง)
- **D4 ดาเมจฐานของ 3 ท่า**: design ให้แต่ตัวคูณ (1.0/0.9/1.2) — เสนอฐาน 14 (เดิม Side Slash 10 แต่คอมโบเดิมเฉลี่ย ~15 และมีหน่วง 0.5 วิ ซึ่งตอนนี้ไม่มี) ปรับค่าคงที่เดียวใน `KnightAnim.cs`
- **D5 สีแถบกิน**: #5FA85A ไม่อยู่ในจานสี `style.md` → ใช้ #8DB04A (หญ้าสว่าง) ตามที่ design เปิดทางไว้
- **D6 HP ต่อแต้ม**: +20 (เดิม) หรือ +10 ตามตัวอย่าง wireframe ของ design (ถ้าเลือก +10 ต้องแก้ `HpPerPoint` และ selftest ค่าสูงสุดเลเวล 25)
- ข้อตัดสินที่ architect ตัดเองแล้ว (แก้ได้): คิวคลิกใช้เมื่อ "หลังฟัน" จบ (4.2); parry motion ตัดได้หลังจังหวะ 2 (5.2); กลิ้งแบ่ง 20/60/20 (5.1); Tab: รวม STAT POINTS กับ SKILLS เป็นกล่อง STATS 5 แถวเดียว (ไม่ทำกล่องซ้ำ); `Airborne` คงไว้; ท่ากินในสนาม v1 คงเดิม

## 12. ประมาณขนาดงาน: **ใหญ่**
เหตุผล: แตะ Knight.cs เกือบทั้งไฟล์ + แยกชั้นแสดงผลใหม่ + ระบบ animation/ไฟล์ใหม่ + เปลี่ยน protocol + เขียนเทสต์ใหม่ทั้งชุด (SelfTest ~1,000 บรรทัดต้องเขียนใหม่/ลบ, MpTest 2–3 หัวข้อ) และขึ้นกับ asset ภายนอก (license/clip ไม่ครบ). ถ้าแบ่ง: ขั้น 1–8 (โค้ดล้วน ใช้โมเดลโค้ดเดิม) ≈ กลาง–ใหญ่, ขั้น 9 (fbx + animation) ≈ กลาง และเสี่ยงที่สุด
