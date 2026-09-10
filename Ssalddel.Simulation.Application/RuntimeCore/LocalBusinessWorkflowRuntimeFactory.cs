using System;
using Ssalddel.BusinessWorkflow;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Application
{
    public static class LocalBusinessWorkflowRuntimeFactory
    {
        public const string ContractRevision = "business-workflow-runtime.r1";

        public static IBusinessWorkflowRuntime Create(
            LocalSimulationRuntime runtime,
            IBusinessWorkflowRuleEngine rules)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            return new BusinessWorkflowRuntime(
                runtime,
                runtime,
                rules,
                new BusinessWorkflowRuntimeDescriptor
                {
                    RuntimeStableId = runtime.Descriptor.RuntimeStableId,
                    ModeCode = BusinessWorkflowRuntimeModeCodes.LocalProcess,
                    RequiresNetwork = false,
                    ContractRevision = ContractRevision,
                    ClassificationMetadata =
                        rules.Engine정보조회().ClassificationMetadata,
                });
        }
    }
}
