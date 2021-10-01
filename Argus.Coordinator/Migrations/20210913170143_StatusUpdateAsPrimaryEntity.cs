﻿// <auto-generated />

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#pragma warning disable CS1591

namespace Argus.Coordinator.Migrations
{
    public partial class StatusUpdateAsPrimaryEntity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_service_status_reports",
                table: "service_status_reports");

            migrationBuilder.DropIndex(
                name: "ix_service_status_reports_id",
                table: "service_status_reports");

            migrationBuilder.DropColumn(
                name: "id",
                table: "service_status_reports");

            migrationBuilder.RenameColumn(
                name: "report_timestamp",
                table: "service_status_reports",
                newName: "timestamp");

            migrationBuilder.RenameColumn(
                name: "report_status",
                table: "service_status_reports",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "report_source",
                table: "service_status_reports",
                newName: "source");

            migrationBuilder.RenameColumn(
                name: "report_service_name",
                table: "service_status_reports",
                newName: "service_name");

            migrationBuilder.RenameColumn(
                name: "report_message",
                table: "service_status_reports",
                newName: "message");

            migrationBuilder.RenameColumn(
                name: "report_link",
                table: "service_status_reports",
                newName: "link");

            migrationBuilder.AddPrimaryKey(
                name: "pk_service_status_reports",
                table: "service_status_reports",
                columns: new[] { "source", "link" });

            migrationBuilder.CreateIndex(
                name: "ix_service_status_reports_source_link",
                table: "service_status_reports",
                columns: new[] { "source", "link" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_service_status_reports",
                table: "service_status_reports");

            migrationBuilder.DropIndex(
                name: "ix_service_status_reports_source_link",
                table: "service_status_reports");

            migrationBuilder.RenameColumn(
                name: "timestamp",
                table: "service_status_reports",
                newName: "report_timestamp");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "service_status_reports",
                newName: "report_status");

            migrationBuilder.RenameColumn(
                name: "service_name",
                table: "service_status_reports",
                newName: "report_service_name");

            migrationBuilder.RenameColumn(
                name: "message",
                table: "service_status_reports",
                newName: "report_message");

            migrationBuilder.RenameColumn(
                name: "link",
                table: "service_status_reports",
                newName: "report_link");

            migrationBuilder.RenameColumn(
                name: "source",
                table: "service_status_reports",
                newName: "report_source");

            migrationBuilder.AddColumn<int>(
                name: "id",
                table: "service_status_reports",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "pk_service_status_reports",
                table: "service_status_reports",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_service_status_reports_id",
                table: "service_status_reports",
                column: "id",
                unique: true);
        }
    }
}
