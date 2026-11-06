using LockGuard.Domain.Deadlocks;
using MediatR;

namespace LockGuard.Application.Features.Maintenance.Commands.ScanDeadlocks;

public sealed record ScanDeadlocksCommand : IRequest<WaitForGraphSnapshot>;
