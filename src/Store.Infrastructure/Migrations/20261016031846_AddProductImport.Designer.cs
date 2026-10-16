using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Store.Infrastructure;

#nullable disable

namespace Store.Infrastructure.Migrations
{
    [DbContext(typeof(StoreDbContext))]
    [Migration("20261016031846_AddProductImport")]
    partial class AddProductImport
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.13")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "pg_trgm");
            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "unaccent");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Store.Domain.Category", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("NormalizedName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique();

                    b.ToTable("Categories", (string)null);
                });

            modelBuilder.Entity("Store.Domain.ErrorLog", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("CorrelationId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ExceptionType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("HttpMethod")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Level")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid?>("OrderId")
                        .HasColumnType("uuid");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PaymentProviderKey")
                        .HasColumnType("text");

                    b.Property<string>("StackTrace")
                        .HasColumnType("text");

                    b.Property<int>("StatusCode")
                        .HasColumnType("integer");

                    b.Property<DateTime>("TimestampUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("Timestamp");

                    b.HasKey("Id");

                    b.HasIndex("CorrelationId");

                    b.HasIndex("TimestampUtc");

                    b.ToTable("ErrorLog", (string)null);
                });

            modelBuilder.Entity("Store.Domain.ImportBatch", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("CreatedProducts")
                        .HasColumnType("integer");

                    b.Property<int>("ExactDuplicateCount")
                        .HasColumnType("integer");

                    b.Property<string>("FileHash")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("ImportedBy")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<int>("RejectedCount")
                        .HasColumnType("integer");

                    b.Property<int>("RowCount")
                        .HasColumnType("integer");

                    b.Property<long>("StockAdded")
                        .HasColumnType("bigint");

                    b.Property<int>("WarningCount")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAtUtc");

                    b.HasIndex("FileHash");

                    b.ToTable("ImportBatches", (string)null);
                });

            modelBuilder.Entity("Store.Domain.Product", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CategoryId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("NameNormalized")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<decimal>("Price")
                        .HasPrecision(12, 2)
                        .HasColumnType("numeric(12,2)");

                    b.Property<string>("SearchText")
                        .IsRequired()
                        .HasMaxLength(2300)
                        .HasColumnType("character varying(2300)");

                    b.Property<string>("Sku")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<string>("SkuNormalized")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<int>("Stock")
                        .HasColumnType("integer");

                    b.Property<DateTime>("UpdatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<decimal?>("WeightKg")
                        .HasPrecision(12, 3)
                        .HasColumnType("numeric(12,3)");

                    b.Property<uint>("xmin")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("xid")
                        .HasColumnName("xmin");

                    b.HasKey("Id");

                    b.HasIndex("CategoryId");

                    b.HasIndex("SearchText");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("SearchText"), "gin");
                    NpgsqlIndexBuilderExtensions.HasOperators(b.HasIndex("SearchText"), new[] { "gin_trgm_ops" });

                    b.HasIndex("SkuNormalized")
                        .IsUnique();

                    b.ToTable("Products", (string)null);
                });

            modelBuilder.Entity("Store.Domain.StockMovement", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("ImportBatchId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("ProductId")
                        .HasColumnType("uuid");

                    b.Property<int>("Quantity")
                        .HasColumnType("integer");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.HasKey("Id");

                    b.HasIndex("ImportBatchId");

                    b.HasIndex("ProductId");

                    b.ToTable("StockMovements", (string)null);
                });

            modelBuilder.Entity("Store.Domain.Product", b =>
                {
                    b.HasOne("Store.Domain.Category", "Category")
                        .WithMany("Products")
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Category");
                });

            modelBuilder.Entity("Store.Domain.StockMovement", b =>
                {
                    b.HasOne("Store.Domain.ImportBatch", "ImportBatch")
                        .WithMany("StockMovements")
                        .HasForeignKey("ImportBatchId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("Store.Domain.Product", "Product")
                        .WithMany()
                        .HasForeignKey("ProductId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("ImportBatch");

                    b.Navigation("Product");
                });

            modelBuilder.Entity("Store.Domain.Category", b =>
                {
                    b.Navigation("Products");
                });

            modelBuilder.Entity("Store.Domain.ImportBatch", b =>
                {
                    b.Navigation("StockMovements");
                });
#pragma warning restore 612, 618
        }
    }
}
