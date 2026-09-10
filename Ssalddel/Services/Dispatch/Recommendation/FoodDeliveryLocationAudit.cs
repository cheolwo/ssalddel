using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Recommendation;

public static class FoodDeliveryLocationAuditCodes
{
    public const string WithinRange = "WithinRange";
    public const string MissingLocation = "MissingLocation";
    public const string StaleLocation = "StaleLocation";
    public const string MissingTarget = "MissingTarget";
    public const string OutOfRange = "OutOfRange";
}

public sealed record FoodDeliveryLocationAuditResult(
    string Code,
    decimal? DistanceKm,
    DateTime ObservedAtUtc,
    DateTime? DriverLocationAtUtc,
    decimal? DriverLatitude,
    decimal? DriverLongitude,
    decimal? TargetLatitude,
    decimal? TargetLongitude);

public static class FoodDeliveryLocationAudit
{
    public static FoodDeliveryLocationAuditResult Evaluate(
        DriverLocationSnapshot? location,
        decimal? targetLatitude,
        decimal? targetLongitude,
        DateTime nowUtc,
        decimal advisoryRadiusKm = 0.5m,
        TimeSpan? freshness = null)
    {
        if (location is null)
            return Result(FoodDeliveryLocationAuditCodes.MissingLocation, null);
        if (nowUtc - location.RecordedAtUtc > (freshness ?? TimeSpan.FromMinutes(5)))
            return Result(FoodDeliveryLocationAuditCodes.StaleLocation, null);
        if (!targetLatitude.HasValue || !targetLongitude.HasValue)
            return Result(FoodDeliveryLocationAuditCodes.MissingTarget, null);

        var distance = DistanceKm(location.Latitude, location.Longitude, targetLatitude.Value, targetLongitude.Value);
        return Result(distance <= advisoryRadiusKm
            ? FoodDeliveryLocationAuditCodes.WithinRange
            : FoodDeliveryLocationAuditCodes.OutOfRange, distance);

        FoodDeliveryLocationAuditResult Result(string code, decimal? distance)
            => new(code, distance, nowUtc, location?.RecordedAtUtc, location?.Latitude, location?.Longitude,
                targetLatitude, targetLongitude);
    }

    private static decimal DistanceKm(decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        const double earthRadiusKm = 6371.0088d;
        static double Radians(decimal degrees) => (double)degrees * Math.PI / 180d;
        var lat1 = Radians(latitude1);
        var lat2 = Radians(latitude2);
        var deltaLat = lat2 - lat1;
        var deltaLng = Radians(longitude2 - longitude1);
        var a = Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLng / 2d) * Math.Sin(deltaLng / 2d);
        return Math.Round((decimal)(earthRadiusKm * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a))), 4);
    }
}
