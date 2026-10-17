namespace KargoApp.Shipments;

public static class ShipmentConsts
{
    public const string TrackingNumberPrefix = "KRG";
    public const int TrackingNumberDigitCount = 10;
    public const int TrackingNumberLength = 13; // "KRG" + 10 rakam

    public const int MaxNameLength = 128;       // SenderName, ReceiverName
    public const int MaxCityLength = 64;

    public const double MinWeightKg = 0.01;
    public const double MaxWeightKg = 100;
}
