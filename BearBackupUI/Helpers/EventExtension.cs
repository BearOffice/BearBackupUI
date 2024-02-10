using BearBackupUI.Core;

namespace BearBackupUI.Helpers;

internal static class EventExtension
{
    internal static void UnsubscribeAll(this EventHandler<DataArgs>? eventHandler)
    {
        if (eventHandler is null) return;

        foreach (var handler in eventHandler.GetInvocationList().Cast<EventHandler<DataArgs>>())
        {
            eventHandler -= handler;
        }
    }
}
