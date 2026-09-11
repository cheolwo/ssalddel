using MediatR;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Application.Food.Events;

public sealed record 주문자음식주문취소됨Event(
    음식주문응답 주문,
    string 주문자UserId,
    string 사유Code,
    DateTime 발생시각Utc,
    string EventId) : INotification;
