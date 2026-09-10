using System;
using System.Net.Http;
using System.Text.Json;
using Ssalddel.BusinessWorkflow;
using Ssalddel.Simulation.Application;
using Ssalddel.WorkflowRules;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Infrastructure
{
    /// <summary>웹·모바일이 사용할 RemoteHost 업무 흐름 Runtime 조립기다.</summary>
    public static class RemoteBusinessWorkflowRuntimeFactory
    {
        public static IBusinessWorkflowRuntime Create(
            HttpClient client,
            IBusinessWorkflowRuleEngine? rules = null,
            JsonSerializerOptions? jsonOptions = null)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            var selectedRules = rules ?? BusinessWorkflowRuleEngine.기본;
            return new BusinessWorkflowRuntime(
                new RemoteSimulationFoodOrderRuntime(client, jsonOptions),
                new RemoteSimulationLogisticsRuntime(client, jsonOptions),
                selectedRules,
                new BusinessWorkflowRuntimeDescriptor
                {
                    RuntimeStableId = "business-workflow-runtime:remote-host",
                    ModeCode = BusinessWorkflowRuntimeModeCodes.RemoteHost,
                    RequiresNetwork = true,
                    ContractRevision = LocalBusinessWorkflowRuntimeFactory.ContractRevision,
                    ClassificationMetadata =
                        selectedRules.Engine정보조회().ClassificationMetadata,
                });
        }
    }
}
