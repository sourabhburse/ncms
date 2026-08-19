namespace NCMS.IoT.DeviceManagement.Data;

/// <summary>
/// Formats the user-facing "these devices are already in an active task" message shared by the
/// firmware and config task-creation handlers. Devices are identified by serial number (the
/// field-meaningful identifier), and the list is capped so the message stays readable when a
/// large task has many conflicts.
/// </summary>
internal static class ActiveTaskConflictMessage
{
    /// <summary>Maximum serials listed inline before collapsing the remainder to "…and N more".</summary>
    private const int MaxShown = 10;

    /// <param name="serialNumbers">Serial numbers of the conflicting devices (any order).</param>
    /// <param name="taskKind">e.g. "firmware upgrade" or "configuration".</param>
    public static string Build(IReadOnlyList<string> serialNumbers, string taskKind)
    {
        var shown = string.Join(", ", serialNumbers.Take(MaxShown));
        var overflow = serialNumbers.Count - MaxShown;
        var list = overflow > 0 ? $"{shown} …and {overflow} more" : shown;

        return $"{serialNumbers.Count} selected device(s) already have an active {taskKind} task "
            + $"(serial number{(serialNumbers.Count == 1 ? "" : "s")}: {list}). "
            + $"Remove them from the selection, or wait until their existing {taskKind} task "
            + "reaches a terminal state, before creating a new task.";
    }
}
