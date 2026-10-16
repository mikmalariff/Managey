# Panduan Pemasangan Managey

Panduan ni untuk orang IT atau admin yang memasang Managey. Ada tiga cara:

| Cara | Sesuai untuk | Akses |
| --- | --- | --- |
| **A. Windows Server / PC Windows di pejabat** | Syarikat yang mahu data kekal di premis sendiri | Dalam rangkaian pejabat (LAN / Wi-Fi) |
| **B. Cloud (server sewa)** | Staf bekerja di pelbagai lokasi, atau syarikat tiada server | Dari mana-mana, melalui internet (HTTPS) |
| **C. Server pejabat + akses jauh** | Data di pejabat, tapi staf perlu akses dari luar | LAN, dan dari luar melalui VPN atau tunnel |

---

## 1. Dapatkan pakej pemasangan

Pakej ialah fail zip yang mengandungi semua yang diperlukan. Server **tidak perlu** pasang .NET, Node.js atau apa-apa perisian lain.

- **Windows:** `Managey-win-x64.zip`
- **Linux (cloud):** `Managey-linux-x64.zip`

**Cara paling mudah (GitHub):**
1. Buka repo di GitHub, kemudian tab **Actions** → **Package**.
2. Klik run yang terbaru dengan tanda ✓ hijau.
3. Di bahagian **Artifacts**, muat turun **Managey-packages**. Fail ni mengandungi kedua-dua zip.

Untuk versi rasmi, cipta tag (contohnya `v1.0.0`). Zip akan muncul di halaman **Releases**.

**Bina sendiri (developer):** perlukan Node.js 20+ dan .NET 8 SDK.
```powershell
.\scripts\publish.ps1                     # Windows → publish\Managey-win-x64.zip
.\scripts\publish.ps1 -Runtime linux-x64  # Linux   → publish\Managey-linux-x64.zip
```

---

## 2. Cara A — Pasang di Windows Server atau PC Windows

### Keperluan
- Windows Server 2016 atau lebih baru, atau Windows 10/11 (64-bit)
- RAM 2 GB, ruang cakera 1 GB (tambah lagi untuk dokumen yang di-upload)
- Komputer ni mesti **sentiasa hidup** semasa waktu kerja. Untuk PC biasa, matikan mod *Sleep*.
- Disyorkan: **IP tetap** untuk server. Minta orang IT/router tetapkan *DHCP reservation*, supaya alamat tak berubah.

### Langkah pemasangan
1. Salin `Managey-win-x64.zip` ke server, kemudian **klik kanan → Extract All**. Contohnya ke `C:\Users\Admin\Downloads\Managey-win-x64`.
2. Klik butang **Start**, taip `PowerShell`, kemudian **klik kanan → Run as administrator**.
3. Pergi ke folder yang diekstrak dan jalankan skrip pemasangan:
   ```powershell
   cd C:\Users\Admin\Downloads\Managey-win-x64
   powershell -ExecutionPolicy Bypass -File .\install.ps1
   ```
   Pilihan lain:
   ```powershell
   # pasang di folder lain dan guna port lain
   powershell -ExecutionPolicy Bypass -File .\install.ps1 -InstallDir D:\Managey -Port 8080
   ```
4. Skrip akan:
   - salin program ke `C:\Managey`,
   - daftar Managey sebagai **Windows Service** (ia hidup sendiri bila komputer di-restart, dan restart sendiri kalau ia terhenti),
   - buka port 5080 di Windows Firewall,
   - mula sistem dan tunjuk alamat untuk dibuka.

Hasilnya lebih kurang macam ni:
```
Managey installed and running.
Open one of these from any PC on the network:
  http://SERVER-HR:5080
  http://192.168.1.20:5080
First sign-in: admin / ChangeMe123!
```

### Cara akses
| Dari mana | Alamat |
| --- | --- |
| Komputer server itu sendiri | `http://localhost:5080` |
| PC lain dalam pejabat | `http://NAMA-SERVER:5080` atau `http://192.168.x.x:5080` |
| Telefon (sambung Wi-Fi pejabat) | `http://192.168.x.x:5080` |

Guna mana-mana pelayar moden: Chrome, Edge, Firefox atau Safari. Tak perlu pasang apa-apa di PC staf. Elok **bookmark** alamat tu, atau cipta shortcut di desktop staf.

