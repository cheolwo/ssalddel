using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.운송;

namespace 살뜰.Infrastructure.Persistence.Configurations.Transport;

public sealed class 화물연속배차상태Configuration : IEntityTypeConfiguration<화물연속배차상태>
{
    public void Configure(EntityTypeBuilder<화물연속배차상태> builder)
    {
        builder.ToTable("화물연속배차상태");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.기사Id).HasMaxLength(191).IsRequired();
        builder.Property(x => x.마지막ClientRequestId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.기사Id).IsUnique();
        builder.HasIndex(x => new { x.활성여부, x.UpdatedAt });
    }
}

public sealed class 화물운송시간약속Configuration : IEntityTypeConfiguration<화물운송시간약속>
{
    public void Configure(EntityTypeBuilder<화물운송시간약속> builder)
    {
        builder.ToTable("화물운송시간약속");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.의뢰Id).HasMaxLength(128).IsRequired();
        builder.Property(x => x.기사Id).HasMaxLength(191).IsRequired();
        builder.Property(x => x.시간약속Revision).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.의뢰Id, x.Revision }).IsUnique();
        builder.HasIndex(x => new { x.기사Id, x.최종도착한계시각Utc });
    }
}

public sealed class 화물다음콜예약Configuration : IEntityTypeConfiguration<화물다음콜예약>
{
    public void Configure(EntityTypeBuilder<화물다음콜예약> builder)
    {
        builder.ToTable("화물다음콜예약");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReservationId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.기사Id).HasMaxLength(191).IsRequired();
        builder.Property(x => x.활성예약기사Key).HasMaxLength(191);
        builder.Property(x => x.의뢰Id).HasMaxLength(128).IsRequired();
        builder.Property(x => x.현재운송의뢰Id).HasMaxLength(128).IsRequired();
        builder.Property(x => x.상태Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.반환사유Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.추천점수).HasPrecision(18, 2);
        builder.Property(x => x.기사최소지급액).HasPrecision(18, 2);
        builder.Property(x => x.운임부족금액).HasPrecision(18, 2);
        builder.HasIndex(x => x.ReservationId).IsUnique();
        builder.HasIndex(x => x.활성예약기사Key).IsUnique();
        builder.HasIndex(x => new { x.기사Id, x.상태Code, x.만료시각Utc });
        builder.HasIndex(x => new { x.의뢰Id, x.상태Code });
    }
}
