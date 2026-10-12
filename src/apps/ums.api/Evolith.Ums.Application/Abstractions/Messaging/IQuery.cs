namespace Evolith.Ums.Application.Abstractions.Messaging;

using Evolith.Ums.Domain.Kernel;
using MediatR;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
