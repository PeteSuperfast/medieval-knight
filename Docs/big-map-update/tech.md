# Tech design: big-map-update (medieval-knight)

- อ้างอิงโค้ด: branch `main` commit `76fe974` (ที่ checkout อยู่)
- ผู้ออกแบบ: architect, วันที่ 2026-10-11 (แก้ครั้งที่ 2: ปรับให้ตรง design.md — ผังแมพ เมืองทางใต้ โซน 8 ชนิด หน้าต่าง Tab หน้าจอตาย Soul บน HUD ฯลฯ)
- คู่กับ `design.md`: ตัวเลขสมดุลและกติกาในเอกสารนี้เป็นตัวตั้ง ส่วนผังแมพ layout UI ใช้ตาม design.md (ดูหัวข้อ 13)

## 0. สรุปการตัดสินใจหลัก

1. สนามเดิมคือ `Game.ArenaHalf = 19f` = **38 x 38 ม.** (ยืนยันจากโค้ด) → ใหม่ `ArenaHalf = 190f` = **380 x 380 ม.** ศูนย์กลาง origin; X ไปตะวันออก, Z ไปเหนือ, กล้องไม่หมุน (`Game.PlaceCamera` ใช้ `Quaternion.Euler(CamPitch,0,0)` ตายตัว) → เหนืออยู่ด้านบนจอ
2. **ไม่ต้องเปลี่ยน encoding ตำแหน่ง** `NetW.Pos` (cm, i16 ±327.67 ม.) รองรับ ±190 ม. แต่ต้องเพิ่ม `Net.Protocol` เป็น **2** เพราะ payload ของ `Snapshot` (pickup), `PickupTaken`, `Exp` เปลี่ยน และมี `Act` kind ใหม่ (dash) โดย **ไม่ต้องเพิ่ม `enum Msg` ตัวใหม่**
3. ผังแมพตาม design: **เมืองอยู่ทางใต้** (สี่เหลี่ยม X -60..60, Z -190..-100) จุดเกิด (0,-150) รังอยู่เหนือสุด (0,+150) ห่างกัน **300 ม.**; ฉากสร้างเป็น **chunk 38 ม. (10 x 10 = 100 chunk)** รวม mesh ด้วย `Mesh.CombineMeshes` ต่อ chunk ต่อ material
4. สไลม์เกิดเป็น "แพ็ค" รอบผู้เล่นแต่ละคน นอกจอ 15-26 ม. ไม่เกิดในเมือง/ทะเลสาบ/ภูเขา ลบเงียบเมื่อไกลทุกคน > 70 ม. Minotaur อยู่รังคงที่ + leash; **ไม่มีประกาศ "Wave n"**
5. Space = dash (stamina 25, cooldown 0.8 วิ, 6 ม., อมตะ 0.5 วิ), Shift = วิ่ง x1.5, stamina ใช้กับ dash อย่างเดียว (แถบมีขีดแบ่งทุก 25), โล่ฟรี
6. Level 25: ตาราง EXP รวม 16,410, EXP ต่อมอนสเตอร์สเกล 1+0.15 ต่อเลเวล, `AttrCap` 10 (รองรับ 24 แต้ม)
7. ตายแล้วรีเซ็ต: `Game.OnKnightDown` แสดงหน้าจอ YOU DIED (ค่าก่อนรีเซ็ต) → `OnKnightRevive` เรียก `Knight.ResetProgress()`; เวลาฟื้นเล่นคนเดียว 1.4 → 3 วิ; Soul เก็บที่ `Knight.souls` (ไม่เซฟข้ามเซสชัน เกมนี้ไม่มีระบบ save); อาหารใช้โครง `Pickup` เพิ่ม `kind`
8. ไอคอนดาบ: render โมเดลจาก `Weapons.BuildModel` ด้วยกล้อง ortho layer 31 → RenderTexture ตอนเริ่มเกม → คัดลอกเป็น `Texture2D` ใช้กับ `RawImage` (แผน B วาดพิกเซล)
9. นำทาง (ขอบเขตหลัก): ถนนดิน + ป้ายบอกทาง + landmark สูง + ลูกศรชี้รังพร้อมระยะ — **ไม่มี minimap** (เป็น "ตัวเลือก" หัวข้อ 9.6)
10. branch `wip/big-map-update`: **เริ่มจาก `main` ใหม่** (ดูหัวข้อ 9.1) — architect อ่านได้เฉพาะไฟล์ที่ checkout อยู่ ดูเนื้อ wip ไม่ได้

---

## 1. ภาพรวมแนวทาง

ใช้แพทเทิร์นเดิมทั้งหมด: โค้ดล้วน ไม่มี prefab, host-authoritative, client เชื่อใจเรื่องอัศวินตัวเอง, ข้อมูลผู้เล่นอยู่บน `Knight`. งานแบ่งเป็น 3 ก้อน: (A) กติกาผู้เล่น — input, dash, วิ่ง, stamina, เลเวล 25, ตาย/รีเซ็ต, Soul, อาหาร; (B) UI — Tab + ไอคอนดาบ, แถบ stamina มีขีด, Soul บน HUD, หน้าจอตาย; (C) โลก — แมพ 380 ม. แบบ chunk + โซน + collision + ระบบเกิดศัตรูใหม่. A, B ทำบนแมพเดิมได้และ build ผ่านทีละขั้น; C เสี่ยงและใหญ่สุดจึงทำท้าย

---

## 2. ไฟล์ที่จะสร้างใหม่ / แก้

### 2.1 ไฟล์ใหม่ (ใต้ `Assets/MedievalKnight/Scripts/` — Unity สร้าง .meta ตอน build ให้ commit ไปด้วยถ้าโปรเจกต์ track .meta)

| ไฟล์ | หน้าที่ | เหตุผลที่แยก |
| --- | --- | --- |
| `World.cs` | `MapLayout` (ขนาด โซน จุดสำคัญ `ZoneAt(x,z)`), `StaticBatcher` (รวม mesh ต่อ chunk), `WorldBuilder` (พื้น เมือง ป่า ทุ่ง ทะเลสาบ ภูเขา ถนน ป้าย landmark รั้ว), `Obstacles` (collision grid) | `Game.BuildWorld` ตอนนี้ ~110 บรรทัดและจะโตเป็นหลายร้อยบรรทัด |
| `SwordIcons.cs` | สร้างและเก็บไอคอนดาบ 5 เล่ม (`SwordIcons.Get(int weaponId)` คืน `Texture2D`) | ใช้ครั้งเดียวตอนเริ่มเกม แยกจาก `Hud.cs` เพื่อถอยแผนได้ง่าย |

อาหารไม่สร้างไฟล์ใหม่: `FoodDef` + `Foods` อยู่ใน `Weapon.cs` ข้าง `Weapons`/`Pickup` (แยกเป็น `Food.cs` ได้ถ้าอ้วน)

### 2.2 ไฟล์ที่แก้

| ไฟล์ | แก้อะไร | เหตุผล |
| --- | --- | --- |
| `Game.cs` | `ArenaHalf` 19 → 190; ค่าคงที่ตำแหน่ง (`MinotaurHome`, `DummyPos`, `KnightSpawn`, `MenuFocus`) อ่านจาก `MapLayout`; `BuildWorld` เรียก `WorldBuilder`; `SpawnWave` → `SpawnPack(Knight anchor)` + director (`SimulateWorld`); `OnEnemyKilled` (Soul); `RollDrop` (อาหาร); `WriteSnapshot`/`ReadSnapshot` (pickup `kind`); `SpawnPickup`/`UpdatePickups`/`Collect`/`CollectLocal`/`OnPickupTaken`; `Update` (I → Tab, Esc ปิด Tab ก่อน); `OnKnightDown` ใหม่ (หน้าจอตาย) + `OnKnightRevive` (รีเซ็ต); `ReviveDelaySingle` 1.4 → 3; `ResetAll`; `PushEnemiesFrom`; `OnPartyWipe`; `SetupCamera` (far clip); `EnemyExpScale` ใหม่ | ศูนย์กลางกติกาโลก |
| `Knight.cs` | `KnightInput` +`run`,`dash`; `ReadInput`; `Simulate` (วิ่ง, dash, collision); เอา stamina ออกจากท่า/สกิล/โล่; `Dashing` ใน `Invulnerable`; popup "DODGE" ใน `TakeDamage`; `souls`; `Heal`; `ResetProgress()`; `ResetAll` (ล้าง Soul); `PlayRemoteAct` รับ kind 20; `reviveAt`/`ReviveIn` (นับถอยหลังให้ HUD) | ข้อมูลผู้เล่น 1 คนอยู่บน object นี้ |
| `Progress.cs` | `MaxLevel` 25, `NeedTable`, `Need(lvl)`, `AttrCap` 10, ค่าต่อแต้ม, คอมเมนต์สูตร Attack Speed | |
| `Weapon.cs` | `Pickup` +`kind` + โมเดลอาหาร + `Lifetime` ต่อชนิด; `Foods` | โครง pickup มี `netId`/`owner`/`age`/กะพริบ/`Find` ครบ |
| `Enemy.cs` | `soulReward`; `BaseTick` เรียก `Obstacles.Push` (layer ศัตรู) | |
| `Slime.cs` | ระยะไล่ 17 → 30 ม. (`ChaseRange`) และ **ไม่ไล่ผู้เล่นที่ยืนในเขตเมือง**; ตาราง Soul; `expReward` สเกล | |
| `Minotaur.cs` | leash เทียบรัง, `soulReward = 50`, `expReward` สเกล, Charge ชน obstacle แล้วหยุด | ไม่มี leash ตอนนี้ |
| `Net.cs` | `Protocol = 2`; `PickupTaken` +kind; `Exp` +souls; คอมเมนต์ `NetW.Pos` ("38 m") | |
| `Hud.cs` | ลบ `StatsPanel`/`statsText`; **แถว Soul แถวที่ 5 ของแผงซ้ายบน**; **ขีดแบ่ง stamina ทุก 25**; หน้าต่าง Tab ใหม่ (ไอคอนดาบ); **overlay YOU DIED**; ข้อความ controls; skill slot ไม่โชว์ "st"; party panel เลื่อนขึ้น; boss bar ตามระยะ + ลูกศรชี้รัง; popup Soul/DODGE/HP | |
| `InputX.cs` | **ไม่ต้องเพิ่มปุ่มใน `Map()`** — `Tab`, `Space`, `LeftShift`, `J`, `Alpha1-5`, `Escape` มีครบแล้ว (เสริม `RightShift` ได้) | |
| `SelfTest.cs`, `MpTest.cs`, `mptest.ps1` | ดูหัวข้อ 10.1-10.2 | |
| `CLAUDE.md` (ของเกม) | ดูหัวข้อ 10.3 | |
| `Args.cs`, `BuildScript.cs` | ไม่แก้ (shadow distance 55, cascade 2, MSAA 4 เหมาะกับแมพใหญ่อยู่แล้ว) | |

---

## 3. คลาส หน้าที่ และความสัมพันธ์

### 3.1 โลก (`World.cs`)

```
enum Zone { Town, Meadow, AutumnForest, DarkPineForest, Farmland, Lake, Mountain, Lair }

static class MapLayout
{
    public const float Half = Game.ArenaHalf;      // 190
    public const float ChunkSize = 38f;            // 10 x 10 chunk
    // พิกัดตาม design.md หัวข้อ 3 (X ตะวันออก, Z เหนือ)
    public static readonly Vector3 TownSpawn   = new Vector3(0f, 0f, -150f);   // จุดเกิด/ฟื้น (เดิม KnightSpawn (0,-5))
    public static readonly Vector3 TownCenter  = new Vector3(0f, 0f, -145f);
    public static readonly Vector3 Gate        = new Vector3(0f, 0f, -100f);   // ประตูเมือง
    public static readonly Vector3 DummyPos    = new Vector3(25f, 0f, -140f);  // ลานซ้อม (เดิม (5,-3.5))
    public static readonly Vector3 Lair        = new Vector3(0f, 0f, 150f);    // MinotaurHome เดิม (0,12.5); รัศมีรัง ~25 ม.
    public static readonly Vector3 MenuFocus   = new Vector3(0f, 0f, -142f);   // กล้องเมนูมองกลางเมือง (เดิม (0,0,3) = spawn + 8)
    public const float TownMinX = -60f, TownMaxX = 60f, TownMinZ = -190f, TownMaxZ = -100f;
    public static bool InTown(float x, float z);                 // สี่เหลี่ยมของเมือง (ไม่ใช่วงกลม)
    public static Zone ZoneAt(float x, float z);                 // data-driven: รายการสี่เหลี่ยม/วงกลม + ลำดับความสำคัญ
    public static float SpawnTier(Zone z);                       // Meadow/Farmland 1.0, AutumnForest 1.5, DarkPineForest 2.0, Lair 2.0; Town/Lake/Mountain 0 = ไม่เกิด
    public static bool Walkable(float x, float z);               // false ถ้า Lake / Mountain (ใช้ตอนสุ่มจุดเกิดและตอนวาง pickup)
}

class StaticBatcher                                // รวม mesh ต่อ (chunk x, chunk z, material, castShadow)
{
    public void Add(Mesh m, Matrix4x4 xf, Material mat, bool shadows = true);
    public void Build(Transform parent);           // Mesh.CombineMeshes(list, true, true), indexFormat UInt32, GameObject "Chunk x_z"
}

static class WorldBuilder
{
    public static void Build(Transform parent);    // เรียกจาก Game.BuildWorld; System.Random(11) เหมือนเดิม
    // helper: GroundTile, House, Wall/Gate, Tree(round/pine), Rock, Mountain, Lake, Road, Sign, Tower, Windmill, Fence, Banner, Column
}

static class Obstacles                             // collision แบบ circle / AABB บน grid ช่อง 8 ม.
{
    public static void AddCircle(Vector2 c, float r, int blocks);                  // blocks: bit0 = ผู้เล่น, bit1 = ศัตรู
    public static void AddBox(Vector2 c, Vector2 half, float yaw, int blocks);     // กล่องใหญ่ลงทะเบียนในทุกช่องที่ทับ
    public static Vector3 Push(Vector3 p, float radius, int layer);                // คืนตำแหน่งที่ถูกดันออก (วนเฉพาะช่องข้างเคียง)
}
```