### Log masuk pertama
1. Buka alamat di atas dan log masuk dengan `admin` / `ChangeMe123!`.
2. **Tukar kata laluan dengan segera:** klik **Account** (bawah kiri) → **Change password**.
3. Teruskan dengan persediaan awal dalam [Panduan Pengguna](PANDUAN-PENGGUNA.md#1-persediaan-awal-admin).

### Backup (sangat penting)
Semua data ada dalam folder `C:\Managey\data`, iaitu database, fail yang di-upload dan kunci encryption. Fail `appsettings.json` pula simpan tetapan.

Backup manual:
```powershell
powershell -ExecutionPolicy Bypass -File C:\Managey\backup.ps1 -BackupDir D:\Backups\Managey
```
Untuk backup automatik setiap malam, guna **Task Scheduler**:
1. Buka **Task Scheduler** → **Create Task**.
2. Tab **General**: nama `Managey backup`. Tandakan **Run whether user is logged on or not** dan **Run with highest privileges**.
3. Tab **Triggers**: **New** → **Daily**, jam 2:00 pagi.
4. Tab **Actions**: **New** → Program `powershell.exe`, dengan Arguments:
   `-ExecutionPolicy Bypass -File C:\Managey\backup.ps1 -BackupDir D:\Backups\Managey -Keep 30`
5. Simpan. Lebih baik kalau `D:\Backups` ialah cakera lain atau folder rangkaian/NAS.

Skrip ni hentikan sistem selama beberapa saat semasa menyalin, supaya database disalin dalam keadaan konsisten. Ia juga simpan 30 backup terakhir sahaja.

**Restore:** hentikan service (`Stop-Service Managey`), ekstrak zip backup ke `C:\Managey` (ganti folder `data`), kemudian `Start-Service Managey`.

### Kemas kini ke versi baru
Muat turun pakej baru, ekstrak, dan jalankan `install.ps1` dari folder baru tu (sama macam pemasangan). Folder `data` dan `appsettings.json` **tidak disentuh**, dan struktur database dinaik taraf secara automatik. Ambil backup dulu sebelum update.

### Tetapan lanjutan (`C:\Managey\appsettings.json`)
Selepas ubah fail ni, restart service: `Restart-Service Managey`.

- **Port:** `"Urls": "http://0.0.0.0:5080"`. Atau pasang semula dengan `-Port`.
- **Guna SQL Server dan bukan SQLite.** Untuk syarikat besar, atau kalau dah ada SQL Server. Tetapkan ni **sebelum** penggunaan pertama:
  ```json
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=.\\SQLEXPRESS;Database=Managey;Trusted_Connection=True;TrustServerCertificate=True"
  }
  ```
  Akaun service (*Local System*) mesti ada akses ke database tu.
- **HTTPS dalam LAN** (pilihan, perlukan sijil `.pfx` daripada IT anda):
  ```json
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:5443",
        "Certificate": { "Path": "C:\\Managey\\sijil.pfx", "Password": "kata-laluan-sijil" }
      }
    }
  }
  ```
  Buka port 5443 di firewall, kemudian akses melalui `https://NAMA-SERVER:5443`.

### Terlupa kata laluan admin
Buka PowerShell sebagai administrator dan jalankan:
```powershell
C:\Managey\Managey.Api.exe --reset-password admin KataLaluanBaru123
```

### Masalah biasa
| Masalah | Penyelesaian |
| --- | --- |
| PC lain tak boleh buka alamat | Cuba `http://IP-SERVER:5080` dan bukan nama server. Semak PC tu dalam rangkaian yang sama. Semak antivirus/firewall pihak ketiga tak menyekat port 5080. |
| Server dapat IP baru selepas restart | Tetapkan IP tetap atau *DHCP reservation* di router. |
| Service tak mula | Lihat **Event Viewer → Windows Logs → Application**. Atau jalankan `C:\Managey\Managey.Api.exe` dalam PowerShell untuk lihat ralat. |
| Port 5080 dah digunakan | Pasang semula dengan `-Port 8080`. |
| Email notifikasi tak sampai | **Setup → Email → Send test email** akan tunjuk ralat sebenar. |

### Nyahpasang
```powershell
powershell -ExecutionPolicy Bypass -File C:\Managey\uninstall.ps1              # data disimpan
powershell -ExecutionPolicy Bypass -File C:\Managey\uninstall.ps1 -RemoveData  # padam semua
```

---

## 3. Cara B — Letak di cloud

**Ya, boleh.** Sistem ni boleh dijalankan di cloud, dan staf akses melalui internet di alamat macam `https://hr.syarikatanda.com`, dari pejabat, rumah atau telefon. Tak perlu VPN.

### Apa yang diperlukan
1. **Server cloud (VM)**. Linux Ubuntu paling murah dan disyorkan.
   - Contoh penyedia: DigitalOcean, AWS Lightsail, Azure, Google Cloud, Vultr, atau penyedia tempatan.
   - Saiz: 1–2 vCPU, RAM 2 GB. Kosnya lebih kurang **USD 6–15 sebulan**.
   - Pilih region yang dekat. AWS dan Azure ada region Malaysia, dan ada juga Singapura.
2. **Nama domain**, contohnya `hr.syarikatanda.com`. Di pengurus DNS, cipta **A record** yang tunjuk ke IP awam server.
3. Buka port **80 dan 443** di firewall cloud (*security group*). Jangan buka port 5080.

### Pemasangan di Linux (Ubuntu)
```bash
# di komputer anda: hantar pakej ke server
scp Managey-linux-x64.zip ubuntu@IP-SERVER:~

# di server
ssh ubuntu@IP-SERVER
sudo apt-get install -y unzip rsync
unzip Managey-linux-x64.zip -d managey && cd managey
sudo ./install.sh hr.syarikatanda.com
```
Skrip ni pasang Managey sebagai service, dan pasang **Caddy** sebagai pintu masuk. Caddy dapatkan dan perbaharui **sijil HTTPS percuma** secara automatik. Selepas 1–2 minit, buka `https://hr.syarikatanda.com`.

- **Data:** `/opt/managey/data`. Untuk backup, guna snapshot cakera daripada penyedia cloud (aktifkan backup harian), atau salin folder tu.
- **Kemas kini:** muat naik pakej baru dan jalankan `sudo ./install.sh hr.syarikatanda.com` sekali lagi.
- **Reset kata laluan:** `sudo -u managey /opt/managey/Managey.Api --reset-password admin KataLaluanBaru123`.

### Windows Server di cloud
Boleh juga: sewa VM Windows (Azure atau AWS) dan ikut **Cara A**. Tapi untuk akses melalui internet, **mesti guna HTTPS**. Pasang IIS sebagai *reverse proxy* dengan sijil, atau guna cara Kestrel HTTPS di atas dengan sijil daripada pembekal. Tetapkan juga `"ReverseProxy": true` kalau di belakang proxy. Kos VM Windows biasanya lebih tinggi kerana lesen.

### Keselamatan bila dibuka ke internet
- **Wajib HTTPS.** Skrip Linux dah uruskan ni.
- Tukar kata laluan admin, dan minta semua staf guna kata laluan yang kuat.
- Sistem dah sekat cubaan login berulang: 10 kali seminit untuk setiap alamat IP.
- Aktifkan backup harian/snapshot di penyedia cloud.
- Kemas kini Managey dan sistem operasi secara berkala (`sudo apt upgrade`).
- Data peribadi dan gaji akan disimpan di server cloud. Pastikan penyedia dan region yang dipilih selari dengan polisi PDPA syarikat.

---

## 4. Cara C — Server pejabat, staf akses dari luar

Kalau data mesti kekal di pejabat tapi staf perlukan akses dari rumah atau semasa luar kawasan:

| Pilihan | Penerangan |
| --- | --- |
| **VPN pejabat** (paling selamat) | Staf sambung VPN syarikat dulu, kemudian buka alamat LAN seperti biasa. |
| **Tailscale** (mudah) | Pasang Tailscale di server dan di peranti staf. Mereka akses dengan alamat Tailscale server. Percuma untuk pasukan kecil. |
| **Cloudflare Tunnel** | Dapat alamat `https://hr.syarikatanda.com` tanpa buka port di router. Cloudflare uruskan HTTPS. Tetapkan `"ReverseProxy": true`. |

**Jangan** buka port 5080 terus di router ke internet tanpa HTTPS. Kata laluan akan dihantar tanpa encryption.
