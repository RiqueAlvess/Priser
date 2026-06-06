using Microsoft.EntityFrameworkCore;
using Priser.Data;

namespace Priser.Services;

public class EnterpriseSchemaService(AppDbContext db)
{
    private static readonly IReadOnlyDictionary<string, string[]> Tables = new Dictionary<string, string[]>
    {
        ["ComplianceSettings"] = ["\"TenantId\" uuid NOT NULL", "\"DataRetentionDays\" integer NOT NULL", "\"AuditRetentionDays\" integer NOT NULL", "\"SsoEnforced\" boolean NOT NULL", "\"MfaRequired\" boolean NOT NULL", "\"ImmutableBackupsEnabled\" boolean NOT NULL", "\"EncryptionMode\" text NOT NULL"],
        ["UserCustomFields"] = ["\"TenantId\" uuid NOT NULL", "\"UserId\" uuid NOT NULL", "\"FieldKey\" text NOT NULL", "\"FieldValue\" text NOT NULL", "\"IsSensitive\" boolean NOT NULL"],
        ["UserPermissions"] = ["\"TenantId\" uuid NOT NULL", "\"UserId\" uuid NOT NULL", "\"PermissionKey\" text NOT NULL", "\"IsGranted\" boolean NOT NULL", "\"GrantedByUserId\" uuid NULL"],
        ["PointPools"] = ["\"TenantId\" uuid NOT NULL", "\"Name\" text NOT NULL", "\"Balance\" numeric NOT NULL", "\"Currency\" text NOT NULL", "\"Status\" text NOT NULL"],
        ["ApprovalHistories"] = ["\"TenantId\" uuid NOT NULL", "\"ApprovalRequestId\" uuid NOT NULL", "\"ActorId\" uuid NOT NULL", "\"FromStatus\" text NOT NULL", "\"ToStatus\" text NOT NULL", "\"Notes\" text NULL"],
        ["AutomationRules"] = ["\"TenantId\" uuid NOT NULL", "\"Name\" text NOT NULL", "\"TriggerType\" text NOT NULL", "\"ConditionsJson\" text NOT NULL", "\"ActionsJson\" text NOT NULL", "\"ScheduleCron\" text NOT NULL", "\"IsActive\" boolean NOT NULL"],
        ["MilestoneEvents"] = ["\"TenantId\" uuid NOT NULL", "\"AutomationRuleId\" uuid NULL", "\"UserId\" uuid NOT NULL", "\"EventType\" text NOT NULL", "\"Status\" text NOT NULL", "\"PayloadJson\" text NOT NULL", "\"ProcessedAt\" timestamp with time zone NULL"],
        ["MemoryBooks"] = ["\"TenantId\" uuid NOT NULL", "\"RecipientId\" uuid NOT NULL", "\"Title\" text NOT NULL", "\"Occasion\" text NOT NULL", "\"DueAt\" timestamp with time zone NOT NULL", "\"Status\" text NOT NULL"],
        ["MemoryBookEntries"] = ["\"TenantId\" uuid NOT NULL", "\"MemoryBookId\" uuid NOT NULL", "\"AuthorId\" uuid NOT NULL", "\"Message\" text NOT NULL", "\"MediaUrl\" text NULL", "\"Status\" text NOT NULL"],
        ["ShippingAddresses"] = ["\"TenantId\" uuid NOT NULL", "\"UserId\" uuid NOT NULL", "\"RecipientName\" text NOT NULL", "\"EncryptedAddress\" text NOT NULL", "\"Country\" text NOT NULL", "\"IsDefault\" boolean NOT NULL"],
        ["RedemptionHistories"] = ["\"TenantId\" uuid NOT NULL", "\"UserId\" uuid NOT NULL", "\"OrderId\" uuid NOT NULL", "\"PointsSpent\" numeric NOT NULL", "\"FiatAmount\" numeric NOT NULL", "\"Status\" text NOT NULL"],
        ["RecognitionAttachments"] = ["\"TenantId\" uuid NOT NULL", "\"RecognitionId\" uuid NOT NULL", "\"Url\" text NOT NULL", "\"AttachmentType\" text NOT NULL", "\"AltText\" text NULL"],
        ["Integrations"] = ["\"TenantId\" uuid NOT NULL", "\"Provider\" text NOT NULL", "\"IntegrationType\" text NOT NULL", "\"Status\" text NOT NULL", "\"SettingsJson\" text NOT NULL", "\"LastSyncedAt\" timestamp with time zone NULL"],
        ["SyncLogs"] = ["\"TenantId\" uuid NOT NULL", "\"IntegrationId\" uuid NOT NULL", "\"Status\" text NOT NULL", "\"RecordsProcessed\" integer NOT NULL", "\"RecordsFailed\" integer NOT NULL", "\"ErrorSummary\" text NULL", "\"StartedAt\" timestamp with time zone NOT NULL", "\"FinishedAt\" timestamp with time zone NULL"],
        ["SyncMappings"] = ["\"TenantId\" uuid NOT NULL", "\"IntegrationId\" uuid NOT NULL", "\"SourceField\" text NOT NULL", "\"TargetField\" text NOT NULL", "\"IsRequired\" boolean NOT NULL"],
        ["Badges"] = ["\"TenantId\" uuid NOT NULL", "\"Name\" text NOT NULL", "\"Description\" text NULL", "\"IconUrl\" text NULL", "\"CriteriaJson\" text NOT NULL", "\"IsActive\" boolean NOT NULL"],
        ["UserBadges"] = ["\"TenantId\" uuid NOT NULL", "\"UserId\" uuid NOT NULL", "\"BadgeId\" uuid NOT NULL", "\"AwardedAt\" timestamp with time zone NOT NULL", "\"AwardedByUserId\" uuid NULL"],
        ["Leaderboards"] = ["\"TenantId\" uuid NOT NULL", "\"Name\" text NOT NULL", "\"Period\" text NOT NULL", "\"DepartmentId\" uuid NULL", "\"Metric\" text NOT NULL", "\"IsActive\" boolean NOT NULL"],
        ["AchievementRules"] = ["\"TenantId\" uuid NOT NULL", "\"Name\" text NOT NULL", "\"TriggerMetric\" text NOT NULL", "\"Threshold\" integer NOT NULL", "\"BadgeId\" uuid NULL", "\"RewardPoints\" integer NOT NULL", "\"IsActive\" boolean NOT NULL"],
        ["Reports"] = ["\"TenantId\" uuid NOT NULL", "\"CreatedByUserId\" uuid NOT NULL", "\"Name\" text NOT NULL", "\"ReportType\" text NOT NULL", "\"FiltersJson\" text NOT NULL", "\"Visibility\" text NOT NULL"],
        ["AnalyticsCaches"] = ["\"TenantId\" uuid NOT NULL", "\"CacheKey\" text NOT NULL", "\"PayloadJson\" text NOT NULL", "\"ExpiresAt\" timestamp with time zone NOT NULL"],
        ["TenantBillings"] = ["\"TenantId\" uuid NOT NULL", "\"Plan\" text NOT NULL", "\"MonthlyFee\" numeric NOT NULL", "\"PointsLiability\" numeric NOT NULL", "\"InvoiceStatus\" text NOT NULL", "\"CurrentPeriodEndsAt\" timestamp with time zone NULL"],
        ["FeatureFlags"] = ["\"TenantId\" uuid NOT NULL", "\"Key\" text NOT NULL", "\"IsEnabled\" boolean NOT NULL", "\"ConfigurationJson\" text NULL"]
    };

