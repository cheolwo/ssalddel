using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.배차;

namespace 살뜰.Infrastructure.Persistence.Configurations.Dispatch;

public sealed class 운영배차활동사건Configuration : IEntityTypeConfiguration<운영배차활동사건>
{
    public void Configure(EntityTypeBuilder<운영배차활동사건> builder)
    {
        builder.ToTable("운영배차활동사건");
        builder.HasKey(x => x.사건StableId);

        builder.Property(x => x.사건StableId).HasMaxLength(128);
        builder.Property(x => x.운영시장시간대Id).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ShiftId).HasMaxLength(128);
        builder.Property(x => x.제안Id).HasMaxLength(128);
        builder.Property(x => x.주문Id).HasMaxLength(128);
        builder.Property(x => x.업무시도Id).HasMaxLength(220);
        builder.Property(x => x.주체Id).HasMaxLength(191).IsRequired();
        builder.Property(x => x.주체역할Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.사건유형Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.제안유효성Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.책임Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.사유Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.상태값Code).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => new { x.주체Id, x.발생시각Utc });
        builder.HasIndex(x => x.주문Id);
        builder.HasIndex(x => x.제안Id);
        builder.HasIndex(x => x.업무시도Id);
    }
}
