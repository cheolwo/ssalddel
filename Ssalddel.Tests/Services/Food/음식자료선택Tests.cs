using Ssalddel.Services.Development.FoodObserver;

namespace Ssalddel.Tests.Services.Food;

public sealed class 음식자료선택Tests
{
    private static 음식자료후보 Source(long id = 7) => new(id, "된장국", "mfds-cookrcp01", new('a',64),
        DateTimeOffset.Parse("2026-07-21T00:00:00Z"), "https://www.foodsafetykorea.go.kr/", "공공데이터포털 이용허락범위 제한 없음",
        "양파",18,"AutoMatched",new(1,"245",new(2026,7,30),1876,"1kg","전국","https://www.kamis.or.kr/"));
    private static 음식자료선택결과 Choose(음식자료판매후보[] candidates, bool need = true, decimal budget = 5000)
        => 음식자료선택Policy.Select(candidates,"hash",need,budget,7,new(2026,9,8));
    [Fact] public void 같은입력은_순서와무관하게_선호메뉴와_검증판매가를_선택한다()
    {
        음식자료판매후보[] input = [new(Source(3),1,5000,true),new(Source(),2,5000,true)];
        var a = Choose(input); var b = Choose(input.Reverse().ToArray());
        Assert.Equal(7,a.RecipeId); Assert.Equal(a.MenuId,b.MenuId); Assert.Equal(a.Candidates,b.Candidates);
        Assert.Equal(5000,a.SalePrice); Assert.Contains("검증용",a.PriceBasis);
        Assert.Contains("1876",a.Reference); Assert.Contains("오래된",a.Reference); Assert.Contains("2026-07-30",a.Reference);
    }
    [Theory] [InlineData(false,5000,true)] [InlineData(true,4999,true)] [InlineData(true,5000,false)]
    public void 필요없음_예산부족_품절은_주문없이_대기한다(bool needed, decimal budget, bool available)
    { var result = Choose([new(Source(),2,5000,available)],needed,budget); Assert.Equal(0,result.MenuId); Assert.Contains("대기",result.Reason); }
    [Fact] public void 선호품절은_가능한대안으로_이유를_남긴다()
    { var result = Choose([new(Source(),2,5000,false),new(Source(3),1,5000,true)]); Assert.Equal(3,result.RecipeId); Assert.Contains("대안",result.Reason); Assert.Contains(result.Candidates,x=>x.Contains("판매 불가")); }
    [Fact] public void 가격참고없음은_원가나가격을_날조하지_않는다()
    { var result=Choose([new(Source() with {Reference=null},2,5000,true)]); Assert.Equal(5000,result.SalePrice); Assert.Contains("참고 자료 없음",result.Reference); }
    [Fact] public void 비어있거나_중복되거나_권리없는_자료는_거부한다()
    {
        var data = new 음식자료사본("food-observer-menus.r1",DateTimeOffset.Parse("2026-09-08T00:00:00Z"),[Source()]);
        음식자료선택Policy.Validate(data);
        Assert.Throws<InvalidDataException>(()=>음식자료선택Policy.Validate(data with {Menus=[]}));
        Assert.Throws<InvalidDataException>(()=>음식자료선택Policy.Validate(data with {Menus=[Source(),Source()]}));
        Assert.Throws<InvalidDataException>(()=>음식자료선택Policy.Validate(data with {Menus=[Source() with {License="unknown"}]}));
        Assert.Throws<InvalidDataException>(()=>음식자료선택Policy.Validate(data with {Menus=[Source() with {Checksum="bad"}]}));
        Assert.Throws<FileNotFoundException>(()=>음식자료선택Policy.Read(Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json")));
    }
    [Fact] public void 주문재조회는_선택한메뉴_수량_가격과_일치해야한다()
    {
        var selection=Choose([new(Source(),2,5000,true)]);
        var item=new Ssalddel.Contracts.Food.음식주문상품Dto {메뉴Id=2,상품명="된장국",수량=1,단가=5000};
        Assert.True(음식자료선택Policy.OrderMatches(selection,[item]));
        item.수량=2; Assert.False(음식자료선택Policy.OrderMatches(selection,[item]));
        item.수량=1; item.메뉴Id=3; Assert.False(음식자료선택Policy.OrderMatches(selection,[item]));
        item.메뉴Id=2; item.단가=1876; Assert.False(음식자료선택Policy.OrderMatches(selection,[item]));
    }
}
