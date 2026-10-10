# Online co-op (สูงสุด 4 คน)

> แยกมาจาก CLAUDE.md (รายละเอียดเต็ม) — ภาพรวมสั้นอยู่ที่ CLAUDE.md

เกมเริ่มที่เมนู (`Menu.cs`): ช่องชื่อผู้เล่น, **Single Player**, **Host Game** (ช่อง port, แสดง IPv4 ของเครื่อง, คำอธิบาย LAN / port forward / virtual LAN / firewall), **Join Game** (ช่อง IP + port, Ctrl+V วางได้), Quit. ค่าที่พิมพ์จำไว้ใน PlayerPrefs. ไม่มี EventSystem: ปุ่ม/ช่องพิมพ์ hit-test เองผ่าน `InputX`
ไม่มี package / library เพิ่ม: `Net.cs` เขียนบน `System.Net.Sockets` (TCP, `NoDelay`), port เริ่มต้น **7777** (`Net.DefaultPort`). นอก LAN ต้อง forward TCP port หรือใช้ Radmin VPN / ZeroTier

### วิธีเล่น
- Host: Host Game > ตั้ง port > Start Hosting (Windows อาจถาม firewall ครั้งแรก ต้องกด allow) > บอก IP ที่หน้าจอแสดง + port ให้เพื่อน. มุมซ้ายของ HUD แสดง `HOSTING ip : port  players n / 4`
- Client: Join Game > พิมพ์ IP + port > Connect. ต่อไม่ได้ / ห้องเต็ม / เวอร์ชันไม่ตรง จะกลับมาหน้า Join พร้อมข้อความ. Host ปิดห้อง = client กลับเมนูพร้อมข้อความ "The host closed the game."
- **ทุกคนต้องใช้เวอร์ชันเดียวกัน** (`Net.Protocol` = 2 หลังอัปเดตแมพใหญ่; เวอร์ชันเก่า = "version" ถูกปฏิเสธ)

### สถาปัตยกรรม (host-authoritative)
- `Net.Mode` = Single / Host / Client; `Net.Authority` = ไม่ใช่ Client. Host จำลองโลกทั้งหมด (ศัตรู, แพ็ค, ดาเมจ, ดรอป, EXP, Soul, pickup); client จำลองเฉพาะอัศวินของตัวเอง; ฉากและ collision สร้างเองทุกเครื่องจาก seed 11 (ต้องตรงกัน)
- `Conn` = 1 TCP ต่อ client มี thread อ่าน + thread เขียนของตัวเอง main thread ไม่ block; ทุกอย่างเข้าคิวแล้วประมวลผลใน `Net.Poll()` (ต้น `Game.Update`) และส่งใน `Net.LateTick()` (`Game.LateUpdate`)
- เฟรมข้อความ: `[len u16][type u8][payload]` little endian (`NetW` / `NetR`); ตำแหน่งเป็น cm (i16 = ±327 ม. พอสำหรับแมพ ±190 ม.), มุมเป็น u16, HP เป็น u16 ทศนิยม 1 ตำแหน่ง
- Client ส่ง `State` ของอัศวินตัวเอง 20 ครั้ง/วินาที; host ส่ง `Snapshot` 15 ครั้ง/วินาที (ตัวนับแพ็ค, อัศวินทุกคน, ศัตรูทุกตัว, pickup ทุกอัน — เลวร้ายสุด 48 สไลม์ + 60 pickup ≈ 2 KB ต่อ snapshot ≈ 30 KB/s ต่อ client). อัศวินคนอื่นและศัตรูบน client เป็น puppet วาดย้อนหลัง 0.13 / 0.1 วินาทีด้วย `NetInterp` (interpolate)
- `Game.players` = อัศวินทุกคน (`Knight.pid`, `playerName`, `remote`); `Game.knight` = ของเครื่องนี้. สี + ป้ายชื่อมาจาก `Knight.MainColors` / `AccentColors` ตาม pid. HUD มี party list (ชื่อ เลเวล HP), ข้อความเข้า/ออก และบรรทัดสถานะ (`Net.StatusLine`, ping)

