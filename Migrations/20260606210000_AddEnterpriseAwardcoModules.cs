using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Priser.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseAwardcoModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            CreateEnterpriseTable(migrationBuilder, "ComplianceSettings", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"DataRetentionDays\" integer NOT NULL",
                "\"AuditRetentionDays\" integer NOT NULL",
                "\"SsoEnforced\" boolean NOT NULL",
                "\"MfaRequired\" boolean NOT NULL",
                "\"ImmutableBackupsEnabled\" boolean NOT NULL",
                "\"EncryptionMode\" text NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "UserCustomFields", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"UserId\" uuid NOT NULL",
                "\"FieldKey\" text NOT NULL",
                "\"FieldValue\" text NOT NULL",
                "\"IsSensitive\" boolean NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "UserPermissions", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"UserId\" uuid NOT NULL",
                "\"PermissionKey\" text NOT NULL",
                "\"IsGranted\" boolean NOT NULL",
                "\"GrantedByUserId\" uuid NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "PointPools", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"Name\" text NOT NULL",
                "\"Balance\" numeric NOT NULL",
                "\"Currency\" text NOT NULL",
                "\"Status\" text NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "ApprovalHistories", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"ApprovalRequestId\" uuid NOT NULL",
                "\"ActorId\" uuid NOT NULL",
                "\"FromStatus\" text NOT NULL",
                "\"ToStatus\" text NOT NULL",
                "\"Notes\" text NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "AutomationRules", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"Name\" text NOT NULL",
                "\"TriggerType\" text NOT NULL",
                "\"ConditionsJson\" text NOT NULL",
                "\"ActionsJson\" text NOT NULL",
                "\"ScheduleCron\" text NOT NULL",
                "\"IsActive\" boolean NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "MilestoneEvents", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"AutomationRuleId\" uuid NULL",
                "\"UserId\" uuid NOT NULL",
                "\"EventType\" text NOT NULL",
                "\"Status\" text NOT NULL",
                "\"PayloadJson\" text NOT NULL",
                "\"ProcessedAt\" timestamp with time zone NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "MemoryBooks", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"RecipientId\" uuid NOT NULL",
                "\"Title\" text NOT NULL",
                "\"Occasion\" text NOT NULL",
                "\"DueAt\" timestamp with time zone NOT NULL",
                "\"Status\" text NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "MemoryBookEntries", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"MemoryBookId\" uuid NOT NULL",
                "\"AuthorId\" uuid NOT NULL",
                "\"Message\" text NOT NULL",
                "\"MediaUrl\" text NULL",
                "\"Status\" text NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "ShippingAddresses", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"UserId\" uuid NOT NULL",
                "\"RecipientName\" text NOT NULL",
                "\"EncryptedAddress\" text NOT NULL",
                "\"Country\" text NOT NULL",
                "\"IsDefault\" boolean NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "RedemptionHistories", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"UserId\" uuid NOT NULL",
                "\"OrderId\" uuid NOT NULL",
                "\"PointsSpent\" numeric NOT NULL",
                "\"FiatAmount\" numeric NOT NULL",
                "\"Status\" text NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "RecognitionAttachments", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"RecognitionId\" uuid NOT NULL",
                "\"Url\" text NOT NULL",
                "\"AttachmentType\" text NOT NULL",
                "\"AltText\" text NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "Integrations", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"Provider\" text NOT NULL",
                "\"IntegrationType\" text NOT NULL",
                "\"Status\" text NOT NULL",
                "\"SettingsJson\" text NOT NULL",
                "\"LastSyncedAt\" timestamp with time zone NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "SyncLogs", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"IntegrationId\" uuid NOT NULL",
                "\"Status\" text NOT NULL",
                "\"RecordsProcessed\" integer NOT NULL",
                "\"RecordsFailed\" integer NOT NULL",
                "\"ErrorSummary\" text NULL",
                "\"StartedAt\" timestamp with time zone NOT NULL",
                "\"FinishedAt\" timestamp with time zone NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "SyncMappings", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"IntegrationId\" uuid NOT NULL",
                "\"SourceField\" text NOT NULL",
                "\"TargetField\" text NOT NULL",
                "\"IsRequired\" boolean NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "Badges", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"Name\" text NOT NULL",
                "\"Description\" text NULL",
                "\"IconUrl\" text NULL",
                "\"CriteriaJson\" text NOT NULL",
                "\"IsActive\" boolean NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "UserBadges", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"UserId\" uuid NOT NULL",
                "\"BadgeId\" uuid NOT NULL",
                "\"AwardedAt\" timestamp with time zone NOT NULL",
                "\"AwardedByUserId\" uuid NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "Leaderboards", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"Name\" text NOT NULL",
                "\"Period\" text NOT NULL",
                "\"DepartmentId\" uuid NULL",
                "\"Metric\" text NOT NULL",
                "\"IsActive\" boolean NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "AchievementRules", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"Name\" text NOT NULL",
                "\"TriggerMetric\" text NOT NULL",
                "\"Threshold\" integer NOT NULL",
                "\"BadgeId\" uuid NULL",
                "\"RewardPoints\" integer NOT NULL",
                "\"IsActive\" boolean NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "Reports", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"CreatedByUserId\" uuid NOT NULL",
                "\"Name\" text NOT NULL",
                "\"ReportType\" text NOT NULL",
                "\"FiltersJson\" text NOT NULL",
                "\"Visibility\" text NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "AnalyticsCaches", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"CacheKey\" text NOT NULL",
                "\"PayloadJson\" text NOT NULL",
                "\"ExpiresAt\" timestamp with time zone NOT NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "TenantBillings", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"Plan\" text NOT NULL",
                "\"MonthlyFee\" numeric NOT NULL",
                "\"PointsLiability\" numeric NOT NULL",
                "\"InvoiceStatus\" text NOT NULL",
                "\"CurrentPeriodEndsAt\" timestamp with time zone NULL"
            });

            CreateEnterpriseTable(migrationBuilder, "FeatureFlags", new[]
            {
                "\"TenantId\" uuid NOT NULL",
                "\"Key\" text NOT NULL",
                "\"IsEnabled\" boolean NOT NULL",
                "\"ConfigurationJson\" text NULL"
            });

            migrationBuilder.CreateIndex("IX_ComplianceSettings_TenantId", "ComplianceSettings", "TenantId", unique: true);
            migrationBuilder.CreateIndex("IX_UserCustomFields_TenantId_UserId_FieldKey", "UserCustomFields", new[] { "TenantId", "UserId", "FieldKey" }, unique: true);
            migrationBuilder.CreateIndex("IX_UserPermissions_TenantId_UserId_PermissionKey", "UserPermissions", new[] { "TenantId", "UserId", "PermissionKey" }, unique: true);
            migrationBuilder.CreateIndex("IX_PointPools_TenantId_Name", "PointPools", new[] { "TenantId", "Name" }, unique: true);
            migrationBuilder.CreateIndex("IX_ApprovalHistories_TenantId_ApprovalRequestId", "ApprovalHistories", new[] { "TenantId", "ApprovalRequestId" });
            migrationBuilder.CreateIndex("IX_AutomationRules_TenantId_TriggerType_IsActive", "AutomationRules", new[] { "TenantId", "TriggerType", "IsActive" });
            migrationBuilder.CreateIndex("IX_MilestoneEvents_TenantId_EventType_Status", "MilestoneEvents", new[] { "TenantId", "EventType", "Status" });
            migrationBuilder.CreateIndex("IX_MemoryBooks_TenantId_RecipientId_Status", "MemoryBooks", new[] { "TenantId", "RecipientId", "Status" });
            migrationBuilder.CreateIndex("IX_ShippingAddresses_TenantId_UserId", "ShippingAddresses", new[] { "TenantId", "UserId" });
            migrationBuilder.CreateIndex("IX_RedemptionHistories_TenantId_UserId", "RedemptionHistories", new[] { "TenantId", "UserId" });
            migrationBuilder.CreateIndex("IX_RecognitionAttachments_TenantId_RecognitionId", "RecognitionAttachments", new[] { "TenantId", "RecognitionId" });
            migrationBuilder.CreateIndex("IX_Integrations_TenantId_Provider_IntegrationType", "Integrations", new[] { "TenantId", "Provider", "IntegrationType" }, unique: true);
            migrationBuilder.CreateIndex("IX_SyncLogs_TenantId_IntegrationId_StartedAt", "SyncLogs", new[] { "TenantId", "IntegrationId", "StartedAt" });
            migrationBuilder.CreateIndex("IX_SyncMappings_TenantId_IntegrationId_TargetField", "SyncMappings", new[] { "TenantId", "IntegrationId", "TargetField" }, unique: true);
            migrationBuilder.CreateIndex("IX_Badges_TenantId_Name", "Badges", new[] { "TenantId", "Name" }, unique: true);
            migrationBuilder.CreateIndex("IX_UserBadges_TenantId_UserId_BadgeId", "UserBadges", new[] { "TenantId", "UserId", "BadgeId" }, unique: true);
            migrationBuilder.CreateIndex("IX_Leaderboards_TenantId_Period_Metric", "Leaderboards", new[] { "TenantId", "Period", "Metric" });
            migrationBuilder.CreateIndex("IX_AchievementRules_TenantId_TriggerMetric_Threshold", "AchievementRules", new[] { "TenantId", "TriggerMetric", "Threshold" });
            migrationBuilder.CreateIndex("IX_Reports_TenantId_CreatedByUserId", "Reports", new[] { "TenantId", "CreatedByUserId" });
            migrationBuilder.CreateIndex("IX_AnalyticsCaches_TenantId_CacheKey", "AnalyticsCaches", new[] { "TenantId", "CacheKey" }, unique: true);
            migrationBuilder.CreateIndex("IX_TenantBillings_TenantId", "TenantBillings", "TenantId", unique: true);
            migrationBuilder.CreateIndex("IX_FeatureFlags_TenantId_Key", "FeatureFlags", new[] { "TenantId", "Key" }, unique: true);

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "NormalizedName", "Description", "CreatedAt", "UpdatedAt", "DeletedAt" },
                values: new object[]
                {
                    new Guid("00000000-0000-0000-0000-000000000005"),
                    "Viewer",
                    "VIEWER",
                    "Read-only auditor",
                    new DateTime(2026, 6, 6, 21, 0, 0, DateTimeKind.Utc),
                    null,
                    null
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));

            foreach (var table in new[]
                     {
                         "FeatureFlags", "TenantBillings", "AnalyticsCaches", "Reports", "AchievementRules",
                         "Leaderboards", "UserBadges", "Badges", "SyncMappings", "SyncLogs", "Integrations",
                         "RecognitionAttachments", "RedemptionHistories", "ShippingAddresses", "MemoryBookEntries",
                         "MemoryBooks", "MilestoneEvents", "AutomationRules", "ApprovalHistories", "PointPools",
                         "UserPermissions", "UserCustomFields", "ComplianceSettings"
                     })
            {
                migrationBuilder.DropTable(table);
            }
        }

        private static void CreateEnterpriseTable(MigrationBuilder migrationBuilder, string name, IEnumerable<string> domainColumns)
        {
            var columns = string.Join(",\n                    ", new[]
            {
                "\"Id\" uuid NOT NULL",
                "\"CreatedAt\" timestamp with time zone NOT NULL",
                "\"UpdatedAt\" timestamp with time zone NULL",
                "\"DeletedAt\" timestamp with time zone NULL"
            }.Concat(domainColumns));

            migrationBuilder.Sql($"CREATE TABLE IF NOT EXISTS \"{name}\" (\n                    {columns},\n                    CONSTRAINT \"PK_{name}\" PRIMARY KEY (\"Id\")\n                );");
        }
    }
}