ความสัมพันธ์: `Game.Awake` → `BuildWorld()` → `WorldBuilder.Build` (เติม `StaticBatcher` + `Obstacles`). `Knight.Simulate`/`Enemy.BaseTick` เรียก `Obstacles.Push`. director ใน `Game` ใช้ `MapLayout.ZoneAt/InTown/Walkable/SpawnTier`. **ฉากต้อง deterministic** (`System.Random(11)` ไม่ใช้ `UnityEngine.Random`) เพราะ host/client สร้าง collision เองฝั่งตัวเอง

### 3.2 ผู้เล่น (`Knight.cs`)

```
const float RunMult = 1.5f;                        // 6.2 -> 9.3 ม./วิ
public const float DashCost = 25f, DashCooldown = 0.8f, DashDistance = 6f, DashMoveTime = 0.20f, DashInvuln = 0.5f;

public struct KnightInput { ...; public bool run, dash; }

public int souls;                                  // ไม่อยู่ใน Progress เพราะ Progress ถูกรีเซ็ตตอนตาย
public bool Dashing { get { return Time.time < dashInvulnUntil; } }
public bool Invulnerable { get { return invuln || Glowing || Dashing; } }
public float ReviveIn { get; }                     // วินาทีที่เหลือก่อนฟื้น (ให้หน้าจอตายนับถอยหลัง)

void TryDash(Vector2 move);
void CancelAction();                               // StopAllCoroutines + เคลียร์ธง (IsAttacking, lockFacing, whirl, shieldPush, invuln, rigSpin, queued)
public void Heal(float amount);
public void ResetProgress();                       // ตาย: progress = new Progress() (เก็บ kills), ดาบ/Soul ไม่แตะ
public void ResetAll();                            // F5: เดิม + souls = 0
```

### 3.3 เลเวล (`Progress.cs`)
instance ต่อ `Knight` เหมือนเดิม; `Need(lvl)` อ่านจาก `static readonly int[] NeedTable` (24 ช่อง)

### 3.4 อาหาร (`Weapon.cs`)
```
public enum PickupKind : byte { Sword = 0, Food = 1 }
public class FoodDef { public int id; public string name; public float healFrac; public Color color; }
public static class Foods { public static readonly FoodDef[] All; public static readonly float[] AppleChance, MeatChance; }
// Pickup.Spawn(PickupKind kind, int item, Vector3 pos, Transform parent, int owner, int netId); weaponId = "item id" ของทั้งสองชนิด
```

### 3.5 ไอคอนดาบ (`SwordIcons.cs`)
```
static class SwordIcons { public static void Build(); public static Texture2D Get(int weaponId); }   // 128x128
```

### 3.6 Flow สำคัญ
```
Enemy.TakeHit -> Game.OnEnemyKilled(e, by)
   -> ทุกผู้เล่นที่ยังไม่ตาย: GrantExp + souls += e.soulReward   (remote: Net.SendExp(pid, exp, at, credit, souls))
   -> RollDrop(e): ดาบ (เดิม) + อาหาร (ใหม่)
Game.UpdatePickups (authority) -> Collect -> local: CollectLocal / remote: Net.BroadcastPickupTaken(id,pid,kind,item,at)
Knight.DownRoutine -> Game.OnKnightDown(k) (ปิด panel + hud.ShowDeath(ค่าก่อนรีเซ็ต)) ... รอ ReviveDelay ...
                   -> Game.OnKnightRevive(k) (hud.HideDeath, k.ResetProgress(), ฟื้นที่ TownSpawn)
```

---

## 4. ข้อมูลที่ต้องเก็บ และผลต่อระบบ save

**ยืนยัน: เกมนี้ไม่มีระบบ save ความคืบหน้าเลย** (`PlayerPrefs` ใช้แค่ชื่อ/IP/port ในเมนู) ข้อมูลใหม่อยู่ในหน่วยความจำของรอบเล่นเท่านั้น ไม่มี schema ต้อง migrate

| ข้อมูล | เก็บที่ไหน | ล้างเมื่อ | เซฟข้ามเซสชัน? | ส่งผ่านเน็ต |
| --- | --- | --- | --- | --- |
| `souls` (int) | `Knight.souls` (ต่อผู้เล่น อยู่บนเครื่องคนนั้น เหมือน inventory) | **F5** (`ResetAll`) หรือออกจากเกม; **ตายไม่ล้าง** | **ไม่เซฟ** (ไม่มีที่ใช้รอบนี้ และไม่มีระบบ save) | host ส่งจำนวนที่ได้ใน `Msg.Exp` (client บวกเอง); ไม่อยู่ใน `State`/`Snapshot` |
| เลเวล/EXP/การ์ด/แต้ม status | `Knight.progress` | **ตาย** (`ResetProgress`), F5 | ไม่เซฟ | `level` ใน `State`/`Snapshot` เดิม (U8 พอถึง 25) |
| ดาบ (`Inventory`) | `Knight.inventory` | F5 เท่านั้น | ไม่เซฟ | เดิม |
| Dash cooldown / `dashInvulnUntil` / `reviveAt` | field ใน `Knight` | ตาย/F5 | ไม่ | ไม่ส่ง |
| อาหารบนพื้น | `Pickup` (kind = Food) | หมดอายุ 60 วิ / เก็บ / F5 | ไม่ | `Snapshot` pickup list |
| แมพ | สร้างใหม่จากโค้ด + seed 11 | – | ไม่ต้อง | ไม่ส่ง |

---

## 5. Net protocol

### 5.1 ตำแหน่งพอไหม
- `NetW.Pos(v)`: `Mathf.RoundToInt(v * 100f)` clamp ±32767 → ±327.67 ม. อ่านกลับเป็น `(short)` / 100
- แมพ ±190 ม. (+ รั้ว 0.7 ม.; ผู้เล่นยืนได้ไม่เกิน `ArenaHalf - 0.4`) → **พอแน่** ความละเอียด 1 ซม. ไม่ต้องเปลี่ยน encoding
- จุดที่ใช้ `Pos`/`Vec` (State/Snapshot knight+enemy+pickup, Minotaur `smashPoint`, `Act.target`, `Hit.origin`, `KnightHit.from`, `Exp.at`, `PickupTaken.at`) เป็นพิกัดโลกที่ clamp ด้วย `ArenaHalf` อยู่แล้ว
- `Net.ReadKnight` clamp ที่ `Game.ArenaHalf + 1f` → ขยายอัตโนมัติ
- `Game.ClientHit` ระยะ `a.magnitude > 14f` — วิ่ง 9.3 ม./วิ + State 20 Hz + latency ~150 ms ห่างราว 1.5 ม.; dash 6 ม. ยังในเกณฑ์ ไม่ต้องแก้ แต่ SelfTest/MpTest ต้องเพิ่ม "dash แล้วฟันทันที"

### 5.2 `Net.Protocol` -> 2
Payload เปลี่ยน (client เก่าอ่านผิด): ตั้ง `Protocol = 2` ครั้งเดียวที่ commit แรกที่แตะ payload (commit B) ไม่ต้องเพิ่มอีกใน commit ถัดไปของฟีเจอร์นี้

### 5.3 ข้อความ
ไม่เพิ่มสมาชิก `enum Msg` (และ **ไม่ต้องเพิ่ม `Net.SendBanner`** เพราะเลิกประกาศแพ็ค):

| เรื่อง | วิธี | รายละเอียด |
| --- | --- | --- |
| **Dash** | `Msg.Act` kind ใหม่ **20** | `Net.SendAct(20, 1f, endPoint)` ตอนเริ่ม; ฝั่งรับ `PlayRemoteAct` เล่นเอฟเฟกต์ dash (Fx เดิม: streak/ฝุ่น/กะพริบฟ้า) และเรียก `StopAct()` ก่อนเสมอ. i-frame **ไม่ส่ง** — client ตัดสินเอง |
| **วิ่ง** | ไม่ส่ง | puppet คำนวณ `remoteSpeed` อยู่แล้ว; อยากให้ขาก้าวเร็วใช้ `remoteSpeed > 7.5` |
| **Soul** | `Msg.Exp` +`U16 souls` ท้าย payload | `SendExp(pid, amount, at, killCredit, souls)`; ผู้เล่นที่ล้มไม่ได้ (เหมือน EXP) |
| **อาหารดรอป** | `Snapshot` pickup: `U16 id, U8 kind, U8 item, U8 owner, Pos x, Pos z` (+1 ไบต์ต่อ pickup) | |
| **อาหารเก็บ** | `Msg.PickupTaken`: `U16 id, U8 pid, U8 kind, U8 item, Vec at` | client ที่ถูกเลือก `Heal` HP ตัวเอง (trust model) |
| **ตายรีเซ็ตเลเวล** | ไม่ส่ง | client รีเซ็ตเอง; `level` ใน `State` ทำให้เพื่อนเห็น Lv 1 เอง; `PartyLevel` คำนวณจาก `progress.level` ที่ sync อยู่ |
| F5 | `Msg.Reset` เดิม | `ResetAll` ล้าง Soul เพิ่ม |

### 5.4 ขนาด snapshot

| รายการ | ไบต์ |
| --- | --- |
| header (F32 + U16 + U8 + F32) | 11 |
| knight 1 คน (`pid` 1 + `WriteKnight` 28) | 29 |
| สไลม์ (id 2 + type 1 + x,z 4 + yaw 2 + hp/maxHp/armor/maxArmor 16 + flags 1 + state 1) | 27 |
| Minotaur | ~35 |
| pickup (ใหม่) | 9 |

เลวร้ายที่สุด: 4 ผู้เล่น + สไลม์ **48** (`MaxSlimes`) + Minotaur + dummy + pickup **60** → 11+116+1,296+35+27+540 ≈ **2.0 KB/snapshot** x 15 Hz ≈ **30 KB/s ต่อ client** (host ส่งรวม 3 client ~90 KB/s ≈ 0.7 Mbit/s) เดิม (16 ตัว) ≈ 0.7 KB. ขีดโปรโตคอล: ความยาวเฟรม u16, count U8 (255) ยังห่างไกล

**คำแนะนำ**: ไม่ทำ interest management ตอนนี้ — cap สไลม์ 48 และอาหารบนพื้น 40 ชิ้น (เกินลบชิ้นเก่าสุด)

---

## 6. Input, dash, วิ่ง, stamina

### 6.1 จุดที่อ่านปุ่ม (ค้นทั้งโปรเจกต์แล้ว)

| ที่ | ตอนนี้ | แก้เป็น |
| --- | --- | --- |
| `Knight.ReadInput` บรรทัด 365 | `block = Held(LeftShift) \|\| MouseHeld(1)` | `block = MouseHeld(1)`; เพิ่ม `run = Held(LeftShift)` |
| `Knight.ReadInput` บรรทัด 370 | `MouseDown(0) \|\| Down(J) \|\| Down(Space)` | เอา `Space` ออก; เพิ่ม `dash = Down(Space)` (dash ไม่ผ่านระบบ `request`/queue เพราะต้องแทรกได้ทันที) |
| `Game.Update` บรรทัด 1111 | `Down(KeyCode.I)` | `Down(KeyCode.Tab)` toggle |
| **`Game.Update` บรรทัด 1077-1081 (Esc)** | `Down(Escape)` → `LeaveKey()` เสมอ | **ถ้า `InventoryOpen` อยู่: `SetInventory(false)` แล้วข้ามการเรียก `LeaveKey()` ในเฟรมนั้น** (Esc ปิดหน้าต่างก่อน ไม่นับเป็น Esc ออกเมนู ไม่เริ่มนับ 3 วิของ `leaveAskedAt`); ทำตรวจ Esc **ก่อน** บล็อก `LevelStep`/`InventoryOpen` ที่ `return` ในโหมด single (ตอนนี้ Esc อยู่ต้น `Update` ก่อนอยู่แล้ว จึงแค่แทรกเงื่อนไข). หน้าต่างเลเวลอัปยังไม่ปิดด้วย Esc (ต้องเลือกการ์ด) |
| `Hud.InventoryInput` | `Alpha1..5` | คงเดิม + (ทางเลือกของ design) ลูกศร/A D + Enter เลื่อนเลือกช่อง |
| `Hud` controls / footer / `Game.CollectLocal` ("press I to equip") | "I" | "Tab"; controls เพิ่ม "Space Dash", "Shift Run" |
| `InputX.Map()` | มี `Tab`, `Space`, `LeftShift`, `J`, `Escape` | ไม่ต้องเพิ่ม |
| `SelfTest.cs` 339, 818 / `MpTest.cs` 381 | LeftShift = ยกโล่ | เมาส์ขวาค้าง (ขยาย `SetMouse` รองรับปุ่มขวา) |
| `SelfTest.cs` 440/458/542/545, `MpTest.cs` 1203/1206 | `Tap(Key.I)` | `Tap(Key.Tab)` (MpTest 1138 เป็น Tab ของเมนู ไม่เกี่ยว) |

