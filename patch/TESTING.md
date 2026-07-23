# FModel LOCRES Voice Mapper — Test Rehberi

1. `FModel.exe` dosyasını çalıştırın.
2. Oyunun arşivlerini normal FModel akışıyla yükleyin; gerekiyorsa AES ve mapping dosyasını ayarlayın.
3. Üst menüden **Views → LOCRES Voice Mapper** seçeneğini açın.
4. Bir `.locres` seçin.
5. İlk denemede yol filtresine `Dialogue|Dialog|VO|Voice|Quest|Narrative|Audio` yazın.
6. **Taramayı Başlat** düğmesine basın.
7. Sonuçlarda yüksek güven puanlı satırları önce kontrol edin.
8. **Paketi FModel'de Aç** ve **Sesi FModel'de Aç** düğmeleriyle sonuçları doğrulayın.
9. Sonuçları **CSV Dışa Aktar** ile kaydedebilirsiniz.

Notlar:
- Bu sürüm, LOCRES anahtarını paketlerin ham verisinde arar; eşleşen paketleri CUE4Parse ile ayrıştırarak ses referansı ipuçlarını toplar.
- Unversioned paketlerde doğru mapping bulunmuyorsa derin ayrıştırma başarısız olabilir. Ham anahtar eşleşmeleri yine listelenir.
- Sayısal WEM adlarını kesin event zincirine bağlayan tam Wwise HIRC çözümleyicisi sonraki aşamadır.
- İlk geniş tarama oyun boyutuna göre uzun sürebilir. Yol filtresi taramayı ciddi ölçüde hızlandırır.
