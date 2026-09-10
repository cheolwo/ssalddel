using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.사용자;

namespace 살뜰.Infrastructure.Persistence.Configurations.User;

public sealed class HrRoleAssignmentRecordConfiguration : IEntityTypeConfiguration<HrRoleAssignmentRecord>
{
    public void Configure(EntityTypeBuilder<HrRoleAssignmentRecord> builder)
    {
        // 기존 migration/호환 초기화와 같은 키: ScopeId 추가 시 utf8mb4 키 길이 초과.
        builder.HasIndex(x => new { x.UserId, x.ScopeType, x.RoleCode, x.IsActive });
    }
}
