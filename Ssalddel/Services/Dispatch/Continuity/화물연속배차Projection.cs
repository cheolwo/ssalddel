using Ssalddel.Contracts.Common.Dispatch;

namespace 살뜰.Services.Dispatch.Continuity;

public interface I화물연속배차ProjectionStore
{
    ValueTask<화물연속배차상태Dto?> 조회Async(string 기사Id, CancellationToken cancellationToken = default);
    ValueTask 저장Async(화물연속배차상태Dto 상태, CancellationToken cancellationToken = default);
}
