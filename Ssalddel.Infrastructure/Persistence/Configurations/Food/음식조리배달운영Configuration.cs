using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.음식;

namespace 살뜰.Infrastructure.Persistence.Configurations.Food;

public sealed class 음식점조리시간설정Configuration : IEntityTypeConfiguration<음식점조리시간설정>
{
    public void Configure(EntityTypeBuilder<음식점조리시간설정> builder)
    {
        builder.HasIndex(x => new { x.음식점Id, x.설정Revision, x.메뉴Id, x.시작분, x.종료분 }).IsUnique();
        builder.HasIndex(x => new { x.음식점Id, x.설정Revision });
        builder.HasIndex(x => x.변경요청Id);
    }
}

public sealed class 음식배달시도Configuration : IEntityTypeConfiguration<음식배달시도>
{
    public void Configure(EntityTypeBuilder<음식배달시도> builder)
    {
        builder.HasIndex(x => x.시도StableId).IsUnique();
        builder.HasIndex(x => new { x.주문번호, x.시도순번 }).IsUnique();
        builder.HasIndex(x => new { x.기사Id, x.상태Code, x.수락시각Utc });
        builder.HasIndex(x => x.마지막요청Id);
        builder.HasIndex(x => x.검토요청Id);
    }
}
