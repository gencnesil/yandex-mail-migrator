# Yandex Mail Migrator / Mail Migration Desktop

Windows 10/11 uzerinde calisan .NET 8 WPF tabanli IMAP posta tasima uygulamasidir. Kaynak hesaptaki mesajlari silmeden veya flag'lerini degistirmeden hedef hesaba MIME icerigi, internal date ve desteklenen IMAP flag'leriyle `APPEND` eder.

Bu arac Yandex Mail dahil IMAP destekleyen posta servisleri arasinda tek hesap posta kutusu tasimak icin hazirlanmistir.

## Turkce Kullanım

### Programi baslatma

Program hazir EXE dosyasi ile calisir. Uygulamayi acmak icin `artifacts/app/MailMigrationDesktop.exe` dosyasini calistirin.

Installer uretilmis ise `artifacts/installer/MailMigrationDesktop-Setup.exe` dosyasini acip kurulumu tamamlayabilirsiniz. Kurulumdan sonra uygulama Baslat menusu ve secilirse masaustu kisayolu uzerinden acilir.

### Tasima nasil yapilir?

1. Programi acin.
2. `Hesaplar` sekmesinde kaynak posta hesabinin IMAP bilgilerini girin.
3. Ayni ekranda hedef posta hesabinin IMAP bilgilerini girin.
4. `Baglantilari test et` dugmesine basin.
5. Baglanti testi basarili olursa `Posta kutularini analiz et` dugmesine basin.
6. `Klasor esleme` sekmesinde kaynak ve hedef klasor eslesmelerini kontrol edin.
7. `Tasima` sekmesinde gerekirse `Batch` ve `Worker` degerlerini ayarlayin.
8. `Tasimayi baslat` dugmesine basin.
9. Tasima sirasinda islemi `Duraklat`, `Devam et` veya `Guvenli durdur` dugmeleriyle yonetebilirsiniz.
10. Islem tamamlandiginda raporlar `Belgeler/Mail Migration Reports` klasorune kaydedilir.

### Yandex Mail ornek IMAP ayarlari

- IMAP sunucusu: `imap.yandex.com`
- Port: `993`
- Guvenlik: SSL/TLS
- Kullanici adi: tam e-posta adresi

Yandex hesabinda iki adimli dogrulama aciksa normal hesap parolasi yerine uygulama parolasi kullanmaniz gerekebilir.

### Veriler nerede tutulur?

- Uygulama verileri: `%LOCALAPPDATA%/MailMigrationDesktop`
- Raporlar: `Belgeler/Mail Migration Reports`

Kaynak mesajlar silinmez. Program hedef hesaba kopyalama yapar ve yarim kalan islemler icin checkpoint bilgisini saklar.

## English Usage

### Starting the program

The program runs from the ready-to-use EXE file. Start the application by opening `artifacts/app/MailMigrationDesktop.exe`.

If an installer has been built, open `artifacts/installer/MailMigrationDesktop-Setup.exe` and complete the installation. After installation, the app can be started from the Start menu and, if selected, the desktop shortcut.

### How to migrate mail

1. Open the program.
2. In the `Accounts` tab, enter the source mailbox IMAP details.
3. Enter the target mailbox IMAP details on the same screen.
4. Click `Test connections`.
5. If the connection test succeeds, click `Analyze mailboxes`.
6. Review the source and target folder mappings in the `Folder mapping` tab.
7. In the `Migration` tab, adjust `Batch` and `Worker` values if needed.
8. Click `Start migration`.
9. During migration, you can use `Pause`, `Resume` or `Safe stop`.
10. When the migration finishes, reports are saved under `Documents/Mail Migration Reports`.

### Yandex Mail example IMAP settings

- IMAP server: `imap.yandex.com`
- Port: `993`
- Security: SSL/TLS
- Username: full email address

If two-factor authentication is enabled on the Yandex account, you may need to use an app password instead of the regular account password.

### Where data is stored

- Application data: `%LOCALAPPDATA%/MailMigrationDesktop`
- Reports: `Documents/Mail Migration Reports`

Source messages are not deleted. The program copies messages to the target mailbox and stores checkpoint data for interrupted migrations.

## Features

- .NET 8 WPF desktop interface
- Source and target IMAP connection testing
- Password storage with Windows DPAPI CurrentUser
- Recursive mailbox analysis and folder mapping
- Duplicate detection with UIDVALIDITY, UID and message fingerprint
- Pause, resume and safe cancellation support
- SQLite checkpoints and migration state tracking
- HTML, CSV and JSON reports
- Docker-based local IMAP test lab
- Inno Setup installer definition

## Local IMAP test lab

For local testing, the repository includes a Docker Compose lab with two Dovecot IMAP servers.

Test account:

- Source IMAP: `localhost:1143`
- Target IMAP: `localhost:2143`
- User: `test@example.test`
- Password: `Test123!`

This lab is intended for local non-TLS testing only. Production certificate validation is enabled by default.

## Developer notes

The application project is `src/MailMigration.UI/MailMigration.UI.csproj`. The ready application output is expected under `artifacts/app`, and the installer output is expected under `artifacts/installer`.
