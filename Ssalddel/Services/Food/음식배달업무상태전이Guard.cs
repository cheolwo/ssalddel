using Ssalddel.Contracts.Food;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Services.Food;

internal static class 음식배달업무상태전이Guard
{
    internal static 업무상태전이판정 판정(
        string? 현재상태,
        string? 목표상태,
        IBusinessWorkflowRuleEngine? engine = null)
    {
        var decision = (engine ?? BusinessWorkflowRuleEngine.기본).판정(
            new BusinessWorkflowTransitionRequest
            {
                업무흐름코드 = 업무흐름코드.음식배달,
                현재상태코드 = 음식주문상태코드.Normalize(현재상태),
                목표상태코드 = 음식주문상태코드.Normalize(목표상태),
            });
        return new 업무상태전이판정
        {
            허용여부 = decision.허용여부,
            멱등재시도여부 = decision.멱등재시도여부,
            RuleRevision = decision.RuleRevision,
            SourceStableIds = decision.SourceStableIds,
            차단사유코드목록 = decision.차단사유코드목록,
        };
    }

    internal static void 허용확인(string? 현재상태, string? 목표상태)
    {
        var result = 판정(현재상태, 목표상태);
        if (result.허용여부)
        {
            return;
        }

        throw new InvalidOperationException(
            $"음식배달 공통 업무 규칙이 허용하지 않는 상태 전이입니다. "
            + $"현재상태={음식주문상태코드.Normalize(현재상태)}, "
            + $"목표상태={음식주문상태코드.Normalize(목표상태)}, "
            + $"규칙판본={result.RuleRevision}, "
            + $"차단사유={string.Join(',', result.차단사유코드목록)}");
    }
}
