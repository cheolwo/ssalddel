using System;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.WorkflowRules
{
    public static class 업무상태전이Policy
    {
        public static 업무상태전이판정 판정(
            string 업무코드,
            string 현재상태코드,
            string 목표상태코드)
        {
            var decision = BusinessWorkflowRuleEngine.기본.판정(
                new BusinessWorkflowTransitionRequest
            {
                업무흐름코드 = 업무코드,
                현재상태코드 = 현재상태코드,
                목표상태코드 = 목표상태코드,
            });
            return new 업무상태전이판정
            {
                허용여부 = decision.허용여부,
                멱등재시도여부 = decision.멱등재시도여부,
                RuleRevision = decision.RuleRevision,
                SourceStableIds = decision.SourceStableIds.ToArray(),
                차단사유코드목록 = decision.차단사유코드목록.ToArray(),
            };
        }
    }

    public static class 업무수량보존Policy
    {
        public static 업무수량보존판정 판정(업무수량보존요청 request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.입력수량 < 0m
                || request.결과수량 < 0m
                || request.손실수량 < 0m
                || request.허용오차 < 0m)
            {
                return new 업무수량보존판정
                {
                    단위코드 = request.단위코드,
                    차단사유코드목록 = new[] { 업무규칙차단사유코드.음수수량 },
                };
            }

            var difference = request.입력수량 - request.결과수량 - request.손실수량;
            var conserved = Math.Abs(difference) <= request.허용오차;
            return new 업무수량보존판정
            {
                보존여부 = conserved,
                차이수량 = difference,
                단위코드 = request.단위코드,
                차단사유코드목록 = conserved
                    ? Array.Empty<string>()
                    : new[] { 업무규칙차단사유코드.수량불일치 },
            };
        }
    }

    public static class 같이주문상태Policy
    {
        public static 같이주문상태판정 판정(같이주문상태판정요청 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.참여자수 < 0
                || request.총희망수량 < 0m
                || request.최소참여자수 <= 0
                || request.기본확정대기참여자수 <= 0
                || request.기본확정대기수량 <= 0m
                || request.목표참여자수 is <= 0
                || request.목표수량 is <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request));
            }

            var rule = 업무흐름규칙Catalog.조회(업무흐름코드.같이주문);
            var minimumMet = request.참여자수 >= request.최소참여자수;
            var participantTargetMet = !request.목표참여자수.HasValue
                || request.참여자수 >= request.목표참여자수.Value;
            var quantityTargetMet = !request.목표수량.HasValue
                || request.총희망수량 >= request.목표수량.Value;
            var hasExplicitTarget = request.목표참여자수.HasValue || request.목표수량.HasValue;
            var explicitTargetMet = hasExplicitTarget && participantTargetMet && quantityTargetMet;
            var ready = minimumMet && (explicitTargetMet
                || !hasExplicitTarget
                    && (request.참여자수 >= request.기본확정대기참여자수
                        || request.총희망수량 >= request.기본확정대기수량));

            return new 같이주문상태판정
            {
                제안상태코드 = ready
                    ? 같이주문상태코드.확정대기
                    : 같이주문상태코드.수요수집중,
                모집종료결과상태코드 = ready
                    ? 같이주문상태코드.확정
                    : 같이주문상태코드.모집종료목표미달,
                최소참여자충족여부 = minimumMet,
                목표참여자충족여부 = participantTargetMet,
                목표수량충족여부 = quantityTargetMet,
                명시목표충족여부 = explicitTargetMet,
                RuleRevision = rule.RuleRevision,
                SourceStableIds = rule.SourceStableIds.ToArray(),
            };
        }
    }
}
