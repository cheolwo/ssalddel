using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;

namespace Ssalddel.Tests.Services.Food;

public sealed class FoodDeliveryLocationAuditTests
{
    [Fact]
    public void 위치가없거나오래돼도_차단이아닌감사코드로분류한다()
    {
        var now = DateTime.UtcNow;
        Assert.Equal(FoodDeliveryLocationAuditCodes.MissingLocation,
            FoodDeliveryLocationAudit.Evaluate(null, 37.5m, 127m, now).Code);
        var stale = new DriverLocationSnapshot("driver", 37.5m, 127m, 1, "운행중", now.AddMinutes(-6), now);
        Assert.Equal(FoodDeliveryLocationAuditCodes.StaleLocation,
            FoodDeliveryLocationAudit.Evaluate(stale, 37.5m, 127m, now).Code);
    }

    [Fact]
    public void 위치거리는_정상과범위밖을결정적으로분류한다()
    {
        var now = DateTime.UtcNow;
        var location = new DriverLocationSnapshot("driver", 37.588m, 127.085m, 1, "운행중", now, now);
        var close = FoodDeliveryLocationAudit.Evaluate(location, 37.5881m, 127.0851m, now);
        var far = FoodDeliveryLocationAudit.Evaluate(location, 37.6m, 127.1m, now);

        Assert.Equal(FoodDeliveryLocationAuditCodes.WithinRange, close.Code);
        Assert.Equal(FoodDeliveryLocationAuditCodes.OutOfRange, far.Code);
        Assert.True(far.DistanceKm > close.DistanceKm);
    }
}