### 6.2 วิ่ง
ใน `Knight.Simulate`: `speedMul` เดิมคงไว้ แล้วคูณ `RunMult` (1.5) เมื่อ `inp.run && moving && canInput && !IsAttacking && !IsBlocking && !Airborne && !Dead` (ยกโล่ตัดสถานะวิ่งตาม design). ไม่กิน stamina. ข้ามแมพ 380 ม.: เดิน 6.2 ม./วิ ≈ 61 วิ, วิ่ง 9.3 ม./วิ ≈ 41 วิ. **จุดเกิดถึงรัง 300 ม.**: เดิน ≈ 48 วิ, วิ่ง ≈ **32 วิ**

### 6.3 Dash (Space)

| รายการ | ค่า | เหตุผล |
| --- | --- | --- |
| stamina | **25** | ฐาน 100 = dash 4 ครั้งติด; 1 ขีดบนแถบ = 1 dash |
| cooldown | **0.8 วิ** | สั้นกว่า 0.5 จะต่อ i-frame ไม่ขาดสาย |
| ระยะ | **6 ม.** ใน **0.20 วิ** | เท่า Dash Thrust (E) `DashLength = 6f`; design ขอ 5-6 ม. |
| i-frame | **0.5 วิ** นับจากกด | ตาม requirement |
| ทิศ | ทิศ WASD; ไม่กด = ถอยหลัง (ตรงข้ามทิศที่หัน) | ตรงกับ design |
| ฟื้น stamina | หน่วง 0.6 วิ (`lastSpendTime`) ฟื้น 26/วิ x `StaminaRegenMult` | |
| ศัตรูตันทาง | ไม่ทะลุศัตรู `solid` | กันบั๊ก |
| ชนสิ่งกีดขวาง | dash ผ่าน `Obstacles.Push` ทุกเฟรม (หยุดที่กำแพง/บ้าน/ทะเลสาบ/ภูเขา) | |

หมายเหตุเทียบ design: design เสนอห่างขั้นต่ำ ~0.25 วิหลังพุ่งจบ; tech ใช้ cooldown 0.8 วิ (ตัวตั้ง) ซึ่งครอบคลุมแล้ว

| state ตอนกด Space | ผล |
| --- | --- |
| ปกติ/เดิน/วิ่ง | dash ได้ถ้า stamina >= 25 และ cooldown หมด; ไม่พอ → `Deny("Dash")` (แถบ stamina แดงกะพริบเดิม `denied` + ขีดที่ขาดสั่น + `armor_impact` เบาๆ) |
| กำลังฟันคอมโบ / สกิล Q E R | **ยกเลิกท่าแล้ว dash** (`CancelAction()`); cooldown สกิลไม่คืน; ล้าง `queued` |
| Ground Slam กลางอากาศ (`Airborne`) | ห้าม dash |
| ยกโล่ค้าง | dash ได้ (ตัดการยกโล่ระหว่าง dash; ถ้ายังกดคลิกขวาอยู่ก็ยกต่อหลังจบ — ตรงกับ design Q10 "ตัด") |
| ถูกกระเด็น | dash ตัด `knockVel = 0` |
| ตาย / เลเวลอัป / Tab เปิด | ไม่รับ (`canInput` false) |
| สตัน | ผู้เล่นยังไม่มี state สตัน; ถ้าเพิ่มภายหลังให้ห้าม dash ตอนสตัน |

`CancelAction()` รวมสิ่งที่ `ResetAll`/`DownRoutine` เคลียร์ซ้ำ (บรรทัด 304-308, 1108-1110) เป็นเมธอดเดียวเรียกจากสามที่ — กันธงค้าง (เช่น `invuln` ของ Dash Thrust)

### 6.4 i-frame ตรวจที่ไหน และ popup "DODGE"
ตรวจที่เดียว: `Knight.TakeDamage` บรรทัดแรก. `Invulnerable` เพิ่ม `|| Dashing`. ไม่แตะ host (trust model เดิม "ดาเมจที่อัศวินรับตัดสินบนเครื่องเจ้าของ"):
- Single / host-local: `Game.DamageKnight` → `k.TakeDamage`
- Client: host ส่ง `Msg.KnightHit` เสมอ → client เรียก `TakeDamage` → i-frame เช็คบนเครื่อง client (เลื่อนตาม latency ครึ่ง RTT — ยอมรับได้)
- ปุ่ม `H` ผ่าน `TakeDamage` → ใช้ทดสอบ i-frame ใน SelfTest

**"DODGE"**: ทำได้โดยไม่กระทบ trust model เพราะอยู่ฝั่งเดียวกับการตัดสิน i-frame: ใน `TakeDamage` แทรกก่อน `if (... Invulnerable) return;`
```
if (remote || Dead) return;
if (Dashing) { if (Time.time - lastDodgeFx > 0.3f) { lastDodgeFx = Time.time; Popup("DODGE", #CFE9FF); Fx.I.Burst(...); Sfx.I.Play(Snd.Swing, 0.35f); } return; }
if (Invulnerable) return;
```
(จำกัดไม่ให้ขึ้นถี่เกิน 0.3 วิ เพราะสไลม์/Minotaur อาจตีหลายครั้ง). เอฟเฟกต์ใช้ `Fx` เดิม (streak/ฝุ่น/burst) + `Tint` สีฟ้าขาวตอนอมตะ **ไม่มี afterimage** ในขอบเขตหลัก (design ขอเป็นข้อเสนอเพิ่ม ทำทีหลังได้ด้วยการก๊อปสำเนา mesh ของ rig ~3 ชุดโปร่งใส — งานเล็ก-กลาง)

### 6.5 เอา stamina ออกจากท่าดาบ / สกิล / โล่

| ที่ | แก้ |
| --- | --- |
| `AttackDef.stamina` (5 ท่า), `SkillDef.stamina` (4 สกิล) | ลบฟิลด์ (compiler พาไปแก้ `Hud` 674/677, SelfTest) |
| `Knight.SwingCost`, `TryStartAttack` | ลบการหัก/เช็ค stamina |
| `Knight.TrySkill` | ลบ |
| `Knight.BlockCost` + `TakeDamage` ("GUARD BROKEN") | ลบ: บล็อกสำเร็จเสมอเมื่อหันถูกทิศ (< 75 องศา) |
| `Knight.Update` regen | เอา `!IsBlocking` ออก |
| `Hud` skill slot | ลบ `" st"` และสีแดง stamina ไม่พอ แสดงแค่คูลดาวน์ |

**ผลต่อสูตร Attack Speed**: ใหม่ Attack Speed มีผลแค่ความเร็วแอนิเมชันฟัน (`SwingSpeed` clamp 0.4-4) และหารคูลดาวน์สกิล; แก้คอมเมนต์ `Progress.cs` บรรทัด 20-24.

**ผลต่อแต้ม STAMINA**: ใช้ได้กับ dash อย่างเดียว จึงปรับ `StaminaPerPoint` 12 → **15**, `RegenPerPoint` 0.04 → **0.05** (แต้มเต็ม 10: stamina 250 = dash 10 ครั้ง, ฟื้น +50%)

**แถบ stamina มีขีดแบ่งทุก 25 หน่วย (1 ขีด = 1 dash)** — เพิ่มใน `Hud`:
- ตอน `Awake` สร้างขีดแนวตั้งบาง (Image กว้าง 2 px สีดำจาง) เป็นลูกของ `staminaBar.frame` จำนวน 9 อัน (สูงสุด stamina 250 ÷ 25 = 10 ช่อง = 9 ขีดภายใน; ซ่อนอันที่เกินช่อง)
- `LateUpdate` จัดตำแหน่ง: ขีดที่ i อยู่ที่ `anchorX = (i * Knight.DashCost) / k.MaxStamina` (ค่าคงที่ `DashCost` ใช้ร่วมกับโค้ด dash จึงไม่คลาดเคลื่อน); ซ่อนขีดที่ `i * DashCost >= MaxStamina`
- ขีดช่องที่เพิ่งฟื้นพอ dash ได้อีกครั้งวาบเหลืองขาว 0.15 วิ (เทียบ `stamina` ก่อน/หลัง ว่าข้ามผลคูณของ 25 ขึ้นไป); ตอน `Deny` ให้ขีดช่องถัดไปสั่น (offset x สั้นๆ 0.35 วิ ตาม `StaminaDeniedTime` ที่ `UpdateBar` ใช้ `denied` อยู่แล้ว)
- ตัวเลข `x / y` ตรงกลางแถบคงเดิม

**โล่ฟรีทำให้ BLOCK แรงเกินไหม**: ดูความเสี่ยง R5

---

## 7. เลเวล 25, สเตตัส, ตาย/รีเซ็ต, Soul, อาหาร

### 7.1 เส้น EXP ใหม่ (`Progress.NeedTable`, จากเลเวล L ไป L+1)

หลักคิด: สูตรเดิม 30x1.5^(L-1) ที่เลเวล 25 ต้อง ~340,000 EXP เป็นไปไม่ได้ จึงใช้ตารางที่ช่วงต้นช้ากว่าเดิมเล็กน้อย ช่วงกลาง-ปลายเพิ่มแบบเกือบเส้นตรง และ **EXP ต่อมอนสเตอร์สเกลตามเลเวลศัตรู**

| L → L+1 | EXP | สะสม | | L → L+1 | EXP | สะสม |
| --- | --- | --- | --- | --- | --- | --- |
| 1 → 2 | 30 | 30 | | 13 → 14 | 540 | 2,990 |
| 2 → 3 | 45 | 75 | | 14 → 15 | 620 | 3,610 |
| 3 → 4 | 65 | 140 | | 15 → 16 | 710 | 4,320 |
| 4 → 5 | 90 | 230 | | 16 → 17 | 810 | 5,130 |
| 5 → 6 | 120 | 350 | | 17 → 18 | 920 | 6,050 |
| 6 → 7 | 155 | 505 | | 18 → 19 | 1,040 | 7,090 |
| 7 → 8 | 195 | 700 | | 19 → 20 | 1,170 | 8,260 |
| 8 → 9 | 240 | 940 | | 20 → 21 | 1,310 | 9,570 |
| 9 → 10 | 290 | 1,230 | | 21 → 22 | 1,460 | 11,030 |
| 10 → 11 | 345 | 1,575 | | 22 → 23 | 1,620 | 12,650 |
| 11 → 12 | 405 | 1,980 | | 23 → 24 | 1,790 | 14,440 |
| 12 → 13 | 470 | 2,450 | | 24 → 25 | 1,970 | **16,410** |

**EXP สเกลตามเลเวลศัตรู**: `Game.EnemyExpScale = 1 + 0.15 * (PartyLevel - 1)` (คิดตอนศัตรูเกิด เหมือน HP/ดาเมจ) → `Slime.Setup`: `expReward = Exp[k] * Game.I.EnemyExpScale`; `Minotaur.Awake` เช่นกัน. ประมาณการ (ไม่ใช่ค่าวัด; สมมติ EXP ฐานเฉลี่ย ~12 ตอนต้น → ~22 ตอนปลาย):

| เลเวล | ตัวคูณ | EXP/ตัวเฉลี่ย | ต้องฆ่า/เลเวล |
| --- | --- | --- | --- |
| 2 | 1.15 | ~14 | ~3 |
| 5 | 1.6 | ~21 | ~6 |
| 10 | 2.35 | ~40 | ~9 |
| 15 | 3.1 | ~62 | ~11 |
| 20 | 3.85 | ~85 | ~15 |
| 24 | 4.45 | ~98 | ~20 |

รวม ~230 ตัวถึงเลเวล 25 ราว 45-60 นาทีที่ ~5 ตัว/นาที (Minotaur ที่เลเวล 10 ≈ 590 EXP ≈ 1.7 เลเวล). ปรับได้จุดเดียวที่ `NeedTable` และ `0.15`. SelfTest บรรทัด 246 (`Need(1)==30`, `Need(2)==45`) ยังจริง; เช็ค "Minotaur ให้ 250 EXP" (848) เปลี่ยนเป็น `250 * scale ณ ตอนเกิด`

