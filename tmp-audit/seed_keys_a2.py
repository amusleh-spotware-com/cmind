import os, re

resdir = "src/Web/Resources"

keys = ["users.tempPassword.title", "users.tempPassword.warning", "users.tempPassword.label",
        "common.copy", "common.copiedToClipboard"]

en = {
    "users.tempPassword.title": "Temporary password",
    "users.tempPassword.warning": "Copy this password now - it is shown only once. The user must change it on next sign-in.",
    "users.tempPassword.label": "Temporary password",
    "common.copy": "Copy",
    "common.copiedToClipboard": "Copied to clipboard",
}

tr = {
 "ar": {"users.tempPassword.title":"كلمة مرور مؤقتة","users.tempPassword.warning":"انسخ كلمة المرور الآن - تُعرض مرة واحدة فقط. يجب على المستخدم تغييرها عند تسجيل الدخول التالي.","users.tempPassword.label":"كلمة مرور مؤقتة","common.copy":"نسخ","common.copiedToClipboard":"تم النسخ إلى الحافظة"},
 "cs": {"users.tempPassword.title":"Dočasné heslo","users.tempPassword.warning":"Zkopírujte toto heslo nyní - zobrazí se pouze jednou. Uživatel jej musí při příštím přihlášení změnit.","users.tempPassword.label":"Dočasné heslo","common.copy":"Kopírovat","common.copiedToClipboard":"Zkopírováno do schránky"},
 "de": {"users.tempPassword.title":"Temporäres Passwort","users.tempPassword.warning":"Kopieren Sie dieses Passwort jetzt - es wird nur einmal angezeigt. Der Benutzer muss es bei der nächsten Anmeldung ändern.","users.tempPassword.label":"Temporäres Passwort","common.copy":"Kopieren","common.copiedToClipboard":"In die Zwischenablage kopiert"},
 "el": {"users.tempPassword.title":"Προσωρινός κωδικός","users.tempPassword.warning":"Αντιγράψτε τώρα αυτόν τον κωδικό - εμφανίζεται μόνο μία φορά. Ο χρήστης πρέπει να τον αλλάξει στην επόμενη σύνδεση.","users.tempPassword.label":"Προσωρινός κωδικός","common.copy":"Αντιγραφή","common.copiedToClipboard":"Αντιγράφηκε στο πρόχειρο"},
 "es": {"users.tempPassword.title":"Contraseña temporal","users.tempPassword.warning":"Copie esta contraseña ahora - solo se muestra una vez. El usuario debe cambiarla en el próximo inicio de sesión.","users.tempPassword.label":"Contraseña temporal","common.copy":"Copiar","common.copiedToClipboard":"Copiado al portapapeles"},
 "fr": {"users.tempPassword.title":"Mot de passe temporaire","users.tempPassword.warning":"Copiez ce mot de passe maintenant - il n'est affiché qu'une seule fois. L'utilisateur doit le changer à la prochaine connexion.","users.tempPassword.label":"Mot de passe temporaire","common.copy":"Copier","common.copiedToClipboard":"Copié dans le presse-papiers"},
 "hu": {"users.tempPassword.title":"Ideiglenes jelszó","users.tempPassword.warning":"Másolja ki most ezt a jelszót - csak egyszer jelenik meg. A felhasználónak a következő bejelentkezéskor meg kell változtatnia.","users.tempPassword.label":"Ideiglenes jelszó","common.copy":"Másolás","common.copiedToClipboard":"Vágólapra másolva"},
 "id": {"users.tempPassword.title":"Kata sandi sementara","users.tempPassword.warning":"Salin kata sandi ini sekarang - hanya ditampilkan sekali. Pengguna harus menggantinya saat masuk berikutnya.","users.tempPassword.label":"Kata sandi sementara","common.copy":"Salin","common.copiedToClipboard":"Disalin ke papan klip"},
 "it": {"users.tempPassword.title":"Password temporanea","users.tempPassword.warning":"Copia questa password ora - viene mostrata una sola volta. L'utente deve cambiarla al prossimo accesso.","users.tempPassword.label":"Password temporanea","common.copy":"Copia","common.copiedToClipboard":"Copiato negli appunti"},
 "ja": {"users.tempPassword.title":"一時パスワード","users.tempPassword.warning":"このパスワードを今すぐコピーしてください。一度だけ表示されます。ユーザーは次回のサインイン時に変更する必要があります。","users.tempPassword.label":"一時パスワード","common.copy":"コピー","common.copiedToClipboard":"クリップボードにコピーしました"},
 "ko": {"users.tempPassword.title":"임시 비밀번호","users.tempPassword.warning":"이 비밀번호를 지금 복사하세요 - 한 번만 표시됩니다. 사용자는 다음 로그인 시 변경해야 합니다.","users.tempPassword.label":"임시 비밀번호","common.copy":"복사","common.copiedToClipboard":"클립보드에 복사됨"},
 "ms": {"users.tempPassword.title":"Kata laluan sementara","users.tempPassword.warning":"Salin kata laluan ini sekarang - ia dipaparkan sekali sahaja. Pengguna mesti menukarnya pada log masuk seterusnya.","users.tempPassword.label":"Kata laluan sementara","common.copy":"Salin","common.copiedToClipboard":"Disalin ke papan keratan"},
 "pl": {"users.tempPassword.title":"Hasło tymczasowe","users.tempPassword.warning":"Skopiuj to hasło teraz - jest wyświetlane tylko raz. Użytkownik musi je zmienić przy następnym logowaniu.","users.tempPassword.label":"Hasło tymczasowe","common.copy":"Kopiuj","common.copiedToClipboard":"Skopiowano do schowka"},
 "pt-BR": {"users.tempPassword.title":"Senha temporária","users.tempPassword.warning":"Copie esta senha agora - ela é exibida apenas uma vez. O usuário deve alterá-la no próximo login.","users.tempPassword.label":"Senha temporária","common.copy":"Copiar","common.copiedToClipboard":"Copiado para a área de transferência"},
 "ru": {"users.tempPassword.title":"Временный пароль","users.tempPassword.warning":"Скопируйте этот пароль сейчас - он показывается только один раз. Пользователь должен изменить его при следующем входе.","users.tempPassword.label":"Временный пароль","common.copy":"Копировать","common.copiedToClipboard":"Скопировано в буфер обмена"},
 "sk": {"users.tempPassword.title":"Dočasné heslo","users.tempPassword.warning":"Toto heslo teraz skopírujte - zobrazí sa iba raz. Používateľ ho musí pri ďalšom prihlásení zmeniť.","users.tempPassword.label":"Dočasné heslo","common.copy":"Kopírovať","common.copiedToClipboard":"Skopírované do schránky"},
 "sl": {"users.tempPassword.title":"Začasno geslo","users.tempPassword.warning":"To geslo zdaj kopirajte - prikazano je samo enkrat. Uporabnik ga mora ob naslednji prijavi spremeniti.","users.tempPassword.label":"Začasno geslo","common.copy":"Kopiraj","common.copiedToClipboard":"Kopirano v odložišče"},
 "sr": {"users.tempPassword.title":"Привремена лозинка","users.tempPassword.warning":"Копирајте ову лозинку сада - приказује се само једном. Корисник мора да је промени при следећој пријави.","users.tempPassword.label":"Привремена лозинка","common.copy":"Копирај","common.copiedToClipboard":"Копирано у клипборд"},
 "th": {"users.tempPassword.title":"รหัสผ่านชั่วคราว","users.tempPassword.warning":"คัดลอกรหัสผ่านนี้ทันที - จะแสดงเพียงครั้งเดียว ผู้ใช้ต้องเปลี่ยนในการเข้าสู่ระบบครั้งถัดไป","users.tempPassword.label":"รหัสผ่านชั่วคราว","common.copy":"คัดลอก","common.copiedToClipboard":"คัดลอกไปยังคลิปบอร์ดแล้ว"},
 "tr": {"users.tempPassword.title":"Geçici parola","users.tempPassword.warning":"Bu parolayı şimdi kopyalayın - yalnızca bir kez gösterilir. Kullanıcı bir sonraki oturum açışında değiştirmelidir.","users.tempPassword.label":"Geçici parola","common.copy":"Kopyala","common.copiedToClipboard":"Panoya kopyalandı"},
 "vi": {"users.tempPassword.title":"Mật khẩu tạm thời","users.tempPassword.warning":"Sao chép mật khẩu này ngay - chỉ hiển thị một lần. Người dùng phải đổi ở lần đăng nhập tiếp theo.","users.tempPassword.label":"Mật khẩu tạm thời","common.copy":"Sao chép","common.copiedToClipboard":"Đã sao chép vào bảng nhớ tạm"},
 "zh-Hans": {"users.tempPassword.title":"临时密码","users.tempPassword.warning":"请立即复制此密码 - 仅显示一次。用户必须在下次登录时更改。","users.tempPassword.label":"临时密码","common.copy":"复制","common.copiedToClipboard":"已复制到剪贴板"},
}

def esc(s):
    return s.replace("&","&amp;").replace("<","&lt;").replace(">","&gt;")

def locale_of(fn):
    m = re.match(r"Ui\.(.+)\.resx$", fn)
    if not m: return None
    return m.group(1)

for fn in sorted(os.listdir(resdir)):
    if not re.match(r"Ui(\.[A-Za-z-]+)?\.resx$", fn): continue
    path = os.path.join(resdir, fn)
    with open(path, encoding="utf-8") as f:
        content = f.read()
    loc = locale_of(fn)
    vals = en if loc is None else {k: tr.get(loc, {}).get(k, en[k]) for k in keys}
    lines = []
    for k in keys:
        if ('name="' + k + '"') in content:
            continue
        lines.append('  <data name="' + k + '" xml:space="preserve"><value>' + esc(vals[k]) + '</value></data>')
    if not lines:
        continue
    block = "\n".join(lines) + "\n"
    content = content.replace("</root>", block + "</root>")
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)
    print("updated " + fn + " (+" + str(len(lines)) + " keys)")

print("done")
