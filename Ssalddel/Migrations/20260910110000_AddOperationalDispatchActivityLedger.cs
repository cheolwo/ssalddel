using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20260910110000_AddOperationalDispatchActivityLedger")]
public sealed class AddOperationalDispatchActivityLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "운영배차활동사건",
            columns: table => new
            {
                사건StableId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                발생시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                운영시장시간대Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ShiftId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                제안Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                주문Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                주체Id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                주체역할Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                사건유형Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                제안유효성Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                책임Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                사유Code = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                상태값Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                지표단위수 = table.Column<int>(type: "int", nullable: false),
                기록시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_운영배차활동사건", x => x.사건StableId);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_운영배차활동사건_제안Id",
            table: "운영배차활동사건",
            column: "제안Id");

        migrationBuilder.CreateIndex(
            name: "IX_운영배차활동사건_주문Id",
            table: "운영배차활동사건",
            column: "주문Id");

        migrationBuilder.CreateIndex(
            name: "IX_운영배차활동사건_주체Id_발생시각Utc",
            table: "운영배차활동사건",
            columns: new[] { "주체Id", "발생시각Utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "운영배차활동사건");
    }
}
