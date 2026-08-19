using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.Host.Models;

/// <summary>
/// Presentation-only helpers that map domain enums / states to Tabler badge classes.
/// No business logic — purely how a value is coloured in the UI.
/// </summary>
public static class Ui
{
    public static string Badge(UpgradeTaskStatus status) => status switch
    {
        UpgradeTaskStatus.NotStarted => "bg-secondary-lt",
        UpgradeTaskStatus.InProgress => "bg-blue-lt",
        UpgradeTaskStatus.Completed  => "bg-green-lt",
        UpgradeTaskStatus.Cancelled  => "bg-red-lt",
        _                            => "bg-secondary-lt"
    };

    // Same colours as UpgradeTaskStatus — the two task types share one visual language.
    public static string Badge(ConfigureTaskStatus status) => status switch
    {
        ConfigureTaskStatus.NotStarted => "bg-secondary-lt",
        ConfigureTaskStatus.InProgress => "bg-blue-lt",
        ConfigureTaskStatus.Completed  => "bg-green-lt",
        ConfigureTaskStatus.Cancelled  => "bg-red-lt",
        _                              => "bg-secondary-lt"
    };

    public static string Badge(EscalationState state) => state switch
    {
        EscalationState.WaitingToDownload => "bg-secondary-lt",
        EscalationState.Downloading       => "bg-blue-lt",
        EscalationState.UpgradeSucceeded  => "bg-green-lt",
        EscalationState.UpgradeFailure    => "bg-red-lt",
        EscalationState.UpgradeTimeout    => "bg-orange-lt",
        EscalationState.Terminated        => "bg-dark-lt",
        EscalationState.Aborted           => "bg-secondary-lt",
        _                                 => "bg-secondary-lt"
    };

    public static string OnlineBadge(bool online) => online ? "bg-green-lt" : "bg-secondary-lt";

    public static string OnlineLabel(bool online) => online ? "Online" : "Offline";

    // Same colours as UpgradeTaskStatus/ConfigureTaskStatus — one visual language for all task types.
    public static string Badge(ApplicationTaskStatus status) => status switch
    {
        ApplicationTaskStatus.NotStarted => "bg-secondary-lt",
        ApplicationTaskStatus.InProgress => "bg-blue-lt",
        ApplicationTaskStatus.Completed  => "bg-green-lt",
        ApplicationTaskStatus.Cancelled  => "bg-red-lt",
        _                                 => "bg-secondary-lt"
    };

    public static string Badge(ApplicationEscalationState state) => state switch
    {
        ApplicationEscalationState.WaitingToStart => "bg-secondary-lt",
        ApplicationEscalationState.Downloading    => "bg-blue-lt",
        ApplicationEscalationState.Succeeded      => "bg-green-lt",
        ApplicationEscalationState.Failed         => "bg-red-lt",
        ApplicationEscalationState.TimedOut       => "bg-orange-lt",
        ApplicationEscalationState.Terminated     => "bg-dark-lt",
        _                                          => "bg-secondary-lt"
    };

    public static string EnabledBadge(bool enabled) => enabled ? "bg-green-lt" : "bg-secondary-lt";
}
