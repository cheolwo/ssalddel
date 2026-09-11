using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class SyncPublicDataHrAndFreightContinuity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hr_role_assignments_user_id_scope_type_scope_id_role_code_is~",
                table: "hr_role_assignments");

            migrationBuilder.DropIndex(
                name: "IX_hr_employment_contracts_worker_user_id_employer_scope_type_e~",
                table: "hr_employment_contracts");

            migrationBuilder.CreateTable(
                name: "화물다음콜예약",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReservationId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    기사Id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    활성예약기사Key = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    의뢰Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    현재운송의뢰Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RecommendationRound = table.Column<int>(type: "int", nullable: false),
                    보유시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    만료시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    음성알림예정시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    음성알림발송시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    추천점수 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    기사최소지급액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    운임부족금액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    반환사유Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_화물다음콜예약", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "화물연속배차상태",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    기사Id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    활성여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    마지막ClientRequestId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    마지막유상운송완료시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    다음유상픽업시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    변경시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_화물연속배차상태", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "화물운송시간약속",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    의뢰Id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    기사Id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    목표도착시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    최종도착한계시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    화주최종요청시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    잠금완충분 = table.Column<int>(type: "int", nullable: false),
                    픽업서비스분 = table.Column<int>(type: "int", nullable: false),
                    하차서비스분 = table.Column<int>(type: "int", nullable: false),
                    기준경로분 = table.Column<int>(type: "int", nullable: false),
                    보수경로분 = table.Column<int>(type: "int", nullable: false),
                    시간약속Revision = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    잠금시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_화물운송시간약속", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_building_category_catalog",
                columns: table => new
                {
                    CategoryCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayNameKo = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DescriptionKo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorldRoleCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PresentationEligible = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_building_category_catalog", x => x.CategoryCode);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_building_register_titles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RegisterManagementPk = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegisterKindCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegisterTypeCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SigunguCode = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LegalDongCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LandLot = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RoadAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedRoadAddressKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuildingName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DongName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MainPurposeCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MainPurposeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StructureCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StructureName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuildingAreaSquareMeters = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    TotalFloorAreaSquareMeters = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    SiteAreaSquareMeters = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    OfficialBuildingCoveragePercent = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    OfficialFloorAreaRatioPercent = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    HeightMeters = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    AboveGroundFloorCount = table.Column<int>(type: "int", nullable: true),
                    UndergroundFloorCount = table.Column<int>(type: "int", nullable: true),
                    ApprovalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceSnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_building_register_titles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_data_ingestion_runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RunKey = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DatasetId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatusCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    FetchedCount = table.Column<int>(type: "int", nullable: false),
                    NormalizedCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    InsertedCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedCount = table.Column<int>(type: "int", nullable: false),
                    ExistingCount = table.Column<int>(type: "int", nullable: false),
                    SourceVersion = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorSummary = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_data_ingestion_runs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_data_region_mappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SourceId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExternalRegionCode = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegionStableId = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpatialPrecisionCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MappingRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_data_region_mappings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_licensed_business_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceDatasetId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpenServiceId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpenServiceName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ManagementNumber = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessTypeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LicenseCategoryName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessStatusCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessStatusName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetailedStatusCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetailedStatusName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LotAddress = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RoadAddress = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedRoadAddressKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceCoordinateX = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: true),
                    SourceCoordinateY = table.Column<decimal>(type: "decimal(20,8)", precision: 20, scale: 8, nullable: true),
                    SourceCoordinateReferenceSystem = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LicenseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SourceLastModifiedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    SourceRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceHashSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceSnapshotId = table.Column<long>(type: "bigint", nullable: true),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_licensed_business_records", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_administrative_building_category_aggregates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AdministrativeRegionStableId = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceVintage = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CategoryCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BuildingCount = table.Column<long>(type: "bigint", nullable: false),
                    BuildingAreaSquareMeters = table.Column<decimal>(type: "decimal(24,4)", precision: 24, scale: 4, nullable: false),
                    TotalFloorAreaSquareMeters = table.Column<decimal>(type: "decimal(24,4)", precision: 24, scale: 4, nullable: false),
                    NamedBuildingCount = table.Column<long>(type: "bigint", nullable: false),
                    GeometryLinkedCount = table.Column<long>(type: "bigint", nullable: false),
                    UnresolvedBuildingCount = table.Column<long>(type: "bigint", nullable: false),
                    EvidenceKindCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RuleRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AggregateHashSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_administrative_building_category_aggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_administrative_building_category_aggregates_public_bu~",
                        column: x => x.CategoryCode,
                        principalTable: "public_building_category_catalog",
                        principalColumn: "CategoryCode",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_building_business_aggregates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuildingRecordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SourceRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalBusinessCount = table.Column<int>(type: "int", nullable: false),
                    OpenBusinessCount = table.Column<int>(type: "int", nullable: false),
                    SuspendedBusinessCount = table.Column<int>(type: "int", nullable: false),
                    ClosedBusinessCount = table.Column<int>(type: "int", nullable: false),
                    UnresolvedStatusCount = table.Column<int>(type: "int", nullable: false),
                    EvidenceKindCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RuleRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AggregateHashSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_building_business_aggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_building_business_aggregates_public_building_register~",
                        column: x => x.BuildingRecordId,
                        principalTable: "public_building_register_titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_building_category_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuildingRecordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CategoryCode = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPrimary = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AssignmentMethodCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceKindCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RuleRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceMainPurposeCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceMainPurposeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClassifiedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_building_category_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_building_category_assignments_public_building_categor~",
                        column: x => x.CategoryCode,
                        principalTable: "public_building_category_catalog",
                        principalColumn: "CategoryCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_public_building_category_assignments_public_building_registe~",
                        column: x => x.BuildingRecordId,
                        principalTable: "public_building_register_titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_building_massing_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuildingRecordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ObservedAboveGroundFloorCount = table.Column<int>(type: "int", nullable: true),
                    EstimatedAboveGroundFloorCount = table.Column<int>(type: "int", nullable: true),
                    PresentationAboveGroundFloorCount = table.Column<int>(type: "int", nullable: false),
                    OfficialBuildingCoveragePercent = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    OfficialFloorAreaRatioPercent = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    SimpleBuildingToSiteRatioPercent = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    SimpleGrossFloorToSiteRatioPercent = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    SiteAreaSquareMeters = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    BuildingAreaSquareMeters = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    TotalFloorAreaSquareMeters = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    HeightMeters = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    EstimatedFloorHeightMeters = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: true),
                    FootprintTierCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HeightTierCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DensityTierCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceKindCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RuleRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProfileHashSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_building_massing_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_building_massing_profiles_public_building_register_ti~",
                        column: x => x.BuildingRecordId,
                        principalTable: "public_building_register_titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_building_region_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuildingRecordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LegalRegionStableId = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AdministrativeRegionStableId = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignmentMethodCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfidenceCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceVintage = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RuleRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_building_region_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_building_region_assignments_public_building_register_~",
                        column: x => x.BuildingRecordId,
                        principalTable: "public_building_register_titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_data_raw_snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FirstCollectionRunId = table.Column<long>(type: "bigint", nullable: false),
                    SourceId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DatasetId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceVersion = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CollectedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    EvidenceAsOfUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    ContentHashSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalFileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StorageContainer = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StorageObjectName = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StorageLocation = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_data_raw_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_data_raw_snapshots_public_data_ingestion_runs_FirstCo~",
                        column: x => x.FirstCollectionRunId,
                        principalTable: "public_data_ingestion_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_business_building_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BusinessRecordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuildingRecordId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AssignmentStatusCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssignmentMethodCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConfidenceCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CandidateBuildingCount = table.Column<int>(type: "int", nullable: false),
                    RuleRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvaluatedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_business_building_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_business_building_assignments_public_building_registe~",
                        column: x => x.BuildingRecordId,
                        principalTable: "public_building_register_titles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_public_business_building_assignments_public_licensed_busines~",
                        column: x => x.BusinessRecordId,
                        principalTable: "public_licensed_business_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_building_visual_composition_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BuildingMassingProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    VisualFamilyCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PresentationFloorCount = table.Column<int>(type: "int", nullable: false),
                    MiddleFloorRepeatCount = table.Column<int>(type: "int", nullable: false),
                    SiteCoverageTierCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SurroundingSpaceTierCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LodTierCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PresentationOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RuleRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlanHashSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_building_visual_composition_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_building_visual_composition_plans_public_building_mas~",
                        column: x => x.BuildingMassingProfileId,
                        principalTable: "public_building_massing_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "public_data_normalized_records",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RawSnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    RecordKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StableId = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DatasetId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegionStableId = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetricCode = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NumericValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    TextValue = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnitCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceAsOfUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    CollectedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    SpatialPrecisionCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TemporalPrecisionCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QualityCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LimitationCode = table.Column<string>(type: "varchar(240)", maxLength: 240, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DimensionKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceVersion = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataRevision = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_data_normalized_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_public_data_normalized_records_public_data_raw_snapshots_Raw~",
                        column: x => x.RawSnapshotId,
                        principalTable: "public_data_raw_snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "public_building_category_catalog",
                columns: new[] { "CategoryCode", "DescriptionKo", "DisplayNameKo", "PresentationEligible", "SortOrder", "WorldRoleCode" },
                values: new object[,]
                {
                    { "agriculture", "동물·식물 관련 시설 등 농업 생산을 지원하는 건축물", "농업", true, 20, "farm" },
                    { "business-office", "업무시설 등 사무 기능의 건축물", "업무", true, 50, "town" },
                    { "commercial", "근린생활·판매시설 등 생활권 상업 건축물", "상업·생활", true, 40, "town" },
                    { "culture-tourism", "문화·집회·숙박·관광·운동 관련 건축물", "문화·관광", true, 100, "town" },
                    { "education-research", "교육연구시설", "교육·연구", true, 80, "civic" },
                    { "industrial", "공장 등 제조·산업 기능의 건축물", "산업", true, 70, "industrial" },
                    { "logistics-storage", "창고시설 등 보관·적재와 관계되는 건축물", "물류·창고", true, 30, "hub" },
                    { "medical-welfare", "의료시설과 노유자시설", "의료·복지", true, 90, "civic" },
                    { "other", "공식 주용도는 있으나 현재 규칙에 대응하지 않는 건축물", "기타", false, 900, "generic" },
                    { "public-community", "공공·안전·공동체 기능으로 검토할 건축물", "공공·공동체", true, 60, "civic" },
                    { "religious", "종교시설", "종교", true, 130, "settlement" },
                    { "residential", "단독·공동주택 등 사람이 거주하는 건축물", "주거", true, 10, "settlement" },
                    { "transport", "운수시설과 자동차 관련 시설", "교통", true, 110, "transport" },
                    { "unresolved", "공식 주용도가 없거나 행정동 배정·분류가 해결되지 않은 건축물", "미분류", false, 999, "unresolved" },
                    { "utility-infrastructure", "발전·방송통신·자원순환·위험물 처리 관련 건축물", "기반시설", true, 120, "infrastructure" }
                });

            migrationBuilder.InsertData(
                table: "public_data_region_mappings",
                columns: new[] { "Id", "ExternalRegionCode", "MappingRevision", "RegionStableId", "SourceId", "SpatialPrecisionCode", "ValidFromUtc", "ValidToUtc" },
                values: new object[,]
                {
                    { -1003L, "CHN", "iso3166-1-alpha3-v2026-08", "country:cn", "world-bank-indicators", "country", null, null },
                    { -1002L, "USA", "iso3166-1-alpha3-v2026-08", "country:us", "world-bank-indicators", "country", null, null },
                    { -1001L, "KOR", "iso3166-1-alpha3-v2026-08", "country:kr", "world-bank-indicators", "country", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_role_assignments_user_id_scope_type_role_code_is_active",
                table: "hr_role_assignments",
                columns: new[] { "user_id", "scope_type", "role_code", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_contracts_worker_user_id_employer_scope_type_c~",
                table: "hr_employment_contracts",
                columns: new[] { "worker_user_id", "employer_scope_type", "contract_status" });

            migrationBuilder.CreateIndex(
                name: "IX_화물다음콜예약_기사Id_상태Code_만료시각Utc",
                table: "화물다음콜예약",
                columns: new[] { "기사Id", "상태Code", "만료시각Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_화물다음콜예약_의뢰Id_상태Code",
                table: "화물다음콜예약",
                columns: new[] { "의뢰Id", "상태Code" });

            migrationBuilder.CreateIndex(
                name: "IX_화물다음콜예약_활성예약기사Key",
                table: "화물다음콜예약",
                column: "활성예약기사Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_화물다음콜예약_ReservationId",
                table: "화물다음콜예약",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_화물연속배차상태_기사Id",
                table: "화물연속배차상태",
                column: "기사Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_화물연속배차상태_활성여부_UpdatedAt",
                table: "화물연속배차상태",
                columns: new[] { "활성여부", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_화물운송시간약속_기사Id_최종도착한계시각Utc",
                table: "화물운송시간약속",
                columns: new[] { "기사Id", "최종도착한계시각Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_화물운송시간약속_의뢰Id_Revision",
                table: "화물운송시간약속",
                columns: new[] { "의뢰Id", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_administrative_building_category_aggregates_Administr~",
                table: "public_administrative_building_category_aggregates",
                columns: new[] { "AdministrativeRegionStableId", "SourceVintage", "CategoryCode", "RuleRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_administrative_building_category_aggregates_CategoryC~",
                table: "public_administrative_building_category_aggregates",
                column: "CategoryCode");

            migrationBuilder.CreateIndex(
                name: "IX_public_building_business_aggregates_BuildingRecordId_SourceR~",
                table: "public_building_business_aggregates",
                columns: new[] { "BuildingRecordId", "SourceRevision", "RuleRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_building_category_assignments_BuildingRecordId_RuleRe~",
                table: "public_building_category_assignments",
                columns: new[] { "BuildingRecordId", "RuleRevision", "IsPrimary" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_building_category_assignments_CategoryCode_RuleRevisi~",
                table: "public_building_category_assignments",
                columns: new[] { "CategoryCode", "RuleRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_public_building_massing_profiles_BuildingRecordId_RuleRevisi~",
                table: "public_building_massing_profiles",
                columns: new[] { "BuildingRecordId", "RuleRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_building_region_assignments_AdministrativeRegionStabl~",
                table: "public_building_region_assignments",
                columns: new[] { "AdministrativeRegionStableId", "SourceVintage" });

            migrationBuilder.CreateIndex(
                name: "IX_public_building_region_assignments_BuildingRecordId_RuleRevi~",
                table: "public_building_region_assignments",
                columns: new[] { "BuildingRecordId", "RuleRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_building_register_titles_RegisterManagementPk_SourceR~",
                table: "public_building_register_titles",
                columns: new[] { "RegisterManagementPk", "SourceRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_building_register_titles_SigunguCode_LegalDongCode_Va~",
                table: "public_building_register_titles",
                columns: new[] { "SigunguCode", "LegalDongCode", "ValidToUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_public_building_visual_composition_plans_BuildingMassingProf~",
                table: "public_building_visual_composition_plans",
                columns: new[] { "BuildingMassingProfileId", "RuleRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_business_building_assignments_BuildingRecordId_Assign~",
                table: "public_business_building_assignments",
                columns: new[] { "BuildingRecordId", "AssignmentStatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_public_business_building_assignments_BusinessRecordId_RuleRe~",
                table: "public_business_building_assignments",
                columns: new[] { "BusinessRecordId", "RuleRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_data_ingestion_runs_RunKey",
                table: "public_data_ingestion_runs",
                column: "RunKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_data_ingestion_runs_SourceId_DatasetId_StartedAtUtc",
                table: "public_data_ingestion_runs",
                columns: new[] { "SourceId", "DatasetId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_public_data_normalized_records_RawSnapshotId",
                table: "public_data_normalized_records",
                column: "RawSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_public_data_normalized_records_RecordKey",
                table: "public_data_normalized_records",
                column: "RecordKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_data_normalized_records_RegionStableId_MetricCode_Evi~",
                table: "public_data_normalized_records",
                columns: new[] { "RegionStableId", "MetricCode", "EvidenceAsOfUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_public_data_raw_snapshots_FirstCollectionRunId",
                table: "public_data_raw_snapshots",
                column: "FirstCollectionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_public_data_raw_snapshots_SourceId_DatasetId_ContentHashSha2~",
                table: "public_data_raw_snapshots",
                columns: new[] { "SourceId", "DatasetId", "ContentHashSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_data_region_mappings_SourceId_ExternalRegionCode",
                table: "public_data_region_mappings",
                columns: new[] { "SourceId", "ExternalRegionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_licensed_business_records_BusinessStatusCode_SourceRe~",
                table: "public_licensed_business_records",
                columns: new[] { "BusinessStatusCode", "SourceRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_public_licensed_business_records_NormalizedRoadAddressKey_So~",
                table: "public_licensed_business_records",
                columns: new[] { "NormalizedRoadAddressKey", "SourceRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_public_licensed_business_records_SourceId_OpenServiceId_Mana~",
                table: "public_licensed_business_records",
                columns: new[] { "SourceId", "OpenServiceId", "ManagementNumber", "SourceRevision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "화물다음콜예약");

            migrationBuilder.DropTable(
                name: "화물연속배차상태");

            migrationBuilder.DropTable(
                name: "화물운송시간약속");

            migrationBuilder.DropTable(
                name: "public_administrative_building_category_aggregates");

            migrationBuilder.DropTable(
                name: "public_building_business_aggregates");

            migrationBuilder.DropTable(
                name: "public_building_category_assignments");

            migrationBuilder.DropTable(
                name: "public_building_region_assignments");

            migrationBuilder.DropTable(
                name: "public_building_visual_composition_plans");

            migrationBuilder.DropTable(
                name: "public_business_building_assignments");

            migrationBuilder.DropTable(
                name: "public_data_normalized_records");

            migrationBuilder.DropTable(
                name: "public_data_region_mappings");

            migrationBuilder.DropTable(
                name: "public_building_category_catalog");

            migrationBuilder.DropTable(
                name: "public_building_massing_profiles");

            migrationBuilder.DropTable(
                name: "public_licensed_business_records");

            migrationBuilder.DropTable(
                name: "public_data_raw_snapshots");

            migrationBuilder.DropTable(
                name: "public_building_register_titles");

            migrationBuilder.DropTable(
                name: "public_data_ingestion_runs");

            migrationBuilder.DropIndex(
                name: "IX_hr_role_assignments_user_id_scope_type_role_code_is_active",
                table: "hr_role_assignments");

            migrationBuilder.DropIndex(
                name: "IX_hr_employment_contracts_worker_user_id_employer_scope_type_c~",
                table: "hr_employment_contracts");

            migrationBuilder.CreateIndex(
                name: "IX_hr_role_assignments_user_id_scope_type_scope_id_role_code_is~",
                table: "hr_role_assignments",
                columns: new[] { "user_id", "scope_type", "scope_id", "role_code", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_employment_contracts_worker_user_id_employer_scope_type_e~",
                table: "hr_employment_contracts",
                columns: new[] { "worker_user_id", "employer_scope_type", "employer_scope_id", "contract_status" });
        }
    }
}
