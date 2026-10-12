# Phase 1 Tech — state, แยกโหมด, portal, ปุ่ม, net

## 1. State (`RunLoop.State`)
`Menu`(= !InSession) → `Base` ⇄ `Prepare` → `World` → `Summary` → `Base`
| ย้ายจาก → ไป | เมื่อ | ทำอะไร |
|---|---|---|
| Menu → Base | Single Player จากเมนู | `LoopSave.Load`; knight ที่ `BasePoint`; ไม่มี actor; ถ้า `inRun` → ABANDONED → Summary; ไม่งั้นเปิด Prepare + `AutoPack` |
| Base ⇄ Prepare | Tab / Esc (ปิด) | Prepare = overlay; timeScale 0 (single เท่านั้น) |
| Prepare → World | ENTER WORLD (carry มี Weapon, ถ้าไม่มี Food ถามยืนยัน) | `inRun=true`+เซฟ → fade 0.5s → `ClearWorld`, `knight.ResetForRun(TownSpawn)`, `inventory.SetSingle(held)`, `SpawnActors()`, objective |
| World → Summary | ตาย (หลัง dim 1.5s) / เข้า portal ค้าง 1.5s | `Survive()`/`Die()` + เซฟ + `RunResult` |
| Summary → Base | RETURN TO BASE | `ClearWorld`, ลบ portal, `ResetForRun(BasePoint)` (Dead=false), เซฟ, `AutoPack`, เปิด Prepare |
Esc ในโลก: ครั้งแรกแสดง `Leaving now counts as a death. Press Esc again.` (ใช้ `LeaveKey` เดิม เปลี่ยนข้อความเมื่อ Loop) ครั้งที่สองออกเมนูโดย **ไม่แตะเซฟ** (flag `inRun` ค้าง → รอบถัดไปเป็น ABANDONED)

## 2. แยกโหมด: `Game.Loop = InSession && run != null && Net.Mode == Single`
| เรื่อง | Single (loop) | Single v1 (`-single`/`-selftest`) | Host / Join |
|---|---|---|---|
| เริ่ม | Menu Single Player → `StartSession(true)` | `StartSession()` | `StartHost`/`Join` → `StartSession()` |
| `Net.Authority` | true (Single) — logic loop ทั้งหมดอยู่หลัง `Loop` ซึ่งบังคับ Single จึงไม่รันบน client | true | Host true / Client false |
| actor / pack | เฉพาะ state World | เกิดตอนเริ่ม | เกิดตอนเริ่ม |
| ตาย | จบรอบ ไม่ revive | revive 3s เลเวล 1 | revive 5s |
| Minotaur | ตายแล้วไม่เกิดใหม่ + portal | respawn 20s | respawn 20s |
| pickup ดาบ/อาหาร | เข้าช่อง Carry / Pack full; อาหารไม่กินทันที | `inventory.Add` / กินทันที (ข้ามคน HP เต็ม) | เหมือน v1 + `Msg.PickupTaken` |
| F5 | ปิด (`RequestReset` ปฏิเสธ) | ใช้ได้ | Host ใช้ได้ (mptest ช่วง 9) |
| Tab | Hud โหมด loop / Prepare ที่ฐาน | Tab เดิม | Tab เดิม (overlay ไม่หยุดเวลา) |
| เซฟ | `LoopSave` | ไม่มี | ไม่มี |
| Soul | ซ่อน UI | แสดง | แสดง |
ห้ามหยุดเวลาตอนออนไลน์: `Paused` เดิม (`PanelOpen && !Net.Multiplayer`) ไม่เปลี่ยน; Prepare/Summary ใช้เฉพาะ Loop (ไม่ออนไลน์อยู่แล้ว)
กฎฉาก: ไม่มีการสร้างฉากเพิ่ม (Portal ถูกสร้างตอนรัน ไม่มี obstacle ไม่ใช้ `UnityEngine.Random`); ฐานใช้ลานกลาง (|x|<28, Z -42..32) โดยไม่วางของใดๆ

## 3. บอสโลก + Portal
- ตรวจ: `Enemy.TakeHit` → `Game.OnEnemyKilled(e)`; ถ้า `Loop && e is Minotaur` → `run.OnBossKilled(e.transform.position)` (เรียกก่อน `RollDrop` ได้ ลำดับไม่สำคัญ ดรอปเดิม: Horned King's Blade + Meat x2 ตกที่ตำแหน่งตาย)
- ตั้ง `minoRespawnAt = float.MaxValue` และ `SimulateWorld` ไม่ respawn เมื่อ Loop; `bossDown=true`
- สร้าง `Portal` ที่ตำแหน่งตาย; แถบกลางจอ 4 วิ `THE MINOTAUR HAS FALLEN. Portal open. Collect your loot, then step in.`; objective `Step into the Portal`; ลูกศร Hud ชี้ portal
- `RunLoop.Tick()`: `bossDown && knight alive && flat dist ≤ 2.0 m` → สะสม `unscaledDeltaTime`; ≥1.5s → VICTORY; ออกนอกรัศมี → รีเซ็ต + วงโหลดใน `LoopUi`
- ตายหลังบอสล้มแต่ก่อนเข้า portal = YOU FELL (รอดเมื่อเข้า portal เท่านั้น)
- ไม่มีทางกลับอื่น: `Prepare`/Esc ไม่ใช่ทางออก

## 4. ปุ่ม (`InputX.Map()`: เพิ่มเฉพาะ `C`)
| ปุ่ม | บริบท | ผล |
|---|---|---|
| Tab | Base | เปิด/ปิด Prepare |
| Tab | World | Tab window (Hud โหมด loop) |
| 1 / 2 | World ไม่มีหน้าต่าง/ไม่กำลังฟัน | เปลี่ยน `heldSlot` → `inventory.SetSingle` + `RebuildSword` (การ์ดเลเวลอัปยังใช้ 1-3 ตอน `LevelStep!=0`) |
| C | World | กิน foods[0] ถ้า HP เต็ม `HP full` ไม่กิน |
| คลิกช่อง Food (Tab) | World | กินชิ้นนั้น |
| คลิกขวาช่องใน Tab | World | Drop (เฉพาะ Loop) |
| ลูกศร/WASD, Enter/Space, Q/E | Prepare, Summary | ตาม design §5 (เมาส์คลิกได้ทุกปุ่ม) |
ข้อความ UI อังกฤษตาม design §3/§5/§7 ใส่เป็นค่าคงที่ใน `LoopUi`/`RunLoop` (`Pack full! Can't pick up <name>`, `Carry is full`, `Pack a Weapon first`, `Enter without food? [Enter] Go [Esc] Back`, `Iron Sword restored`, `LEFT BEHIND (stash full)`, ABANDONED)

## 5. เครือข่าย
ไม่มีข้อความใหม่ ไม่แก้รูปแบบ `Msg.*` ไม่ส่ง Loadout/portal ผ่านเน็ต → **`Net.Protocol` คง 2**; Client/Host ของ v1 ไม่รู้จัก loop; `Pickup.plus/dropped` เป็นข้อมูลเครื่องเดียว (snapshot เดิมไม่ส่ง)
