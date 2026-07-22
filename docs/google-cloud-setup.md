# Panduan Google Cloud dan Gmail OAuth EunSlip

Panduan ini ditujukan untuk administrator Google Workspace atau personel IT yang
menyiapkan akun pengirim payroll. EunSlip hanya meminta akses untuk mengirim
email (`gmail.send`) serta identitas dasar akun (`openid` dan `email`).

## Prasyarat

- Akun Google Cloud yang diizinkan membuat project dan OAuth client.
- Akun Gmail/Google Workspace khusus payroll yang akan dipakai EunSlip.
- Persetujuan administrator Workspace untuk aplikasi internal dan scope Gmail,
  bila kebijakan organisasi mewajibkannya.
- EunSlip sudah dipasang pada komputer accounting yang dipercaya.

## 1. Buat project dan aktifkan Gmail API

1. Buka [Google Cloud Console](https://console.cloud.google.com/) dan buat atau
   pilih project khusus EunSlip.
2. Buka **APIs & Services > Library**.
3. Cari **Gmail API**, pilih hasil resminya, lalu klik **Enable**.

Jangan mengaktifkan API atau scope lain yang tidak digunakan EunSlip.

## 2. Konfigurasikan Google Auth Platform

1. Buka **Google Auth Platform > Branding**.
2. Isi nama aplikasi `EunSlip`, support email, dan developer contact yang aktif.
3. Buka **Audience**:
   - pilih **Internal** bila project berada di organisasi Google Workspace yang
     sama dengan akun payroll;
   - gunakan **External** hanya jika kebutuhan organisasi memang melintasi
     domain. Pada mode Testing, tambahkan akun payroll sebagai test user.
4. Buka **Data Access** dan pastikan scope yang digunakan adalah:
   - `https://www.googleapis.com/auth/gmail.send`
   - `openid`
   - `email`

Mode External/Testing tidak cocok untuk operasional jangka panjang karena grant
test user dapat kedaluwarsa. Koordinasikan status production dan verifikasi
dengan administrator Google Workspace sebelum go-live.

## 3. Buat OAuth client Desktop app

1. Buka **Google Auth Platform > Clients**.
2. Pilih **Create client**.
3. Pilih application type **Desktop app**.
4. Beri nama yang menunjukkan komputer atau lingkungan, misalnya
   `EunSlip Production Desktop`.
5. Unduh file JSON dan simpan sebagai `client_secret.json` di lokasi sementara
   yang hanya dapat diakses personel berwenang.

Desktop app memakai browser sistem dan loopback redirect lokal. Jangan membuat
Web application client dan jangan memakai metode manual copy/paste authorization
code yang sudah tidak didukung Google.

## 4. Masukkan credential ke EunSlip

Credential tidak ditanam ke source code atau installer. Provisioning dilakukan
setelah instalasi agar satu artefak installer tidak menyebarkan credential ke
semua komputer.

1. Buka `client_secret.json` dengan text editor.
2. Salin seluruh JSON, termasuk objek `installed`.
3. Di EunSlip buka **Settings > OAuth Client Setup**.
4. Tempel JSON, lalu pilih **Save OAuth Client**.
5. Pastikan status OAuth menjadi **Ready**. Field input dikosongkan oleh
   aplikasi setelah penyimpanan.
6. Hapus salinan sementara `client_secret.json` sesuai prosedur keamanan IT.

EunSlip melindungi credential tersimpan menggunakan DPAPI machine scope dan
menyimpannya di database bersama. Jangan commit, mengirim lewat email/chat, atau
memasukkan `client_secret.json` ke repository maupun build artifact.

## 5. Hubungkan akun payroll

1. Pada Settings pilih **Connect Gmail**.
2. Browser default akan terbuka. Login menggunakan akun Gmail payroll yang
   disetujui, periksa nama aplikasi dan scope, lalu berikan consent.
3. Kembali ke EunSlip dan pastikan akun tampil sebagai connected.
4. Di Gmail, pastikan sender display name disetel menjadi
   `PT. EUNSUNG INDONESIA` sesuai kebijakan organisasi.

Token refresh disimpan terenkripsi di `C:\ProgramData\EunSlip\oauth` dan tetap
dipertahankan saat upgrade maupun uninstall. Tombol **Disconnect Gmail**
menghapus token EunSlip pada komputer, tetapi administrator tetap dapat mencabut
grant melalui pengaturan akun Google bila diperlukan.

## 6. Uji sebelum produksi

Gunakan data dummy dan satu alamat penerima internal:

1. pastikan stamp serta template email sudah benar;
2. proses satu recipient dummy sampai email terkirim;
3. periksa sender display name, subject, body, lampiran PDF, dan riwayat EunSlip;
4. jangan memakai data payroll produksi sebelum UAT ini disetujui.

## Pemecahan masalah

- **`access_denied` / aplikasi diblokir:** periksa Audience, test user, kebijakan
  Workspace, serta approval scope Gmail oleh administrator.
- **`org_internal`:** akun yang login berada di luar organisasi project
  Internal.
- **`redirect_uri_mismatch`:** pastikan client type adalah Desktop app, bukan
  Web application.
- **Consent berulang atau token cepat kedaluwarsa:** periksa apakah aplikasi
  masih External/Testing.
- **Akun salah:** pilih Disconnect Gmail, cabut akses jika diperlukan, lalu
  hubungkan ulang dengan akun payroll yang benar.

Referensi resmi:

- [OAuth 2.0 for Desktop Apps](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Google Auth Platform overview](https://support.google.com/cloud/answer/15548748)
- [Manage app audience](https://support.google.com/cloud/answer/15549945)
- [Gmail API authorization](https://developers.google.com/workspace/gmail/api/auth/about-auth)
