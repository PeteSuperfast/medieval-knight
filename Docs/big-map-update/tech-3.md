> ส่วน 3/14 ของ `tech.md` (ดัชนี: `tech.md`) · ต่อจาก `tech-2.md` · ต่อที่ `tech-4.md`

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

