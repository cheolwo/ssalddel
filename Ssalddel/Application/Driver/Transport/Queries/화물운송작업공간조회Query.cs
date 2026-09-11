using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Application.Driver.Transport;

public sealed record 화물운송작업공간조회Query(string 기사Id) : IRequest<기사화물운송작업공간응답>;
