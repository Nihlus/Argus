﻿// <auto-generated />

using System;
using Argus.Coordinator.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#pragma warning disable CS1591

namespace Argus.Coordinator.Migrations
{
    [DbContext(typeof(CoordinatorContext))]
    [Migration("20210904184507_DateTimeOffsetToDateTime")]
    partial class DateTimeOffsetToDateTime
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "5.0.9");

            modelBuilder.Entity("Argus.Coordinator.Model.ServiceState", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ResumePoint")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("ServiceStates");
                });

            modelBuilder.Entity("Argus.Coordinator.Model.ServiceStatusReport", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("Id")
                        .IsUnique();

                    b.ToTable("ServiceStatusReports");
                });

            modelBuilder.Entity("Argus.Coordinator.Model.ServiceStatusReport", b =>
                {
                    b.OwnsOne("Argus.Common.Messages.BulkData.StatusReport", "Report", b1 =>
                        {
                            b1.Property<int>("ServiceStatusReportId")
                                .HasColumnType("INTEGER");

                            b1.Property<string>("Image")
                                .IsRequired()
                                .HasColumnType("TEXT");

                            b1.Property<string>("Message")
                                .IsRequired()
                                .HasColumnType("TEXT");

                            b1.Property<string>("ServiceName")
                                .IsRequired()
                                .HasColumnType("TEXT");

                            b1.Property<string>("Source")
                                .IsRequired()
                                .HasColumnType("TEXT");

                            b1.Property<int>("Status")
                                .HasColumnType("INTEGER");

                            b1.Property<DateTime>("Timestamp")
                                .HasColumnType("TEXT");

                            b1.HasKey("ServiceStatusReportId");

                            b1.HasIndex("Image");

                            b1.HasIndex("ServiceName");

                            b1.HasIndex("Source");

                            b1.ToTable("ServiceStatusReports");

                            b1.WithOwner()
                                .HasForeignKey("ServiceStatusReportId");
                        });

                    b.Navigation("Report")
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
