using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.Services.Dispatch.Common;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Application,
    "운영 배차 활동 사건의 멱등 추가와 기간 조회를 영속 원장에 요청한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 40,
    StepKey = "application.operational-dispatch-ledger-port",
    DependsOnStepKeys = ["contract.operational-dispatch-activity"],
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "구현체는 사건 Stable ID의 멱등성을 보장해야 하며 Redis 임시 자료를 권위 원장으로 사용할 수 없다.")]
public interface I운영배차활동원장Store
{
    ValueTask<운영배차활동사건Dto?> 사건조회Async(
        string 사건StableId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> 사건추가Async(
        운영배차활동사건Dto 사건,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<운영배차활동사건Dto>> 기간조회Async(
        string 주체Id,
        DateTimeOffset 시작시각UtcInclusive,
        DateTimeOffset 종료시각UtcExclusive,
        CancellationToken cancellationToken = default);
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Application,
    "배차 판단에 필요한 수신 상태와 단기 지표의 재구성 가능한 투영을 읽고 갱신한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 50,
    StepKey = "application.operational-dispatch-decision-projection-port",
    DependsOnStepKeys = ["application.operational-dispatch-ledger-port", "domain.operational-dispatch-short-window"],
    ExecutionStage = SsalddelCodeExecutionStage.Projection,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "투영은 빠른 판정용 읽기 모델이며 영속 활동 원장에서 재구성할 수 있어야 한다. 투영만으로 책임이나 운영 결과를 확정하지 않는다.")]
public interface I운영배차판정ProjectionStore
{
    ValueTask<운영배차수신상태Dto?> 수신상태조회Async(
        string 주체Id,
        CancellationToken cancellationToken = default);

    ValueTask 수신상태저장Async(
        운영배차수신상태Dto 상태,
        CancellationToken cancellationToken = default);

    ValueTask<운영배차단기지표Dto?> 단기지표조회Async(
        string 주체Id,
        CancellationToken cancellationToken = default);

    ValueTask 단기지표저장Async(
        string 주체Id,
        운영배차단기지표Dto 지표,
        CancellationToken cancellationToken = default);
}