### ข้อความ (`enum Msg`, protocol = `Net.Protocol` 2)
| ทิศทาง | ข้อความ |
| --- | --- |
| client > host | `Hello` (magic, version, ชื่อ), `State`, `Act` (เริ่มท่าดาบ 0-4 / สกิล 10-13 / **dash 20**), `Hit` (ดาบฉันโดนศัตรู id นี้ ดาเมจเท่านี้), `Ping`, `Bye` |
| host > client | `Welcome` (pid + รายชื่อ), `Refuse` (เหตุผล), `Join`, `Leave`, `Snapshot` (pickup มี `kind`), `Act` (relay พร้อม pid), `EnemyHit` (ตัวเลข/ประกาย/HP ที่เหลือ/ตาย), `KnightHit` (มอนสเตอร์ตีอัศวินของคุณ), `Exp` (EXP + Soul), `PickupTaken` (id, pid, kind, item), `Banner`, `Reset` (F5), `Pong`, `Bye` |
| ทั้งสองทาง | `Test` (ใช้โดย `MpTest.cs` เท่านั้น) |

เปลี่ยนรูปแบบข้อความเมื่อไหร่ต้องเพิ่ม `Net.Protocol` (คนละเวอร์ชันจะถูก `Refuse`). การรีเซ็ตตอนตายไม่ส่งข้อความ: client รีเซ็ตเลเวลเอง เพื่อนเห็น Lv 1 ผ่าน `State`

### Authority / trust model
- Host เป็นคนตัดสิน HP ศัตรู, การตาย, EXP, Soul, ดรอป, ใครเก็บของ, การเกิดของสไลม์ / บอส และ F5
- Client ถูกเชื่อในเรื่องของอัศวินตัวเอง: ตำแหน่ง, HP/เกราะ (รวม i-frame ของ dash และการกินอาหาร), เลเวล, ดาบที่ถือ, การบล็อก (`KnightHit` ให้ client คิดบล็อก/เกราะเอง) และดาเมจที่รายงานใน `Hit`. Host ตรวจแบบหลวมๆ เท่านั้น (`Game.ClientHit`: ศัตรูยังไม่ตาย, ดาเมจ 0..20000, ระยะจากอัศวิน/ศัตรูสมเหตุผล, ไม่เกิน 150 hit/วินาที, ข้อความพังหรือเงียบ 10 วินาที = ตัดการเชื่อมต่อ) — ออกแบบสำหรับเล่นกับเพื่อน ไม่กันโกง ไม่เข้ารหัส ไม่มีรหัสผ่านห้อง

### กฎ multiplayer
- EXP / Soul: ผู้เล่นทุกคนที่ยังไม่ตายได้เต็มจำนวนต่อการฆ่า 1 ครั้ง; จำนวน kill นับให้คนที่ปิดฉากเท่านั้น
- ศัตรูเล็งผู้เล่นที่ยังไม่ตายที่ใกล้ที่สุด (`Game.NearestKnight`); แพ็คสไลม์เกิดรอบผู้เล่นแต่ละคน (คนแยกกันไกล = ต่างคนต่างมีแพ็ค)
- HP + เกราะศัตรู x(1 + 0.6 ต่อผู้เล่นเพิ่ม 1 คน) (`Game.CoopHpPerPlayer`) และใช้เลเวลเฉลี่ยของปาร์ตี้ — คิด **ตอนศัตรูเกิด**
- ดาบจากสไลม์: ใครเดินทับก่อนได้. ดาบบอส: Minotaur ดรอปคนละ 1 เล่ม แต่ละคนเห็นและเก็บได้เฉพาะของตัวเอง. อาหาร: ใครเดินทับก่อนที่ HP ไม่เต็มได้
- เลเวล / EXP / อัปเกรด / แต้มสเตตัส / Soul / inventory / ดาบที่ใส่ เป็นของแต่ละคน (อยู่บนเครื่องของคนนั้น)
- ออนไลน์ไม่มีการหยุดเกม: หน้าต่างเลเวลอัป / Tab เป็น overlay โลกเดินต่อ ยังเดินได้ และได้โล่ป้องกัน 6 วินาทีตอนหน้าต่างเลเวลอัปเปิด (`Game.LevelUpProtect`); hit-stop ปิดตอนออนไลน์
- ตาย: ฟื้นที่เมือง (จุดเกิดเรียงข้างกัน 1.8 ม.) หลัง 5 วินาที (`ReviveDelayCoop`; single player 3 วินาที) เลเวลกลับเป็น 1. ทุกคนล้มพร้อมกัน = สไลม์ทั้งหมดถูกลบ แพ็คเริ่มนับใหม่ Minotaur กลับรังเลือดเต็ม (`Game.OnPartyWipe`)
- F5 เฉพาะ host (รีเซ็ตทุกคนเป็นเลเวล 1 กลับเมือง). ห้องเต็ม 4 คน คนที่ 5 ถูกปฏิเสธ. คนเข้ากลางเกมได้ (เริ่มเลเวล 1 ในเมือง)

