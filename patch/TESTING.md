# FModel LOCRES Voice Mapper — VO Filtreli Test Rehberi

1. `FModel.exe` dosyasını çalıştırın.
2. Oyunun arşivlerini normal FModel akışıyla yükleyin; gerekiyorsa AES ve mapping dosyasını ayarlayın.
3. Üst menüden **Views → LOCRES Voice Mapper** seçeneğini açın.
4. Bir `.locres` seçin.
5. İlk taramada **Yol filtresi alanını boş bırakın**. Böylece adı `VO` içermeyen gerçek diyalog paketleri de kaçırılmaz.
6. **Ses türü** bölümünden önce **Yalnızca VO — Dengeli** seçeneğini kullanın.
7. **Yalnızca ses adayı bulunanlar** açıkken fiziksel sese bağlanamayan sonuçlar gizlenir. Eşleşmeyen diyalog paketlerini incelemek için bunu kapatabilirsiniz.
8. **Taramayı Başlat** düğmesine basın.
9. Sonuçlarda önce `Kesin VO` ve `Güçlü VO` satırlarını kontrol edin.
10. **Paketi FModel'de Aç** ve **Sesi FModel'de Aç** düğmeleriyle sonuçları doğrulayın.
11. Sonuçları **CSV Dışa Aktar** ile kaydedebilirsiniz. CSV içinde VO puanı, sınıfı ve kanıtları da bulunur.

## Filtre modları

- **Tümü (tanılama):** VO dışı sonuçları da gösterir. Yanlış pozitif/negatifleri karşılaştırmak için kullanın.
- **Yalnızca VO — Dengeli:** Geniş kapsamlı varsayılan moddur. En az bir güçlü VO işareti ister; müzik/SFX/UI karşı kanıtlarını düşürür.
- **Yalnızca VO — Katı:** En az iki güçlü işaret, fiziksel veya paket içi ses kanıtı ve doğrudan diyalog/event bağlantısı ister.
- **Yalnızca VO — Kesin:** Çok yüksek puan, en az üç güçlü işaret, ses bağlantısı ve sıfır VO-dışı karşı kanıt ister. Sonuç sayısı az olabilir.

## Kullanılan kanıtlar

Pozitif işaretler arasında `DialogueWave`, `DialogueVoice`, `VoiceEvent`, `VoiceLine`, `Speaker + Subtitle`, `Play_VO`, FMOD `event:/VO`, `/VO/`, `/Voice/`, `/Dialogue/`, konuşma namespace/key yapıları, bark/announcer/narration gibi adlandırmalar ve ses dosyasıyla anahtar benzerliği bulunur.

Negatif işaretler arasında `Music`, `BGM`, `OST`, `SFX`, `Foley`, silah/patlama/ayak sesi, ambiyans, çevre, UI/HUD/menü ve bildirim sesleri bulunur. Unreal'ın genel `/Script/Engine` sınıf yolu negatif işaret sayılmaz.

## Sayısal WEM davranışı

- `123456789.wem` gibi yalnızca sayısal dosyalar, adlarından VO kabul edilmez.
- `Play_VO`, `DialogueWave`, `/Voice/`, Wwise/FMOD event veya benzeri bağımsız bir bağlantı bulunduğunda kabul edilebilir.
- Event zinciri yoksa puan düşürülür; Katı/Kesin modda çoğunlukla elenir.

## Notlar

- Bu sürüm LOCRES anahtarını paketlerin ham verisinde arar; eşleşen paketleri CUE4Parse ile ayrıştırarak ses referansı ve VO bağlamı toplar.
- Unversioned paketlerde doğru mapping bulunmuyorsa derin ayrıştırma başarısız olabilir. Bu durumda ham anahtar eşleşmeleri `Tümü` modunda görülebilir.
- Tam oyun-bağımsız Wwise HIRC `Event → Media ID → WEM` çözümlemesi henüz her oyunda garanti değildir.
- İlk geniş tarama oyun boyutuna göre uzun sürebilir. Sonraki taramalarda oyunun gerçek paket yapısını gördükten sonra yol filtresini daraltabilirsiniz.
