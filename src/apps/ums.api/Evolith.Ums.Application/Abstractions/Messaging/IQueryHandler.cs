namespace Evolith.Ums.Application.Abstractions.Messaging;

using Evolith.Ums.Domain.Kernel;
using MediatR;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
