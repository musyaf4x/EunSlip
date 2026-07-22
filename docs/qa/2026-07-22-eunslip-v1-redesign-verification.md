# EunSlip v1 Redesign Verification — 2026-07-22

## Ruang lingkup dan batas aman

- Build yang diuji: branch `codex/eunslip-v1-redesign`, konfigurasi Debug, .NET 10.
- E2E memakai `C:\Users\hafid\Downloads\EunSlip_Handoff\EunSlip_Payroll_Template.xlsx` dengan periode `JULY 2026` dan tanggal pembayaran `22/07/2026`.
- Pengujian berhenti di langkah 4 setelah tombol `KONFIRMASI & KIRIM` terbukti aktif. Tidak ada email yang dikirim.
- Pengujian tidak menghubungkan/memutus Gmail, mengubah OAuth, menghapus stamp, menghapus riwayat, membuat installer, melakukan push, atau deploy.

## Automated gates

| Gate | Result | Evidence |
|---|---|---|
| Repository diff check | PASS | `git diff --check` tidak menemukan whitespace error |
| Full build | PASS | `dotnet build EunSlip.slnx --no-restore`: 0 warnings, 0 errors |
| Core tests | PASS | 85/85 passed |
| Infrastructure tests | PASS | 93/93 passed |
| Desktop tests | PASS | 63/63 passed |
| Seluruh test suite | PASS | `dotnet test EunSlip.slnx --no-build`: 241/241 passed, 0 failed |
| Sending lock | PASS | `MainViewModelTests.SendingState_DisablesNavigationAndClose` dan coordinator test doubles lulus |
| PDF evidence writer | PASS | `Generate_WritesReferenceEvidenceWhenPathIsProvided`: 1/1 passed |
| PDF raster review | PASS | [`artifacts/pdf/eunslip-reference-sample-1.png`](../../artifacts/pdf/eunslip-reference-sample-1.png) |

## PDF review

PDF bukti diregenerasi ke [`artifacts/pdf/eunslip-reference-sample.pdf`](../../artifacts/pdf/eunslip-reference-sample.pdf), diraster pada 144 DPI, lalu dibandingkan dengan [`payslip-layout-reference.png`](../../payslip-layout-reference.png).

Hasil inspeksi:

- satu halaman, seluruh frame berada di area cetak;
- identitas karyawan dan metadata pekerjaan terbagi seimbang;
- kolom income/deduction, garis total, dan angka nominal tidak bertabrakan;
- blok `NETT INCOME`, OT Hours, dan Payment Date memiliki jarak yang konsisten;
- stamp berada di zona otorisasi tanpa menimpa label atau garis tanda tangan;
- tidak ada teks terpotong, garis menembus teks, atau layout bertumpuk seperti pada hasil lama.

## Desktop E2E

| Surface | Result | Notes |
|---|---|---|
| Home | PASS | Status Gmail dan stamp `Siap`, CTA aktif, batch terbaru tampil, notice interrupted tersembunyi saat tidak ada batch interrupted, dan active nav benar |
| Wizard langkah 1 | PASS | File picker menerima workbook dummy; periode/tanggal wajib; `LANJUT` baru aktif setelah input lengkap |
| Wizard langkah 2 | PASS | 2 penerima tervalidasi; tabel, header, baris, ringkasan, dan navigasi tidak terpotong |
| Wizard langkah 3 | PASS | Subject/body tersusun jelas; Gmail dan stamp sama-sama `Siap`; preview PDF lokal berhasil dibuka |
| Wizard langkah 4 | PASS | Ringkasan menunjukkan periode, 2 penerima, akun Gmail, dan dua prasyarat `Siap`; `KONFIRMASI & KIRIM` aktif; pengujian berhenti sebelum send |
| History | PASS | Status terlokalisasi, batch/detail dapat dipilih, NIK tersamarkan, action gating sesuai status, pilihan bertahan setelah pindah halaman, dan retry/recovery entry tervalidasi oleh unit tests |
| Settings | PASS | Ready-state setelah loaded, layout dua kolom, disclosure setup, language selector, dan kontrol non-destruktif tampil benar; status loading/ready juga dikunci oleh `SettingsViewModelTests`; tidak ada perubahan setting |
| About | PASS | Identitas produk/vendor, versi, lisensi, kontak, dan lokasi log tampil utuh |
| Minimum viewport | PASS | Window diuji pada WPF minimum `1024 × 680` (capture content/non-client `1011 × 674`); Home, History detail, Settings, dan About tetap terjangkau tanpa page-level horizontal overflow; tombol dan tabel History tetap muat |
| 150% scaling | NOT RUN | Tidak mengubah display scaling Windows melalui automation karena perubahan tersebut bersifat global terhadap desktop pengguna |

## Temuan yang ditutup selama E2E

1. Status run mode mengikuti kultur binding WPF yang sebelumnya tetap `en-US`; root window kini mewarisi `CurrentUICulture` dan regression smoke test menguncinya.
2. Tabel batch History tidak lagi menampilkan horizontal scrollbar pada viewport normal.
3. Seleksi History dipulihkan berdasarkan batch ID setelah refresh/navigasi sehingga panel detail tidak hilang.
4. History memakai rasio kolom responsif dan kolom detail berbobot sehingga tombol maupun data tetap terlihat pada ukuran minimum.

## Scope decision

TASK 13 dan TASK 14 tidak lagi memiliki acceptance blocker yang ditemukan oleh build, automated tests, PDF review, atau E2E aman ini. TASK 15 (installer/dokumentasi operasional) dan TASK 16 (UAT Gmail nyata) tetap pending dan tidak dikerjakan dalam verifikasi ini.
