using Microsoft.EntityFrameworkCore;

namespace NCMS.IoT.DeviceManagement.Data;

/// <summary>
/// Loads a task's full child-device set and invokes the aggregate root's
/// <c>Reconcile()</c> so its <c>Status</c> stays a faithful projection of its devices.
///
/// <para>The mutated task is left tracked by <paramref name="db"/> but NOT saved — the
/// caller persists it together with the child transition that triggered the reconcile,
/// inside the same transaction.</para>
///
/// <para>Extension point (large-scale): when per-status aggregate counters are added to
/// the task entities, these helpers no longer need to materialise every child row — the
/// task can reconcile from its own counters and these methods become a thin
/// <c>FindAsync + Reconcile</c>. Call sites stay unchanged.</para>
/// </summary>
internal static class TaskReconciliation
{
    public static async Task ReconcileUpgradeTaskAsync(
        DeviceManagementDbContext db, Guid taskId, CancellationToken ct)
    {
        var task = await db.UpgradeTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return;

        // EF identity resolution returns already-tracked child instances, so any device
        // transitioned earlier in this unit of work is reflected with its new status.
        var devices = await db.UpgradeTaskDevices
            .Where(d => d.UpgradeTaskId == taskId)
            .ToListAsync(ct);

        task.Reconcile(devices);
    }

    public static async Task ReconcileConfigureTaskAsync(
        DeviceManagementDbContext db, Guid taskId, CancellationToken ct)
    {
        var task = await db.ConfigureTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return;

        var devices = await db.ConfigureTaskDevices
            .Where(d => d.ConfigureTaskId == taskId)
            .ToListAsync(ct);

        task.Reconcile(devices);
    }

    /// <summary>
    /// Application tasks always target exactly one package (no bundle fan-out), so a device's
    /// Status is set directly by the dispatch/ack/fail/timeout calls — no per-item projection
    /// needed. This just re-derives the parent task's Status from its child devices.
    /// </summary>
    public static async Task ReconcileApplicationTaskAsync(
        DeviceManagementDbContext db, Guid taskId, CancellationToken ct)
    {
        var task = await db.ApplicationTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return;

        var devices = await db.ApplicationTaskDevices
            .Where(d => d.ApplicationTaskId == taskId)
            .ToListAsync(ct);

        task.Reconcile(devices);
    }
}
