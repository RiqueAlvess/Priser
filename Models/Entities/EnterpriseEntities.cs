namespace Priser.Models.Entities;

public class ComplianceSetting : BaseEntity
{
    public Guid TenantId { get; set; }
    public int DataRetentionDays { get; set; } = 2555;
    public int AuditRetentionDays { get; set; } = 2555;
    public bool SsoEnforced { get; set; }
    public bool MfaRequired { get; set; }
    public bool ImmutableBackupsEnabled { get; set; } = true;
    public string EncryptionMode { get; set; } = "application";
}

public class UserCustomField : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string FieldKey { get; set; } = "";
    public string FieldValue { get; set; } = "";
    public bool IsSensitive { get; set; }
}

public class UserPermission : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string PermissionKey { get; set; } = "";
    public bool IsGranted { get; set; } = true;
    public Guid? GrantedByUserId { get; set; }
}

public class PointPool : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "Corporate Pool";
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "PTS";
    public string Status { get; set; } = "active";
}

public class ApprovalHistory : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid ApprovalRequestId { get; set; }
    public Guid ActorId { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string? Notes { get; set; }
}

public class AutomationRule : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string TriggerType { get; set; } = "anniversary";
    public string ConditionsJson { get; set; } = "{}";
    public string ActionsJson { get; set; } = "{}";
    public string ScheduleCron { get; set; } = "0 8 * * *";
    public bool IsActive { get; set; } = true;
}

public class MilestoneEvent : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? AutomationRuleId { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string PayloadJson { get; set; } = "{}";
    public DateTime? ProcessedAt { get; set; }
}

public class MemoryBook : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid RecipientId { get; set; }
    public string Title { get; set; } = "";
    public string Occasion { get; set; } = "anniversary";
    public DateTime DueAt { get; set; }
    public string Status { get; set; } = "collecting";
}

public class MemoryBookEntry : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid MemoryBookId { get; set; }
    public Guid AuthorId { get; set; }
    public string Message { get; set; } = "";
    public string? MediaUrl { get; set; }
    public string Status { get; set; } = "submitted";
}

public class ShippingAddress : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string RecipientName { get; set; } = "";
    public string EncryptedAddress { get; set; } = "";
    public string Country { get; set; } = "";
    public bool IsDefault { get; set; }
}

public class RedemptionHistory : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public decimal PointsSpent { get; set; }
    public decimal FiatAmount { get; set; }
    public string Status { get; set; } = "completed";
}

public class RecognitionAttachment : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid RecognitionId { get; set; }
    public string Url { get; set; } = "";
    public string AttachmentType { get; set; } = "image";
    public string? AltText { get; set; }
}

public class Integration : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = "manual";
    public string IntegrationType { get; set; } = "hris";
    public string Status { get; set; } = "disabled";
    public string SettingsJson { get; set; } = "{}";
    public DateTime? LastSyncedAt { get; set; }
}

public class SyncLog : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid IntegrationId { get; set; }
    public string Status { get; set; } = "queued";
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public string? ErrorSummary { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}

public class SyncMapping : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid IntegrationId { get; set; }
    public string SourceField { get; set; } = "";
    public string TargetField { get; set; } = "";
    public bool IsRequired { get; set; }
}

public class Badge : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string CriteriaJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
}

public class UserBadge : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid BadgeId { get; set; }
    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
    public Guid? AwardedByUserId { get; set; }
}

public class Leaderboard : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Period { get; set; } = "monthly";
    public Guid? DepartmentId { get; set; }
    public string Metric { get; set; } = "recognitions_received";
    public bool IsActive { get; set; } = true;
}

public class AchievementRule : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string TriggerMetric { get; set; } = "recognitions_received";
    public int Threshold { get; set; } = 1;
    public Guid? BadgeId { get; set; }
    public int RewardPoints { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Report : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Name { get; set; } = "";
    public string ReportType { get; set; } = "engagement";
    public string FiltersJson { get; set; } = "{}";
    public string Visibility { get; set; } = "private";
}

public class AnalyticsCache : BaseEntity
{
    public Guid TenantId { get; set; }
    public string CacheKey { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public DateTime ExpiresAt { get; set; }
}

public class TenantBilling : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Plan { get; set; } = "starter";
    public decimal MonthlyFee { get; set; }
    public decimal PointsLiability { get; set; }
    public string InvoiceStatus { get; set; } = "current";
    public DateTime? CurrentPeriodEndsAt { get; set; }
}

public class FeatureFlag : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Key { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string? ConfigurationJson { get; set; }
}
