> ส่วน 11/14 ของ `tech.md` (ดัชนี: `tech.md`) · ต่อจาก `tech-10.md` · ต่อที่ `tech-12.md`

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

