using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.WorkflowRules.Contracts;
using Xunit;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class Simulation음식배달Tests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(20)]
    public void 새시간규칙은_합산Tick과단계별Tick의_주문결과와일정이같다(int ticks)
    {
        var batch = CreateContext(true, true);
        var steps = CreateContext(true, true);
        var a = 조리주문등록(batch);
        var b = 조리주문등록(steps);
        a = batch.Service.Advance(a.SessionStableId, new 경영SimulationTick진행Request
        { CommandId = "command:p0:batch", ExpectedRevision = a.Revision, TickCount = ticks });
        for (var tick = 1; tick <= ticks; tick++) b = Advance(steps, b, tick);
        Assert.Equal(b.FoodDeliveries.Select(order => (order.FoodOrderStableId, order.StateCode, order.CookingStartedTick, order.ReadyForPickupTick)),
            a.FoodDeliveries.Select(order => (order.FoodOrderStableId, order.StateCode, order.CookingStartedTick, order.ReadyForPickupTick)));
        Assert.Equal(b.Tasks.Select(task => (task.TaskStableId, task.StateCode, task.ScheduledStartTick, task.ExpectedEndTick)),
            a.Tasks.Select(task => (task.TaskStableId, task.StateCode, task.ScheduledStartTick, task.ExpectedEndTick)));
        var saved = batch.Service.Save(a.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:batch", ExpectedRevision = a.Revision });
        Assert.Equal(SimulationTickRuleRevisions.SingleStep, saved.TickRuleRevision);
        var restored = SimulationSessionReplay.Restore(saved);
        Assert.Equal(saved.ReplayHash, restored.CreateSavePackage(new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:restored", ExpectedRevision = a.Revision }).ReplayHash);
    }

    [Fact]
    public void 구형시간규칙은_합산방식을유지하고_복원후저장에도판본을보존한다()
    {
        var context = CreateContext(true, true);
        var genesis = context.Service.Save(context.Session.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:legacy-genesis", ExpectedRevision = context.Session.Revision });
        genesis.TickRuleRevision = string.Empty;
        genesis.ReplayHash = SimulationReplayHasher.Calculate(genesis);
        // JSON에서 새 필드가 없었던 구형 자료를 재현한다.
        var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(genesis))!.AsObject();
        json.Remove(nameof(SimulationSessionSavePackage.TickRuleRevision));
        var legacy = SimulationSessionReplay.Restore(System.Text.Json.JsonSerializer.Deserialize<SimulationSessionSavePackage>(json.ToJsonString())!);
        var food = FoodDelivery();
        food.RestaurantPreparationOnly = food.AwaitRestaurantResponse = food.NpcAutoAccept = true;
        food.ActorStableId = food.OrdererStableId;
        var current = legacy.ConfirmFoodDelivery(new Simulation음식배달ConfirmRequest
        { CommandId = "command:p0:legacy-submit", ExpectedRevision = genesis.SavedWorldRevision, FoodDelivery = food });
        current = legacy.Advance(new 경영SimulationTick진행Request
        { CommandId = "command:p0:legacy-tick", ExpectedRevision = current.Revision, TickCount = 5 });
        Assert.Null(Assert.Single(current.FoodDeliveries).CookingStartedTick);
        Assert.Equal(6, Assert.Single(current.Tasks, task => task.TaskTypeCode == "FoodPreparationOnly").ScheduledStartTick);
        var saved = legacy.CreateSavePackage(new SimulationSessionSaveRequest { SaveStableId = "save:p0:legacy", ExpectedRevision = current.Revision });
        Assert.Empty(saved.TickRuleRevision);
        var replayed = SimulationSessionReplay.Restore(saved);
        Assert.Equal(saved.ReplayHash, replayed.CreateSavePackage(new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:legacy-again", ExpectedRevision = current.Revision }).ReplayHash);
        saved.TickRuleRevision = SimulationTickRuleRevisions.SingleStep;
        Assert.Equal("SimulationReplayHashMismatch",
            Assert.Throws<SimulationConflictException>(() => SimulationSessionReplay.Restore(saved)).Message);
        saved.TickRuleRevision = "world-tick:unknown";
        Assert.Throws<SimulationContractException>(() => SimulationSessionReplay.Restore(saved));
    }

    [Fact]
    public void 새시간규칙의_턴마감은_일반Tick과같은음식점처리순서로진행하고_재생된다()
    {
        var ticks = CreateContext(true, true);
        var turns = CreateContext(true, true);
        var a = 조리주문등록(ticks);
        var b = 조리주문등록(turns);
        for (var tick = 1; tick <= 7; tick++)
        {
            a = Advance(ticks, a, tick);
            b = turns.Service.ConfirmTurnClosing(b.SessionStableId, new SimulationTurnClosingConfirmRequest
            {
                CommandId = "command:p0:close:" + tick, ExpectedRevision = b.Revision,
                Preview = new SimulationTurnClosingPreviewRequest { ExpectedRevision = b.Revision },
            });
            Assert.Equal(a.FoodDeliveries.Select(order => (order.FoodOrderStableId, order.StateCode, order.CookingStartedTick, order.ReadyForPickupTick)),
                b.FoodDeliveries.Select(order => (order.FoodOrderStableId, order.StateCode, order.CookingStartedTick, order.ReadyForPickupTick)));
            Assert.Equal(a.Tasks.Select(task => (task.TaskStableId, task.StateCode, task.ScheduledStartTick, task.ExpectedEndTick)),
                b.Tasks.Select(task => (task.TaskStableId, task.StateCode, task.ScheduledStartTick, task.ExpectedEndTick)));
        }
        var saved = turns.Service.Save(b.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:turns", ExpectedRevision = b.Revision });
        var restored = SimulationSessionReplay.Restore(saved);
        Assert.Equal(saved.ReplayHash, restored.CreateSavePackage(new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:turns-restored", ExpectedRevision = b.Revision }).ReplayHash);
    }

    [Fact]
    public void 구형저장의_턴마감은_음식점자동수락을새로실행하지않는다()
    {
        var context = CreateContext(true, true);
        var before = 조리주문등록(context);
        var saved = context.Service.Save(before.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:old-closing", ExpectedRevision = before.Revision });
        saved.TickRuleRevision = string.Empty;
        saved.ReplayHash = SimulationReplayHasher.Calculate(saved);
        var legacy = SimulationSessionReplay.Restore(saved);
        var after = legacy.ConfirmTurnClosing(new SimulationTurnClosingConfirmRequest
        {
            CommandId = "command:p0:old-closing", ExpectedRevision = before.Revision,
            Preview = new SimulationTurnClosingPreviewRequest { ExpectedRevision = before.Revision },
        });
        Assert.All(after.FoodDeliveries, order => Assert.Empty(order.RestaurantResponseDecisionStableId));
        Assert.All(after.Tasks, task => Assert.Equal("FoodOrderNpcSubmission", task.TaskTypeCode));
        var result = legacy.CreateSavePackage(new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:old-closing-result", ExpectedRevision = after.Revision });
        Assert.Empty(result.TickRuleRevision);
        Assert.Equal(result.ReplayHash, SimulationSessionReplay.Restore(result).CreateSavePackage(new SimulationSessionSaveRequest
        { SaveStableId = "save:p0:old-closing-again", ExpectedRevision = after.Revision }).ReplayHash);
    }

    private static 경영SimulationSessionSnapshot 조리주문등록(Context context)
    {
        var current = context.Session;
        for (var i = 1; i <= 3; i++)
        {
            var food = FoodDelivery(); food.FoodOrderStableId = "food-order:p0:" + i;
            food.RestaurantPreparationOnly = food.AwaitRestaurantResponse = food.NpcAutoAccept = true;
            food.ActorStableId = food.OrdererStableId;
            current = context.Service.ConfirmFoodDelivery(current.SessionStableId, new Simulation음식배달ConfirmRequest
            { CommandId = "command:p0:" + i, ExpectedRevision = current.Revision, FoodDelivery = food });
        }
        return current;
    }

    [Fact]
    public void 조리정책중지는_진행중작업을취소하지않고_새작업만대기시킨다()
    {
        var context = CreateContext(true, true);
        var current = context.Session;
        for (var i = 1; i <= 2; i++)
        {
            var food = FoodDelivery(); food.FoodOrderStableId = "food-order:pause:" + i;
            food.RestaurantPreparationOnly = food.AwaitRestaurantResponse = food.NpcAutoAccept = true;
            food.ActorStableId = food.OrdererStableId;
            current = context.Service.ConfirmFoodDelivery(current.SessionStableId, new Simulation음식배달ConfirmRequest
            { CommandId = "command:pause:" + i, ExpectedRevision = current.Revision, FoodDelivery = food });
        }
        current = Advance(context, current, 1);
        var request = new SimulationNpcPolicyChangeRequest
        { CommandId = "command:pause:policy", ExpectedRevision = current.Revision, PolicyStableId = "policy:restaurant:cooking",
            AutomationEnabled = false, CookingSlots = 0 };
        Assert.Throws<SimulationContractException>(() => context.Service.UpdateNpcPolicy(current.SessionStableId, request));
        request.CookingSlots = 1;
        current = context.Service.UpdateNpcPolicy(current.SessionStableId, request);
        for (var i = 2; i <= 4; i++) current = Advance(context, current, i);
        Assert.Single(current.FoodDeliveries, order => order.ReadyForPickupTick.HasValue);
        Assert.Single(current.FoodDeliveries, order => !order.CookingStartedTick.HasValue);
        request.CommandId = "command:resume:stale";
        request.AutomationEnabled = true;
        Assert.Throws<SimulationConflictException>(() => context.Service.UpdateNpcPolicy(current.SessionStableId, request));
        request.ExpectedRevision = current.Revision;
        current = context.Service.UpdateNpcPolicy(current.SessionStableId, request);
        for (var i = 5; i <= 7; i++) current = Advance(context, current, i);
        Assert.All(current.FoodDeliveries, order => Assert.NotNull(order.ReadyForPickupTick));
    }

    [Fact]
    public void 조리자리는_FIFO로배정하고_진행중설정변경은_대기주문에만적용하며_재생된다()
    {
        var context = CreateContext(true, true);
        var current = context.Session;
        for (var i = 1; i <= 3; i++)
        {
            var food = FoodDelivery();
            food.FoodOrderStableId = "food-order:queue:" + i;
            food.RestaurantPreparationOnly = food.AwaitRestaurantResponse = food.NpcAutoAccept = true;
            food.ActorStableId = food.OrdererStableId;
            current = context.Service.ConfirmFoodDelivery(current.SessionStableId, new Simulation음식배달ConfirmRequest
            { CommandId = "command:queue:" + i, ExpectedRevision = current.Revision, FoodDelivery = food });
        }
        current = Advance(context, current, 1);
        Assert.Single(current.Tasks, task => task.TaskTypeCode == "FoodPreparationOnly");
        current = Advance(context, current, 2);
        Assert.Equal("food-order:queue:1", Assert.Single(current.FoodDeliveries, order => order.CookingStartedTick.HasValue).FoodOrderStableId);
        current = context.Service.UpdateNpcPolicy(current.SessionStableId, new SimulationNpcPolicyChangeRequest
        { CommandId = "command:queue:policy", ExpectedRevision = current.Revision, PolicyStableId = "policy:restaurant:cooking",
            AutomationEnabled = true, CookingSlots = 2, CookingDurationTicks = 1 });
        Assert.Equal(2, current.FoodDeliveries.Single(order => order.FoodOrderStableId == "food-order:queue:1").PreparationDurationTicks);
        current = Advance(context, current, 3);
        Assert.Equal(2, current.FoodDeliveries.Count(order => order.PreparationDurationTicks == 1));
        current = Advance(context, current, 4);
        Assert.All(current.FoodDeliveries, order => Assert.Equal(음식배달상태코드.픽업대기, order.StateCode));
        var saved = context.Service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:cooking:ready", ExpectedRevision = current.Revision });
        var restored = new 경영SimulationSessionService(new InMemory경영SimulationSessionStore(), context.SaveStore)
            .Restore(new SimulationSessionRestoreRequest { SaveStableId = saved.SaveStableId });
        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
        Assert.Equal(2, Assert.Single(restored.Session.NpcWorkPolicies).CookingSlots);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NPC자동수락은_다음Tick에권한을확인하고_조리대기만만들며_재생된다(bool authorized)
    {
        var context = CreateContext(authorized);
        var food = FoodDelivery();
        food.RestaurantPreparationOnly = true;
        food.AwaitRestaurantResponse = true;
        food.NpcAutoAccept = true;
        food.ActorStableId = food.OrdererStableId;
        var current = context.Service.ConfirmFoodDelivery(context.Session.SessionStableId,
            new Simulation음식배달ConfirmRequest { CommandId = "command:auto:submit", ExpectedRevision = context.Session.Revision, FoodDelivery = food });
        Assert.Empty(Assert.Single(current.FoodDeliveries).RestaurantResponseDecisionStableId);
        current = Advance(context, current, 1);
        Assert.Equal(authorized, !string.IsNullOrEmpty(Assert.Single(current.FoodDeliveries).RestaurantResponseDecisionStableId));
        for (var tick = 2; tick <= 5; tick++) current = Advance(context, current, tick);
        var order = Assert.Single(current.FoodDeliveries);
        Assert.Null(order.CookingStartedTick);
        Assert.Null(order.DispatchCandidateTick);
        Assert.Empty(order.StateHistory);
        Assert.Equal(authorized ? 2 : 1, current.Decisions.Length);
        Assert.Equal(authorized ? "FoodOrderCookingQueued" : "FoodOrderNpcSubmission",
            current.Tasks.Single(task => task.TaskStableId == order.TaskStableId).TaskTypeCode);
        var saved = context.Service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:auto:queued", ExpectedRevision = current.Revision });
        var restored = new 경영SimulationSessionService(new InMemory경영SimulationSessionStore(), context.SaveStore)
            .Restore(new SimulationSessionRestoreRequest { SaveStableId = saved.SaveStableId });
        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
        Assert.Equal(current.Decisions.Length, restored.Session.Decisions.Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 주문대기_응답권한_수락거절_멱등_저장재생(bool accept)
    {
        var context = CreateContext(true);
        var food = FoodDelivery();
        food.RestaurantPreparationOnly = true;
        food.AwaitRestaurantResponse = true;
        food.ActorStableId = food.OrdererStableId;
        var current = context.Service.ConfirmFoodDelivery(context.Session.SessionStableId,
            new Simulation음식배달ConfirmRequest { CommandId = "command:inbox:submit", ExpectedRevision = context.Session.Revision, FoodDelivery = food });
        for (var tick = 1; tick <= 3; tick++) current = Advance(context, current, tick);
        var order = Assert.Single(current.FoodDeliveries);
        Assert.Equal(음식배달상태코드.주문대기, order.StateCode);
        Assert.Empty(order.StateHistory);
        var response = new Simulation음식점응답Request
        {
            CommandId = "command:inbox:response", ExpectedRevision = current.Revision,
            FoodOrderStableId = order.FoodOrderStableId, FoodOrderRevision = order.Revision,
            ActorStableId = food.OrdererStableId, Accept = accept,
            RejectionReasonCode = accept ? string.Empty : "reason:capacity",
        };
        Assert.Throws<SimulationContractException>(() => context.Service.ConfirmRestaurantResponse(current.SessionStableId, response));
        response.ActorStableId = "actor:sim.restaurant-1";
        response.FoodOrderRevision++;
        Assert.Throws<SimulationConflictException>(() => context.Service.ConfirmRestaurantResponse(current.SessionStableId, response));
        response.FoodOrderRevision--;
        response.ExpectedRevision--;
        Assert.Throws<SimulationConflictException>(() => context.Service.ConfirmRestaurantResponse(current.SessionStableId, response));
        response.ExpectedRevision++;
        if (!accept)
        {
            response.RejectionReasonCode = string.Empty;
            Assert.Throws<SimulationContractException>(() => context.Service.ConfirmRestaurantResponse(current.SessionStableId, response));
            response.RejectionReasonCode = "reason:capacity";
        }
        var pending = context.Service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:inbox:pending", ExpectedRevision = current.Revision });
        var restoredService = new 경영SimulationSessionService(new InMemory경영SimulationSessionStore(), context.SaveStore);
        var pendingRestore = restoredService.Restore(new SimulationSessionRestoreRequest { SaveStableId = pending.SaveStableId });
        Assert.Equal(pending.ReplayHash, pendingRestore.ReplayHash);
        current = restoredService.ConfirmRestaurantResponse(current.SessionStableId, response);
        Assert.Equal(current.Revision, restoredService.ConfirmRestaurantResponse(current.SessionStableId, response).Revision);
        context = new Context(restoredService, context.SaveStore, context.Session);
        for (var tick = 4; tick <= 8; tick++) current = Advance(context, current, tick);
        order = Assert.Single(current.FoodDeliveries);
        Assert.Equal(accept ? 음식배달상태코드.픽업대기 : 음식배달상태코드.거절, order.StateCode);
        Assert.Equal(response.RejectionReasonCode, order.RejectionReasonCode);
        Assert.Null(order.DispatchCandidateTick);
        Assert.Null(order.DeliveredTick);
        Assert.NotEmpty(order.RestaurantResponseDecisionStableId);
        var saved = context.Service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:inbox:responded", ExpectedRevision = current.Revision });
        var replayService = new 경영SimulationSessionService(new InMemory경영SimulationSessionStore(), context.SaveStore);
        var replay = replayService.Restore(new SimulationSessionRestoreRequest { SaveStableId = saved.SaveStableId });
        Assert.Equal(saved.ReplayHash, replay.ReplayHash);
        replayService.ConfirmRestaurantResponse(current.SessionStableId, response);
        response.CommandId = "command:inbox:second-response";
        response.ExpectedRevision = current.Revision;
        response.FoodOrderRevision = order.Revision;
        Assert.Throws<SimulationConflictException>(() => replayService.ConfirmRestaurantResponse(current.SessionStableId, response));
    }

    [Fact]
    public void 음식점전용처리는_픽업대기에서멈추고_배정전달수령을만들지않는다()
    {
        var context = CreateContext();
        var food = FoodDelivery();
        food.RestaurantPreparationOnly = true;
        var preview = context.Service.PreviewFoodDelivery(context.Session.SessionStableId, food);
        Assert.Equal(2, preview.TotalDurationTicks);
        Assert.Contains("StopsAtPickupReady", preview.BoundaryCodes);
        var request = new Simulation음식배달ConfirmRequest
        { CommandId = "command:restaurant:accept", ExpectedRevision = context.Session.Revision, FoodDelivery = food };
        var current = context.Service.ConfirmFoodDelivery(context.Session.SessionStableId, request);
        Assert.Equal(current.Revision, context.Service.ConfirmFoodDelivery(context.Session.SessionStableId, request).Revision);
        for (var tick = 1; tick <= 10; tick++) current = Advance(context, current, tick);
        var order = Assert.Single(current.FoodDeliveries);
        Assert.Equal(음식배달상태코드.픽업대기, order.StateCode);
        Assert.Equal(2, order.StateHistory.Length);
        Assert.Null(order.DispatchCandidateTick);
        Assert.Null(order.PickedUpTick);
        Assert.Null(order.DeliveredTick);
        Assert.Null(order.ReceivedTick);
        Assert.Equal(SimulationTaskStateCodes.Completed, Assert.Single(current.Tasks).StateCode);
        Assert.Equal(new[] { 음식배달상태코드.픽업대기 }, Assert.Single(current.Tasks).OutputCandidateCodes);
        var saved = context.Service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:restaurant:ready", ExpectedRevision = current.Revision });
        var restored = new 경영SimulationSessionService(new InMemory경영SimulationSessionStore(), context.SaveStore)
            .Restore(new SimulationSessionRestoreRequest { SaveStableId = saved.SaveStableId });
        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
        Assert.Equal(음식배달상태코드.픽업대기, Assert.Single(restored.Session.FoodDeliveries).StateCode);
        Assert.Contains("SimulationFoodDeliveryNotDelivered", context.Service.PreviewFoodDeliveryReceipt(
            current.SessionStableId, Receipt(order.Revision)).Decision.BlockReasonCodes);
    }

    [Fact]
    public void 같은명령을배송형에서음식점전용으로바꾸면_멱등충돌로거부한다()
    {
        var context = CreateContext();
        Confirm(context);
        var food = FoodDelivery();
        food.RestaurantPreparationOnly = true;
        Assert.Throws<SimulationConflictException>(() => context.Service.ConfirmFoodDelivery(
            context.Session.SessionStableId, new Simulation음식배달ConfirmRequest
            { CommandId = "command:food-delivery.accept-1", ExpectedRevision = context.Session.Revision, FoodDelivery = food }));
    }

    [Fact]
    public void Preview는_가상주문경계와예상기간을보여주지만_원장을변경하지않는다()
    {
        var context = CreateContext();

        var preview = context.Service.PreviewFoodDelivery(
            context.Session.SessionStableId,
            FoodDelivery());

        Assert.Equal(음식배달상태코드.주문대기, preview.SuggestedStateCode);
        Assert.Equal(6, preview.TotalDurationTicks);
        Assert.Contains("RealDriverDispatch", preview.ExcludedOperationalEffectCodes);
        Assert.Contains("NoPersonalAddress", preview.BoundaryCodes);
        Assert.Contains("ReceiptRequiresSeparateConfirmation", preview.BoundaryCodes);
        Assert.Equal(SimulationDecisionStateCodes.Previewed,
            preview.CommonDecisionPreview.Decision.StateCode);
        Assert.Empty(context.Service.Get(context.Session.SessionStableId).FoodDeliveries);
    }

    [Fact]
    public void Confirm은_운영주문없이_주문대기원장과생애주기Task를생성한다()
    {
        var context = CreateContext();

        var confirmed = Confirm(context);
        var order = Assert.Single(confirmed.FoodDeliveries);

        Assert.Equal(음식배달상태코드.주문대기, order.StateCode);
        Assert.Equal("menu-item:sim.potato-stew-1", order.MenuItemStableId);
        Assert.Equal("facility:sim.restaurant-1", order.RestaurantFacilityStableId);
        Assert.Equal("facility:sim.residence-1", order.DestinationFacilityStableId);
        Assert.Null(order.DeliveredTick);
        Assert.Equal(SimulationTaskStateCodes.Scheduled,
            confirmed.Tasks.Single(value => value.TaskStableId == order.TaskStableId).StateCode);
    }

    [Fact]
    public void WorldTick은_조리부터전달완료까지_허용된상태전이를순서대로남긴다()
    {
        var context = CreateContext();
        var current = Confirm(context);

        current = Advance(context, current, 1);
        Assert.Equal(음식배달상태코드.조리중, Assert.Single(current.FoodDeliveries).StateCode);
        current = Advance(context, current, 2);
        Assert.Equal(음식배달상태코드.픽업대기, Assert.Single(current.FoodDeliveries).StateCode);
        current = Advance(context, current, 3);
        Assert.Equal(음식배달상태코드.기사배정, Assert.Single(current.FoodDeliveries).StateCode);
        current = Advance(context, current, 4);
        Assert.Equal(음식배달상태코드.픽업완료, Assert.Single(current.FoodDeliveries).StateCode);
        current = Advance(context, current, 5);
        current = Advance(context, current, 6);
        var delivered = Assert.Single(current.FoodDeliveries);

        Assert.Equal(음식배달상태코드.전달완료, delivered.StateCode);
        Assert.Equal(current.CurrentTick, delivered.DeliveredTick);
        Assert.Equal(5, delivered.StateHistory.Length);
        Assert.Equal(new[]
        {
            음식배달상태코드.조리중,
            음식배달상태코드.픽업대기,
            음식배달상태코드.기사배정,
            음식배달상태코드.픽업완료,
            음식배달상태코드.전달완료,
        }, delivered.StateHistory.Select(value => value.ToStateCode).ToArray());
    }

    [Fact]
    public void 수령확인은_전달완료전에는차단되고_별도Confirm과Tick이필요하다()
    {
        var context = CreateContext();
        var confirmed = Confirm(context);
        var early = Receipt(Assert.Single(confirmed.FoodDeliveries).Revision);

        var blocked = context.Service.PreviewFoodDeliveryReceipt(
            context.Session.SessionStableId, early);

        Assert.Contains("SimulationFoodDeliveryNotDelivered", blocked.Decision.BlockReasonCodes);

        var delivered = AdvanceToDelivered(context, confirmed);
        var order = Assert.Single(delivered.FoodDeliveries);
        var receipt = Receipt(order.Revision);
        var scheduled = context.Service.ConfirmFoodDeliveryReceipt(
            context.Session.SessionStableId,
            new Simulation음식배달수령ConfirmRequest
            {
                CommandId = "command:food-delivery.receipt-1",
                ExpectedRevision = delivered.Revision,
                Receipt = receipt,
            });

        Assert.Equal(음식배달상태코드.전달완료,
            Assert.Single(scheduled.FoodDeliveries).StateCode);
        var received = Advance(context, scheduled, 7);
        var completed = Assert.Single(received.FoodDeliveries);
        Assert.Equal(음식배달상태코드.수령확인, completed.StateCode);
        Assert.Equal(received.CurrentTick, completed.ReceivedTick);
    }

    [Fact]
    public void 같은Command재시도는_음식배달원장을중복생성하지않는다()
    {
        var context = CreateContext();
        var request = new Simulation음식배달ConfirmRequest
        {
            CommandId = "command:food-delivery.accept-idempotent",
            ExpectedRevision = context.Session.Revision,
            FoodDelivery = FoodDelivery(),
        };

        var first = context.Service.ConfirmFoodDelivery(context.Session.SessionStableId, request);
        var retry = context.Service.ConfirmFoodDelivery(context.Session.SessionStableId, request);

        Assert.Equal(first.Revision, retry.Revision);
        Assert.Single(retry.FoodDeliveries);
        Assert.Single(retry.Tasks);
    }

    [Fact]
    public void SaveReplay는_수령확인과전체음식배달계보를동일하게복원한다()
    {
        var context = CreateContext();
        var delivered = AdvanceToDelivered(context, Confirm(context));
        var order = Assert.Single(delivered.FoodDeliveries);
        var scheduled = context.Service.ConfirmFoodDeliveryReceipt(
            context.Session.SessionStableId,
            new Simulation음식배달수령ConfirmRequest
            {
                CommandId = "command:food-delivery.receipt-save",
                ExpectedRevision = delivered.Revision,
                Receipt = Receipt(order.Revision),
            });
        var completed = Advance(context, scheduled, 7);
        var package = context.Service.Save(
            context.Session.SessionStableId,
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:sim.food-delivery-1",
                ExpectedRevision = completed.Revision,
            });

        var restoreService = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(), context.SaveStore);
        var restored = restoreService.Restore(new SimulationSessionRestoreRequest
        {
            SaveStableId = package.SaveStableId,
        });
        var restoredOrder = Assert.Single(restored.Session.FoodDeliveries);

        Assert.Equal(package.ReplayHash, restored.ReplayHash);
        Assert.Equal(음식배달상태코드.수령확인, restoredOrder.StateCode);
        Assert.Equal(6, restoredOrder.StateHistory.Length);
        Assert.Equal("participant:sim.orderer-1", restoredOrder.OrdererStableId);
        Assert.Contains("source:fixture.food-delivery-1", restoredOrder.SourceStableIds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void 잘못된수량은_음식배달접수경계에서_원장생성없이거부한다(int quantity)
    {
        var context = CreateContext();
        var request = FoodDelivery();
        request.Quantity = quantity;

        Assert.Throws<SimulationContractException>(() => context.Service.ConfirmFoodDelivery(
            context.Session.SessionStableId, new Simulation음식배달ConfirmRequest
            {
                CommandId = "command:food-delivery.invalid-quantity",
                ExpectedRevision = context.Session.Revision,
                FoodDelivery = request,
            }));

        var current = context.Service.Get(context.Session.SessionStableId);
        Assert.Equal(context.Session.Revision, current.Revision);
        Assert.Empty(current.FoodDeliveries);
        Assert.Empty(current.Tasks);
    }

    [Fact]
    public void 동일명령의수량변경은_충돌로거부하고_첫주문을유지한다()
    {
        var context = CreateContext();
        var confirmed = Confirm(context);
        var changed = FoodDelivery();
        changed.Quantity = 3m;

        Assert.Throws<SimulationConflictException>(() => context.Service.ConfirmFoodDelivery(
            context.Session.SessionStableId, new Simulation음식배달ConfirmRequest
            {
                CommandId = "command:food-delivery.accept-1",
                ExpectedRevision = context.Session.Revision,
                FoodDelivery = changed,
            }));

        var current = context.Service.Get(context.Session.SessionStableId);
        Assert.Equal(confirmed.Revision, current.Revision);
        Assert.Equal(2m, Assert.Single(current.FoodDeliveries).Quantity);
        Assert.Single(current.Tasks);
    }

    [Fact]
    public void 전달전수령Confirm실패는_Task와원장개정을변경하지않는다()
    {
        var context = CreateContext();
        var confirmed = Confirm(context);

        Assert.Throws<SimulationConflictException>(() => context.Service.ConfirmFoodDeliveryReceipt(
            context.Session.SessionStableId, new Simulation음식배달수령ConfirmRequest
            {
                CommandId = "command:food-delivery.early-receipt",
                ExpectedRevision = confirmed.Revision,
                Receipt = Receipt(Assert.Single(confirmed.FoodDeliveries).Revision),
            }));

        var current = context.Service.Get(context.Session.SessionStableId);
        Assert.Equal(confirmed.Revision, current.Revision);
        Assert.Single(current.Tasks);
        Assert.Null(Assert.Single(current.FoodDeliveries).ReceiptTaskStableId);
    }

    [Fact]
    public void 오래된주문개정의수령은_현재세션개정이어도거부한다()
    {
        var context = CreateContext();
        var confirmed = Confirm(context);
        var oldRevision = Assert.Single(confirmed.FoodDeliveries).Revision;
        var delivered = AdvanceToDelivered(context, confirmed);

        var preview = context.Service.PreviewFoodDeliveryReceipt(
            context.Session.SessionStableId, Receipt(oldRevision));
        Assert.Contains("SimulationFoodDeliveryRevisionMismatch", preview.Decision.BlockReasonCodes);
        Assert.Throws<SimulationConflictException>(() => context.Service.ConfirmFoodDeliveryReceipt(
            context.Session.SessionStableId, new Simulation음식배달수령ConfirmRequest
            {
                CommandId = "command:food-delivery.stale-receipt",
                ExpectedRevision = delivered.Revision,
                Receipt = Receipt(oldRevision),
            }));
        Assert.Equal(delivered.Revision, context.Service.Get(context.Session.SessionStableId).Revision);
    }

    [Fact]
    public void 관찰자가상태사본을변경해도_권위주문과계보는변경되지않는다()
    {
        var context = CreateContext();
        var delivered = AdvanceToDelivered(context, Confirm(context));
        var copy = Assert.Single(delivered.FoodDeliveries);
        copy.StateCode = 음식배달상태코드.수령확인;
        copy.SourceStableIds[0] = "source:observer.modified";
        copy.StateHistory[0].ToStateCode = 음식배달상태코드.거절;

        var authoritative = Assert.Single(context.Service.Get(context.Session.SessionStableId).FoodDeliveries);
        Assert.Equal(음식배달상태코드.전달완료, authoritative.StateCode);
        Assert.Null(authoritative.ReceivedTick);
        Assert.DoesNotContain("source:observer.modified", authoritative.SourceStableIds);
        Assert.Equal(음식배달상태코드.조리중, authoritative.StateHistory[0].ToStateCode);
    }

    private static Context CreateContext(bool restaurant = false, bool cooking = false)
    {
        var saveStore = new InMemorySimulationSessionSaveStore();
        var service = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(), saveStore);
        var session = service.Create(new 경영SimulationSession생성Request
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:sim.food-delivery-1",
            ScenarioDataRevision = "scenario-data:r1",
            ScenarioSeed = 20260811,
            RuleRevision = "rule:r1",
            DurationTicks = 20,
            NpcWorkforce = restaurant ? new SimulationNpcWorkforceInitialStateRequest
            {
                Policies = cooking ? new[] { new SimulationNpcWorkPolicyInitialRequest {
                    PolicyStableId = "policy:restaurant:cooking", OrganizationStableId = "organization:restaurant",
                    FacilityStableId = "facility:sim.restaurant-1", ActionCode = SimulationNpcActionCodes.RestaurantCooking,
                    RequiredCapabilityCode = SimulationNpcCapabilityCodes.RestaurantCooking, CookingSlots = 1, WorkDurationTicks = 2,
                    InteractionPointKey = "interaction:restaurant:cooking", ActionVisualKey = "visual:restaurant:cooking",
                    SourceStableIds = new[] { "source:fixture:restaurant" } } } : Array.Empty<SimulationNpcWorkPolicyInitialRequest>(),
                Organizations = new[] { new SimulationNpcOrganizationInitialRequest {
                    OrganizationStableId = "organization:restaurant", DisplayName = "음식점",
                    FacilityStableIds = new[] { "facility:sim.restaurant-1" },
                    AllowedCapabilityCodes = new[] { SimulationNpcCapabilityCodes.RestaurantCooking },
                    SourceStableIds = new[] { "source:fixture:restaurant" } } },
                Actors = new[] { new SimulationNpcActorInitialRequest {
                    ActorStableId = "actor:sim.restaurant-1", OrganizationStableId = "organization:restaurant",
                    DisplayName = "조리 담당", HomeFacilityStableId = "facility:sim.restaurant-1", ReferenceRoleCode = "RestaurantOperator",
                    AssignableCapabilityCodes = new[] { SimulationNpcCapabilityCodes.RestaurantCooking },
                    SourceStableIds = new[] { "source:fixture:restaurant" } } },
                CapabilityGrants = new[] { new SimulationNpcCapabilityGrantInitialRequest {
                    GrantStableId = "grant:restaurant", OrganizationStableId = "organization:restaurant",
                    ActorStableId = "actor:sim.restaurant-1", FacilityStableId = "facility:sim.restaurant-1",
                    CapabilityCode = SimulationNpcCapabilityCodes.RestaurantCooking, GrantedByActorStableId = "actor:sim.restaurant-1",
                    SourceStableIds = new[] { "source:fixture:restaurant" } } },
            } : new SimulationNpcWorkforceInitialStateRequest(),
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim.residents-1",
                TerritoryStableId = "territory:sim.town-1",
                SettlementStableId = "settlement:sim.town-1",
                GameDateStartsOn = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            },
        });
        return new Context(service, saveStore, session);
    }

    private static 경영SimulationSessionSnapshot Confirm(Context context)
        => context.Service.ConfirmFoodDelivery(
            context.Session.SessionStableId,
            new Simulation음식배달ConfirmRequest
            {
                CommandId = "command:food-delivery.accept-1",
                ExpectedRevision = context.Session.Revision,
                FoodDelivery = FoodDelivery(),
            });

    private static Simulation음식배달PreviewRequest FoodDelivery()
        => new()
        {
            FoodOrderStableId = "food-order:sim.potato-stew-1",
            MenuItemStableId = "menu-item:sim.potato-stew-1",
            RestaurantFacilityStableId = "facility:sim.restaurant-1",
            DestinationFacilityStableId = "facility:sim.residence-1",
            DeliveryScopeStableId = "delivery-scope:sim.town-1",
            OrdererStableId = "participant:sim.orderer-1",
            ActorStableId = "actor:sim.restaurant-1",
            Quantity = 2m,
            UnitCode = "serving",
            PreparationDurationTicks = 2,
            DeliveryDurationTicks = 2,
            SourceStableIds = new[] { "source:fixture.food-delivery-1" },
        };

    private static Simulation음식배달수령PreviewRequest Receipt(long revision)
        => new()
        {
            FoodOrderStableId = "food-order:sim.potato-stew-1",
            FoodOrderRevision = revision,
            ActorStableId = "participant:sim.orderer-1",
            ReceiptDurationTicks = 1,
            SourceStableIds = new[] { "source:fixture.food-delivery-receipt-1" },
        };

    private static 경영SimulationSessionSnapshot AdvanceToDelivered(
        Context context,
        경영SimulationSessionSnapshot current)
    {
        for (var tick = 1; tick <= 6; tick++) current = Advance(context, current, tick);
        return current;
    }

    private static 경영SimulationSessionSnapshot Advance(
        Context context,
        경영SimulationSessionSnapshot current,
        int suffix)
        => context.Service.Advance(
            context.Session.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = $"command:tick.food-delivery-{suffix}",
                ExpectedRevision = current.Revision,
                TickCount = 1,
            });

    private sealed record Context(
        경영SimulationSessionService Service,
        InMemorySimulationSessionSaveStore SaveStore,
        경영SimulationSessionSnapshot Session);
}
