# โครงสร้างไฟล์

> แยกมาจาก CLAUDE.md (รายละเอียดเต็ม) — ภาพรวมสั้นอยู่ที่ CLAUDE.md. ส่วน loop (ฐาน / โลก / เซฟ) ดู `loop.md`

| ที่อยู่ (สคริปต์อยู่ใต้ `Assets/MedievalKnight/Scripts/`) | หน้าที่ |
| --- | --- |
| `Game.cs` | จุดเริ่มเกม (`Boot`), กล้อง top-down, แสง, สร้างฉาก (`WorldBuilder`), แพ็คสไลม์ (`UpdatePacks`), EXP + Soul (`OnEnemyKilled`), เลเวลอัป 2 ขั้น (`LevelStep`), ดรอป (`RollDrop`), เก็บของ (`UpdatePickups`), Tab (`SetInventory`), ตาย / ฟื้น, shake, hit-stop, ปุ่ม F1 / F5 / H / Tab / M / Esc. **Loop**: `StartSession(loop)`, `run`, `Loop` / **`Coop`**, `OnLoopDeath`; phase 2: `worldHpMul` / `worldDmgMul` / `worldPackMul`; **phase 4**: `WantsSnapshot` / `SendsState` / `PidFree` / `WelcomeList` (ที่ Net ถาม), join/left ของห้อง, เก็บของของเพื่อน, กล้อง spectate, `TestDownSeconds` |
| `World.cs` | `MapLayout` (โซน, จุดเกิด / รัง / ประตู / `BasePoint`, `Walkable`, `SpawnTier` — **ตอบตาม `MapLayout.Current` (biome)**; `TownSpawnOf` / `WalkableIn` สำหรับ biome อื่น), `Obstacles` (collision บน grid 8 ม.: `Push`; **ชุดต่อ biome** `Use(biome)`), `StaticBatcher`, `WorldBuilder` (ฉากทุ่งทั้งหมด — `System.Random(11)` ล้วน) |
| `Biome.cs` | `enum Biome { Meadow, Snow }`, `Biomes.FromSeed / Roll / Valid / Label` |
| `Ground.cs` | `Ground.Y(x,z)` (ทุ่ง = 0; หิมะ = ตารางความสูง bilinear), `Top(x,z,r)` (จุดสูงสุดในรัศมี สำหรับวงเตือน/ริง), `Snap` — ใช้กับภาพเท่านั้น (ตัวละคร กล้อง Fx) การสู้/ระยะ/ชนยังเป็น 2D |
| `SnowLayout.cs` | ข้อมูลแมพหิมะที่ bake ไว้ (`Resources/Snow/*`): ตารางความสูง/ห้ามเดิน, จุดเกิด/รัง/หุ่นฟาง/หมู่บ้านปลอดภัย, โซน A-D3, ป้าย; DTO `SnowLayoutDto` / `SnowMeta` |
| `SnowBuilder.cs` | สร้างฉากหิมะ (ชิ้นวาง ~1,180 ชิ้น -> `StaticBatcher`, ไฟล์พื้น/น้ำ/ทางเป็น renderer ตรง, กังหันหมุน) + `Obstacles` ของ biome หิมะ; เรียกจาก `Game.SetBiome` ครั้งแรกตอนจอดำ |
| `SelfTestSnow.cs` | เคสแมพหิมะ (`-snowonly`; ข้อมูล/เซฟ/กฎอยู่ใน `-looponly` ด้วย) |
| `Knight.cs` | ผู้เล่น: เดิน หัน วิ่ง โล่ HP / Armor / Stamina; **ท่าฟัน 3 ท่า** (`Click` > `BeginSwing` > `SwingRoutine` ขับด้วย `ActClock`, `HitArea` ทั้งพัดและแถบ `capWidth`, คิวคลิก `queuedClick`, `DebugAttack`); **กลิ้ง** (`TryRoll` / `RollRoutine`, `Rolling`, Act 20); **ปัดโล่** (`ParryRoutine`, `ParryLocked`, `ParryMotionCount`, Act 22) และ stamina ของโล่ (`BlockStaminaCost`); **กินอาหาร** (`TryBeginEat` / `UpdateEating` / `FinishEat`, `Eating`, `EatProgress`, flag `NetEating` 64, Act 21); `progress` / `inventory` / `souls`, ตัวเลขต่อสู้จากดาบอย่างเดียว (`DamageMult()` / `CritChance()` / `CritMult()` / `SwingSpeed` / `Reach(SwingDef)`), `RollDamage(base, out crit)`, `Equip`, `SetWeapon` (loop), `SpendPoint` (5 สเตตัส), `Heal`, `ResetProgress` (ตาย) / `ResetAll` (F5) / `ResetForRun` (loop), `CancelAction`; parry (`ParryOpen`) / clash (`ClashOpen`) ใน `TakeDamage(..., by, heavy)`; **phase 4**: `Down` / `Fallen` / `DownLeft`, `Revive()`, `RevTarget`, `NetFallen`, DownRoutine แบบทีม |
| `KnightAnim.cs` | **ข้อมูลเวลาของท่าทั้งหมด**: `SwingDef[3]` (`KnightAnim.Swings`), `Roll`, `Parry`, `Eat` (ค่าคงที่ในโค้ด), `PoseKey`/`ShieldKey` (ท่าของ CodedBody), `ActClock` (phase/t จากเวลา + ฟังก์ชันล้วน `Locate`/`HitWindow`/`Pose`) — เพิ่มท่าใหม่ที่นี่ |
| `KnightBody.cs` | ตัวแสดงผลอัศวิน: `KnightBody` (abstract), `CodedBody` (โมเดลโค้ดเดิม: ท่าเดิน ยืน โล่ ท่าฟัน/กลิ้ง/ปัด/กิน), `KnightGear.BuildShield`; `Knight.BodyKind` = "coded" (ตัว "file" มาพร้อม .fbx ขั้น 9) |
| `ModelLib.cs` | ระบบรับ .fbx: `ModelLib.Model/Clip/Spawn/Strip/Recolor` (ไม่มีไฟล์ = null ไม่ throw) และ `EffectLib.Play(name)` (โหลด `Resources/Effects/<name>`; ไม่มี = ข้ามเงียบ ใช้ `Fx` เดิม) |
| `Progress.cs` | เลเวล / EXP (`Need(lvl)`), แต้มสเตตัส 5 ตัว `Attr {Hp, Def, Stamina, Speed, Resist}` (`Spend`, `AttrCap`, `MoveSpeedMult`, `DamageTakenMult`, `KnockbackTakenMult`) — instance ของอัศวินแต่ละคน; ไม่มีการ์ดอัปเกรดดาเมจ/ความเร็ว/คริ/สกิลแล้ว |
| `Weapon.cs` | `WeaponDef` + `Weapons.All`, `Weapons.BuildModel` (จาก `Prims` หรือไฟล์ `modelPath`), `Inventory` (`SetSingle` = ภาพฉายของ HELD ใน loop), `Foods`, `Pickup` (`kind`, หมุน + วงแหวน, ดาบ 90 วิ อาหาร 60 วิ, `plus` / `dropped` ของ loop) |
| `Loadout.cs` | **Loop**: `Item`, `ItemBag`, `Loadout` (stash / carry / HELD / `worlds` โลกเก่า / `runWorld` / `unseen`), `RunResult`; `Die` → **`Fall(drops)`** (ของเข้ากอง) / `Survive(decay)` / `LoseCarry` / `TakeFromPile` / `TakeAll` / `Sanitize` / `runCoop` — logic ล้วน |
| `OldWorld.cs` | **Loop**: `Pile`, `OldWorld` (seed, เวลา, left, **`piles`**, `AddDrop`, `MergeRadius` 3, ค่าคงที่ `StartLeft` 3, `HpMul` 1.5, `DmgMul` 1.25, `PackMul` 1.5, `MaxPile` 48/โลก, `MaxWorlds` 99), `Drop`, `TeamLine`, `CrumbNote` / `CrumbLine` / `WorldNote` — ข้อมูลล้วน |
| `LoopSave.cs` | **Loop**: อ่าน/เขียนเซฟ JSON (atomic, `.bak`, `.corrupt`, validate); DTO `ow[]` (`pv:2` + `piles[]`; ไม่มี `pv` = กองเดียว) / `runWorld` / `rpos` / `unseen[]` / `rc` (Version ยัง 1) |
| `RunLoop.cs` | **Loop**: state ฐาน / PREPARE / โลก / สรุป, เข้าโลก (ใหม่ / เก่า), เก็บ / กิน / ทิ้ง, ตาย, บอส + portal; ตัวคูณโลกเก่า, `PilePoint`, หลายกอง (`PileView`), `UiTake` / `UiTakeAll`, autosave; **ออนไลน์**: `team`, READY, `OnTeamEnter`, `HostVictory` / `HostWipe` / `HostAbandon`, `OnGrant`, `OnTeamRunEnd` |
| `LoopUi.cs` | **Loop**: PREPARE (NEW WORLD / OLD WORLDS), หน้ารายการโลกเก่า, หน้าต่าง Carry, หน้าต่างเก็บของจากกอง, หน้าสรุป (G3 ตารางทีม), fade, objective, ป้ายโลกเก่า, ป้าย/ลูกศรกอง, READY (canvas ของตัวเอง) |
| `Portal.cs` | **Loop**: portal โลก (วงแหวน + ลำแสง + วงโหลด) |
| `LootPile.cs` | **Loop phase 2**: กองของโลกเก่า (ถุง + ด้ามอาวุธ + วงทอง + ลำแสงทอง 12 ม.); `Vanish()` ตอนเก็บหมด |
| `SwordIcons.cs` | ไอคอนดาบ 128 x 128: ถ่ายโมเดลด้วยกล้อง ortho (layer 31) ตอนเริ่ม; แผนสำรองวาดพิกเซล |
| `Sfx.cs` | เสียง: โหลด 6 คลิปจาก `Resources/Audio`, AudioSource 2D 8 ตัว, สุ่ม pitch / volume, ปุ่ม F2 |
| `Enemy.cs` | คลาสแม่ศัตรู: HP + Armor, กระเด็น, สตัน, แถบเลือด, ตัวเลขดาเมจ (`NumberStyle` สีเลข), `soulReward`, `ClampToArena`; **p7**: `Deflect`/`NetDeflect` (parry/clash), `glowAmt`, `HeavyWarn` |
| `Slime.cs` | สไลม์ 3 สี กระโดดไล่ผู้เล่นใน 30 ม. ยกเว้นคนที่ยืนในเมือง |
| `Minotaur.cs` | บอส: AI 3 ท่า (Sweep / Smash / Charge) มีเครื่องหมายเตือน, leash (ห่างรัง > 40 ม. / ไม่มีใครใน 45 ม. 5 วิ = กลับรัง) |
| `Dummy.cs` | หุ่นฟาง: ไม่โจมตี ไม่ให้ EXP / Soul พังแล้วเกิดใหม่ |
| `Fx.cs` | เอฟเฟกต์: อนุภาคลูกบาศก์ + mesh แบนบนพื้น (เส้นฟัน วงคลื่น เส้นพุ่ง เครื่องหมายเตือน) |
| `Hud.cs` | UI (uGUI `Text`): แผงซ้ายบน (HP / Armor / Stamina / EXP / Soul), แถบบอส, ลูกศรชี้รัง/portal (`SetArrowTarget`), การ์ดสเตตัส 5 ใบตอนเลเวลอัป (`ShowStatChoice`, `LevelUpInput`), Tab (v1; กล่อง STATS 5 แถว), แถบกินอาหาร + "Hold F: Eat" (`UpdateEatBar`), YOU DIED, `Notice`, `Banner`, ปุ่มควบคุม (`SetLoopTexts`) |
| `Prims.cs` | mesh low-poly ในโค้ด + material (`Lit` / `Glow`), `Tint` |
| `InputX.cs` | ตัวกลางรับปุ่ม / เมาส์; ปุ่มใหม่ต้องเพิ่มใน `Map()` |
| `Net.cs` | Online: `Conn` (TCP + thread), `NetW` / `NetR`, `enum Msg`, host / join / handshake, snapshot (ต่อ conn), `Protocol` 7 |
| `TeamRun.cs` | **phase 4**: ห้อง (`roster`, `leavers`), READY, mirror ของ carry, Enter, ล้ม/ชุบ/wipe, portal ทีม, Take / Piles / Worlds |
| `TeamNet.cs` | **phase 4**: เข้ารหัส/ถอดรหัสข้อความ loop (`Pack` `Drop` `Take` `Revive` `Reached` `Team` `Worlds` `Enter` `Piles` `TakeAck` `Portal` `HomeIn` `RunEnd`) |
| `MapUi.cs` | **phase 7**: แผนที่ M (canvas ของตัวเอง; texture 190x190 จาก `MapLayout.ZoneAt`; มาร์กเกอร์ ตัวเอง/เพื่อน/portal/กอง `RunLoop.PileMarks`/รัง; ไม่แสดงศัตรู; ไม่หยุดเกม) |
| `TeamUi.cs` | **phase 4**: รายชื่อห้อง R1/T1, รอรอบ R4, ล้ม K1, ดู S1, นับกลับ X1, ป้าย DOWN เหนือหัว |
| `NetInterp.cs` | บัฟเฟอร์ interpolate ตำแหน่ง/มุมของ puppet |
| `Menu.cs` | เมนู Single / Host / Join / Connecting (ทั้งสาม → loop; `-arena` Host/Join = v1) |
| `Args.cs` | สวิตช์ command line (`-loop`, `-save`, `-arena`, `-biome meadow|snow`) |
| `MpTest.cs` | ทดสอบออนไลน์ v1 (`-mptest -arena`) + เมนู (`-menutest`); `mptest.ps1` เป็นตัวเปิด |
| `MpLoopTest.cs` | ทดสอบ loop ออนไลน์หลายโปรเซส (`-mptest` ไม่มี `-arena`); `mptest-loop.ps1` เป็นตัวเปิด |
| `SelfTest.cs` | ทดสอบอัตโนมัติ `-selftest <โฟลเดอร์>` (v1 ทั้งหมด แล้วเคส loop ท้ายไฟล์; `-looponly` = เฉพาะ loop) เขียน `SELFTEST PASS/FAIL` แล้วปิดเอง |
| `Assets/MedievalKnight/Resources/Audio/` | เสียง mp3 6 ไฟล์ (Git LFS) โหลดใน `Sfx.cs` |
| `Assets/MedievalKnight/Resources/Mats/`, `Assets/Settings/Renderer3D.asset`, `Assets/Scenes/Main.unity` | material ฐาน 3 ตัว, URP renderer (`BuildScript.Setup3D` สร้าง), ฉากหลัก (มีแค่ Main Camera) |
| `Assets/Editor/BuildScript.cs` | `BuildWindows`, `Setup3D`, `CreateMainScene` |
| `build.bat` | บิลด์ Windows ด้วย Unity batchmode → `Builds\MedievalKnight.exe`, log ที่ `build.log` |
| `Docs/big-map-update/`, `Docs/phase1-minimal-loop/`, `Docs/phase2-old-world/`, `Docs/phase4-coop/` | requirement / design / tech / decisions / status ของแต่ละงานใหญ่ |
| `_reference/` | ไฟล์อ้างอิง 2.5D เดิม (ไม่ได้ใช้ในเกมแล้ว) |
