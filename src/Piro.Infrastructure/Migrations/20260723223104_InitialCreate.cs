using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsReadonly = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "UTC"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasSeenShowcase = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReEscalateAfterInactivityMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StartDateTime = table.Column<long>(type: "bigint", nullable: false),
                    EndDateTime = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Investigating"),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AcknowledgedAt = table.Column<long>(type: "bigint", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "text", nullable: true),
                    Visibility = table.Column<string>(type: "text", nullable: false, defaultValue: "Private"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentImpact = table.Column<string>(type: "text", nullable: false, defaultValue: "DOWN")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Maintenances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartDateTime = table.Column<long>(type: "bigint", nullable: false),
                    RRule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    IsGlobal = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maintenances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IntegrationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetDescriptor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationEventOutbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrderingKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEventOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OidcProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Authority = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClientSecret = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AllowedDomains = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OidcProviderConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnCallSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "UTC"),
                    NotifyOnShiftStart = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnCallSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Path = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Header = table.Column<string>(type: "text", nullable: true),
                    Subheader = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PermissionName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PiroLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Exception = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    SourceContext = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PiroLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostmortemFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Heading = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HelpText = table.Column<string>(type: "text", nullable: true),
                    FieldType = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostmortemFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SiteData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "string"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "default"),
                    WorkerTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    HashedKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MaskedKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Postmortems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReviewOwnerUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewOwnerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ImpactStartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ImpactEndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postmortems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Postmortems_AspNetUsers_ReviewOwnerUserId",
                        column: x => x.ReviewOwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EscalationPolicyId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Integrations_EscalationPolicies_EscalationPolicyId",
                        column: x => x.EscalationPolicyId,
                        principalTable: "EscalationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CurrentStatus = table.Column<string>(type: "text", nullable: false, defaultValue: "NO_DATA"),
                    PublicStatus = table.Column<string>(type: "text", nullable: false, defaultValue: "UP"),
                    DefaultStatus = table.Column<string>(type: "text", nullable: false, defaultValue: "NO_DATA"),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    HistoryDaysDesktop = table.Column<int>(type: "integer", nullable: false),
                    HistoryDaysMobile = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EscalationPolicyId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_EscalationPolicies_EscalationPolicyId",
                        column: x => x.EscalationPolicyId,
                        principalTable: "EscalationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IncidentImpactChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    Impact = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentImpactChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentImpactChanges_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorName = table.Column<string>(type: "text", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    OldStatus = table.Column<string>(type: "text", nullable: true),
                    NewStatus = table.Column<string>(type: "text", nullable: true),
                    Visibility = table.Column<string>(type: "text", nullable: false, defaultValue: "Private"),
                    RelatedIncidentId = table.Column<int>(type: "integer", nullable: true),
                    AlertId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTimelineEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentTimelineEvents_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaintenanceId = table.Column<int>(type: "integer", nullable: false),
                    StartDateTime = table.Column<long>(type: "bigint", nullable: false),
                    EndDateTime = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Scheduled"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceEvents_Maintenances_MaintenanceId",
                        column: x => x.MaintenanceId,
                        principalTable: "Maintenances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EscalationSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RetryIntervalMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ScheduleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationSteps_EscalationPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "EscalationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EscalationSteps_OnCallSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "OnCallSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OnCallLayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceRule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FirstOccurrenceStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FirstOccurrenceEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnCallLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnCallLayers_OnCallSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "OnCallSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnCallOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ReplacesUserId = table.Column<int>(type: "integer", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnCallOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnCallOverrides_AspNetUsers_ReplacesUserId",
                        column: x => x.ReplacesUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnCallOverrides_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnCallOverrides_OnCallSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "OnCallSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<string>(type: "character varying(100)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostmortemFieldValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostmortemId = table.Column<int>(type: "integer", nullable: false),
                    FieldDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostmortemFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostmortemFieldValues_PostmortemFieldDefinitions_FieldDefin~",
                        column: x => x.FieldDefinitionId,
                        principalTable: "PostmortemFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostmortemFieldValues_Postmortems_PostmortemId",
                        column: x => x.PostmortemId,
                        principalTable: "Postmortems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostmortemIncidents",
                columns: table => new
                {
                    PostmortemId = table.Column<int>(type: "integer", nullable: false),
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostmortemIncidents", x => new { x.PostmortemId, x.IncidentId });
                    table.ForeignKey(
                        name: "FK_PostmortemIncidents_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostmortemIncidents_Postmortems_PostmortemId",
                        column: x => x.PostmortemId,
                        principalTable: "Postmortems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostmortemTimelineEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostmortemId = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostmortemTimelineEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostmortemTimelineEntries_Postmortems_PostmortemId",
                        column: x => x.PostmortemId,
                        principalTable: "Postmortems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalReferences_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventsJson = table.Column<string>(type: "text", nullable: false),
                    MinSeverity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Target = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationSubscriptions_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OAuthTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuthTokens_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    IntegrationInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Handle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsAccountFallback = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotificationPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserNotificationPreferences_Integrations_IntegrationInstanc~",
                        column: x => x.IntegrationInstanceId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebhookRequestLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawPayload = table.Column<string>(type: "jsonb", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    AlertId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookRequestLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookRequestLogs_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Checks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Cron = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: "* * * * *"),
                    TypeDataJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CurrentStatus = table.Column<string>(type: "text", nullable: false, defaultValue: "NO_DATA"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HistoryDaysDesktop = table.Column<int>(type: "integer", nullable: true),
                    HistoryDaysMobile = table.Column<int>(type: "integer", nullable: true),
                    IsMultiRegion = table.Column<bool>(type: "boolean", nullable: false),
                    IntegrationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Checks_Integrations_IntegrationId",
                        column: x => x.IntegrationId,
                        principalTable: "Integrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Checks_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceServices",
                columns: table => new
                {
                    MaintenanceId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Impact = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceServices", x => new { x.MaintenanceId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_MaintenanceServices_Maintenances_MaintenanceId",
                        column: x => x.MaintenanceId,
                        principalTable: "Maintenances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PageServices",
                columns: table => new
                {
                    PageId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ShowChecks = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageServices", x => new { x.PageId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_PageServices_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PageServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDependencies",
                columns: table => new
                {
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    DependsOnServiceId = table.Column<int>(type: "integer", nullable: false),
                    PropagationMode = table.Column<string>(type: "text", nullable: false, defaultValue: "Blocking"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDependencies", x => new { x.ServiceId, x.DependsOnServiceId });
                    table.ForeignKey(
                        name: "FK_ServiceDependencies_Services_DependsOnServiceId",
                        column: x => x.DependsOnServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDependencies_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnCallLayerUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LayerId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnCallLayerUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnCallLayerUsers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnCallLayerUsers_OnCallLayers_LayerId",
                        column: x => x.LayerId,
                        principalTable: "OnCallLayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CheckId = table.Column<int>(type: "integer", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Comparison = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    AlertValue = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FailureThreshold = table.Column<int>(type: "integer", nullable: false),
                    SuccessThreshold = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false, defaultValue: "Warning"),
                    IsAlerting = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertConfigs_Checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "Checks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CheckDataPoints",
                columns: table => new
                {
                    CheckId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    WorkerRegion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "default"),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Dimensions = table.Column<string>(type: "jsonb", nullable: false),
                    DataType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckDataPoints", x => new { x.CheckId, x.Timestamp, x.WorkerRegion });
                    table.ForeignKey(
                        name: "FK_CheckDataPoints_Checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "Checks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncidentServices",
                columns: table => new
                {
                    IncidentId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: false),
                    Impact = table.Column<string>(type: "text", nullable: false),
                    TriggeringCheckId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentServices", x => new { x.IncidentId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_IncidentServices_Checks_TriggeringCheckId",
                        column: x => x.TriggeringCheckId,
                        principalTable: "Checks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IncidentServices_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncidentServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlertConfigId = table.Column<int>(type: "integer", nullable: true),
                    CheckId = table.Column<int>(type: "integer", nullable: true),
                    ServiceId = table.Column<int>(type: "integer", nullable: true),
                    IncidentId = table.Column<int>(type: "integer", nullable: true),
                    ImpactAtFireTime = table.Column<string>(type: "text", nullable: false, defaultValue: "DOWN"),
                    Message = table.Column<string>(type: "text", nullable: true),
                    MessageFingerprint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    EscalationCurrentStep = table.Column<int>(type: "integer", nullable: true),
                    EscalationStepStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EscalationStepAttempts = table.Column<int>(type: "integer", nullable: false),
                    EscalationExhaustedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAt = table.Column<long>(type: "bigint", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "text", nullable: true),
                    LastUserActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EscalationPolicyId = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false, defaultValue: "Internal"),
                    SourceRequestLogId = table.Column<int>(type: "integer", nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_AlertConfigs_AlertConfigId",
                        column: x => x.AlertConfigId,
                        principalTable: "AlertConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_Checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "Checks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_EscalationPolicies_EscalationPolicyId",
                        column: x => x.EscalationPolicyId,
                        principalTable: "EscalationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_WebhookRequestLogs_SourceRequestLogId",
                        column: x => x.SourceRequestLogId,
                        principalTable: "WebhookRequestLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EscalationDeliveryLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlertId = table.Column<int>(type: "integer", nullable: false),
                    StepIndex = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ChannelType = table.Column<string>(type: "text", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationDeliveryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationDeliveryLogs_Alerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "Alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PostmortemFieldDefinitions",
                columns: new[] { "Id", "FieldType", "Heading", "HelpText", "IsActive", "IsSystem", "Key", "SortOrder" },
                values: new object[,]
                {
                    { 1, "LongText", "Overview", "A high-level summary of what happened, for a general audience.", true, true, "overview", 0 },
                    { 2, "LongText", "What Happened", "A detailed, chronological account of the incident.", true, true, "what_happened", 1 },
                    { 3, "LongText", "Resolution", "How the incident was ultimately resolved.", true, true, "resolution", 2 },
                    { 4, "LongText", "Root Causes", "The conditions that allowed the incident to happen. Aim for the underlying causes, not just the trigger.", true, true, "root_causes", 3 },
                    { 5, "LongText", "Impact", "Who and what was affected, and to what degree.", true, true, "impact", 4 },
                    { 6, "LongText", "What Went Well?", "Things that worked as intended during detection and response.", true, true, "what_went_well", 5 },
                    { 7, "LongText", "What Didn't Go So Well?", "Things that hindered detection or response and should be improved.", true, true, "what_didnt", 6 },
                    { 8, "LongText", "Action Items", "Concrete follow-up work, each with an accountable owner.", true, true, "action_items", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertConfigs_CheckId",
                table: "AlertConfigs",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_AlertConfigId_ResolvedAt",
                table: "Alerts",
                columns: new[] { "AlertConfigId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_CheckId",
                table: "Alerts",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_EscalationPolicyId",
                table: "Alerts",
                column: "EscalationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_IncidentId",
                table: "Alerts",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_ServiceId_ResolvedAt",
                table: "Alerts",
                columns: new[] { "ServiceId", "ResolvedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_Source_ExternalId",
                table: "Alerts",
                columns: new[] { "Source", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_SourceRequestLogId",
                table: "Alerts",
                column: "SourceRequestLogId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_HashedKey",
                table: "ApiKeys",
                column: "HashedKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Name",
                table: "ApiKeys",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId",
                table: "ApiKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckDataPoints_CheckId_Timestamp",
                table: "CheckDataPoints",
                columns: new[] { "CheckId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Checks_IntegrationId",
                table: "Checks",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_Checks_IsActive",
                table: "Checks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Checks_ServiceId_Slug",
                table: "Checks",
                columns: new[] { "ServiceId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationDeliveryLogs_AlertId",
                table: "EscalationDeliveryLogs",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPolicies_Name",
                table: "EscalationPolicies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationSteps_PolicyId_Order",
                table: "EscalationSteps",
                columns: new[] { "PolicyId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationSteps_ScheduleId",
                table: "EscalationSteps",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalReferences_IntegrationId",
                table: "ExternalReferences",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalReferences_TargetType_TargetId",
                table: "ExternalReferences",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentImpactChanges_IncidentId_Timestamp",
                table: "IncidentImpactChanges",
                columns: new[] { "IncidentId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_StartDateTime",
                table: "Incidents",
                column: "StartDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_Status",
                table: "Incidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentServices_ServiceId",
                table: "IncidentServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentServices_TriggeringCheckId",
                table: "IncidentServices",
                column: "TriggeringCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTimelineEvents_IncidentId_OccurredAt",
                table: "IncidentTimelineEvents",
                columns: new[] { "IncidentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_EscalationPolicyId",
                table: "Integrations",
                column: "EscalationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_EndDateTime",
                table: "MaintenanceEvents",
                column: "EndDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_MaintenanceId",
                table: "MaintenanceEvents",
                column: "MaintenanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_MaintenanceId_StartDateTime",
                table: "MaintenanceEvents",
                columns: new[] { "MaintenanceId", "StartDateTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_StartDateTime",
                table: "MaintenanceEvents",
                column: "StartDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvents_Status",
                table: "MaintenanceEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceServices_ServiceId",
                table: "MaintenanceServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryLogs_IdempotencyKey",
                table: "NotificationDeliveryLogs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryLogs_IntegrationId",
                table: "NotificationDeliveryLogs",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEventOutbox_OrderingKey_Id",
                table: "NotificationEventOutbox",
                columns: new[] { "OrderingKey", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEventOutbox_Status_NextAttemptAt",
                table: "NotificationEventOutbox",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_Enabled",
                table: "NotificationSubscriptions",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_IntegrationId",
                table: "NotificationSubscriptions",
                column: "IntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_UserId",
                table: "NotificationSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthTokens_IntegrationId",
                table: "OAuthTokens",
                column: "IntegrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnCallLayers_ScheduleId_Order",
                table: "OnCallLayers",
                columns: new[] { "ScheduleId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnCallLayerUsers_LayerId_Position",
                table: "OnCallLayerUsers",
                columns: new[] { "LayerId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_OnCallLayerUsers_UserId",
                table: "OnCallLayerUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnCallOverrides_ReplacesUserId",
                table: "OnCallOverrides",
                column: "ReplacesUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnCallOverrides_ScheduleId_StartsAtUtc_EndsAtUtc",
                table: "OnCallOverrides",
                columns: new[] { "ScheduleId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OnCallOverrides_UserId",
                table: "OnCallOverrides",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pages_Path",
                table: "Pages",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageServices_PageId",
                table: "PageServices",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_PageServices_ServiceId",
                table: "PageServices",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PiroLogs_Level",
                table: "PiroLogs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_PiroLogs_Timestamp",
                table: "PiroLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemFieldDefinitions_Key",
                table: "PostmortemFieldDefinitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemFieldValues_FieldDefinitionId",
                table: "PostmortemFieldValues",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemFieldValues_PostmortemId_FieldDefinitionId",
                table: "PostmortemFieldValues",
                columns: new[] { "PostmortemId", "FieldDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemIncidents_IncidentId",
                table: "PostmortemIncidents",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_Postmortems_ReviewOwnerUserId",
                table: "Postmortems",
                column: "ReviewOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Postmortems_Status",
                table: "Postmortems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PostmortemTimelineEntries_PostmortemId",
                table: "PostmortemTimelineEntries",
                column: "PostmortemId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDependencies_DependsOnServiceId",
                table: "ServiceDependencies",
                column: "DependsOnServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_EscalationPolicyId",
                table: "Services",
                column: "EscalationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_Slug",
                table: "Services",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteData_Key",
                table: "SiteData",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationPreferences_IntegrationInstanceId",
                table: "UserNotificationPreferences",
                column: "IntegrationInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationPreferences_UserId",
                table: "UserNotificationPreferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationPreferences_UserId_IntegrationInstanceId_Ha~",
                table: "UserNotificationPreferences",
                columns: new[] { "UserId", "IntegrationInstanceId", "Handle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookRequestLogs_IntegrationId_ReceivedAt",
                table: "WebhookRequestLogs",
                columns: new[] { "IntegrationId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerRegistrations_WorkerTokenHash",
                table: "WorkerRegistrations",
                column: "WorkerTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CheckDataPoints");

            migrationBuilder.DropTable(
                name: "EscalationDeliveryLogs");

            migrationBuilder.DropTable(
                name: "EscalationSteps");

            migrationBuilder.DropTable(
                name: "ExternalReferences");

            migrationBuilder.DropTable(
                name: "IncidentImpactChanges");

            migrationBuilder.DropTable(
                name: "IncidentServices");

            migrationBuilder.DropTable(
                name: "IncidentTimelineEvents");

            migrationBuilder.DropTable(
                name: "MaintenanceEvents");

            migrationBuilder.DropTable(
                name: "MaintenanceServices");

            migrationBuilder.DropTable(
                name: "NotificationDeliveryLogs");

            migrationBuilder.DropTable(
                name: "NotificationEventOutbox");

            migrationBuilder.DropTable(
                name: "NotificationSubscriptions");

            migrationBuilder.DropTable(
                name: "OAuthTokens");

            migrationBuilder.DropTable(
                name: "OidcProviderConfigs");

            migrationBuilder.DropTable(
                name: "OnCallLayerUsers");

            migrationBuilder.DropTable(
                name: "OnCallOverrides");

            migrationBuilder.DropTable(
                name: "PageServices");

            migrationBuilder.DropTable(
                name: "PiroLogs");

            migrationBuilder.DropTable(
                name: "PostmortemFieldValues");

            migrationBuilder.DropTable(
                name: "PostmortemIncidents");

            migrationBuilder.DropTable(
                name: "PostmortemTimelineEntries");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ServiceDependencies");

            migrationBuilder.DropTable(
                name: "SiteData");

            migrationBuilder.DropTable(
                name: "UserNotificationPreferences");

            migrationBuilder.DropTable(
                name: "WorkerRegistrations");

            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "Maintenances");

            migrationBuilder.DropTable(
                name: "OnCallLayers");

            migrationBuilder.DropTable(
                name: "Pages");

            migrationBuilder.DropTable(
                name: "PostmortemFieldDefinitions");

            migrationBuilder.DropTable(
                name: "Postmortems");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "AlertConfigs");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "WebhookRequestLogs");

            migrationBuilder.DropTable(
                name: "OnCallSchedules");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Checks");

            migrationBuilder.DropTable(
                name: "Integrations");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "EscalationPolicies");
        }
    }
}
