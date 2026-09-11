using MediatR;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Application.Food.Commands;

public sealed record 주문자음식주문취소Command(
    string 주문번호,
    주문자음식주문취소요청 Payload,
    string 주문자UserId) : IRequest<음식주문응답?>;
