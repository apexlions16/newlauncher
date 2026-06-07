# Odium Vault Launcher

Kişisel yedekler ve kapalı ekip içerikleri için shard tabanlı launcher/vault sistemi.

## Kurulum

Windows:

```bat
install_dependencies.bat
run_launcher.bat
```

Sessiz başlatma:

```bat
run_launcher_sessiz.vbs
```

## Mimari

- Büyük vault shard dosyaları: Hugging Face Dataset repo
- Sık güncellenen küçük katalog: GitHub Raw veya başka HTTP endpoint
- Yerel ayarlar: `launcher_data/`

Remote DB URL:

```text
https://raw.githubusercontent.com/apexlions16/newlauncher/main/catalog/tokenizer_library.bin
```

## Çoklu HF repo desteği

Ana depo dolarsa launcher ayarlarından ek HF depo bilgisi eklenebilir. İndirmede önce katalog kaydındaki depo, sonra ana depo, sonra yedek depolar denenir.

## Test

```bash
python scripts/self_test.py
```
