> ส่วน 6/14 ของ `tech.md` (ดัชนี: `tech.md`) · ต่อจาก `tech-5.md` · ต่อที่ `tech-7.md`

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

