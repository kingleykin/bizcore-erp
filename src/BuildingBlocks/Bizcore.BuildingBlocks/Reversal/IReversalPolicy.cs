using System.Security.Claims;

namespace Bizcore.BuildingBlocks.Reversal
{
    /// <summary>
    /// Dynamic policy quyết định field nào có thể restore, dựa trên
    /// trạng thái hiện tại của entity và quyền hạn của actor.
    /// Mỗi Domain Service tự implement interface này để kiểm soát logic của mình.
    /// </summary>
    public interface IReversalPolicy<T>
    {
        ReversalDecision CanRestore(string field, T currentEntity, ClaimsPrincipal actor);
    }

    /// <summary>Kết quả của một quyết định reversal — Allow hoặc Deny kèm lý do.</summary>
    public record ReversalDecision(bool IsAllowed, string Reason)
    {
        public static ReversalDecision Allow()
            => new(true, "Field có thể khôi phục.");

        public static ReversalDecision Deny(string reason)
            => new(false, reason);
    }
}
