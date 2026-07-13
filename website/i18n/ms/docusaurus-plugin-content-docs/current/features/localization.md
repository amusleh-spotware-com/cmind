# Pelokalan (i18n)

cMind adalah sepenuhnya boleh dilokalisasi dan dihantar dalam **23 bahasa yang sama yang cTrader sendiri sokong**, jadi pedagang menggunakan platform — dan membaca dokumen ini — dalam bahasa mereka sendiri. Inggeris adalah fallback; sebarang terjemahan yang hilang merosot dengan anggun kepada Inggeris daripada menunjukkan kosong atau kunci mentah.

## Bahasa yang disokong

Arab (RTL), Cina (Dipermudah), Ceko, Inggeris, Perancis, Jerman, Yunani, Hungary, Indonesia, Itali, Jepun, Korea, Melayu, Poland, Portugis (Brazil), Rusia, Serbia, Slovak, Slovenian, Sepanyol, Thai, Turki, Vietnam.

Sumber kebenaran tunggal adalah `Core.Constants.SupportedCultures` — middleware budaya-permintaan, penukar bahasa, ujian pariti sumber, dan pintu rentetan tanpa kod keras semuanya membaca daripadanya. Menambah bahasa adalah perubahan satu baris di sana tambah fail sumbernya.

## Bagaimana ia berfungsi (Blazor Server)

- **Sumber.** Rentetan UI hidup dalam `src/Web/Resources/Ui.resx` (asas Inggeris) tambah satu `Ui.<culture>.resx` setiap bahasa. Komponen membacanya melalui `IStringLocalizer<Ui>` — `@L["key"]`, tidak pernah literal. Fail `.resx` dijana daripada `tools/i18n/ui-translations.json` (`pwsh tools/i18n/gen-resx.ps1`), sumber kebenaran yang mesra penterjemah.
- **Penyelesaian budaya.** `RequestLocalizationMiddleware` memilih budaya daripada kuki `.AspNetCore.Culture` terlebih dahulu, kemudian `Accept-Language` pelayar, kemudian Inggeris.
- **Menukar.** Penukar bahasa bar apl (dan bahagian **Tetapan → Bahasa**) menavigasi ke titik akhir `GET /set-culture` — muat semula penuh di luar litar Blazor, kerana litar tidak boleh menukar budaya hidup. Ia menulis kuki dan, untuk pengguna yang menyusun masuk, bertahan dengan pilihan kepada profil mereka (`UserProfile.Locale`); muatan semula but litar segar dalam bahasa yang dipilih.
- **Ketekunan & log masuk.** Tempat tempatan profil yang disimpan ditulis kembali ke kuki budaya semasa log masuk, jadi pengguna mendarat dalam bahasa mereka di setiap peranti.
- **Kanan-ke-kiri.** Arab (dan mana-mana bahasa RTL masa depan) menetapkan `<html dir="rtl">` dan membungkus susun atur dalam `MudRTLProvider` MudBlazor, mencerminkan seluruh cangkul.
- **ICU.** Hos Web berjalan dengan ICU didayakan (`InvariantGlobalization=false`); kod wayar/parse tinggal pada `CultureInfo.InvariantCulture`, jadi hanya pemformatan setiap budaya UI dipengaruhi — tidak pernah ujian belakang atau CSV.

## Pintu — tiada teks UI kod keras

Rentetan pengguna-menghadap baru **tidak boleh** digabung tanpa dilokalisasi dalam skop yang tercakup:

- Ujian arkeologi yang tidak dapat dibina (`NoHardcodedUiTextTests`) mengimbas fail `.razor` yang dimigrasikan dan gagal pada mana-mana literal, atribut yang mengandungi teks (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`) yang bukan pencarian `@L["…"]`.
- Ujian pariti sumber (`ResourceParityTests`) gagal pembinaan jika mana-mana bahasa kehilangan kunci atau kapal nilai kosong — setiap bahasa sentiasa mempunyai setiap kunci.

## Menambah atau menukar rentetan

1. Tambah/edit kunci dalam `tools/i18n/ui-translations.json` untuk **setiap** budaya.
2. Janakan semula `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Rujuknya dalam komponen dengan `@L["your.key"]`.
4. `dotnet test` — pintu pariti dan rentetan-kod keras menjaga anda jujur.

## Pelokalan dokumen

Dokumen ini juga dilokalisasi. Docusaurus i18n dikonfigurasi untuk semua 23 lokal (`website/i18n/`), dengan lungsur lokal dalam bar navigasi dan RTL untuk Arab. Perancah fail terjemahan lokal dengan `npm run write-translations -- --locale <code>` dan terjemahkan di bawah `website/i18n/<code>/`. Menurut mandat pelokalan, **menambah atau menukar mana-mana dokumen bermakna mengemaskini setiap lokal dalam perubahan yang sama.**