### Command line (`Args.cs`)
`-single` ข้ามเมนูเข้า single player, `-host` / `-join <ip>` พร้อม `-port <n>` `-name <ชื่อ>`, `-bind <ip>` (host ฟังเฉพาะ address นั้น; เทสต์ใช้ 127.0.0.1 จึงไม่เด้ง firewall), `-netversion <n>` (ปลอมเวอร์ชันเพื่อทดสอบ), `-joindelay <วินาที>`, `-selftest <โฟลเดอร์>`, `-mptest <โฟลเดอร์> -role H|A|B|C -expect <จำนวน client> [-refusals] [-expecttext <ข้อความ>]`, `-menutest <โฟลเดอร์>`

### ทดสอบหลายโปรเซส (เฉพาะ 127.0.0.1)
- `powershell -ExecutionPolicy Bypass -File mptest.ps1` เปิด host 1 + client 3 (Alice / Bob / Cara) + 3 ตัวที่ต้องเข้าไม่ได้ (ห้องเต็ม, เวอร์ชันไม่ตรง, port ที่ไม่มีใคร host) หน้าต่าง 960x540 ใช้เวลาราว 2.5 นาที แล้วสรุป PASS / FAIL ต่อ role; log + ภาพอยู่ที่ `mptest\<role>\` (host เป็นคนสั่งผ่าน `Msg.Test`, เช็คส่วนใหญ่เทียบ summary ของทุกโปรเซส)
- ทุกคนเริ่มในเมือง (ปลอดภัย ไม่มีสไลม์): สคริปต์ทดสอบเดินทุกคนไปลานกลางก่อนแล้วรอแพ็ค จากนั้นปิดผู้กำกับด้วย `packsOff` และใช้ `EnsureSlimes` เมื่อต้องมีสไลม์; มีเช็คตำแหน่ง 4 มุมแมพ (±5 ซม. ทุกจอ)
- **รู้อยู่แล้ว**: เช็ค "A's walk looks smooth on B" (การเดินของ client A เนียนบนจอ client B) ตกบนเครื่องนี้ตั้งแต่ commit `4b146ba` (ก่อนอัปเดตแมพใหญ่) ตอนรัน 7 หน้าต่างพร้อมกัน — ยังไม่ได้ตามหาสาเหตุ (buffer margin ติดลบ ~0.4 วิบน B)
- เมนู: `Builds\MedievalKnight.exe -menutest <โฟลเดอร์เต็ม> -port 47821 -bind 127.0.0.1 -screen-fullscreen 0 -logFile <log>` ดูบรรทัด `MENUTEST`
- ยังไม่ครอบคลุม: เครือข่ายจริง (LAN / internet / latency / packet loss), ทุกคนล้มพร้อมกัน (`OnPartyWipe`), การสเกล HP ตามจำนวนคน, คนเข้ากลางเกมหลังมีการต่อสู้ไปแล้ว, firewall prompt ตอน host แบบปกติ (bind ทุก address), ประสิทธิภาพบนเครื่องอ่อน / จอ 4K

