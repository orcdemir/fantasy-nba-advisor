# Claude Routine talimatı — Fantasy NBA sabah raporu

Bu depo, 8 takımlı bir Yahoo H2H 9-CAT fantasy basketbol ligi için günlük transfer analizi yapar.
GitHub Actions her sabah ~05:30'da (TR) veriyi toplar ve analiz eder. Sen ~06:00'da sonucu okuyup raporlarsın.
**Hiçbir transfer yapma, Yahoo'ya erişmeye çalışma.** Sadece öneri sun; karar kullanıcıya ait.

## Adımlar

1. `data/snapshots/latest.json` ve `data/analysis/latest.json` dosyalarını oku.
2. **Veri güncelliği:** `collectedAt` bugünün tarihi (Türkiye saati) değilse veya analizin `status` değeri `ok` değilse
   öneri üretme. Sadece şunu raporla: veri neden eski/hatalı (analizdeki `message` ve snapshot `warnings` alanları).
3. Analiz `ok` ve `swaps` doluysa, en iyi **en fazla 3** öneriyi raporla. Her biri için:
   - **AL / BIRAK** oyuncu adları ve pozisyon/takım,
   - `totalDelta` ve en çok etkilenen kategoriler (`impacts` içinden en büyük pozitif ve negatif olanlar),
   - `risks` alanındaki riskler (sakatlık, küçük örneklem, düşük dakika).
   Sayıları kendin hesaplama; `analysis/latest.json` içindekileri aktar.
4. `swaps` boşsa: "Bugün anlamlı bir transfer fırsatı yok." de ve nedeni kısaca belirt (analizdeki `message`).
5. Her öneri için şunu hatırlat: **Yahoo'da müsaitliğini ve waiver durumunu kontrol et.** Müsaitlik tahminidir;
   rakip kadrolar elle güncellenen bir dosyadan gelir. Oyuncu müsait değilse sıradaki öneriye bak.
6. `snapshot.warnings` doluysa (eşleşmeyen oyuncu adları) kısa bir not olarak ekle; kullanıcı `data/league-rosters.json`
   dosyasındaki ismi düzeltmeli.
7. Raporu `reports/YYYY-MM-DD.md` olarak yaz ve commit'le (YYYY-MM-DD Türkiye tarihi).

## Çıktı biçimi

**İlk satır** telefon bildiriminde görüneceği için tek satır, en fazla ~100 karakter olmalı:

- Fırsat varsa: `AL: <oyuncu> / BIRAK: <oyuncu> (+<totalDelta>)`
- Fırsat yoksa: `Bugün transfer önerisi yok`
- Veri sorunu varsa: `Veri eski/hatalı: öneri yok`

Ardından kısa ayrıntı. Bilmediğin veya veride olmayan bir şeyi uydurma.
