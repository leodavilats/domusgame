using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domus.Infrastructure.Migrations
{
    public partial class AddParticipantBadges : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParticipantBadges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<int>(type: "integer", nullable: false),
                    EarnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceRoundId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSeasonId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantBadges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantBadges_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantBadges_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantBadges_Rounds_SourceRoundId",
                        column: x => x.SourceRoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipantBadges_Seasons_SourceSeasonId",
                        column: x => x.SourceSeasonId,
                        principalTable: "Seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ParticipantBadges_ParticipantCode",
                table: "ParticipantBadges",
                columns: new[] { "ParticipantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantBadges_RoomId",
                table: "ParticipantBadges",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantBadges_SourceRoundId",
                table: "ParticipantBadges",
                column: "SourceRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantBadges_SourceSeasonId",
                table: "ParticipantBadges",
                column: "SourceSeasonId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParticipantBadges");
        }
    }
}
