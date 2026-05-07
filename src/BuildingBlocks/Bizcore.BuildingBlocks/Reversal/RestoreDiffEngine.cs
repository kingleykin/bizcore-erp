using System.Security.Claims;
using System.Text.Json;

namespace Bizcore.BuildingBlocks.Reversal
{
    /// <summary>
    /// Kết quả diff của một field đơn lẻ: current vs previous, có thể restore hay không.
    /// </summary>
    public class FieldDiff
    {
        public string  Field          { get; init; } = null!;
        public string? CurrentValue   { get; init; }
        public string? PreviousValue  { get; init; }
        public bool    HasChanged     { get; init; }
        public bool    CanRestore     { get; init; }
        public string  Reason         { get; init; } = null!;
        public string  SuggestedAction => CanRestore ? "Restore" : "Manual Action Required";
    }

    /// <summary>
    /// Tổng hợp kết quả diff của toàn bộ entity — được trả về cho Admin UI.
    /// </summary>
    public record RestoreSuggestion(
        Guid                      AuditEntryId,
        string                    EntityType,
        string                    EntityId,
        DateTime                  OriginalChangedAt,
        string                    OriginalAction,
        IReadOnlyList<FieldDiff>  Fields,
        int                       RestorableCount,
        int                       BlockedCount
    );

    /// <summary>
    /// Engine so sánh BeforeJson (AuditEntry) với current entity state.
    /// Áp dụng IReversalPolicy để đánh dấu từng field: Restore / Manual Action Required.
    /// </summary>
    public static class RestoreDiffEngine
    {
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static RestoreSuggestion ComputeDiff<TEntity>(
            Guid                      auditEntryId,
            string                    entityType,
            string                    entityId,
            DateTime                  changedAt,
            string                    originalAction,
            string                    beforeJson,
            TEntity                   currentEntity,
            IReversalPolicy<TEntity>  policy,
            ClaimsPrincipal           actor)
        {
            // Serialize current entity → JSON để so sánh field-by-field
            var currentJson = JsonSerializer.Serialize(currentEntity, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var beforeDoc  = JsonDocument.Parse(beforeJson);
            var currentDoc = JsonDocument.Parse(currentJson);

            var diffs = new List<FieldDiff>();

            foreach (var beforeProp in beforeDoc.RootElement.EnumerateObject())
            {
                var fieldName    = beforeProp.Name;
                var previousVal  = ExtractValue(beforeProp.Value);

                // Lấy current value của field tương ứng
                string? currentVal = null;
                if (currentDoc.RootElement.TryGetProperty(fieldName, out var currentProp))
                    currentVal = ExtractValue(currentProp);

                // Kiểm tra field bị masked ("***") → không thể compare hay restore
                if (previousVal == "***")
                {
                    diffs.Add(new FieldDiff
                    {
                        Field         = fieldName,
                        CurrentValue  = "***",
                        PreviousValue = "***",
                        HasChanged    = false,
                        CanRestore    = false,
                        Reason        = "Field nhạy cảm — đã được che giấu, không thể restore."
                    });
                    continue;
                }

                bool hasChanged = !string.Equals(previousVal, currentVal, StringComparison.Ordinal);

                // Hỏi policy: field này có được restore không?
                var decision = policy.CanRestore(fieldName, currentEntity, actor);

                diffs.Add(new FieldDiff
                {
                    Field         = fieldName,
                    CurrentValue  = currentVal,
                    PreviousValue = previousVal,
                    HasChanged    = hasChanged,
                    CanRestore    = hasChanged && decision.IsAllowed,
                    Reason        = hasChanged
                        ? decision.Reason
                        : "Giá trị không thay đổi so với audit entry này."
                });
            }

            // Chỉ đưa vào result những field thực sự thay đổi
            var changed = diffs
                .Where(d => d.HasChanged)
                .OrderBy(d => d.Field)
                .ToList();

            return new RestoreSuggestion(
                auditEntryId,
                entityType,
                entityId,
                changedAt,
                originalAction,
                changed,
                RestorableCount: changed.Count(d => d.CanRestore),
                BlockedCount:    changed.Count(d => !d.CanRestore)
            );
        }

        private static string? ExtractValue(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String  => el.GetString(),
            JsonValueKind.Null    => null,
            _                     => el.GetRawText()
        };
    }
}
