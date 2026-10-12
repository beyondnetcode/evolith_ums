namespace Evolith.Ums.Application.Abstractions.Messaging;

using MediatR;
using Evolith.Ums.Domain.Kernel;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
