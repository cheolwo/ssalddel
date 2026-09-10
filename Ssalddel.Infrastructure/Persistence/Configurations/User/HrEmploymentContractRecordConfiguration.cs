using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.사용자;

namespace 살뜰.Infrastructure.Persistence.Configurations.User;

public sealed class HrEmploymentContractRecordConfiguration : IEntityTypeConfiguration<HrEmploymentContractRecord>
{
    public void Configure(EntityTypeBuilder<HrEmploymentContractRecord> builder)
    {
        // 기존 migration/DatabaseCompatibilityInitializer의 3열 인덱스와 일치시킨다.
        // ScopeId까지 포함하면 utf8mb4에서 MySQL의 3072-byte 한도를 초과한다.
        builder.HasIndex(x => new { x.WorkerUserId, x.EmployerScopeType, x.ContractStatus });
    }
}
