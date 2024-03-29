﻿// <auto-generated />
using System;
using Argus.Coordinator.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CS1591

namespace Argus.Coordinator.Migrations
{
    [DbContext(typeof(CoordinatorContext))]
    [Migration("20220220144657_AddImageSource")]
    partial class AddImageSource
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Argus.Common.Messages.BulkData.ImageSource", b =>
                {
                    b.Property<string>("ServiceName")
                        .HasColumnType("text")
                        .HasColumnName("service_name");

                    b.Property<string>("Source")
                        .HasColumnType("text")
                        .HasColumnName("source");

                    b.Property<DateTimeOffset>("FirstVisitedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("first_visited_at");

                    b.Property<DateTimeOffset?>("LastRevisitedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_revisited_at");

                    b.Property<int>("RevisitCount")
                        .HasColumnType("integer")
                        .HasColumnName("revisit_count");

                    b.Property<string>("SourceIdentifier")
                        .HasColumnType("text")
                        .HasColumnName("source_identifier");

                    b.HasKey("ServiceName", "Source")
                        .HasName("pk_service_image_sources");

                    b.HasIndex("FirstVisitedAt")
                        .HasDatabaseName("ix_service_image_sources_first_visited_at");

                    b.HasIndex("LastRevisitedAt")
                        .HasDatabaseName("ix_service_image_sources_last_revisited_at");

                    b.HasIndex("RevisitCount")
                        .HasDatabaseName("ix_service_image_sources_revisit_count");

                    b.HasIndex("ServiceName", "Source")
                        .IsUnique()
                        .HasDatabaseName("ix_service_image_sources_service_name_source");

                    b.ToTable("service_image_sources", (string)null);
                });

            modelBuilder.Entity("Argus.Common.Messages.BulkData.StatusReport", b =>
                {
                    b.Property<string>("Source")
                        .HasColumnType("text")
                        .HasColumnName("source");

                    b.Property<string>("Link")
                        .HasColumnType("text")
                        .HasColumnName("link");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("message");

                    b.Property<string>("ServiceName")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("service_name");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("timestamp");

                    b.HasKey("Source", "Link")
                        .HasName("pk_service_status_reports");

                    b.HasIndex("Status")
                        .HasDatabaseName("ix_service_status_reports_status");

                    b.HasIndex("Timestamp")
                        .HasDatabaseName("ix_service_status_reports_timestamp");

                    b.HasIndex("Source", "Link")
                        .IsUnique()
                        .HasDatabaseName("ix_service_status_reports_source_link");

                    b.ToTable("service_status_reports", (string)null);
                });

            modelBuilder.Entity("Argus.Coordinator.Model.ServiceState", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<string>("ResumePoint")
                        .HasColumnType("text")
                        .HasColumnName("resume_point");

                    b.HasKey("Id")
                        .HasName("pk_service_states");

                    b.HasIndex("Id")
                        .IsUnique()
                        .HasDatabaseName("ix_service_states_id");

                    b.ToTable("service_states", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}