### 7.2 เพดานอัปเกรด
คงเพดานเดิม (Damage x10, Attack Speed x4 / ฟัน x3, Crit damage x10, crit chance 75%). 24 การ์ด x ค่าเฉลี่ย 0.75: Damage เต็มที่ ~12 การ์ด, Speed ~4, Crit chance ~17 → รวมสามอย่างเกิน 24 ผู้เล่นต้องเลือกสายเฉพาะทาง = มีความหมายถึงเลเวล 25 โดยไม่ต้องเพิ่มเพดาน; `MakeOffers` รองรับตัวที่เต็มแล้ว

### 7.3 แต้ม status
- เลเวล 25 = **24 แต้ม**; `AttrCap` 6 รวม 18 → 6 แต้มเสียเปล่า. แนะนำ **`AttrCap = 10`** (30 > 24 ยังมีทางเลือก)

| สเตตัส | ต่อแต้ม (ใหม่) | เต็ม 10 | เดิมเต็ม 6 |
| --- | --- | --- | --- |
| HP | +20 max HP | +200 | +120 |
| BLOCK | Armor +10, กัน **+1.2%** (`BlockPerPoint` 0.02 → 0.012) | Armor +100, กัน 92% | กัน 92% |
| STAMINA | +15 stamina, ฟื้น +5% | 250, +50% | 172, +24% |

- HP ต่อเลเวล +10 (`Knight.HpPerLevel`): ที่เลเวล 25 = 100 + 240 + (HP แต้ม x 20) ตัวอย่างสาย HP 10 แต้ม = **540**; เลเวลอัปยังเติมเลือดเต็ม
- UI ที่ดึงจากค่าคงที่อยู่แล้ว: `Hud.ShowStatChoice`, การ์ด "/6" ใช้ `Progress.AttrCap`

### 7.4 สเกลศัตรู และเลเวล 25 เล่นได้ไหม
สูตรเดิม `EnemyHpScale = (1 + 0.45*(PartyLevel-1)) * (1 + 0.6*(players-1))`, `EnemyDamageScale = 1 + 0.08*(PartyLevel-1)`. เลเวล 25 เดี่ยว: HP x**11.8**, ดาเมจ x**2.92**. ประเมินมือ (ไม่ได้รันเกม): สไลม์เขียว 30 → 354 HP; แดง ~1,534 HP + เกราะ 354; ผู้เล่นสายดาเมจ ~100-300 ต่อฮิต → เขียว ~3-4 ฮิต แดง ~8-12 ฮิต (สายกระจายการ์ดช้าลง ~2 เท่า); Minotaur ~7,500 รวม ที่ ~400 DPS ≈ 19 วิ; ดาเมจรับ: แดง ~55, Minotaur Smash ~111 ต่อ HP+เกราะ ~540+120 → ตึงแต่ยุติธรรมเพราะมี dash/โล่ฟรี

**คำแนะนำ: คงสูตรเดิม** ใส่ log ใน SelfTest ตอนไต่ถึง 25 แล้วปรับ 0.45 / 0.08 / 0.15 หลังเล่นจริง (ถ้าหนักเกิน ลด 0.08 → 0.06 ก่อน)

### 7.5 ตายแล้วรีเซ็ต + หน้าจอตาย

**ลำดับเหตุการณ์** (ทุกโหมด): `Knight.TakeDamage` hp ≤ 0 → `DownRoutine` → **`Game.OnKnightDown(k)`** (ใหม่) → รอ `ReviveDelay` → **`Game.OnKnightRevive(k)`** (แก้) → ฟื้นที่ `spawnPoint` (ลานเมือง (0,-150) ± ออฟเซ็ต pid)

**`Game.OnKnightDown(k)`** (เฉพาะเครื่องเจ้าของ `!k.remote`):
1. ปิดหน้าต่างเลเวลอัป/Tab: `LevelStep = 0`, `offers = null`, `hud.HideLevelUp()`, `SetInventory(false)`, `progress.pendingChoices = 0` (ออนไลน์ overlay ไม่หยุดเกม ผู้เล่นตายขณะเปิดได้)
2. เก็บค่าก่อนรีเซ็ต: `level = progress.level`, `swords = inventory.OwnedCount`, `souls`
3. `hud.ShowDeath(level, swords, Weapons.All.Length, souls, k)`

**หน้าจอตาย (`Hud`)** — overlay เต็มจอ (ไม่มี raycast) ตาม design หัวข้อ 9:
- `BuildDeathPanel()` สร้างในลำดับหลัง panel อื่น (เหนือ HUD): dim เต็มจอ (single: ดำแดงเข้มทึบ ~0.75 ค่อยๆ เข้าใน 0.6 วิ; ออนไลน์: ~0.35), ข้อความ "YOU DIED" `#C42B2B` 60 px ตัวหนา, บรรทัด "You lose:  Lv N  >  Lv 1 / all upgrade cards and stat points" (เทาอ่อน), "You keep:  Swords (a / 5)   Souls N" (ทอง; Soul `#9FE8FF`), บรรทัดนับถอยหลัง "Returning to town in  N ..." (ออนไลน์: "Returning in N ...") อ่านจาก `k.ReviveIn` ทุกเฟรม (`Mathf.CeilToInt`)
- `HideDeath()` เรียกจาก `Game.OnKnightRevive`, `ResetAll` (F5 ระหว่างตาย — `StopAllCoroutines` ฆ่า `DownRoutine` อยู่แล้ว จึงต้องซ่อนใน `ResetAll` ด้วย), `EndSession`
- เล่นคนเดียว: การ์ด/คีย์อื่นใช้ไม่ได้อยู่แล้วเพราะ `canInput = !Dead` (เหลือ Esc/F5 ตาม design); เอา banner เดิม "Knight down! Reviving..." / "You are down! Back in N seconds..." (Knight.cs บรรทัด 1112) ออก
- ต้องแสดงค่า **ก่อน** รีเซ็ตเสมอ เพราะรีเซ็ตทำใน `OnKnightRevive` (หลังรอ) — ตรงกับ design ("ตัวเลขที่แสดงคือค่าก่อนตาย")

**เวลาฟื้น**: `Game.ReviveDelaySingle` **1.4 → 3.0** (ค่าคงที่เดียว ใน `Game.cs` บรรทัด 16 ใช้ใน `Knight.DownRoutine`); ออนไลน์ `ReviveDelayCoop = 5` เดิม. **ผลกับ SelfTest**: ส่วนที่ฆ่าอัศวินแล้วรอฟื้น (~บรรทัด 1030-1041) ต้องเพิ่ม timeout เป็น ≥ 4 วิ และ assertion "level คงเดิม" เปลี่ยนเป็น "level = 1"; ตรวจ `ReviveIn` นับลง, overlay เปิด/ปิด, ข้อความมีเลเวลก่อนตาย; MpTest คำสั่ง `die` (503) ที่รอฟื้นใน co-op (5 วิ) ไม่ได้รับผลโดยตรง แต่ single-player self-test ยาวขึ้นราว 1.6 วิต่อครั้งที่ตาย

**`Game.OnKnightRevive(Knight k)`**: เดิม `progress.exp = 0f` → ใหม่ `hud.HideDeath(); k.ResetProgress()` (ก่อน `hp = MaxHp` ใน `DownRoutine` จึงฟื้นเต็มตามค่าเลเวล 1). banner "Revived - back to Level 1" (สีฟ้าอ่อน) + `knight_ready` เดิม; snap กล้อง `camFocus = k.transform.position` (ไม่ lerp ข้าม 150+ ม.); ฟื้นมีโล่ 2.5 วิเดิม (`invulnUntil`); เคลียร์สไลม์ที่ยังมีชีวิตในรัศมี 60 ม. รอบ `spawnPoint` แบบเงียบ (เมืองเป็นเขตปลอดภัย)

`Knight.ResetProgress()`: เก็บ `kills` → `progress = new Progress()`; ไม่แตะ `inventory`, `souls`; `hp/armor/stamina` เต็ม; ล้างคิว/coroutine ที่ค้าง

**ต่างจาก F5:**

| | ตาย (`ResetProgress`) | F5 (`Game.ResetAll` + `Knight.ResetAll`) |
| --- | --- | --- |
| เลเวล/EXP/การ์ด/แต้ม status | ล้าง | ล้าง |
| ดาบ | **อยู่** | ล้าง เหลือ Iron Sword |
| Soul | **อยู่** | ล้าง |
| โลก (ศัตรู, pickup, Minotaur) | ไม่แตะ (ยกเว้นเคลียร์สไลม์รอบเมือง) | `ClearWorld` + `SpawnActors` ใหม่ |
| ใครกด | อัตโนมัติเฉพาะคนที่ตาย | single / host เท่านั้น (ออนไลน์รีเซ็ตทุกคน `Msg.Reset`) |

**ผลข้างเคียง/ co-op**:
1. ศัตรูที่เกิดตอนเลเวลสูงยังแข็งอยู่ → ลบสไลม์รัศมี 60 ม. รอบเมืองตอนฟื้น + กฎลบเมื่อไกล > 70 ม.
2. `PartyLevel` เป็นค่าเฉลี่ย `progress.level` — คนตายรีเซ็ตทำให้เฉลี่ยลด ศัตรูที่เกิดใหม่อ่อนลง (คิดตอนเกิด ไม่ย้อนหลัง) ตั้งใจ ไม่ต้องแก้
3. `OnPartyWipe`: ทุกคนล้มพร้อมกัน → ทุกคนรีเซ็ตเมื่อฟื้น (แต่ละเครื่องเรียก `OnKnightRevive` เอง); `OnPartyWipe` ลบสไลม์ รีเซ็ต pack timer และพา Minotaur กลับรัง (`ResetToLair`)
4. remote บน host: `ApplyNet` รับ `level = 1` จาก `State`; เล่น `LevelUpFx` เฉพาะตอนเพิ่ม

### 7.6 Soul

| หัวข้อ | การตัดสินใจ |
| --- | --- |
| เก็บที่ไหน | `Knight.souls` (int, ต่อผู้เล่น, เครื่องของคนนั้น) ไม่อยู่ใน `Progress` |
| ได้เท่าไหร่ | ใหม่ `Enemy.soulReward`: เขียว **1** / น้ำเงิน **3** / แดง **6**, Minotaur **50**, หุ่นฟาง 0 (ค่าคงที่ ไม่สเกล) |
| co-op | กฎเดียวกับ EXP: ผู้เล่นที่ยังไม่ตายทุกคนได้เต็มจำนวนต่อการฆ่า 1 ครั้ง (`Game.OnEnemyKilled`) |
| ตายหาย? | **ไม่หาย** |
| F5 ล้าง? | **ล้าง** (`ResetAll`: `souls = 0`) |
| เซฟข้ามเซสชัน? | **ไม่เซฟ** (ไม่มีที่ใช้ และไม่มีระบบ save) |
| แสดงผล | **จอหลัก: แถวที่ 5 ของแผงซ้ายบน** ตำแหน่ง (116,-156) ตัวเลข 20 px ตัวหนา `#9FE8FF` + ไอคอนหยดวิญญาณ (Image วงรีเล็ก/Panel สี `#9FE8FF`) ไม่มีแถบ; แผง `BarsPanel` สูง 146 → ~180 (`Panel("BarsPanel", ..., new Vector2(400, 146))` → 400 x 180). ตัวเลขเด้งขยาย 1.2 เท่า 0.3 วิเมื่อเพิ่ม (เทียบค่าเดิม/ใหม่ใน `LateUpdate`). **popup "+N Soul" สี `#9FE8FF`** (`Hud.Popup`, ขนาด 18 เล็กกว่าเลข dmg) ที่จุดตายของศัตรู (host/single: ตอน `OnEnemyKilled`; client: ตอนรับ `Msg.Exp` ที่มี `at`). อนุภาคฟ้าขาวพุ่งเข้าหาตัวอัศวิน (ทางเลือก: `Fx.Burst` สั้นๆ ที่จุดตาย). หน้าต่าง Tab: แสดงด้วย |
| เน็ต | `Msg.Exp` + `U16 souls` |

### 7.7 อาหาร

