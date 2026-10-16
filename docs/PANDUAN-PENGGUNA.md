# Panduan Pengguna Managey

Managey ialah sistem HR syarikat: kehadiran, cuti, permohonan dan kelulusan, dokumen, dan gaji. Ia dibuka melalui pelayar web, sama ada di komputer atau telefon.

## Siapa nampak apa

| Peranan | Boleh buat |
| --- | --- |
| **Staff** | Clock in/out, mohon cuti, claim dan permohonan lain, lihat baki cuti, slip gaji, direktori staf dan dokumen syarikat |
| **Manager** | Semua di atas + luluskan permohonan staf bawahan + lihat kehadiran pasukan |
| **HR** | Semua di atas + urus staf, cuti, cuti umum, semua permohonan, laporan kehadiran, payroll |
| **Admin** | Semua + aliran kelulusan, email, tetapan syarikat, audit log |

Menu di sebelah kiri hanya tunjuk apa yang dibenarkan untuk peranan anda.

---

## 1. Persediaan awal (Admin)

Buat sekali sahaja selepas sistem dipasang, mengikut susunan ni.

1. **Tukar kata laluan admin:** **Account → Change password**.
2. **Setup → Company:** nama syarikat, no. pendaftaran, alamat, **mata wang**, **hari bekerja** (contohnya Isnin–Jumaat, atau Ahad–Khamis), serta waktu mula dan tamat kerja. Clock-in selepas waktu mula ditanda *lewat*.
3. **Setup → Departments:** tambah jabatan, contohnya Kewangan, Jualan dan Operasi.
4. **Setup → Leave types:** semak jenis cuti dan bilangan hari setahun. Tandakan *Unpaid* untuk cuti tanpa gaji (akan ditolak dari gaji), dan *Supporting document required* untuk cuti yang perlukan dokumen sokongan seperti cuti sakit.
5. **Setup → Public holidays:** masukkan cuti umum tahun ni. Hari ni tak dikira sebagai hari cuti.
6. **Setup → Payroll:** klik **Apply** pada preset negara anda (**Malaysia**, **Singapore** atau **Indonesia**), atau cipta skim caruman sendiri. Lihat [bahagian 5](#5-payroll-hr).
7. **Setup → Approval flows:** tentukan siapa luluskan setiap jenis permohonan. Contoh default untuk cuti: *Direct manager → HR*.
8. **Setup → Email** (pilihan): masukkan tetapan pelayan email syarikat dan tekan **Send test email**. Selepas tu, approver dapat email bila ada permohonan baru, dan pemohon dapat email bila ia diluluskan atau ditolak.
9. **Masukkan staf:**
   - Sekaligus: **People → Import from Excel → Download the Excel template**. Isi templat, upload, semak preview, kemudian tekan **Import**. **Muat turun senarai kata laluan sementara** yang dipaparkan selepas import, sebab ia tak akan ditunjuk lagi.
   - Seorang demi seorang: **People → Add employee**.
   - Pastikan setiap staf ada **Reports to** (manager mereka), sebab manager ni yang luluskan permohonan mereka dulu.
10. Beri setiap staf alamat sistem, username dan kata laluan sementara mereka. Minta mereka tukar kata laluan selepas log masuk pertama.

---

## 2. Staf: kegunaan harian

### Kehadiran
**Attendance → Clock in** bila mula kerja, dan **Clock out** bila balik. Nota boleh ditambah, contohnya "kerja di site klien". Rekod 30 hari terakhir dipaparkan di bawah.

### Mohon cuti
1. Pergi ke **Leave → Apply leave**, atau **Apply leave** di Overview.
2. Pilih jenis cuti dan tarikh. Untuk satu hari, boleh pilih *Half day*. Sistem tunjuk bilangan hari bekerja, tidak termasuk hujung minggu dan cuti umum, serta baki cuti anda.
3. Lampirkan dokumen kalau perlu, contohnya MC, kemudian tekan **Submit request**.
4. Status boleh dilihat di **My requests**. Selagi masih *Pending*, permohonan boleh dibatalkan.

### Claim, pembelian dan permohonan lain
**My requests → New request** → pilih **Claim**, **Purchase** atau **General**, isi tajuk dan amaun, lampirkan resit, dan tekan **Submit**. Claim yang diluluskan akan **dibayar automatik dalam gaji bulan tu**.

### Slip gaji
**Payslips** → pilih bulan → **Print / Save as PDF**. Slip hanya muncul selepas HR *finalise* payroll bulan tu.

### Lain-lain
- **People:** direktori staf.
- **Documents:** polisi, borang dan buku panduan syarikat.
- **Account:** tukar kata laluan.

---

## 3. Manager

- **Approvals:** senarai permohonan yang menunggu kelulusan anda. Nombor merah di menu menunjukkan bilangannya. Buka satu permohonan, kemudian tekan **Approve** atau **Reject** (sebab wajib diisi bila menolak).
- **Team attendance:** rekod clock-in staf bawahan anda.
- Halaman **Overview** tunjuk siapa yang bercuti hari ni.

---

## 4. HR: tugas harian dan bulanan

- **All requests:** semua permohonan syarikat, boleh ditapis ikut jenis dan status.
- **Attendance report:** kehadiran semua staf ikut tarikh. **Export CSV** untuk buka di Excel.
- **People → (staf):** profil, peranan, kelayakan cuti tahun ni (boleh berbeza setiap orang), dan **Pay details**.
- **Documents → Upload:** kongsi polisi atau borang dengan semua staf.

---

## 5. Payroll (HR)

### Persediaan (sekali sahaja)
**Setup → Payroll** mengandungi **skim caruman**, iaitu potongan berkanun dan potongan syarikat.

- **Preset negara:** satu klik untuk masukkan semua skim. Lepas tu skim boleh diubah.
  - **Malaysia:** EPF (biasa, 60+, pekerja asing), SOCSO kategori 1 & 2, EIS, LINDUNG 24 Jam (SKBBK), PCB.
  - **Singapore:** CPF ikut kumpulan umur, SDL, CDAC/ECF/MBMF/SINDA.
  - **Indonesia:** BPJS Ketenagakerjaan (JHT, JP, JKK, JKM), BPJS Kesehatan, PPh 21.
- **Skim sendiri** (**Add scheme**) untuk negara lain atau potongan syarikat. Pilih cara kira:

| Cara | Contoh |
| --- | --- |
| Percentage of wages | Pencen 5% pekerja / 7.5% majikan, had gaji 5,000 |
| Percentage by wage band | Kadar berbeza ikut jumlah gaji |
| Fixed amount by wage band | Jadual caruman rasmi. Boleh *paste* terus dari Excel. |
| Fixed amount | Yuran kesatuan 10 sebulan |
| Amount per employee | Cukai bulanan (PCB, PPh 21, PAYE) yang berbeza setiap orang |

### Butiran gaji setiap staf
**People → (staf) → Pay details:**
- **Basic salary.** Biarkan kosong kalau staf tu tiada dalam payroll.
- No. ID / pasport, no. cukai, bank dan no. akaun.
- **Contributions:** tandakan skim yang staf tu sertai, masukkan **no. ahli**, dan untuk skim *Amount per employee* seperti cukai, masukkan amaun bulanan.
  Contoh Malaysia: staf berumur 60+ guna *EPF aged 60+* dan *SOCSO second category*, bukan skim biasa.
  Contoh Singapura: pilih skim CPF ikut umur staf, dan dana SHG ikut komuniti mereka.
- **Fixed monthly allowances & deductions:** elaun atau potongan tetap. Tandakan *Contributable* kalau amaun tu dikira dalam gaji untuk caruman.

### Proses setiap bulan
1. **Payroll → Run payroll** → pilih bulan → **Create payslips**. Sistem masukkan secara automatik:
   - gaji pokok, elaun dan potongan tetap,
   - **claim yang diluluskan**, dibayar sekali sahaja,
   - **potongan cuti tanpa gaji**, dikira gaji ÷ hari sebulan × hari cuti (hari sebulan ditetapkan di Setup → Payroll),
   - semua caruman mengikut skim.
2. Semak jadual. Amaran merah muncul kalau ada staf tanpa ID, no. ahli atau akaun bank.
3. Untuk tambah OT, bonus atau potongan sekali sahaja, klik **Edit** pada staf tu. Caruman dikira semula bila disimpan. Tandakan *Enter contributions manually* untuk taip amaun sendiri.
4. Ubah gaji atau skim staf? Tekan **Rebuild** untuk jana semula semua slip.
5. Tekan **Finalise** bila semua betul. Bulan tu akan dikunci, dan staf boleh lihat slip gaji mereka.
6. Muat turun fail yang diperlukan:
   - **Payroll summary (Excel):** ringkasan untuk akaun/pengurusan.
   - **Bank payment file (CSV):** untuk bayaran pukal di bank.
   - **CSV setiap skim:** senarai caruman untuk dihantar ke agensi.
   - **Malaysia:** **KWSP i-Akaun file**, untuk upload sebagai bulan caruman yang berikutnya, dan **PERKESO ASSIST file**. Fail PERKESO perlukan *Employer reference* (kod majikan PERKESO) pada skim SOCSO.

**Tips:** semak jumlah di portal agensi (KWSP, PERKESO, CPF, BPJS) sebelum bayar, terutama pada kali pertama. Kadar caruman berubah dari semasa ke semasa (contohnya had gaji JP Indonesia dikemas kini setiap Mac), jadi kemas kini skim di **Setup → Payroll** bila ada pengumuman baru.

---

## 6. Admin

- **Setup → Approval flows:** sehingga 5 peringkat kelulusan untuk setiap jenis permohonan. Pilihannya *Direct manager*, *HR*, *Admin* atau *orang tertentu*. Admin boleh luluskan apa-apa peringkat.
- **Audit log:** rekod siapa buat apa dan bila, termasuk log masuk, perubahan gaji, kelulusan dan export.
- **People → (staf) → Role / Active:** tukar peranan atau nyahaktif staf yang berhenti. Staf yang tak aktif tak boleh log masuk dan tak dimasukkan dalam payroll, tapi rekod lama mereka kekal.
