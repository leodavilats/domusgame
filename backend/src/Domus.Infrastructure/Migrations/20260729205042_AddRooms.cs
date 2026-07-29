using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domus.Infrastructure.Migrations
{
    public partial class AddRooms : Migration
    {
        private const string DefaultRoomId = "019891f0-0000-7000-8000-000000000001";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InviteCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedInviteCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InviteRotatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomMemberships_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomMemberships_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_NormalizedInviteCode",
                table: "Rooms",
                column: "NormalizedInviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomMemberships_ParticipantId",
                table: "RoomMemberships",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "UX_RoomMemberships_RoomParticipant",
                table: "RoomMemberships",
                columns: new[] { "RoomId", "ParticipantId" },
                unique: true);

            migrationBuilder.Sql($"""
                INSERT INTO "Rooms"
                    ("Id", "Name", "InviteCode", "NormalizedInviteCode", "InviteRotatedAt", "CreatedAt")
                SELECT
                    '{DefaultRoomId}'::uuid,
                    COALESCE(NULLIF(btrim(g."GcName"), ''), 'GC Domus'),
                    upper(COALESCE(NULLIF(btrim(g."InviteCode"), ''), 'DOMUS2026')),
                    upper(COALESCE(NULLIF(btrim(g."InviteCode"), ''), 'DOMUS2026')),
                    g."InviteRotatedAt",
                    g."InviteRotatedAt"
                FROM "GcSettings" g
                WHERE NOT EXISTS (SELECT 1 FROM "Rooms")
                ORDER BY g."Id"
                LIMIT 1;
                """);

            migrationBuilder.Sql($"""
                INSERT INTO "Rooms"
                    ("Id", "Name", "InviteCode", "NormalizedInviteCode", "InviteRotatedAt", "CreatedAt")
                SELECT '{DefaultRoomId}'::uuid, 'GC Domus', 'DOMUS2026', 'DOMUS2026', now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM "Rooms")
                  AND (EXISTS (SELECT 1 FROM "Participants") OR EXISTS (SELECT 1 FROM "Seasons"));
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "Seasons",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Seasons"
                SET "RoomId" = (SELECT "Id" FROM "Rooms" ORDER BY "CreatedAt", "Id" LIMIT 1)
                WHERE "RoomId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "RoomId",
                table: "Seasons",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Seasons_Rooms_RoomId",
                table: "Seasons",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "UX_Seasons_SingleActivePerRoom",
                table: "Seasons",
                columns: new[] { "RoomId", "Status" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.Sql("""
                INSERT INTO "RoomMemberships" ("Id", "RoomId", "ParticipantId", "JoinedAt")
                SELECT gen_random_uuid(), r."Id", p."Id", COALESCE(p."JoinedAt", now())
                FROM "Participants" p
                CROSS JOIN (SELECT "Id" FROM "Rooms" ORDER BY "CreatedAt", "Id" LIMIT 1) r
                WHERE NOT EXISTS (
                    SELECT 1 FROM "RoomMemberships" m
                    WHERE m."RoomId" = r."Id" AND m."ParticipantId" = p."Id");
                """);

            migrationBuilder.DropIndex(
                name: "UX_Seasons_SingleActive",
                table: "Seasons");

            migrationBuilder.DropTable(
                name: "GcSettings");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GcSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    GcName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InviteCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InviteRotatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NormalizedInviteCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GcSettings", x => x.Id);
                    table.CheckConstraint("CK_GcSettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.Sql("""
                INSERT INTO "GcSettings"
                    ("Id", "GcName", "InviteCode", "NormalizedInviteCode", "InviteRotatedAt")
                SELECT 1, "Name", "InviteCode", "NormalizedInviteCode", "InviteRotatedAt"
                FROM "Rooms"
                ORDER BY "CreatedAt", "Id"
                LIMIT 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Seasons_Rooms_RoomId",
                table: "Seasons");

            migrationBuilder.DropIndex(
                name: "UX_Seasons_SingleActivePerRoom",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Seasons");

            migrationBuilder.DropTable(
                name: "RoomMemberships");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.CreateIndex(
                name: "UX_Seasons_SingleActive",
                table: "Seasons",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 1");
        }
    }
}
