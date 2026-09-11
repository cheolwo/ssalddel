using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20260911030000_AddFoodDeliveryInterruptionRecovery")]
public sealed class AddFoodDeliveryInterruptionRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "플랫폼_참고_조리_분",
            table: "음식주문",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "음식점_선택_조리_분",
            table: "음식주문",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "적용_조리_분",
            table: "음식주문",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "조리_시간_결정_출처_code",
            table: "음식주문",
            type: "varchar(50)",
            maxLength: 50,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "업무시도Id",
            table: "운영배차활동사건",
            type: "varchar(220)",
            maxLength: 220,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex(
            name: "IX_운영배차활동사건_업무시도Id",
            table: "운영배차활동사건",
            column: "업무시도Id");

        migrationBuilder.CreateTable(
            name: "음식점조리시간설정",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                음식점_id = table.Column<long>(type: "bigint", nullable: false),
                메뉴_id = table.Column<long>(type: "bigint", nullable: true),
                시작_분 = table.Column<int>(type: "int", nullable: false),
                종료_분 = table.Column<int>(type: "int", nullable: false),
                조리_예상_분 = table.Column<int>(type: "int", nullable: false),
                설정_revision = table.Column<long>(type: "bigint", nullable: false),
                변경_요청_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                변경_user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_음식점조리시간설정", x => x.id))
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex(
            name: "IX_음식점조리시간설정_변경요청Id",
            table: "음식점조리시간설정",
            column: "변경_요청_id");
        migrationBuilder.CreateIndex(
            name: "IX_음식점조리시간설정_음식점Id_설정Revision",
            table: "음식점조리시간설정",
            columns: new[] { "음식점_id", "설정_revision" });
        migrationBuilder.CreateIndex(
            name: "IX_음식점조리시간설정_음식점Id_설정Revision_메뉴Id_시작분_종료분",
            table: "음식점조리시간설정",
            columns: new[] { "음식점_id", "설정_revision", "메뉴_id", "시작_분", "종료_분" },
            unique: true);

        migrationBuilder.CreateTable(
            name: "음식배달시도",
            columns: table => new
            {
                id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                시도_stable_id = table.Column<string>(type: "varchar(220)", maxLength: 220, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                주문번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                제안_id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                기사_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                추천_라운드 = table.Column<int>(type: "int", nullable: false),
                시도_순번 = table.Column<int>(type: "int", nullable: false),
                상태_code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                revision = table.Column<long>(type: "bigint", nullable: false),
                수락_시각_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                표시_준비_예정_시각_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                가게_도착_시각_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                픽업_완료_시각_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                중단_시각_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                전달_완료_시각_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                현장_대기_초 = table.Column<int>(type: "int", nullable: true),
                중단_사유_code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                중단_메모 = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                책임_code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                조리_지연_재배차_여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                재조리_요청_stable_id = table.Column<string>(type: "varchar(220)", maxLength: 220, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                재조리_요청_시각_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                유산_추정_여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                마지막_요청_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                검토_요청_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                검토_user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                검토_사유 = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                악용_확정_여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_음식배달시도", x => x.id))
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex(
            name: "IX_음식배달시도_시도StableId",
            table: "음식배달시도",
            column: "시도_stable_id",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_음식배달시도_주문번호_시도순번",
            table: "음식배달시도",
            columns: new[] { "주문번호", "시도_순번" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_음식배달시도_기사Id_상태Code_수락시각Utc",
            table: "음식배달시도",
            columns: new[] { "기사_id", "상태_code", "수락_시각_utc" });
        migrationBuilder.CreateIndex(
            name: "IX_음식배달시도_마지막요청Id",
            table: "음식배달시도",
            column: "마지막_요청_id");
        migrationBuilder.CreateIndex(
            name: "IX_음식배달시도_검토요청Id",
            table: "음식배달시도",
            column: "검토_요청_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "음식배달시도");
        migrationBuilder.DropTable(name: "음식점조리시간설정");
        migrationBuilder.DropIndex(name: "IX_운영배차활동사건_업무시도Id", table: "운영배차활동사건");
        migrationBuilder.DropColumn(name: "업무시도Id", table: "운영배차활동사건");
        migrationBuilder.DropColumn(name: "플랫폼_참고_조리_분", table: "음식주문");
        migrationBuilder.DropColumn(name: "음식점_선택_조리_분", table: "음식주문");
        migrationBuilder.DropColumn(name: "적용_조리_분", table: "음식주문");
        migrationBuilder.DropColumn(name: "조리_시간_결정_출처_code", table: "음식주문");
    }
}
