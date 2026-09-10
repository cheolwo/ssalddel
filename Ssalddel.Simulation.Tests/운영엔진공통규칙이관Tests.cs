using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules;
using Xunit;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "음식 배달 후보 점수와 창고 출고 배분의 공통 계산 규칙 호환을 검증한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3계약회귀,
    Boundary = "후보 계산 시험이며 실제 배차 확정·재고 저장·업무 완료 증거가 아니다.")]
public sealed class 운영엔진공통규칙이관Tests
{
    [Theory]
    [InlineData(1, 0, 0, 2, 86)]
    [InlineData(1, 10, 1, 7, -19)]
    [InlineData(5, 0, 2, 8, -168)]
    public void 음식배달은_기존픽업창과거리점수를유지한다(
        int distance, int readyAfter, int stage, int gap, int score)
    {
        var now = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var result = 음식배달픽업평가Policy.판정(distance, 0, now.AddMinutes(readyAfter), now, 30, 1, 2);
        Assert.Equal(stage, result.단계);
        Assert.Equal(gap, result.차이분);
        Assert.Equal(score, result.추천점수);
    }

    [Fact]
    public void 조리시각미확정은_시간벌점없이_거리와대기점수를사용한다()
    {
        var result = 음식배달픽업평가Policy.판정(2, 5, null, DateTime.MinValue, 0, -1, -1);
        Assert.Equal(81, result.추천점수);
        Assert.Equal("조리예정시각 미확정", result.설명);
    }

    [Fact]
    public void 동점기사와창고는_기존정렬우선순위를유지한다()
    {
        var evaluation = 음식배달픽업평가Policy.판정(1, 0, null, DateTime.MinValue, 30, 0, 0);
        Assert.Equal(new[] { "a", "b" }, 음식배달픽업평가Policy.정렬(
            new[] { "b", "a" }, _ => evaluation, _ => 0, _ => 1m, id => id));
        var a = new Stock(1, 20, 10, 1);
        var b = new Stock(2, 10, 10, 1);
        var plan = Plan(new[] { 1, 1 }, new[] { a, b }, new[] { a, b });
        Assert.All(plan, item => Assert.Equal(20, item.Candidate.Warehouse));
    }

    [Fact]
    public void 여러라인의같은재고는_최소가용량만한번배분한다()
    {
        var plan = Plan(new[] { 3, 3 }, new[] { new Stock(1, 10, 7, 1) }, new[] { new Stock(1, 10, 5, 1) });
        Assert.Equal(new[] { 3, 2 }, plan.Select(value => value.Quantity));
    }

    [Fact]
    public void 단일창고완결을_개별라인의높은점수보다우선한다()
    {
        var plan = Plan(new[] { 2, 2 },
            new[] { new Stock(1, 10, 2, 100), new Stock(2, 20, 2, 1) },
            new[] { new Stock(3, 30, 2, 100), new Stock(4, 20, 2, 1) });
        Assert.All(plan, value => Assert.Equal(20, value.Candidate.Warehouse));
    }

    [Fact]
    public void 단일창고가부족하면_후보순서대로분할하고_원본수량은변경하지않는다()
    {
        var stock = new[] { new Stock(1, 10, 2, 10), new Stock(2, 20, 1, 5) };
        var plan = Plan(new[] { 4 }, stock);
        Assert.Equal(3, plan.Sum(value => value.Quantity));
        Assert.Equal(new long[] { 10, 20 }, plan.Select(value => value.Candidate.Warehouse));
        Assert.Equal(new[] { 2, 1 }, stock.Select(value => value.Available));
    }

    private static IReadOnlyList<(int LineIndex, Stock Candidate, int Quantity)> Plan(
        int[] quantities, params Stock[][] candidates)
        => 창고출고배분Policy.판정(quantities, candidates.Select(value => (IReadOnlyList<Stock>)value).ToArray(),
            value => value.Id, value => value.Warehouse, value => value.Available, value => value.Score, _ => false);

    private sealed record Stock(long Id, long Warehouse, int Available, decimal Score);
}
