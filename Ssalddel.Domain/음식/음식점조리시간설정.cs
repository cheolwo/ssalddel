using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.음식;

[Table("음식점조리시간설정")]
public sealed class 음식점조리시간설정
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("음식점_id")]
    public long 음식점Id { get; set; }

    [Column("메뉴_id")]
    public long? 메뉴Id { get; set; }

    [Column("시작_분")]
    public int 시작분 { get; set; }

    [Column("종료_분")]
    public int 종료분 { get; set; }

    [Column("조리_예상_분")]
    public int 조리예상분 { get; set; }

    [Column("설정_revision")]
    public long 설정Revision { get; set; }

    [Column("변경_요청_id")]
    public Guid 변경요청Id { get; set; }

    [Column("변경_user_id")]
    [MaxLength(450)]
    public string 변경UserId { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}
