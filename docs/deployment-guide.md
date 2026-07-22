# Panduan Build dan Deployment EunSlip

Dokumen ini menjelaskan pembuatan installer, instalasi awal, upgrade, uninstall,
serta handoff operasional EunSlip versi 1.

## Dukungan dan model instalasi

- Target: Windows 10 dan Windows 11 x64.
- Installer: `EunSlip-Setup-x64.exe`, dijalankan sebagai administrator.
- Aplikasi: self-contained .NET 10; target komputer tidak perlu memasang .NET.
- Binary dipasang ke `C:\Program Files\EunSlip`.
- Data bersama berada di `C:\ProgramData\EunSlip`.
- Aplikasi dan installer versi 1 belum ditandatangani secara digital.

Installer tidak berisi OAuth credential, token Gmail, data payroll, stamp, atau
alamat penerima uji.

## Build installer

Prasyarat mesin build:

- .NET SDK 10.
- Inno Setup 6 (`ISCC.exe`).
- Working tree yang sudah lolos automated test.

Jalankan dari root repository:

```powershell
& .\scripts\Build-Installer.ps1 -Version 1.0.0
```

Script melakukan `dotnet publish` Release untuk `win-x64` dengan
`--self-contained true`, lalu mengompilasi Inno Setup. Hasilnya:

```text
installer\output\EunSlip-Setup-x64.exe
```

Script juga mencetak SHA-256. Catat versi, hash, commit Git, tanggal build, dan
petugas pembuat build pada release record. Jangan commit file `.exe` hasil build.

## Verifikasi lifecycle installer

Lifecycle test memakai direktori sandbox di `%TEMP%`; tidak menyentuh data asli
`C:\ProgramData\EunSlip`.

```powershell
& .\scripts\Test-InstallerLifecycle.ps1
```

Test harus membuktikan:

- clean install membuat binary dan struktur data bersama;
- upgrade 1.0.0 ke 1.0.1 mempertahankan marker database, OAuth, dan stamp;
- uninstall menghapus binary tetapi mempertahankan seluruh marker data.

## Clean install

1. Salin installer dan hash melalui kanal internal yang disetujui.
2. Verifikasi SHA-256 sebelum menjalankan file.
3. Login menggunakan akun Windows administrator atau minta pendampingan IT.
4. Jalankan `EunSlip-Setup-x64.exe` dan selesaikan wizard.
5. Bila SmartScreen atau antivirus memberi peringatan, hentikan dan minta IT
   memverifikasi hash/asal file atau melakukan whitelisting. Jangan menonaktifkan
   atau melewati kontrol keamanan Windows.
6. Jalankan EunSlip sebagai user biasa, bukan **Run as administrator**.
7. Pastikan folder berikut tersedia dan dapat dipakai user accounting:

```text
C:\ProgramData\EunSlip\
  database\
  stamp\
  oauth\
  temp\
  logs\
  runtime\
```

## Konfigurasi pascainstalasi

1. Ikuti [panduan Google Cloud](google-cloud-setup.md).
2. Masukkan OAuth desktop client JSON melalui Settings dan hubungkan akun Gmail
   payroll.
3. Impor stamp PNG/JPEG yang telah disetujui.
4. Pilih bahasa antarmuka; restart aplikasi bila diminta.
5. Verifikasi subject/body default dan sender display name.
6. Jalankan UAT satu recipient menggunakan data dummy.

## Upgrade manual

1. Pastikan tidak ada proses pengiriman dan tutup EunSlip.
2. Catat versi aktif serta hash installer baru.
3. Jalankan installer versi lebih baru sebagai administrator. AppId yang stabil
   membuat Inno Setup memperbarui instalasi yang sama.
4. Jalankan EunSlip sebagai user biasa dan verifikasi:
   - history/database tetap ada;
   - Gmail masih terhubung;
   - stamp, bahasa, dan email template tetap sama;
   - log masih mengikuti retensi aplikasi.
5. Jalankan satu dummy smoke test. Jangan melakukan downgrade tanpa prosedur
   migrasi database yang telah diuji.

Binary di Program Files boleh diganti, tetapi installer tidak menghapus
`C:\ProgramData\EunSlip`.

## Uninstall

1. Pastikan pengiriman sudah selesai dan tutup aplikasi.
2. Buka **Settings > Apps > Installed apps**, pilih EunSlip, lalu Uninstall.
3. Uninstall menghapus binary dan shortcut, tetapi sengaja mempertahankan:
   - history/database;
   - OAuth client dan token Gmail;
   - stamp;
   - bahasa dan email template;
   - log yang masih berada dalam retensi.

Data tetap berada di `C:\ProgramData\EunSlip`. Penghapusan permanen hanya boleh
dilakukan sebagai tindakan IT terpisah setelah persetujuan pemilik data. Saat
komputer dipensiunkan, IT juga harus mencabut grant OAuth akun payroll dan
menjalankan prosedur secure disposal organisasi.

## Checklist handoff

- [ ] Installer version dan SHA-256 dicatat.
- [ ] Automated suite dan installer lifecycle test lulus.
- [ ] Target Windows 10 atau Windows 11 x64 dikonfirmasi.
- [ ] SmartScreen/antivirus approval tersedia.
- [ ] Google Cloud project, Audience, scope, dan OAuth client diverifikasi.
- [ ] Akun Gmail payroll dan display name diverifikasi.
- [ ] Stamp dan template email disetujui.
- [ ] UAT satu recipient dummy lulus.
- [ ] PIC operasional dan jalur eskalasi IT terdokumentasi.
