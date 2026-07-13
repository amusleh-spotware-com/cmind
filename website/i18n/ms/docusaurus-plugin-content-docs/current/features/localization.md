# Penyesuaian (i18n)

cMind boleh disesuaikan sepenuhnya dan dihantar dalam **23 bahasa yang sama yang disokong cTrader sendiri**, jadi pedagang
menggunakan platform — dan membaca dokumen ini — dalam bahasa mereka sendiri. Bahasa Inggeris ialah sandaran; mana-mana terjemahan
yang hilang mundur dengan elegan ke bahasa Inggeris berbanding menunjukkan kosong atau kunci mentah.

## Bahasa yang disokong

Arab (RTL), Cina (Ringkas), Czech, Inggeris, Perancis, Jerman, Greek, Hungary, Indonesia,
Itali, Jepun, Korea, Melayu, Poland, Portugis (Brazil), Rusia, Serbia, Slovak, Slovenia,
Sepanyol, Thai, Turki, Vietnam.

Satu sumber kebenaran ialah `Core.Constants.SupportedCultures` — middles ware tengah permintaan, penukar bahasa,
ujian paroiti sumber, dan gerbang teks tanpa-hardcode semua membacanya. Menambah bahasa ialah perubahan satu baris di sana
ditambah fail sumbernya.

## Cara ia berfungsi (Blazor Server)

- **Sumber.** Rentetan UI tinggal dalam `src/Web/Resources/Ui.resx` (paksi bahasa Inggeris) ditambah satu
  `Ui.<budaya>.resx` setiap bahasa. Komponen membacanya melalui `IStringLocalizer<Ui>` — `@L["key"]`,
  bukan literal. Fail `.resx` dijana dari `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), sumber kebenaran yang mesra penterjemah.
- **Resolusi budaya.** `RequestLocalizationMiddleware` memilih budaya dari kuki `.AspNetCore.Culture`
  dulu, kemudian `Accept-Language` pelayar, kemudian bahasa Inggeris.
- **Penukaran.** Penukar bahasa app-bar (dan bahagian **Tetapan → Bahasa**) navigasi ke titik akhir
  `GET /set-culture` — muat semula penuh di luar litar Blazor, kerana litar tidak boleh menukar budaya secara langsung. Ia menulis kuki dan, untuk pengguna yang daftar masuk, mengekalkan pilihan ke profil mereka
  (`UserProfile.Locale`); muat semulaboot litar segar dalam bahasa yang dipilih.
- **Kekekalan & daftar masuk.** Locale profil yang disimpan ditulis balik ke kuki budaya pada daftar masuk,
  jadi pengguna mendarat dalam bahasa mereka pada setiap peranti.
- **Kiri-ke-kanan.** Arab (dan mana-mana bahasa RTL masa depan) menetapkan `<html dir="rtl">` dan membungkus reka letak dalam
  `MudRTLProvider` Blazor, mencerminkan seluruh shell.
- **ICU.** Hos Web berjalan dengan ICU dibolehkan (`InvariantGlobalization=false`); kod wire/parse kekal pada
  `CultureInfo.InvariantCulture`, jadi hanya pemformatan UI setiap budaya terjejas — tidak pernah backtest atau CSV.

## Gerbang — tiada teks UI yang di-hardcode

Rentetan baharu yang menghadap pengguna **tidak boleh** digabung tanpa disesuaikan dalam skop yang diliputi:

- Ujian arch-guard yang gagal-bina (`NoHardcodedUiTextTests`) mengimbas fail `.razor` yang dipindahkan dan gagal pada
  mana-mana literal, atribut membawa teks (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) yang bukan carian `@L["…"]`.
- Ujian paroiti sumber (`ResourceParityTests`) gagal bina jika mana-mana bahasa hilang kunci atau ships
  nilai kosong — setiap bahasa sentiasa mempunyai setiap kunci.

## Menambah atau menukar rentetan

1. Tambah/edit kunci dalam `tools/i18n/ui-translations.json` untuk **setiap** budaya.
2. Jana semula `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Rujukannya dalam komponen dengan `@L["your.key"]`.
4. `dotnet test` — gerbang paroiti dan teks tanpa-hardcode memastikan kejujuran anda.

## Penyesuaian dokumen

Dokumen ini disesuaikan juga. i18n Docusaurus dikonfigurasi untuk kesemua 23 locale (`website/i18n/`), dengan
dropdown locale dalam navbar dan RTL untuk Arab. Perancah fail terjemahan locale dengan
`npm run write-translations -- --locale <code>` dan terjemahkan di bawah `website/i18n/<code>/`. Mengikut
mandat penyesuaian, **menambah atau menukar mana-mana doc bermakna mengemas kini setiap locale dalam perubahan yang sama.**
