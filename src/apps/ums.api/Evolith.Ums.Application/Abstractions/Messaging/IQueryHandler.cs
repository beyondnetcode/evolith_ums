namespace Evolith.Ums.Application.Abstractions.Messaging;

using MediatR;
using Evolith.Ums.Domain.Kernel;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