| หัวข้อ | การตัดสินใจ |
| --- | --- |
| โครง | ใช้ `Pickup` + `PickupKind kind`; `Setup` แยก 2 สาขา (อาหาร = โมเดลเล็กลอยหมุน ~0.5 ม. + วงแหวนเขียวอ่อน ไม่มีลำแสง). ได้ `netId`, `owner`, `age`, `Find`, กะพริบ, sync ใน `Snapshot` |
| ชนิด | **Apple** ฟื้น **15%** ของ MaxHp และ **Roast Meat** ฟื้น **40%** (ฟื้นเป็นสัดส่วนเพราะ MaxHp 100 → 540) |
| โอกาสดรอป (`Game.RollDrop` ที่เดียว สุ่มอิสระจากดาบ) | Apple: เขียว **14%** / น้ำเงิน **16%** / แดง **18%**; Meat: เขียว 0 / น้ำเงิน **3%** / แดง **8%**; Minotaur: Meat **2 ชิ้น** 100%; หุ่นฟาง 0. hook `Game.foodOverride` สำหรับ SelfTest |
| ฟื้น | `Knight.Heal(healFrac * MaxHp)` เฉพาะ HP; popup เขียว `#5FE08A` "+N" + ประกาย |
| co-op | ใครเดินทับก่อน (`owner = -1`) แต่ `UpdatePickups` **ข้ามคนที่ HP เต็ม** (`k.hp >= k.MaxHp - 0.5`; remote ใช้ hp ที่ `State` รายงาน) และข้ามคนที่ตายอยู่; ถ้าทุกคนเต็มรอจนหมดอายุ. คนที่ HP เต็ม เดินเข้าใกล้ครั้งแรก: `Update` ของ **เครื่องเจ้าของ** แสดง popup เทา "HP full" ครั้งเดียวต่อ pickup (ทำฝั่ง local ไม่ต้องใช้เน็ต) |
| อายุบนพื้น | **60 วิ** (กะพริบ 8 วิสุดท้าย); ดาบ 90 วิเดิม; `Pickup.Lifetime` เป็นต่อชนิด; อาหารบนพื้นสูงสุด 40 ชิ้น |
| เก็บ | host: `Collect` → kind Food: local → `CollectLocal` (heal), remote → `PickupTaken(id,pid,kind,item,at)` ให้ client heal เอง |
| balance | แมพใหญ่ไม่มีที่ฟื้น HP อื่นนอกจากเลเวลอัป + อาหาร + กลับเมือง: ฟื้นเฉลี่ย ~+3% MaxHp ต่อตัวที่ฆ่า; ดู Q3 |

---

## 8. UI (ข้อ 7-9) และไอคอนดาบ

### 8.1 หน้าต่าง Tab (layout ตาม design.md หัวข้อ 6)

- `Game.Update` ใช้ `KeyCode.Tab` toggle; ลอจิก `SetInventory`/`InventoryOpen`/`PanelOpen`/`Paused` **คงเดิมทั้งหมด** (single หยุดเกม, ออนไลน์เป็น overlay กลางจอ ไม่ย้ายชิดขวา **ไม่มีโล่ป้องกันเพิ่ม** — โล่ 6 วิ `LevelUpProtect` ใช้เฉพาะหน้าต่างเลเวลอัปเหมือนเดิม; การย้ายชิดขวา/โล่ของ Tab เป็นข้อเสนอเพิ่มของ design อยู่นอกขอบเขตหลัก เปิดเป็นคำถามได้) ; ปิดอัตโนมัติเมื่อหน้าต่างเลเวลอัปเปิดหรือผู้เล่นตาย; **Esc ปิดหน้าต่างก่อน** (6.1)
- `Hud.BuildInventoryPanel` → สร้างใหม่ตาม design: กรอบ **ราว 1000 x 520** (กรอบทอง `#BF9E4D`, พื้น `#121219`; จอ 960x540 ของ mptest ยังพอเพราะ CanvasScaler match height อ้าง 1280x720)
  - **ซ้าย**: กล่อง **EQUIPPED** มีไอคอนดาบขนาดใหญ่ (RawImage ~160 px) + ชื่อ/ระดับ +N/ความหายาก + สเตตัสของดาบ (DMG/SPD/CRIT/REACH จาก `WeaponDef.StatLine()` และ `Inventory.DamageOf`); โฮเวอร์ช่องอื่น = พรีวิวพร้อมลูกศรเทียบ (ทางเลือก); ด้านล่าง **ช่องดาบ 5 ช่องเรียงแนวนอน ช่องละ 118 x 190 px** (พื้น `#1F1F2B`, กรอบสีตามความหายาก: Common `#B0B0B0` / Uncommon `#5FCF6B` / Rare `#D94A4A` / Legendary `#FFC93C`; ช่องที่ใส่อยู่กรอบเขียวสว่างหนา + แถบ "EQUIPPED" + ดาว; ป้าย +N มุมขวาล่าง "+5 MAX"; กด 1-5 / คลิก)
  - **ดาบที่ยังไม่มี**: ไอคอน **ย้อมมืดเป็นเงา** (`RawImage.color = (0,0,0,1)` คูณสี = silhouette ดำ — ใช้ texture เดียวกันไม่ต้องสร้างเพิ่ม) + "?" ตัวใหญ่ ชื่อ "???"; โฮเวอร์/เลื่อนเลือกแสดง `dropHint` (ไม่เฉลยชื่อ/สเตตัส — ตามข้อเสนอ design Q3 ซึ่งยังเป็นคำถามเปิด; ค่าเริ่มต้นที่ tech ทำ = ตาม design)
  - **ขวา** 3 บล็อกเรียงลง: **CHARACTER** (Lv n / 25, แถบ EXP, Health/Armor/Stamina, Sword hit/Swing/Crit/Reach), **STAT POINTS** (HP/BLOCK/STAMINA เป็นแถบ `n/10` พร้อมผลต่อแต้ม), **SKILLS** (Q/E/R/F: dmg/spd/crit), และแถวล่าง **SOUL n   Kills n** (ไม่มี Wave — เลิกแนวคิดเวฟรวมแล้ว ข้อ 9.3)
- ข้อความสถานะ (StringBuilder บรรทัด 642-662 ที่สร้างทุกเฟรม) ย้ายเป็น `RefreshTab()` เรียกเฉพาะตอนเปิด/ทุก ~0.2 วิ → ลด GC ของ HUD หลัก
- **จอหลัก**: ลบ `StatsPanel`+`statsText` (บรรทัด 129-132); **คงไว้**: HP/Armor/Stamina/EXP, **+ แถว Soul** (7.6), ไอคอนสกิล, boss bar, banner, notice, ตัวเลขดาเมจ. **ตำแหน่งที่ต้องเลื่อน**: แผงซ้ายบนสูง 180 สิ้นสุดที่ y = -196 → `PartyPanel` จาก (16,-326) เป็น **(16,-206)** (เลื่อนขึ้น 120 px), `NetStatus` -330 → **-210**, แถว party `y = -354 - i*26` → **`-234 - i*26`**; แผง controls (ขวาบน) คงที่ถ้าไม่มี minimap (ถ้าทำ minimap ตาม 9.6 ย้ายไป (-16,-196))
- ข้อความ "Tab = Inventory & Stats" มุมซ้ายล่างจางๆ 5 วิแรก (design 11) — `Hud.Notice`-แบบข้อความเล็กหนึ่งบรรทัด ไม่ต้องมี API ใหม่
- Boss bar / "Minotaur returns in" (`Hud.LateUpdate` ~681-693): แมพใหญ่ไม่ควรโชว์บอสตลอด → แสดงแถบเฉพาะเมื่อ Minotaur ห่างผู้เล่น < 45 ม.; **ลูกศรชี้รัง + ระยะ** แสดงตลอด (หัวข้อ 9.5)

### 8.2 ไอคอนดาบ (render จากโมเดล `Weapons.BuildModel` ลง RenderTexture)

`SwordIcons.Build()` (ครั้งเดียว `Game.Awake` **หลัง** `SetupLight()`):
1. root ชั่วคราวที่ `(0, -1000, 0)` ตั้ง layer **31** ให้ทุก child (`SetLayerRecursively`)
2. วนทุกดาบ: `Weapons.BuildModel(root, def)`; ปิด cast/receive shadow; หมุนให้ใบเฉียง **45 องศา** (ปลายขึ้นขวา); คำนวณ bounds จาก `Renderer.bounds` ตั้ง `orthographicSize` พอดี (+10%)
3. กล้องชั่วคราว: `orthographic`, `cullingMask = 1 << 31`, มองลงเอียง ~70 องศา, `clearFlags = SolidColor` สี **ทึบ** (0.12, 0.12, 0.17 เท่าสีพื้นช่องดาบ) — ไม่พึ่ง alpha, `targetTexture = RenderTexture(128,128,16, ARGB32)`, `enabled = false`
4. `cam.Render()` ทีละดาบ (นอก callback ของ pipeline เช่นใน `Awake`) → `RenderTexture.active = rt; tex.ReadPixels(...); tex.Apply()` เก็บเป็น **`Texture2D` ถาวร**
5. ทำลายโมเดล/กล้อง/RT ชั่วคราว เก็บ `Texture2D[5]`
6. กล้องหลัก `cullingMask &= ~(1 << 31)`
7. `Hud` ใช้ `RawImage.texture = SwordIcons.Get(i)`; ดาบที่ยังไม่มี = `color` ดำ (silhouette) — พื้นหลังทึบเป็นสีพื้นช่อง จึงเห็นเป็นกล่องดำสนิทถ้าคูณสีดำ: **แก้**: ให้ไอคอนที่ยังไม่มีคูณ `color = (0.08,0.08,0.1)` (มืดมากแต่ยังเห็นรูปร่างจากเส้นขอบ/แสง) หรือวาดซ้ำแบบ mask — dev เลือกวิธีที่เห็น silhouette ชัดสุดตอนทดสอบ; ถ้ายังไม่ชัด ให้เรนเดอร์ไอคอนเงาแยกด้วย material สีดำ `Prims.Glow(black)` บนพื้นหลังเทา (texture ที่ 2 ต่อดาบ)

**ข้อควรระวัง URP:**
- `Light.cullingMask` ไม่มีผลใน URP forward → ห้ามเพิ่มไฟพิเศษ (จะส่องโลกจริง); ใช้ sun + ambient/cubemap จาก `SetupLight()` จึงต้อง `Build` หลัง `SetupLight`
- เงา: ปิด cast/receive ที่โมเดลไอคอน
- Alpha: URP อาจบังคับ alpha = 1 → ใช้พื้นหลังทึบ
- `Camera.Render()` ของ base camera ใน Unity 6 ใช้ได้เมื่อเรียกนอก render loop; ถ้าเตือน/ไม่ render ใช้ `RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt })`
- MSAA pipeline 4x: RT `antiAliasing` ไม่ตรงถูกปรับเอง ไม่เป็นไร
- material ดาบมาจาก `Prims.Lit`/`Prims.Glow` (`Prims.Load` โหลด `Resources/Mats/*` เอง)
- `SwordIcons.Get` ต้องไม่คืน null: ถ้าสร้างไม่สำเร็จ fallback แผน B

**แผน B**: วาด silhouette ดาบเป็น `Texture2D` 128x128 ด้วยโค้ดล้วน (ใบ = สี่เหลี่ยมหมุน 45 องศา กว้าง `bladeWidth` ยาว `bladeLen`, การ์ด = แท่งขวาง `guardColor`, ด้าม `gripColor`, แกนเรืองตาม `coreColor`) ไม่พึ่ง URP

**SelfTest ตรวจ**: ไอคอนทั้ง 5 ไม่ null, 128 px, พิกเซลที่ไม่ใช่สีพื้นหลัง > 300, hash ไม่ซ้ำ

### 8.3 ตัวเลือกเสริมที่ถูกกว่า minimap (ทำได้ในขอบเขตหลักถ้าเวลาเหลือ)
ป้ายชื่อโซนกลางจอ 2 วิตอนข้ามเขต: `Game.Update` เช็ค `MapLayout.ZoneAt(knight.Pos)` ทุก 0.25 วิ เมื่อเปลี่ยนโซนเรียก `hud.Banner(zoneName, color)` เดิม (งานเล็ก ไม่มี net)

---

## 9. แมพใหญ่ (ข้อ 1 ของ requirement) รายละเอียดทางเทคนิค

### 9.0 ยืนยันขนาดเดิมจากโค้ด

| ที่ | ค่า | ผลที่ได้ |
| --- | --- | --- |
| `Game.ArenaHalf = 19f` ("the fenced field is 38 x 38 m") | ครึ่งด้าน 19 ม. | สนาม **38 x 38 ม.** |
| `Prims.Box("Outside" 160x160)`, ต้นไม้/หิน 120 ชิ้นนอกรั้ว, `farClipPlane = 200`, shadow distance 55 | | |
| ที่ใช้ `ArenaHalf` | `Knight.Simulate` clamp, `GroundSlam` `lim`, `Enemy.ClampToArena`, `Pickup.Spawn`, `Net.ReadKnight`, `SpawnWave`, SelfTest 139 | เปลี่ยนค่าคงที่ที่เดียวขยายครบ |

ใหม่ `ArenaHalf = 190f` → **380 x 380 ม.** (พื้นที่ 100 เท่า)

### 9.1 branch wip: เริ่มจาก main ใหม่
แนะนำ **branch ใหม่ `feat/big-map-update` จาก `main` (76fe974)** ไม่ merge/cherry-pick wip ทั้งก้อน: (1) wip แก้ `InputX.cs`/`Knight.cs`/`Progress.cs` ค้างครึ่งกลาง ไม่เคย build (2) `InputX.Map()` บน main มีปุ่มครบแล้ว (3) แผนนี้เปลี่ยนตัวเลข (EXP, AttrCap, stamina) ต่างจากที่ wip น่าจะทำ. architect อ่านได้เฉพาะไฟล์ที่ checkout อยู่ จึงไม่ได้เห็นเนื้อ wip; dev ดูได้ด้วย `git diff main..wip/big-map-update` เป็นข้อมูลอ้างอิงเฉยๆ

