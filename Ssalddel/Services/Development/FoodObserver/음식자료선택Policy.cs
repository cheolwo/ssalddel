using System.Security.Cryptography;
using System.Text.Json;

namespace Ssalddel.Services.Development.FoodObserver;

public sealed record 음식자료가격참고(long RecordId, string ItemCode, DateOnly Date, decimal PriceKrw, string Unit, string Region, string SourceUrl);
public sealed record 음식자료후보(long RecipeId, string Title, string SourceKey, string Checksum, DateTimeOffset ObservedAtUtc,
    string SourceUrl, string License, string Ingredient, long IngredientId, string MappingStatus, 음식자료가격참고? Reference);
public sealed record 음식자료사본(string SchemaVersion, DateTimeOffset FrozenAtUtc, 음식자료후보[] Menus);
public sealed record 음식자료판매후보(음식자료후보 Source, long MenuId, decimal SalePrice, bool Available);
public sealed record 음식자료선택결과(string SnapshotHash, long RecipeId, long MenuId, string Title, string Reason,
    string Source, string Reference, decimal SalePrice, string PriceBasis, string[] Candidates);

/// <summary>읽기 사본으로 후보만 고른다. 실제 주문은 기존 API의 현재 상태 검증을 거친다.</summary>
public static class 음식자료선택Policy
{
    public static (음식자료사본 Data, string Hash) Read(string path)
    {
        if (new FileInfo(path).Length > 32768) throw new InvalidDataException("MenuSnapshotTooLarge");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 32768) throw new InvalidDataException("MenuSnapshotTooLarge");
        var data = JsonSerializer.Deserialize<음식자료사본>(bytes, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Validate(data);
        return (data!, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public static bool OrderMatches(음식자료선택결과 selection, IReadOnlyList<Ssalddel.Contracts.Food.음식주문상품Dto> items)
        => selection.MenuId > 0 && items.Count == 1 && items[0].메뉴Id == selection.MenuId
            && items[0].상품명 == selection.Title && items[0].수량 == 1 && items[0].단가 == selection.SalePrice;

    public static void Validate(음식자료사본? data)
    {
        if (data is null || data.SchemaVersion != "food-observer-menus.r1" || data.FrozenAtUtc == default
            || data.Menus is null || data.Menus.Length is < 1 or > 3
            || data.Menus.Select(x => x?.RecipeId).Distinct().Count() != data.Menus.Length)
            throw new InvalidDataException("MenuSnapshotInvalid");
        foreach (var m in data.Menus)
            if (m is null || m.RecipeId <= 0 || string.IsNullOrWhiteSpace(m.Title) || m.Title.Length > 100
                || m.SourceKey != "mfds-cookrcp01" || m.Checksum is null || m.Checksum.Length != 64
                || !m.Checksum.All(Uri.IsHexDigit) || m.ObservedAtUtc == default || m.ObservedAtUtc > data.FrozenAtUtc
                || m.License != "공공데이터포털 이용허락범위 제한 없음" || m.IngredientId <= 0
                || string.IsNullOrWhiteSpace(m.SourceUrl) || string.IsNullOrWhiteSpace(m.Ingredient)
                || (m.Reference is { } r && (r.RecordId <= 0 || r.PriceKrw <= 0 || r.Date == default
                    || string.IsNullOrWhiteSpace(r.Unit) || string.IsNullOrWhiteSpace(r.Region))))
                throw new InvalidDataException("MenuSourceInvalid");
    }

    public static 음식자료선택결과 Select(IReadOnlyList<음식자료판매후보> candidates, string hash,
        bool mealNeeded, decimal budget, long preferredRecipeId, DateOnly today)
    {
        var lines = candidates.OrderBy(x => x.Source.RecipeId).Select(x =>
            $"{x.Source.Title}: " + (!x.Available ? "판매 불가" : x.SalePrice <= 0 || x.SalePrice > budget ? "예산/가격 조건 불충족" : "선택 가능")).ToArray();
        var chosen = mealNeeded ? candidates.Where(x => x.Available && x.MenuId > 0 && x.SalePrice > 0 && x.SalePrice <= budget)
            .OrderByDescending(x => x.Source.RecipeId == preferredRecipeId).ThenBy(x => x.Source.RecipeId).FirstOrDefault() : null;
        if (chosen is null) return new(hash, 0, 0, "", mealNeeded ? "예산 내 판매 가능 메뉴 없음 · 주문 대기" : "식사 필요 없음 · 주문 대기",
            "", "", 0, "검증용 합성 판매가 · 실제 음식점 가격 아님", lines);
        var m = chosen.Source;
        var reference = m.Reference is { } r
            ? $"KAMIS {m.Ingredient} {r.Date:yyyy-MM-dd} · {r.Region} · {r.PriceKrw:0.##}원/{r.Unit} · "
                + (r.Date > today ? "미래 기준일 확인 필요" : today.DayNumber - r.Date.DayNumber > 30 ? "오래된 참고값" : "과거 관측 참고값")
                + $" · {m.MappingStatus} · 관측 ID {r.RecordId} · 판매가/원가로 사용 안 함"
            : "KAMIS 참고 자료 없음 · 판매가/원가 추정 안 함";
        return new(hash, m.RecipeId, chosen.MenuId, m.Title,
            $"식사 필요 · 예산 {budget:0}원 이내 · " + (m.RecipeId == preferredRecipeId ? "선호 메뉴" : "판매 가능한 대안"),
            $"식약처 {m.SourceKey} / 자료 {m.RecipeId} · 수집 {m.ObservedAtUtc:yyyy-MM-dd} · 재료 {m.Ingredient} · 내부 검증용 이름 참조\n{m.SourceUrl}",
            reference, chosen.SalePrice, "검증용 합성 판매가 · 실제 음식점 가격 아님", lines);
    }
}