    private static readonly string[] Indexes =
    [
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ComplianceSettings_TenantId\" ON \"ComplianceSettings\" (\"TenantId\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_UserCustomFields_TenantId_UserId_FieldKey\" ON \"UserCustomFields\" (\"TenantId\", \"UserId\", \"FieldKey\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_UserPermissions_TenantId_UserId_PermissionKey\" ON \"UserPermissions\" (\"TenantId\", \"UserId\", \"PermissionKey\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PointPools_TenantId_Name\" ON \"PointPools\" (\"TenantId\", \"Name\")",
        "CREATE INDEX IF NOT EXISTS \"IX_ApprovalHistories_TenantId_ApprovalRequestId\" ON \"ApprovalHistories\" (\"TenantId\", \"ApprovalRequestId\")",
        "CREATE INDEX IF NOT EXISTS \"IX_AutomationRules_TenantId_TriggerType_IsActive\" ON \"AutomationRules\" (\"TenantId\", \"TriggerType\", \"IsActive\")",
        "CREATE INDEX IF NOT EXISTS \"IX_MilestoneEvents_TenantId_EventType_Status\" ON \"MilestoneEvents\" (\"TenantId\", \"EventType\", \"Status\")",
        "CREATE INDEX IF NOT EXISTS \"IX_MemoryBooks_TenantId_RecipientId_Status\" ON \"MemoryBooks\" (\"TenantId\", \"RecipientId\", \"Status\")",
        "CREATE INDEX IF NOT EXISTS \"IX_ShippingAddresses_TenantId_UserId\" ON \"ShippingAddresses\" (\"TenantId\", \"UserId\")",
        "CREATE INDEX IF NOT EXISTS \"IX_RedemptionHistories_TenantId_UserId\" ON \"RedemptionHistories\" (\"TenantId\", \"UserId\")",
        "CREATE INDEX IF NOT EXISTS \"IX_RecognitionAttachments_TenantId_RecognitionId\" ON \"RecognitionAttachments\" (\"TenantId\", \"RecognitionId\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Integrations_TenantId_Provider_IntegrationType\" ON \"Integrations\" (\"TenantId\", \"Provider\", \"IntegrationType\")",
        "CREATE INDEX IF NOT EXISTS \"IX_SyncLogs_TenantId_IntegrationId_StartedAt\" ON \"SyncLogs\" (\"TenantId\", \"IntegrationId\", \"StartedAt\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_SyncMappings_TenantId_IntegrationId_TargetField\" ON \"SyncMappings\" (\"TenantId\", \"IntegrationId\", \"TargetField\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Badges_TenantId_Name\" ON \"Badges\" (\"TenantId\", \"Name\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_UserBadges_TenantId_UserId_BadgeId\" ON \"UserBadges\" (\"TenantId\", \"UserId\", \"BadgeId\")",
        "CREATE INDEX IF NOT EXISTS \"IX_Leaderboards_TenantId_Period_Metric\" ON \"Leaderboards\" (\"TenantId\", \"Period\", \"Metric\")",
        "CREATE INDEX IF NOT EXISTS \"IX_AchievementRules_TenantId_TriggerMetric_Threshold\" ON \"AchievementRules\" (\"TenantId\", \"TriggerMetric\", \"Threshold\")",
        "CREATE INDEX IF NOT EXISTS \"IX_Reports_TenantId_CreatedByUserId\" ON \"Reports\" (\"TenantId\", \"CreatedByUserId\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AnalyticsCaches_TenantId_CacheKey\" ON \"AnalyticsCaches\" (\"TenantId\", \"CacheKey\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TenantBillings_TenantId\" ON \"TenantBillings\" (\"TenantId\")",
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_FeatureFlags_TenantId_Key\" ON \"FeatureFlags\" (\"TenantId\", \"Key\")"
    ];

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (tableName, domainColumns) in Tables)
        {
            await db.Database.ExecuteSqlRawAsync(BuildCreateTableSql(tableName, domainColumns), cancellationToken);
        }

        foreach (var indexSql in Indexes)
        {
            await db.Database.ExecuteSqlRawAsync(indexSql, cancellationToken);
        }
    }

    private static string BuildCreateTableSql(string tableName, IEnumerable<string> domainColumns)
    {
        var columns = string.Join(",\n                    ", new[]
        {
            "\"Id\" uuid NOT NULL",
            "\"CreatedAt\" timestamp with time zone NOT NULL DEFAULT NOW()",
            "\"UpdatedAt\" timestamp with time zone NULL",
            "\"DeletedAt\" timestamp with time zone NULL"
        }.Concat(domainColumns));

        return $"CREATE TABLE IF NOT EXISTS \"{tableName}\" (\n                    {columns},\n                    CONSTRAINT \"PK_{tableName}\" PRIMARY KEY (\"Id\")\n                );";
    }
}