### 9.2 สร้างฉากใหญ่ในโค้ด

**สิ่งกีดขวางที่เกมใช้ตอนนี้**: ไม่มี collision กับฉากเลย — ต้นไม้ รั้ว เสาเป็นแค่ตกแต่ง; ผู้เล่น/ศัตรูถูกบีบด้วย `Mathf.Clamp` ที่ `ArenaHalf`; มีแค่ศัตรู `solid` (Dummy, Minotaur) ผลักผู้เล่นใน `Knight.Simulate` และศัตรูผลักกันใน `SimulateWorld`

**ผังโซน — ใช้ design.md หัวข้อ 3 เป็นตัวตั้ง** (X ตะวันออก, Z เหนือ):

| โซน (`enum Zone`) | พื้นที่โดยประมาณ (X, Z) | ตัวคูณสไลม์ (`SpawnTier`) | ผ่านได้? |
| --- | --- | --- | --- |
| `Town` เมือง | **สี่เหลี่ยม X -60..60, Z -190..-100** (ไม่ใช่วงกลมที่ origin) | 0 (ห้ามเกิด) = เขตปลอดภัย | ได้ |
| `Meadow` ทุ่ง | X -80..80, Z -95..110 | 1.0 | ได้ |
| `AutumnForest` ป่าใบส้ม | X -190..-90, Z -40..110 | 1.5 | ได้ |
| `DarkPineForest` ป่าสนมืด | X 100..190, Z 40..190 | 2.0 | ได้ |
| `Farmland` ไร่นา | X 90..190, Z -150..-30 | 1.0 | ได้ |
| `Lake` ทะเลสาบ | X -190..-90, Z -190..-90 | 0 (ไม่เกิด) | **ไม่ได้** |
| `Mountain` ภูเขา | X -190..-80, Z 100..190 + กำแพงภูเขาตามขอบอื่น | 0 (ไม่เกิด) | **ไม่ได้** |
| `Lair` รัง | ศูนย์กลาง (0,+150) รัศมี ~25 ม. | 2.0 | ได้ |

`ZoneAt` ตรวจตามลำดับความสำคัญ: Lair → Town → Lake → Mountain → โซนพื้นที่อื่น → Meadow (default) เพื่อให้ขอบที่ซ้อนกัน (เช่น Mountain Z 100..110 ทับ AutumnForest) ชัดเจน. พิกัดจริงเป็นข้อมูลล้วนในตาราง `MapLayout` (แก้ได้ไม่กระทบโครง)

**จุดสำคัญ**: จุดเกิด/ฟื้น `TownSpawn (0,-150)` (ออนไลน์ `SpawnPointFor(pid)` ออฟเซ็ต x ± 1.8 เหมือนเดิม — ยังอยู่ในเมือง), ลานหุ่นฟาง `DummyPos (25,-140)` (รั้วไม้ติดเมืองฝั่งตะวันออก), ประตูเมือง `Gate (0,-100)` (ซุ้มหิน 2 เสา), รัง `Lair (0,150)`, `MenuFocus (0,0,-142)`, Crossroads (0,-20), Watchtower (-60,70), กังหันลม ในไร่ (~(130,-100)). ระยะจุดเกิด→รัง **300 ม.**

**ถนน**: แถบแบนสีน้ำตาลอ่อนจากประตูเมือง (0,-100) ขึ้นเหนือถึง (0,+120) + ทางแยกไปป่าซ้าย/ขวาที่ Crossroads (ใช้ `GroundBox` เทคนิคเดิมของ RoadNS/EW; รวมใน `StaticBatcher`)

**ป้ายบอกทาง / landmark**: ป้ายบอกทางที่ Crossroads/ประตู = แผ่นไม้รูปลูกศรบนเสา (box + พิรามิด) ที่ชี้ทิศ พร้อมสีสัญลักษณ์ ไม่มีตัวหนังสือ (ตัวอักษรในโลกต้องใช้ `TextMesh`/world-space canvas ซึ่ง shader `GUI/Text Shader` อาจไม่ติด build URP — ถ้า design ต้องการข้อความ "Lair >" ให้ระบุ เสนอเป็น world-space `Text` ผ่านระบบ popup ของ `Hud` เมื่อผู้เล่นเข้าใกล้ป้าย ~12 ม. แทน); landmark สูง: หอคอย/หอนาฬิกาในเมือง, Watchtower (หินสูง ~14 ม. ใช้ `Prims.Lathe`/box ซ้อน), กังหันลม (ใบพัดหมุนช้าด้วย `Update` เดียว ไม่รวม batch), เสาไฟสีส้มที่รัง (`Prims.Glow`) — ทุกอย่างอยู่ใน `WorldBuilder`

**พื้นผิว**: ไทล์พื้น 19 ม. (20 x 20 = 400 ไทล์) สีตามโซน (`ZoneAt` ที่กลางไทล์) รวมต่อ chunk; patch หญ้าเลื่อมสี ~3 ชิ้นต่อ chunk (offset y แยกเล็กน้อยแบบเดิมกัน z-fight); ทะเลสาบ = ไทล์น้ำสีเขียวอมฟ้า (material ทึบหรือ `Fx` โปร่งใสเบา ไม่มีเงา) + ท่าเรือไม้/เรือ; `Outside` ขยายเป็น ~500 x 500 ม.

**ขอบแมพ**: ขอบที่ต่างกันตามโซนเป็น **ข้อเสนอเพิ่มของ design** — ทำได้ในขอบเขตง่าย: ขอบใต้/ตะวันตกด้านทะเลสาบ = ผืนน้ำ+ขอบ; ด้านเหนือ = ภูเขา; ที่เหลือ = รั้วไม้เดิม (เสาทำเป็น chunk, ราวแยกท่อน 38 ม. ตาม chunk) ตามลำดับ `ZoneAt` ของแนวขอบ; **ถ้าทำยาก/เวลาไม่พอ ให้คงรั้วเดิมทั้ง 4 ด้าน** (ไม่กระทบกติกา). กำแพงล่องหนคือ clamp `ArenaHalf - 0.4` เดิม

**collision ใหม่ (`Obstacles`)**:
- **บ้าน, กำแพงเมือง, ประตู (เสาประตูเท่านั้น ช่องประตูกว้าง ~6 ม. เปิดโล่ง), ราว/รั้วลานซ้อม, หินใหญ่, เสารัง, หอ** = box/circle บล็อก **ทั้งผู้เล่นและศัตรู** (bit0 | bit1)
- **ทะเลสาบและภูเขา = ผ่านไม่ได้ (ผู้เล่นและศัตรู)**: ลงทะเบียนเป็น `AddBox` ใหญ่ (สี่เหลี่ยมโซน; กล่อง 100 x 100 ม. ลงทะเบียนในทุกช่อง grid 8 ม. ที่ทับ ≈ 160 ช่อง ไม่หนัก) แล้ว `Push` ดันออกจากขอบกล่อง; ภูเขารูปทรงจริง (กรวยหลายเหลี่ยม) เป็นเพียงภาพ ขอบ collision ใช้กล่องเรียบเพื่อความเสถียร และวางกรวยภูเขาไว้ **ภายใน** กล่องเพื่อไม่ให้ดูเหมือนกำแพงล่องหน. นอกจากนี้ `MapLayout.Walkable(x,z)` ใช้เป็นตัวช่วยตอนสุ่มจุด (ด้านล่าง) ไม่ใช่ตัวตัดสิน collision
- **ต้นไม้** = วงกลมรัศมี ~0.45 ม. บล็อก **ผู้เล่นเท่านั้น** (bit0) — สไลม์กระโดดเส้นตรงจึงไม่ติด (ไม่มี pathfinding)
- ดอกไม้/กรวด/แพตช์พื้น/ถนน ไม่บล็อก
- grid ช่อง 8 ม. (48 x 48); `Push(p, radius, layer)` ตรวจ 9 ช่องรอบตัว; เรียกใน `Knight.Simulate` (หลังคำนวณ `p` ก่อน clamp รวมช่วง dash) และ `Enemy.BaseTick` (layer ศัตรู)
- Minotaur Charge ชน obstacle ที่บล็อกศัตรู → หยุดพุ่งเข้า `Recover`
- **วงแหวนเกิดสไลม์ต้องไม่สุ่มลงโซน Lake/Mountain/Town**: ทุกจุดที่สุ่มต้องผ่าน `MapLayout.SpawnTier(ZoneAt) > 0 && Walkable` และไม่อยู่ในกล่อง obstacle ที่บล็อกศัตรู (ตรวจด้วย `Obstacles.Push` ว่าตำแหน่งไม่ถูกดัน — คืนค่าเท่าเดิม) มิฉะนั้นสุ่มใหม่ (สูงสุด 20 ครั้ง ไม่ได้ → ข้ามรอบ)
- ผู้เล่น client ใช้ collision บนเครื่องตัวเอง (ฉาก deterministic) puppet ไม่ต้อง

**จำนวน mesh / draw call / เงา / ระยะกล้อง**:

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

### 9.5 นำทาง (ขอบเขตหลัก — ไม่มี minimap)
1. **ถนนดินจากประตูเมืองถึงรัง** + **ป้ายบอกทาง** ทุกสี่แยก (9.2) + **landmark สูง** (หอในเมือง, Watchtower, กังหันลม, เสาไฟส้มที่รัง)
2. **ลูกศรชี้รังพร้อมระยะ** บน HUD: Image ลูกศรเล็กใต้แผงซ้ายบนหรือใต้ boss text หมุนด้วย `Mathf.Atan2` จากเวกเตอร์ `Lair - knight.Pos` (กล้องไม่หมุน จึงใช้ทิศโลก = ทิศจอตรงๆ) + ข้อความ "Lair 143 m" (`Mathf.RoundToInt(distance)`); ซ่อนเมื่อห่างรังน้อยกว่า 30 ม.; ตำแหน่ง/สีจริงตาม design ถ้ามีกำหนด
3. ป้ายชื่อโซน (8.3) ถ้าทำ

### 9.6 ตัวเลือก: minimap (ไม่อยู่ในขอบเขตหลัก — ถ้าคนขอเลือกเอา)
- **ต้องทำ**: (1) สร้าง `Texture2D` 128x128 (ครอบ 380 ม. ~3 ม./พิกเซล) ตอนเริ่มเกม โดยสุ่ม `MapLayout.ZoneAt` ต่อพิกเซลลงสีโซน (เขียว/ส้ม/เขียวเข้ม/น้ำเงิน/เทา) + วาดถนนและกำแพงเมือง; (2) `Hud`: `RawImage` 170 x 170 มุมขวาบน (-16,-16) พื้น `#101418` ขอบทอง `#BFA050` ใช้ `uvRect` เลื่อนตามผู้เล่นให้เห็นราว 140 ม.; (3) ลูกศรผู้เล่น (หมุนตามทิศที่หัน), จุดเพื่อนสีตาม pid (`Knight.UiColor`) อัปเดตตำแหน่งทุกเฟรมจาก `Game.players`; เพื่อนนอกขอบแสดงลูกศรที่ขอบ + อักษรย่อชื่อ; ไอคอนเมือง/รังคงที่; ตัว "N" ที่ขอบบน; (4) ย้ายแผง controls ลงใต้ minimap (-16,-196); (5) ทดสอบที่ 960x540
- **ขนาดงาน: กลาง** (ประมาณ 1 commit เพิ่ม ~250-350 บรรทัดใน `Hud.cs` + ปรับ SelfTest ภาพหน้าจอ) ไม่แตะ net/protocol (ใช้ `Game.players` ที่ sync อยู่แล้ว)

---

## 10. ลำดับขั้นการทำ (แต่ละขั้น build ผ่านและ SelfTest ผ่านก่อน commit)

เริ่ม: `git checkout -b feat/big-map-update main` (ไม่แตะ wip). commit message ระบุผู้ขอ (test_disk) ตาม CLAUDE.md หลัก

