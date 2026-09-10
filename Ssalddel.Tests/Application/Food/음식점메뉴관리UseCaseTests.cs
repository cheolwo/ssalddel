using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed class 음식점메뉴관리UseCaseTests
{
    [Fact]
    public async Task 같은음식점과메뉴내용의재등록은_기존메뉴를반환한다()
    {
        await using var db = CreateContext();
        db.음식점공개프로필.Add(new 음식점공개프로필 { Id = 17, 상호명 = "분식집" });
        await db.SaveChangesAsync();
        var useCase = new 음식점메뉴관리UseCase(db);
        var request = new 음식점메뉴등록요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            메뉴명 = "김밥",
            설명 = "기본 김밥",
            판매가 = 4000,
            공개여부 = true
        };

        var first = await useCase.등록Async(17, request, CancellationToken.None);
        request.클라이언트요청Id = Guid.NewGuid();
        var retried = await useCase.등록Async(17, request, CancellationToken.None);

        Assert.True(first.새로생성됨);
        Assert.False(retried.새로생성됨);
        Assert.Equal(first.Id, retried.Id);
        Assert.Equal(1, await db.음식점메뉴.CountAsync());
    }

    [Fact]
    public async Task 수정은_음식점범위와Revision을검사한다()
    {
        await using var db = CreateContext();
        db.음식점공개프로필.Add(new 음식점공개프로필 { Id = 17, 상호명 = "분식집" });
        await db.SaveChangesAsync();
        var useCase = new 음식점메뉴관리UseCase(db);
        var created = await useCase.등록Async(17, new 음식점메뉴등록요청
        {
            클라이언트요청Id = Guid.NewGuid(), 메뉴명 = "김밥", 판매가 = 4000, 공개여부 = true
        }, CancellationToken.None);

        Assert.Null(await useCase.수정Async(18, created.Id, new 음식점메뉴수정요청
        {
            예상Revision = created.Revision, 메뉴명 = "김밥", 판매가 = 4500
        }, CancellationToken.None));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => useCase.수정Async(17, created.Id,
            new 음식점메뉴수정요청 { 예상Revision = created.Revision - 1, 메뉴명 = "김밥", 판매가 = 4500 },
            CancellationToken.None));
        var updated = await useCase.수정Async(17, created.Id, new 음식점메뉴수정요청
        {
            예상Revision = created.Revision, 메뉴명 = "김밥", 판매가 = 4500, 품절여부 = true
        }, CancellationToken.None);

        Assert.Equal(4500, updated!.판매가);
        Assert.True(updated.품절여부);
        Assert.NotEqual(created.Revision, updated.Revision);
    }

    private static SsalddelContext CreateContext() => new(
        new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options,
        new PassThroughEncryptionService());

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
