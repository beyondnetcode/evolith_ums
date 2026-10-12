namespace Evolith.Ums.Application.Abstractions.Messaging;

using Evolith.Ums.Domain.Kernel;
using MediatR;

public interface ICommand : IRequest<Result>
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