| ขั้น | commit | ทำอะไร | ไฟล์หลัก | พิสูจน์ว่าผ่าน |
| --- | --- | --- | --- | --- |
| A | ปุ่ม + วิ่ง + dash + ตัด stamina | `KnightInput.run/dash`; `TryDash`, `CancelAction`, `Dashing`, popup DODGE; เอา stamina ออกจากท่า/สกิล/โล่; ค่า stamina ใหม่; **ขีดแบ่งบนแถบ stamina**; ข้อความ controls/skill slot; Tab (หน้าต่างเดิมก่อน) + Esc ปิด Tab | `Knight.cs`, `Game.cs`, `Hud.cs`, `Progress.cs`, SelfTest | build; SelfTest dash/วิ่ง/i-frame/Space ไม่ฟัน/คลิกขวาบล็อก/Tab/Esc ปิด Tab |
| B | เลเวล 25 + ตาย + Soul + **Protocol 2** | `MaxLevel 25`, `NeedTable`, `AttrCap 10` + ค่าต่อแต้ม, `EnemyExpScale`, `ResetProgress`, `OnKnightDown`, **หน้าจอตาย**, `ReviveDelaySingle 3`, `Knight.souls`, `Enemy.soulReward`, `Msg.Exp` + souls, **แถว Soul + แผงสูง 180**, `Protocol = 2` | `Progress.cs`, `Game.cs`, `Knight.cs`, `Enemy.cs`, `Slime.cs`, `Minotaur.cs`, `Net.cs`, `Hud.cs` | SelfTest (ไต่ถึง 25, ตายรีเซ็ต+overlay+นับถอยหลัง, Soul); **mptest.ps1** ผ่าน |
| C | อาหาร | `PickupKind`, `Foods`, โมเดล, `RollDrop`, `UpdatePickups`/`Collect`/`CollectLocal`/`OnPickupTaken`, snapshot+PickupTaken เพิ่ม kind, `Heal`, `foodOverride`, popup "HP full" | `Weapon.cs`, `Game.cs`, `Net.cs`, SelfTest, MpTest | SelfTest; mptest |
| D | หน้าต่าง Tab + ไอคอนดาบ | `SwordIcons`, Tab layout ตาม design (EQUIPPED + 5 ช่อง + คอลัมน์ขวา), ลบ `StatsPanel`, เลื่อน party panel -120 px, `RefreshTab` | `SwordIcons.cs`, `Hud.cs`, `Game.cs`, SelfTest | SelfTest ไอคอน/Tab; ดูภาพ `selftest\*.png` |
| E | แมพ: โซน/เมือง/ป่า/ทุ่ง/ทะเลสาบ/ภูเขา chunk + collision | `ArenaHalf 190`, `MapLayout` (เมืองใต้), `StaticBatcher`, `WorldBuilder`, `Obstacles`, far clip, snap กล้อง; **SelfTest/MpTest ย้ายผู้เล่นไปทุ่ง**; ยังใช้ `SpawnWave` เดิมชั่วคราว (clamp ใหม่) | `World.cs`, `Game.cs`, `Knight.cs`, `Enemy.cs`, SelfTest, MpTest | build; SelfTest ปรับพิกัด; log เฟรมไทม์ |
| F | ระบบเกิดศัตรูใหม่ + leash + นำทาง | `SpawnPack`, per-player timer, `MaxSlimes`, ลบไกล, เขตปลอดภัยเมือง, `ChaseRange`, `Minotaur` leash, boss bar ตามระยะ, ลูกศรรัง, เลิกประกาศ Wave, เคลียร์สไลม์รอบเมืองตอนฟื้น, ป้ายโซน (ถ้ามีเวลา) | `Game.cs`, `Slime.cs`, `Minotaur.cs`, `Hud.cs` | SelfTest + mptest |
| G | เก็บงาน | `CLAUDE.md`, mptest.ps1 timeout, zip + Drive ตามกฎหลัก | docs | ครบทุกเช็ก |

หมายเหตุ: A-D ทำบนแมพเดิม (จุดเกิด (0,-5)) ได้ทั้งหมด SelfTest เดิมยังใช้พิกัดแมพเดิมได้จนถึง E. ขั้น E ย้ายจุดเกิดไป (0,-150) ในเมือง และ **เมืองห้ามเกิดสไลม์ (กติกาขั้น F)** — ใน E ที่ยังใช้ `SpawnWave` เดิมจึงเกิดสไลม์ในเมืองได้ชั่วคราว ไม่ผิด; พอถึง F เทสต์ที่รอสไลม์ต้องย้ายผู้เล่นออกนอกเมืองก่อนเสมอ (ดู 10.1). ไม่รวม E กับ F ใน commit เดียว

### 10.1 SelfTest.cs — ที่ต้องแก้ / เพิ่ม

| ข้อ | เนื้อหา |
| --- | --- |
| แก้ | `Key.I` → `Key.Tab` (440, 458, 542, 545); `LeftShift` = บล็อก → เมาส์ขวาค้าง (339, 818) |
| แก้ | 244-246 ตารางเลเวล: ลูปถึง `MaxLevel 25` ตรวจ `NeedTable` สะสม = 16,410 |
| ลบ | เช็ค stamina ต่อท่า/สกิล/ดาบ (272-286, 484-507, 587-601) → "ฟัน/ใช้สกิลแล้ว stamina ไม่ลด" |
| แก้ | บล็อก 346 → ตัดดาเมจเท่าเดิมแต่ stamina ไม่ลด |
| แก้ | ลูปเลเวล 855-910 → 24 แต้ม ถึง 25; 892/901 `AttrCap` 10, บล็อกเพดาน 92% ที่ 10 แต้ม; 848 Minotaur EXP = `250 * scale` |
| แก้ | 938-947 regen stamina ค่าใหม่ (`RegenPerPoint 0.05`) |
| แก้ | 1036-1041 ตาย: level = **1**, ดาบ/Soul อยู่; **เวลารอฟื้น 3 วิ** (timeout ≥ 4 วิ), overlay เปิด/ปิด, `ReviveIn` นับลง; 1054-1063 F5 ล้าง Soul เพิ่ม |
| **แก้ (แมพใหม่)** | **ทุกที่ที่สมมติจุดเกิดที่ (0,-5), หุ่นฟาง (5,-3.5), Minotaur (0,12.5)**: ใช้ `MapLayout.DummyPos/Lair` และ **teleport ผู้เล่นไปที่หน้าหุ่น/หน้า Minotaur ก่อน** แต่ละส่วนทดสอบ (เดิมอยู่ใกล้กันหมด ตอนนี้ห่าง 300 ม.); ส่วนที่รอสไลม์/EXP ให้ teleport ไปทุ่ง เช่น (0,-40) ก่อน (เพราะเมือง = เขตปลอดภัย ไม่เกิดสไลม์) ; บรรทัด 139 ใช้ `ArenaHalf` อยู่แล้ว; ใส่ hook `Game.safeZoneOff` (test) ถ้าสะดวกกว่า teleport |
| เพิ่ม | วิ่งเร็ว ~1.5 เท่า ไม่ลด stamina |
| เพิ่ม | dash: ~6 ม., stamina -25, ถี่กว่า 0.8 วิ ถูกปฏิเสธ, i-frame (ยิง `TakeDamage` ใน 0.5 วิไม่ลด HP + มี popup DODGE; หลัง 0.55 วิลด), ยกเลิกท่าฟัน, ห้ามกลางอากาศ, Space ไม่ฟัน |
| เพิ่ม | แถบ stamina: จำนวนขีด = `floor(MaxStamina / 25) - 1`; ตรวจ `Hud` มีขีดตรงตำแหน่ง |
| เพิ่ม | Soul 1/3/6/50, ตายไม่หาย, แถว Soul บน HUD ตรงกับ `knight.souls` |
| เพิ่ม | อาหาร: ดรอป, เก็บแล้ว HP +15%/40% ของ MaxHp, HP เต็มไม่กิน (+ popup HP full), หมดอายุ 60 วิ |
| เพิ่ม | ไอคอนดาบ 5 เล่ม (ไม่ null, ไม่ซ้ำ, พิกเซลไม่ว่าง) |
| เพิ่ม | Esc ขณะ Tab เปิด: ปิดหน้าต่าง **และ `LeaveKey` ไม่ถูกเรียก** (ไม่มี notice "Press Esc again") |
| เพิ่ม | แมพ: `ArenaHalf == 190`; จุดเกิด (0,-150) อยู่ใน `InTown`; เดินไปมุม (189,-189) ถูก clamp; ชนบ้าน/ลงทะเลสาบ/ขึ้นภูเขาถูกดันออก; ต้นไม้บล็อกผู้เล่นไม่บล็อกสไลม์; เวลาสร้างฉาก (log); เฟรมไทม์ (log) |
| เพิ่ม | เกิดสไลม์: ไม่เกิดเมื่อผู้เล่นอยู่ในเมือง, อยู่ในวง 15-26 ม., ไม่เกิดใน Lake/Mountain, ไกล > 70 ม. ถูกลบ, `MaxSlimes`, สไลม์ไม่ไล่ผู้เล่นในเมือง |
| เพิ่ม | Minotaur leash: ลากออกห่างรัง > 40 ม. แล้วกลับรัง; ลูกศรชี้รัง: มุม/ระยะตรง `MapLayout.Lair` |

### 10.2 MpTest.cs / mptest.ps1
- `MpTest.cs` 381 (`case "block"` ใช้ `Key.LeftShift`) → เมาส์ขวา; 1203/1206 `Key.I` → `Key.Tab`
- **ทุกคนเกิดในเมือง (0,-150)** — คำสั่ง `tp`/`HostScript` เดิมที่สมมติพิกัดใกล้ origin และรอสไลม์ ต้องย้ายไปทุ่ง (เช่น (0,-40) ± ออฟเซ็ต) ก่อนส่วนที่ต้องมีศัตรู; `MoveDrops(to)` และ `Tp` เดิม (651, 683) ปรับพิกัด
- เพิ่ม cmd: `dash` (กด Space จริง), `tpfar x z` (วางที่มุมแมพ) ให้ host เทียบ summary ว่า client อื่นเห็นตำแหน่งตรง ±0.05 ม. (**พิสูจน์ encoding cm ที่ ±190 ม.**), `eatfood`
- เพิ่มเช็ค: เพื่อนเห็น dash (`ActionsSeen`), Soul ได้ทั้งสองคนต่อการฆ่า 1 ตัว, อาหารคนแรกที่ HP ไม่เต็มได้ HP และอีกคนไม่ได้, คนตายแล้วเลเวลที่เพื่อนเห็นเป็น 1, คน HP เต็มไม่กินอาหาร
- ทดสอบเวอร์ชัน: ปลอมเลขเวอร์ชันให้ต่างจาก `Protocol = 2` ต้องถูก `Refuse`
- `mptest.ps1`: ขยาย timeout (เดิมราว 1.5 นาที → ~2.5 นาที เพราะขั้นใหม่ + ฟื้น 5 วิ + วิ่งไปทุ่ง); หน้าต่าง 960x540 เหมือนเดิม

### 10.3 CLAUDE.md ของเกม — ต้องแก้ตาม
คำอธิบายเกม (เลเวล 1-10 → 1-25, สนาม 380 ม.); ตารางโครงสร้างไฟล์ (เพิ่ม `World.cs`, `SwordIcons.cs`; แก้แถว `Game.cs`, `Knight.cs`, `Progress.cs`, `Weapon.cs`, `Hud.cs`, `Net.cs`); "ปุ่ม" (Shift วิ่ง, Space dash, คลิกขวาโล่, คลิกซ้าย/J ฟัน, Tab แทน I, Esc ปิด Tab ก่อน, F5 ล้าง Soul); "ระบบเลเวลและอัปเกรด" (ตาราง EXP ใหม่, EXP สเกล, Attack Speed ไม่เกี่ยว stamina, ตายรีเซ็ต + หน้าจอตาย 3 วิ); "แต้มสเตตัส" (24 แต้ม, `AttrCap` 10, ค่าใหม่); "ดาบและช่องอาวุธ" (Tab, ไอคอน, ไม่มี stamina ต่อครั้ง); หัวข้อใหม่ "Soul", "อาหาร", "แมพ/โซน/การเกิดของศัตรู/นำทาง"; "แนวทางแก้โค้ด"; "Online co-op" (`Net.Protocol` 2, ตารางข้อความ, ขนาด snapshot, `Pos` 380 ม., Soul/อาหาร/ตายรีเซ็ต/`OnPartyWipe`); "ทดสอบหลายโปรเซส" (cmd ใหม่ ข้อจำกัด)

---

## 11. ความเสี่ยง และสิ่งที่อาจพังของเดิม

