namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The one seam an administrator is told through. One call per event site writes one
/// <c>UserNotification</c> per administrator of the NAMED company, inside the caller's unit of work:
/// it never commits, never touches the network, never enqueues a push, and reads nothing for the
/// ambient tenant — the event's company is an argument, because the caller's override may name
/// another company at the moment it fires.
/// </summary>
public interface IAdminNotifier
{
    Task NotifyAsync(AdminEvent adminEvent, CancellationToken cancellationToken);
}
