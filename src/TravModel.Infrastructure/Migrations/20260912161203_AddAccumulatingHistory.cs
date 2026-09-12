using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravModel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccumulatingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HistoricalStarts_HorseId_ExternalRaceId",
                table: "HistoricalStarts");

            migrationBuilder.AddColumn<Guid>(
                name: "IngestionRunId",
                table: "Observations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanonicalStartKey",
                table: "HistoricalStarts",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompletenessFlags",
                table: "HistoricalStarts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstSeenAtUtc",
                table: "HistoricalStarts",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenAtUtc",
                table: "HistoricalStarts",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ObservedAtUtc",
                table: "HistoricalStarts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetrievedAtUtc",
                table: "HistoricalStarts",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "HistoricalStarts",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "HistoricalStarts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ExternalIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalStartRevisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoricalStartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TrackName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RaceNumber = table.Column<int>(type: "int", nullable: false),
                    DistanceMetres = table.Column<int>(type: "int", nullable: false),
                    StartMethod = table.Column<int>(type: "int", nullable: false),
                    PostPosition = table.Column<int>(type: "int", nullable: false),
                    FinishPosition = table.Column<int>(type: "int", nullable: true),
                    KilometerTimeSeconds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    Odds = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    Shoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sulky = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrackCondition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrizeMoneySek = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Galloped = table.Column<bool>(type: "bit", nullable: false),
                    RaceComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalStartRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalStartRevisions_HistoricalStarts_HistoricalStartId",
                        column: x => x.HistoricalStartId,
                        principalTable: "HistoricalStarts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdentityReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CandidateName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CandidateEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Cursor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceQualifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Capability = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BaseUri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TermsUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RobotsUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheckedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceQualifications", x => x.Id);
                });

            migrationBuilder.Sql("""
                UPDATE HistoricalStarts
                SET CanonicalStartKey = CONCAT('legacy:', ExternalRaceId),
                    SourceName = 'Legacy',
                    RetrievedAtUtc = SYSUTCDATETIME(),
                    FirstSeenAtUtc = SYSUTCDATETIME(),
                    LastSeenAtUtc = SYSUTCDATETIME();

                INSERT INTO ExternalIdentities (Id, EntityType, EntityId, SourceName, ExternalId, FirstSeenAtUtc, LastSeenAtUtc, IsVerified)
                SELECT NEWID(), 'Track', Id, ExternalSource, ExternalId, SYSUTCDATETIME(), SYSUTCDATETIME(), 1 FROM Tracks;
                INSERT INTO ExternalIdentities (Id, EntityType, EntityId, SourceName, ExternalId, FirstSeenAtUtc, LastSeenAtUtc, IsVerified)
                SELECT NEWID(), 'Meeting', Id, ExternalSource, ExternalId, SYSUTCDATETIME(), SYSUTCDATETIME(), 1 FROM Meetings;
                INSERT INTO ExternalIdentities (Id, EntityType, EntityId, SourceName, ExternalId, FirstSeenAtUtc, LastSeenAtUtc, IsVerified)
                SELECT NEWID(), 'Race', Id, ExternalSource, ExternalId, SYSUTCDATETIME(), SYSUTCDATETIME(), 1 FROM Races;
                INSERT INTO ExternalIdentities (Id, EntityType, EntityId, SourceName, ExternalId, FirstSeenAtUtc, LastSeenAtUtc, IsVerified)
                SELECT NEWID(), 'Horse', Id, ExternalSource, ExternalId, SYSUTCDATETIME(), SYSUTCDATETIME(), 1 FROM Horses;
                INSERT INTO ExternalIdentities (Id, EntityType, EntityId, SourceName, ExternalId, FirstSeenAtUtc, LastSeenAtUtc, IsVerified)
                SELECT NEWID(), 'Person', Id, ExternalSource, ExternalId, SYSUTCDATETIME(), SYSUTCDATETIME(), 1 FROM People;
                INSERT INTO ExternalIdentities (Id, EntityType, EntityId, SourceName, ExternalId, FirstSeenAtUtc, LastSeenAtUtc, IsVerified)
                SELECT NEWID(), 'Starter', s.Id, r.ExternalSource, CONCAT(r.ExternalId, ':', h.ExternalId), SYSUTCDATETIME(), SYSUTCDATETIME(), 1
                FROM Starters s JOIN Races r ON r.Id = s.RaceId JOIN Horses h ON h.Id = s.HorseId;

                INSERT INTO SourceQualifications (Id, SourceName, Capability, BaseUri, Status, CheckedAtUtc)
                VALUES
                  (NEWID(), 'SvenskTravsport', 'recent-form', 'https://sportapp.travsport.se/', 'Pending', SYSUTCDATETIME()),
                  (NEWID(), 'ATG', 'recent-form', 'https://www.atg.se/', 'Pending', SYSUTCDATETIME()),
                  (NEWID(), 'ATG', 'equipment', 'https://www.atg.se/', 'Pending', SYSUTCDATETIME()),
                  (NEWID(), 'Skoinfo', 'equipment', 'https://skoinfo.se/', 'Pending', SYSUTCDATETIME()),
                  (NEWID(), 'Travmaskinen', 'recent-form', 'https://travmaskinen.se/', 'Pending', SYSUTCDATETIME());
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Observations_IngestionRunId",
                table: "Observations",
                column: "IngestionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalStarts_HorseId_CanonicalStartKey",
                table: "HistoricalStarts",
                columns: new[] { "HorseId", "CanonicalStartKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalStarts_SourceName_ExternalRaceId",
                table: "HistoricalStarts",
                columns: new[] { "SourceName", "ExternalRaceId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_EntityType_EntityId",
                table: "ExternalIdentities",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_EntityType_SourceName_ExternalId",
                table: "ExternalIdentities",
                columns: new[] { "EntityType", "SourceName", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalStartRevisions_ContentHash",
                table: "HistoricalStartRevisions",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalStartRevisions_HistoricalStartId_RetrievedAtUtc",
                table: "HistoricalStartRevisions",
                columns: new[] { "HistoricalStartId", "RetrievedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityReviews_EntityType_SourceName_ExternalId_Status",
                table: "IdentityReviews",
                columns: new[] { "EntityType", "SourceName", "ExternalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCheckpoints_Provider_Scope",
                table: "ProviderCheckpoints",
                columns: new[] { "Provider", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SourceQualifications_SourceName_Capability",
                table: "SourceQualifications",
                columns: new[] { "SourceName", "Capability" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Observations_IngestionRuns_IngestionRunId",
                table: "Observations",
                column: "IngestionRunId",
                principalTable: "IngestionRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Observations_IngestionRuns_IngestionRunId",
                table: "Observations");

            migrationBuilder.DropTable(
                name: "ExternalIdentities");

            migrationBuilder.DropTable(
                name: "HistoricalStartRevisions");

            migrationBuilder.DropTable(
                name: "IdentityReviews");

            migrationBuilder.DropTable(
                name: "ProviderCheckpoints");

            migrationBuilder.DropTable(
                name: "SourceQualifications");

            migrationBuilder.DropIndex(
                name: "IX_Observations_IngestionRunId",
                table: "Observations");

            migrationBuilder.DropIndex(
                name: "IX_HistoricalStarts_HorseId_CanonicalStartKey",
                table: "HistoricalStarts");

            migrationBuilder.DropIndex(
                name: "IX_HistoricalStarts_SourceName_ExternalRaceId",
                table: "HistoricalStarts");

            migrationBuilder.DropColumn(
                name: "IngestionRunId",
                table: "Observations");

            migrationBuilder.DropColumn(
                name: "CanonicalStartKey",
                table: "HistoricalStarts");

            migrationBuilder.DropColumn(
                name: "CompletenessFlags",
                table: "HistoricalStarts");

            migrationBuilder.DropColumn(
                name: "FirstSeenAtUtc",
                table: "HistoricalStarts");

            migrationBuilder.DropColumn(
                name: "LastSeenAtUtc",
                table: "HistoricalStarts");

            migrationBuilder.DropColumn(
                name: "ObservedAtUtc",
                table: "HistoricalStarts");

            migrationBuilder.DropColumn(
                name: "RetrievedAtUtc",
                table: "HistoricalStarts");

            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "HistoricalStarts");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "HistoricalStarts");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalStarts_HorseId_ExternalRaceId",
                table: "HistoricalStarts",
                columns: new[] { "HorseId", "ExternalRaceId" },
                unique: true);
        }
    }
}