| # | เรื่อง | ความเสี่ยง | คำแนะนำ (ข้อเดียว) |
| --- | --- | --- | --- |
| R1 | สร้างฉากใหญ่ช้า/กิน RAM | ต้นไม้ 2,500 + บ้าน ถ้า GameObject ต่อชิ้น ~15,000 renderer เฟรมตก | บังคับ `StaticBatcher` ทุกอย่าง (ห้าม `Prims.Part` ตรงในฉาก); ถ้าสร้างเกิน ~2 วิ ลดจำนวนต้นไม้ก่อน |
| R2 | ศัตรูติด collision | สไลม์กระโดดเส้นตรงติดบ้าน/กำแพง/ขอบทะเลสาบ | ศัตรูบล็อกเฉพาะบ้าน/กำแพง/หินใหญ่/ทะเลสาบ/ภูเขา; ต้นไม้บล็อกเฉพาะผู้เล่น; spawn ring กรองจุดไม่ให้ลง Lake/Mountain |
| R3 | สไลม์เกิดไม่ถึง | ระยะไล่เดิม 17 ม.; แพ็คไกลเดินเฉย | `ChaseRange` 30 ม. + วง 15-26 ม. + ลบเมื่อ > 70 ม. |
| R4 | snapshot โตตาม cap | 48 ตัว ~30 KB/s ต่อ client | ไม่ทำ interest filtering ตอนนี้ — cap 48 และ pickup 40 |
| R5 | โล่ฟรี + กัน 80-92% เล่นเต่าได้ | ยกโล่ช้าลง x0.4 ฟันไม่ได้ขณะบล็อก แต่ไม่เสียอะไร | ยอมรับตาม requirement แล้วปรับ `BaseBlock`/`BlockPerPoint` หลังเทสจริง |
| R6 | dash ยกเลิกสกิลหลังเสีย cooldown | เสียสกิลถ้ากดพลาด | ยอมรับ ไม่คืน cooldown |
| R7 | ธงค้างหลัง `StopAllCoroutines` | `invuln` ของ Dash Thrust, `whirl`, `lockFacing`, `shieldPush`, `IsAttacking`, `rigSpin` | รวม cleanup เป็น `CancelAction()` ใช้ร่วมกับ `ResetAll`/`DownRoutine` |
| R8 | balance เลเวล 25 | ศัตรู HP x11.8 ดาเมจ x2.92, EXP เป็นค่าประมาณ | คงสูตร + log ใน SelfTest + ปรับหลังเทสเล่น |
| R9 | ไอคอนดาบบน URP | alpha, เงา, ไฟ, `Camera.Render()` | พื้นทึบ + ปิดเงา + sun เดิม + `Texture2D`; ล้มเหลวใช้แผน B |
| R10 | ตายรีเซ็ตแต่ศัตรูแข็งเดิมอยู่ | ผู้เล่น Lv 1 เจอสไลม์ HP x11 | เคลียร์สไลม์รัศมี 60 ม. รอบเมืองตอนฟื้น + ลบเมื่อไกล |
| R11 | ตายไกลเมือง | วิ่งกลับ ~32 วิ (จุดเกิด→รัง 300 ม.) | ไม่เพิ่ม checkpoint รอบนี้ (Q2) |
| R12 | SelfTest/MpTest เดิมแตก | ใช้ I/Shift/Space, stamina, 6 แต้ม, เลเวล 10, พิกัดแมพเดิม, ฟื้น 1.4 วิ, **สมมติเกิดใกล้ศัตรู** | แก้ตาม 10.1-10.2 ในขั้นเดียวกับโค้ดที่เปลี่ยน (ห้าม commit ที่เทสแดง) |
| R13 | ฉากไม่ deterministic | collision ไม่ตรง → rubber-banding | `System.Random(11)` ล้วน ห้าม `UnityEngine.Random` ใน `WorldBuilder` |
| R14 | `Pickup.weaponId` ใช้เป็น item id อาหาร | อ่านสับสน | คอมเมนต์ชัด หรือ rename `item` |
| R15 | wip branch | ไม่ build, ไม่ตรงแผน | เริ่มจาก main (9.1) |
| R16 | ผู้เล่นคนละฝั่งแมพ | host จำลองทั้งแมพ | cap 48 ทำให้ CPU คงที่ |
| R17 | collision กล่องใหญ่ของทะเลสาบ/ภูเขาดันผู้เล่นออกผิดทิศ | ผู้เล่นถูกดึงข้ามกล่องถ้ากล่องบาง/หลาย frame | ใช้กล่องหนา (ครบโซน) และ `Push` ดันออกทางขอบที่ใกล้สุด; SelfTest ตรวจ |
| R18 | ป้ายบอกทางไม่มีตัวหนังสือ | design ขอข้อความ "Lair >" | ป้ายลูกศร + popup ข้อความ HUD เมื่อเข้าใกล้ (9.2) |
| R19 | ขนาดงานรวม | ฟีเจอร์เยอะมาก | แบ่ง 7 commit (หัวข้อ 10) |

---

## 12. ประมาณขนาดงาน

**ใหญ่** (ไม่เปลี่ยนจากเดิม) — A กลาง, B กลาง-ใหญ่ (เพิ่มหน้าจอตาย + แถว Soul), C เล็ก-กลาง, D กลาง, E **ใหญ่** (โซน 8 ชนิด + ทะเลสาบ/ภูเขาเป็น obstacle + เมืองมีกำแพง/ประตู), F กลาง, G เล็ก; minimap (ตัวเลือก) +กลาง. การปรับผังเป็นเมืองใต้ + ทะเลสาบ/ภูเขาทำให้ E หนักขึ้นเล็กน้อยแต่ยังอยู่ในระดับ "ใหญ่"

---

## 13. จุดที่ต้องตรงกับ design (ตกลงแล้ว)

1. **ผังแมพ**: ตามตาราง 9.2 (design หัวข้อ 3): เมืองใต้ X -60..60 Z -190..-100, จุดเกิด (0,-150), ลานหุ่น (25,-140), ประตู (0,-100), รัง (0,+150) รัศมี ~25, Meadow/Autumn/DarkPine/Farmland/Lake/Mountain ตามพิกัด design; ถนนจากประตูขึ้นเหนือถึงรัง
2. **เขตปลอดภัย** = สี่เหลี่ยมเมือง (`InTown`) ทั้งห้ามเกิดสไลม์, สไลม์ไม่ไล่ผู้เล่นในเมือง, และการลบสไลม์ตอนฟื้น (รัศมี 60 ม. รอบจุดเกิด)
3. **โซน** 8 ชนิด + ตัวคูณสไลม์ตาม 9.2 (Lake/Mountain/Town ไม่เกิด)
4. **ทะเลสาบ/ภูเขาผ่านไม่ได้** ผู้เล่นและศัตรู (กล่อง `Obstacles`); กำแพงเมือง/ประตู/บ้านเป็น obstacle; ขอบแมพต่างกันตามโซน = ข้อเสนอ (ทำถ้าไม่ยาก ไม่งั้นรั้วเดิม)
5. **หน้าต่าง Tab** ตาม design หัวข้อ 6: ~1000 x 520, ช่องดาบ 5 ช่องแนวนอน 118 x 190, กล่อง EQUIPPED ไอคอนใหญ่ + สเตตัส, คอลัมน์ขวา CHARACTER / STAT POINTS / SKILLS / SOUL+Kills, ดาบที่ยังไม่มี = เงามืด + "?"; ไอคอน 128 px พื้นทึบ เฉียง 45 องศา; ออนไลน์: overlay กลางจอ ไม่ย้ายชิดขวา ไม่มีโล่เพิ่ม
6. **Soul** แถวที่ 5 ของแผงซ้ายบน (116,-156) แผงสูง ~180 และ popup "+N Soul" `#9FE8FF`
7. **แถบ stamina** มีขีดทุก 25 หน่วย (1 ขีด = 1 dash) + วาบตอนฟื้นพอ
8. **หน้าจอตาย** YOU DIED + "You lose / You keep" + นับถอยหลัง; ค่าก่อนรีเซ็ตแสดงจาก `OnKnightDown`; ฟื้น single 1.4 → **3 วิ** (`Game.ReviveDelaySingle`) ออนไลน์ 5 วิเดิม
9. **Esc ขณะ Tab เปิด** = ปิดหน้าต่าง ไม่ถือเป็น Esc ออกเมนู
10. **Dash** ไม่กดทิศ = ถอยหลัง; popup "DODGE" (`TakeDamage`, ฝั่ง client เจ้าของ ไม่กระทบ trust model); เอฟเฟกต์ใช้ `Fx` เดิม ไม่มี afterimage ในขอบเขตหลัก
11. **นำทาง**: ถนน + ป้ายบอกทาง + landmark สูง + ลูกศรชี้รังพร้อมระยะ; **ไม่มี minimap** (ตัวเลือก 9.6)
12. **ประกาศ "Wave n"**: เลิก (ไม่มี `Net.SendBanner`); Tab แสดง Kills; (Q6 เปิดให้คนขอ)
13. **ตัวเลขสมดุลตาม tech เป็นตัวตั้ง**: dash 25 / 0.8 วิ / 6 ม., วงเกิด 15-26 ม., ไล่ 30 ม., ลบ 70 ม., อาหาร 2 ชนิด % MaxHp 60 วิ, `AttrCap` 10, ตาราง EXP — design.md จะปรับตาม

**จุดที่ tech ยังต่างจาก design (ให้ปูส้มยืนยัน/แก้ design.md)**:
- (ก) **ระยะเวลาเดินจุดเกิด → รัง**: 300 ม. = วิ่ง ~32 วิ / เดิน ~48 วิ แต่ design 3.1 อยากได้ 45-60 วิที่ความเร็ววิ่ง. ทางเลือก: ยอมรับ ~32 วิ (แนะนำ) หรือขยับรังไปเหนือกว่านี้ไม่ได้แล้ว (ขอบแมพ 190) หรือลด `RunMult` เป็น 1.2 (~40 วิ)
- (ข) **Minotaur "ไม่ออกมาเดินนอกรัง"** (design 3.5): tech ใช้ leash 40 ม. จากรัง (ออกได้นิดหน่อยเพื่อไล่ผู้เล่น) แทนห้ามออกเด็ดขาด
- (ค) **ป้ายบอกทางมีข้อความ** ("Lair >", "Forest <") ใน design: tech ทำเป็นป้ายลูกศรไม่มีตัวหนังสือ + popup ข้อความตอนเข้าใกล้
- (ง) **วงแหวนเกิดสไลม์** design 25-45 ม., tech 15-26 ม. (ตัวตั้ง) — design ต้องปรับ
- (จ) **อาหาร**: design ไอเทมเดียว (drumstick) อยู่บนพื้น 30-45 วิ; tech 2 ชนิด (Apple/Meat) 60 วิ (ตัวตั้ง)
- (ฉ) **dash cooldown** design ~0.25 วิ vs tech 0.8 วิ (ตัวตั้ง)
- (ช) **Tab ออนไลน์**: design ถามย้ายชิดขวา+โล่ (Q8) — tech ไม่ทำในขอบเขตหลัก

---

## 14. คำถาม / ความเสี่ยง (แยกชัดเจน)

### คำถามเปิด (ต้องการคำตอบจากพีท/คนขอ)

- **Q1** EXP ต่อมอนสเตอร์สเกลตามเลเวลศัตรู (x(1+0.15 ต่อเลเวล)) ตกลงตามนี้ไหม? ถ้าไม่สเกล ต้องฆ่าเป็นร้อยตัวต่อเลเวลที่ปลาย หรือรื้อตาราง EXP ให้ต่ำลงมาก
- **Q2** ตายไกลเมือง (จุดเกิด→รัง 300 ม. วิ่ง ~32 วิ) ต้องการจุดฟื้น/checkpoint กลางทางไหม (ค่าเริ่มต้น: ไม่มี)
- **Q3** เมืองฟื้นเลือดช้าๆ ไหม (เช่น +5 HP/วิ ในเขตเมือง; ค่าเริ่มต้น: ไม่มี)
- **Q5** F5 ล้าง Soul — ตกลงไหม (ตายไม่ล้าง; F5 ล้าง)
- **Q6** ประกาศ "Wave n" แบบแพ็ครายคน ยังต้องการไหม (ค่าเริ่มต้น: เลิกประกาศ)
- **Q7** อาหาร 2 ชนิด (Apple/Meat) พอไหม หรือต้องการ 1 ชนิดตาม design / มากกว่า?

### ตัวเลือก (ไม่ใช่คำถามเปิด — เลือกเพิ่มได้)

- **Minimap** (หัวข้อ 9.6): งานเพิ่มระดับกลาง ~1 commit ไม่กระทบ net
- afterimage ของ dash, ป้ายชื่อโซน (8.3, งานเล็ก), Tab ออนไลน์ชิดขวา/โล่, ขอบแมพต่างกันตามโซน

### ความเสี่ยงใหญ่ที่สุด

1. **ขั้น E (แมพ chunk + โซน 8 ชนิด + collision ทะเลสาบ/ภูเขา/กำแพงเมือง)** — ใหญ่สุด กระทบ `Knight`, `Enemy`, SelfTest/MpTest พร้อมกัน, ประสิทธิภาพวัดไม่ได้ก่อนลงมือ → ทำท้ายและวัดเฟรมไทม์ก่อน commit
2. **การปรับ SelfTest/MpTest** (ปุ่ม, stamina, เลเวล 25, ตายรีเซ็ต + ฟื้น 3 วิ, **ผู้เล่นเกิดในเมืองที่ไม่มีสไลม์ ศัตรูอยู่ไกล 300 ม.**) — ถ้าไม่ทำพร้อมโค้ดจะ build ผ่านแต่เทสแดง/ค้างรอสไลม์
3. **ตัวเลข balance** (เลเวล 25, EXP, โล่ฟรี) เป็นค่าประมาณจากการคำนวณมือ ยังไม่ได้เล่นจริง
4. **การตัดสินใจเรื่อง branch wip** ทำจากการอ่านเฉพาะ main — ถ้ามีของใน wip ที่พีทอยากเก็บ บอก dev ก่อนเริ่ม
