# โครงสร้างไฟล์

> แยกมาจาก CLAUDE.md (รายละเอียดเต็ม) — ภาพรวมสั้นอยู่ที่ CLAUDE.md. ส่วน loop (ฐาน / โลก / เซฟ) ดู `loop.md`

| ที่อยู่ | หน้าที่ |
| --- | --- |
| `Assets/MedievalKnight/Scripts/Game.cs` | จุดเริ่มเกม (`Boot`), กล้อง top-down, แสง, สร้างฉาก (`WorldBuilder`), แพ็คสไลม์ (`UpdatePacks`), EXP + Soul (`OnEnemyKilled`), เลเวลอัป 2 ขั้น (`LevelStep`), ดรอป (`RollDrop`), เก็บของ (`UpdatePickups`), Tab (`SetInventory`), ตาย / ฟื้น, shake, hit-stop, ปุ่ม F1 / F5 / H / Tab / M / Esc. **Loop**: `StartSession(loop)`, `run`, `Loop` / **`Coop`**, `OnLoopDeath`; phase 2: `worldHpMul` / `worldDmgMul` / `worldPackMul`; **phase 4**: `WantsSnapshot` / `SendsState` / `PidFree` / `WelcomeList` (ที่ Net ถาม), join/left ของห้อง, เก็บของของเพื่อน, กล้อง spectate, `TestDownSeconds` |
| `Assets/MedievalKnight/Scripts/World.cs` | `MapLayout` (โซน, จุดเกิด / รัง / ประตู / `BasePoint`, `Walkable`, `SpawnTier`), `Obstacles` (collision บน grid 8 ม.: `Push`), `StaticBatcher`, `WorldBuilder` (ฉากทั้งหมด — `System.Random(11)` ล้วน) |
| `Assets/MedievalKnight/Scripts/Knight.cs` | ผู้เล่น: เดิน หัน วิ่ง dash โล่, HP / Armor / Stamina, `Knight.Attacks` (คอมโบ 5 ท่า), `Knight.Skills` (Q E R F), `progress` / `inventory` / `souls`, ตัวเลขต่อสู้รวมดาบ (`DamageMult` / `CritChance` / `CritMult` / `SwingSpeed` / `Reach`), `RollDamage`, `Equip`, `SetWeapon` (loop), `SpendPoint`, `Heal`, `ResetProgress` (ตาย) / `ResetAll` (F5) / `ResetForRun` (loop), `CancelAction`; **phase 4**: `Down` / `Fallen` / `DownLeft`, `Revive()`, `RevTarget`, `NetFallen`, DownRoutine แบบทีม |
| `Assets/MedievalKnight/Scripts/Progress.cs` | เลเวล / EXP (`Need(lvl)`), ผลรวมอัปเกรด, สุ่มการ์ด 3 ใบ (`MakeOffers`), แต้มสเตตัส HP / BLOCK / STAMINA (`Spend`, `AttrCap`) — instance ของอัศวินแต่ละคน |
| `Assets/MedievalKnight/Scripts/Weapon.cs` | `WeaponDef` + `Weapons.All`, `Weapons.BuildModel` (จาก `Prims` หรือไฟล์ `modelPath`), `Inventory` (`SetSingle` = ภาพฉายของ HELD ใน loop), `Foods`, `Pickup` (`kind`, หมุน + วงแหวน, ดาบ 90 วิ อาหาร 60 วิ, `plus` / `dropped` ของ loop) |
| `Assets/MedievalKnight/Scripts/Loadout.cs` | **Loop**: `Item`, `ItemBag`, `Loadout` (stash / carry / HELD / `worlds` โลกเก่า / `runWorld` / `unseen`), `RunResult`; `Die` → **`Fall(drops)`** (ของเข้ากอง) / `Survive(decay)` / `LoseCarry` / `TakeFromPile` / `TakeAll` / `Sanitize` / `runCoop` — logic ล้วน |
| `Assets/MedievalKnight/Scripts/OldWorld.cs` | **Loop**: `Pile`, `OldWorld` (seed, เวลา, left, **`piles`**, `AddDrop`, `MergeRadius` 3, ค่าคงที่ `StartLeft` 3, `HpMul` 1.5, `DmgMul` 1.25, `PackMul` 1.5, `MaxPile` 48/โลก, `MaxWorlds` 99), `Drop`, `TeamLine`, `CrumbNote` / `CrumbLine` / `WorldNote` — ข้อมูลล้วน |
| `Assets/MedievalKnight/Scripts/LoopSave.cs` | **Loop**: อ่าน/เขียนเซฟ JSON (atomic, `.bak`, `.corrupt`, validate); DTO `ow[]` (`pv:2` + `piles[]`; ไม่มี `pv` = กองเดียว) / `runWorld` / `rpos` / `unseen[]` / `rc` (Version ยัง 1) |
| `Assets/MedievalKnight/Scripts/RunLoop.cs` | **Loop**: state ฐาน / PREPARE / โลก / สรุป, เข้าโลก (ใหม่ / เก่า), เก็บ / กิน / ทิ้ง, ตาย, บอส + portal; ตัวคูณโลกเก่า, `PilePoint`, หลายกอง (`PileView`), `UiTake` / `UiTakeAll`, autosave; **ออนไลน์**: `team`, READY, `OnTeamEnter`, `HostVictory` / `HostWipe` / `HostAbandon`, `OnGrant`, `OnTeamRunEnd` |
| `Assets/MedievalKnight/Scripts/LoopUi.cs` | **Loop**: PREPARE (NEW WORLD / OLD WORLDS), หน้ารายการโลกเก่า, หน้าต่าง Carry, หน้าต่างเก็บของจากกอง, หน้าสรุป (G3 ตารางทีม), fade, objective, ป้ายโลกเก่า, ป้าย/ลูกศรกอง, READY (canvas ของตัวเอง) |
| `Assets/MedievalKnight/Scripts/Portal.cs` | **Loop**: portal โลก (วงแหวน + ลำแสง + วงโหลด) |
| `Assets/MedievalKnight/Scripts/LootPile.cs` | **Loop phase 2**: กองของโลกเก่า (ถุง + ด้ามอาวุธ + วงทอง + ลำแสงทอง 12 ม.); `Vanish()` ตอนเก็บหมด |
| `Assets/MedievalKnight/Scripts/SwordIcons.cs` | ไอคอนดาบ 128 x 128: ถ่ายโมเดลด้วยกล้อง ortho (layer 31) ตอนเริ่ม; แผนสำรองวาดพิกเซล |
| `Assets/MedievalKnight/Scripts/Sfx.cs` | เสียง: โหลด 6 คลิปจาก `Resources/Audio`, AudioSource 2D 8 ตัว, สุ่ม pitch / volume, ปุ่ม M |
| `Assets/MedievalKnight/Scripts/Enemy.cs` | คลาสแม่ศัตรู: HP + Armor, กระเด็น, สตัน, แถบเลือด, ตัวเลขดาเมจ, `soulReward`, `ClampToArena` |
| `Assets/MedievalKnight/Scripts/Slime.cs` | สไลม์ 3 สี กระโดดไล่ผู้เล่นใน 30 ม. ยกเว้นคนที่ยืนในเมือง |
| `Assets/MedievalKnight/Scripts/Minotaur.cs` | บอส: AI 3 ท่า (Sweep / Smash / Charge) มีเครื่องหมายเตือน, leash (ห่างรัง > 40 ม. / ไม่มีใครใน 45 ม. 5 วิ = กลับรัง) |
| `Assets/MedievalKnight/Scripts/Dummy.cs` | หุ่นฟาง: ไม่โจมตี ไม่ให้ EXP / Soul พังแล้วเกิดใหม่ |
| `Assets/MedievalKnight/Scripts/Fx.cs` | เอฟเฟกต์: อนุภาคลูกบาศก์ + mesh แบนบนพื้น (เส้นฟัน วงคลื่น เส้นพุ่ง เครื่องหมายเตือน) |
| `Assets/MedievalKnight/Scripts/Hud.cs` | UI (uGUI `Text`): แผงซ้ายบน (HP / Armor / Stamina / EXP / Soul), สกิล, แถบบอส, ลูกศรชี้รัง/portal (`SetArrowTarget`), การ์ดเลเวลอัป, Tab (v1), YOU DIED, `Notice`, `Banner`, ปุ่มควบคุม (`SetLoopTexts`) |
| `Assets/MedievalKnight/Scripts/Prims.cs` | mesh low-poly ในโค้ด + material (`Lit` / `Glow`), `Tint` |
| `Assets/MedievalKnight/Scripts/InputX.cs` | ตัวกลางรับปุ่ม / เมาส์; ปุ่มใหม่ต้องเพิ่มใน `Map()` |
| `Assets/MedievalKnight/Scripts/Net.cs` | Online: `Conn` (TCP + thread), `NetW` / `NetR`, `enum Msg`, host / join / handshake, snapshot (ต่อ conn), `Protocol` 3 |
| `Assets/MedievalKnight/Scripts/TeamRun.cs` | **phase 4**: ห้อง (`roster`, `leavers`), READY, mirror ของ carry, Enter, ล้ม/ชุบ/wipe, portal ทีม, Take / Piles / Worlds |
| `Assets/MedievalKnight/Scripts/TeamNet.cs` | **phase 4**: เข้ารหัส/ถอดรหัสข้อความ loop (`Pack` `Drop` `Take` `Revive` `Reached` `Team` `Worlds` `Enter` `Piles` `TakeAck` `Portal` `HomeIn` `RunEnd`) |
| `Assets/MedievalKnight/Scripts/TeamUi.cs` | **phase 4**: รายชื่อห้อง R1/T1, รอรอบ R4, ล้ม K1, ดู S1, นับกลับ X1, ป้าย DOWN เหนือหัว |
| `Assets/MedievalKnight/Scripts/NetInterp.cs` | บัฟเฟอร์ interpolate ตำแหน่ง/มุมของ puppet |
| `Assets/MedievalKnight/Scripts/Menu.cs` | เมนู Single / Host / Join / Connecting (ทั้งสาม → loop; `-arena` Host/Join = v1) |
| `Assets/MedievalKnight/Scripts/Args.cs` | สวิตช์ command line (`-loop`, `-save`, `-arena`) |
| `Assets/MedievalKnight/Scripts/MpTest.cs` | ทดสอบออนไลน์ v1 (`-mptest -arena`) + เมนู (`-menutest`); `mptest.ps1` เป็นตัวเปิด |
| `Assets/MedievalKnight/Scripts/MpLoopTest.cs` | ทดสอบ loop ออนไลน์หลายโปรเซส (`-mptest` ไม่มี `-arena`); `mptest-loop.ps1` เป็นตัวเปิด |
| `Assets/MedievalKnight/Scripts/SelfTest.cs` | ทดสอบอัตโนมัติ `-selftest <โฟลเดอร์>` (v1 ทั้งหมด แล้วเคส loop ท้ายไฟล์; `-looponly` = เฉพาะ loop) เขียน `SELFTEST PASS/FAIL` แล้วปิดเอง |
| `Assets/MedievalKnight/Resources/Audio/` | เสียง mp3 6 ไฟล์ (Git LFS) โหลดใน `Sfx.cs` |
| `Assets/MedievalKnight/Resources/Mats/`, `Assets/Settings/Renderer3D.asset`, `Assets/Scenes/Main.unity` | material ฐาน 3 ตัว, URP renderer (`BuildScript.Setup3D` สร้าง), ฉากหลัก (มีแค่ Main Camera) |
| `Assets/Editor/BuildScript.cs` | `BuildWindows`, `Setup3D`, `CreateMainScene` |
| `build.bat` | บิลด์ Windows ด้วย Unity batchmode → `Builds\MedievalKnight.exe`, log ที่ `build.log` |
| `Docs/big-map-update/`, `Docs/phase1-minimal-loop/`, `Docs/phase2-old-world/`, `Docs/phase4-coop/` | requirement / design / tech / decisions / status ของแต่ละงานใหญ่ |
| `_reference/` | ไฟล์อ้างอิง 2.5D เดิม (ไม่ได้ใช้ในเกมแล้ว) |
