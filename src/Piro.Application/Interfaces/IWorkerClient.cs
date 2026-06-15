using Piro.Application.Models.Worker;

namespace Piro.Application.Interfaces;

/// <summary>Defines messages the API can push to a connected worker over SignalR.</summary>
public interface IWorkerClient
{
    /// <summary>Sent once on connection to confirm the worker token was accepted.</summary>
    Task Ack(WorkerAckMessage message);

    /// <summary>Instructs the worker to execute a check and return a <see cref="WorkerResultMessage"/>.</summary>
    Task Execute(WorkerExecuteMessage message);
}
