namespace Evolith.Ums.Application.Abstractions.Messaging;

using MediatR;
using Evolith.Ums.Domain.Kernel;

public interface ICommand : IRequest<Result>
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
