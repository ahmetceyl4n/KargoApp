namespace KargoApp.Worker;

// appsettings.json'daki "ScanProcessing" bölümü. Aşama 5'te değiştirip ölçeceğiz.
public class ScanProcessingOptions
{
    // Aynı anda kaç şerit çalışır
    public int Lanes { get; set; } = 4;

    // Bir şerit tek seferde en fazla kaç kargo kilitler
    public int ShipmentsPerBatch { get; set; } = 50;

    // İş yoksa kaç ms bekler
    public int IdleDelayMs { get; set; } = 1000;

    // Geçersiz geçişte en fazla kaç deneme (sonra kalıcı hata)
    public int MaxAttempts { get; set; } = 5;

    // İlk bekleme süresi; her denemede ikiye katlanır (2, 4, 8, 16 sn)
    public int RetryDelayMs { get; set; } = 2000;
}
