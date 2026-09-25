namespace System.Windows.Forms.Automation;

// UI Automation notifications (AccessibleObject.RaiseAutomationNotification) and live regions, as in WinForms.

public enum AutomationNotificationKind
{
    ItemAdded = 0,
    ItemRemoved = 1,
    ActionCompleted = 2,
    ActionAborted = 3,
    Other = 4,
}

public enum AutomationNotificationProcessing
{
    ImportantAll = 0,
    ImportantMostRecent = 1,
    All = 2,
    MostRecent = 3,
    CurrentThenMostRecent = 4,
}

public enum AutomationLiveSetting
{
    Off = 0,
    Polite = 1,
    Assertive = 2,
}

/// <summary>A control whose changes a screen reader announces (a status label, a validation message).</summary>
public interface IAutomationLiveRegion
{
    AutomationLiveSetting LiveSetting { get; set; }
}
