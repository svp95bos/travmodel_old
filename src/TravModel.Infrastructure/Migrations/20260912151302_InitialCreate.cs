using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravModel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Horses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalSource = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BirthYear = table.Column<int>(type: "int", nullable: true),
                    Sex = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Horses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngestionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordsRead = table.Column<int>(type: "int", nullable: false),
                    RecordsWritten = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TrainedThroughUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TrainingDatasetFingerprint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArtifactPath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Observations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RawValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    IsAuthoritative = table.Column<bool>(type: "bit", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Observations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalSource = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExternalSource = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CircumferenceMetres = table.Column<int>(type: "int", nullable: true),
                    HomestretchLengthMetres = table.Column<int>(type: "int", nullable: true),
                    WidthMetres = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    HasOpenStretch = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RawArtifactManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IngestionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawArtifactManifests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawArtifactManifests_IngestionRuns_IngestionRunId",
                        column: x => x.IngestionRunId,
                        principalTable: "IngestionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaceCount = table.Column<int>(type: "int", nullable: false),
                    LogLoss = table.Column<double>(type: "float", nullable: false),
                    BrierScore = table.Column<double>(type: "float", nullable: false),
                    CalibrationError = table.Column<double>(type: "float", nullable: false),
                    TopOneAccuracy = table.Column<double>(type: "float", nullable: false),
                    Coverage = table.Column<double>(type: "float", nullable: false),
                    FlatStakeRoi = table.Column<double>(type: "float", nullable: true),
                    LogLossImprovementLower95 = table.Column<double>(type: "float", nullable: true),
                    WindowStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WindowEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelEvaluations_ModelVersions_ModelVersionId",
                        column: x => x.ModelVersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalStarts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalRaceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
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
                    RaceComment = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalStarts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalStarts_Horses_HorseId",
                        column: x => x.HorseId,
                        principalTable: "Horses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalStarts_People_DriverId",
                        column: x => x.DriverId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricalStarts_People_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalSource = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Races",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalSource = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaceNumber = table.Column<int>(type: "int", nullable: false),
                    ScheduledStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DistanceMetres = table.Column<int>(type: "int", nullable: false),
                    StartMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RaceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConditionsText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Races", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Races_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BettingMarketMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Product = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LegNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettingMarketMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BettingMarketMemberships_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PredictionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureCutoffUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DatasetFingerprint = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionRuns_ModelVersions_ModelVersionId",
                        column: x => x.ModelVersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PredictionRuns_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Starters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HorseNumber = table.Column<int>(type: "int", nullable: false),
                    PostPosition = table.Column<int>(type: "int", nullable: false),
                    DistanceMetres = table.Column<int>(type: "int", nullable: false),
                    IsScratched = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Starters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Starters_Horses_HorseId",
                        column: x => x.HorseId,
                        principalTable: "Horses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Starters_People_DriverId",
                        column: x => x.DriverId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Starters_People_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Starters_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StarterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinishPosition = table.Column<int>(type: "int", nullable: true),
                    FinishCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KilometerTimeSeconds = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: true),
                    WinningMarginMetres = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: true),
                    PrizeMoneySek = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Galloped = table.Column<bool>(type: "bit", nullable: false),
                    RaceComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetrievedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Results_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Results_Starters_StarterId",
                        column: x => x.StarterId,
                        principalTable: "Starters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StarterPredictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredictionRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StarterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WinProbability = table.Column<double>(type: "float", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    MarketOdds = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: true),
                    Won = table.Column<bool>(type: "bit", nullable: true),
                    SettledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarterPredictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StarterPredictions_PredictionRuns_PredictionRunId",
                        column: x => x.PredictionRunId,
                        principalTable: "PredictionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StarterPredictions_Starters_StarterId",
                        column: x => x.StarterId,
                        principalTable: "Starters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BettingMarketMemberships_RaceId_Product_ProductId",
                table: "BettingMarketMemberships",
                columns: new[] { "RaceId", "Product", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalStarts_DriverId",
                table: "HistoricalStarts",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalStarts_HorseId_ExternalRaceId",
                table: "HistoricalStarts",
                columns: new[] { "HorseId", "ExternalRaceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalStarts_TrainerId",
                table: "HistoricalStarts",
                column: "TrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Horses_ExternalSource_ExternalId",
                table: "Horses",
                columns: new[] { "ExternalSource", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ExternalSource_ExternalId",
                table: "Meetings",
                columns: new[] { "ExternalSource", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_TrackId",
                table: "Meetings",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelEvaluations_ModelVersionId",
                table: "ModelEvaluations",
                column: "ModelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_Version",
                table: "ModelVersions",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Observations_ContentHash",
                table: "Observations",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Observations_EntityType_EntityId_Field_RetrievedAtUtc",
                table: "Observations",
                columns: new[] { "EntityType", "EntityId", "Field", "RetrievedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_People_ExternalSource_ExternalId",
                table: "People",
                columns: new[] { "ExternalSource", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionRuns_ModelVersionId",
                table: "PredictionRuns",
                column: "ModelVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionRuns_RaceId_ModelVersionId_FeatureCutoffUtc",
                table: "PredictionRuns",
                columns: new[] { "RaceId", "ModelVersionId", "FeatureCutoffUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Races_ExternalSource_ExternalId",
                table: "Races",
                columns: new[] { "ExternalSource", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Races_MeetingId",
                table: "Races",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_RawArtifactManifests_IngestionRunId_Sha256",
                table: "RawArtifactManifests",
                columns: new[] { "IngestionRunId", "Sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Results_RaceId_StarterId",
                table: "Results",
                columns: new[] { "RaceId", "StarterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Results_StarterId",
                table: "Results",
                column: "StarterId");

            migrationBuilder.CreateIndex(
                name: "IX_StarterPredictions_PredictionRunId_StarterId",
                table: "StarterPredictions",
                columns: new[] { "PredictionRunId", "StarterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StarterPredictions_StarterId",
                table: "StarterPredictions",
                column: "StarterId");

            migrationBuilder.CreateIndex(
                name: "IX_Starters_DriverId",
                table: "Starters",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Starters_HorseId",
                table: "Starters",
                column: "HorseId");

            migrationBuilder.CreateIndex(
                name: "IX_Starters_RaceId_HorseId",
                table: "Starters",
                columns: new[] { "RaceId", "HorseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Starters_TrainerId",
                table: "Starters",
                column: "TrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_ExternalSource_ExternalId",
                table: "Tracks",
                columns: new[] { "ExternalSource", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BettingMarketMemberships");

            migrationBuilder.DropTable(
                name: "HistoricalStarts");

            migrationBuilder.DropTable(
                name: "ModelEvaluations");

            migrationBuilder.DropTable(
                name: "Observations");

            migrationBuilder.DropTable(
                name: "RawArtifactManifests");

            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropTable(
                name: "StarterPredictions");

            migrationBuilder.DropTable(
                name: "IngestionRuns");

            migrationBuilder.DropTable(
                name: "PredictionRuns");

            migrationBuilder.DropTable(
                name: "Starters");

            migrationBuilder.DropTable(
                name: "ModelVersions");

            migrationBuilder.DropTable(
                name: "Horses");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "Races");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Tracks");
        }
    }
}
