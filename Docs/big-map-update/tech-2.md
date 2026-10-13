> ส่วน 2/14 ของ `tech.md` (ดัชนี: `tech.md`) · ต่อจาก `tech-1.md` · ต่อที่ `tech-3.md`

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

