from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGION_FORM = ROOT / "src/OddSnap/Capture/RegionOverlayForm.cs"
INPUT_HELPERS = ROOT / "src/OddSnap/Capture/RegionOverlayForm.Input.Helpers.cs"
APP_CAPTURE = ROOT / "src/OddSnap/App/App.Capture.cs"
MONITOR_SELECTOR = ROOT / "src/OddSnap/Capture/RecordingCaptureTargetSelector.cs"


def replace_once(path: Path, old: str, new: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if new in text:
        return False
    if old not in text:
        raise RuntimeError(f"Expected fragment was not found in {path}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")
    return True


def main() -> None:
    changed = False

    changed |= replace_once(
        REGION_FORM,
        """        _mode = snippingLauncherMode.HasValue ? CaptureMode.Rectangle : initialMode;
        _activeToolId = ToolDef.AllTools.FirstOrDefault(t => t.Mode == _mode)?.Id;
""",
        """        _mode = snippingLauncherMode.HasValue ? CaptureMode.Rectangle : initialMode;
        _activeToolId = snippingLauncherMode == SnippingLauncherMode.Recording
            ? "_snipArea"
            : ToolDef.AllTools.FirstOrDefault(t => t.Mode == _mode)?.Id;
""",
    )

    changed |= replace_once(
        REGION_FORM,
        """        var rectangle = ToolDef.AllTools.First(t => t.Id == "rect");

        var recordingTools = new List<ToolDef> { screenshotToggle, recordingToggle, rectangle };
""",
        """        var rectangle = ToolDef.AllTools.First(t => t.Id == "rect");
        var recordArea = new ToolDef("_snipArea", "Record area", '\\0', CaptureMode.Rectangle, -1);
        var recordWindow = ToolDef.ToolbarActions.First(t => t.Id == "_recordWindow");
        var recordMonitor = ToolDef.ToolbarActions.First(t => t.Id == "_recordMonitor");

        var recordingTools = new List<ToolDef>
        {
            screenshotToggle,
            recordingToggle,
            recordArea,
            recordWindow,
            recordMonitor
        };
""",
    )

    changed |= replace_once(
        REGION_FORM,
        """        "_snipScreenshot" => "camera",
        "_snipRecording" => "record",
        "_snipStart" => "_recordResume",
""",
        """        "_snipScreenshot" => "camera",
        "_snipRecording" => "record",
        "_snipArea" => "_record",
        "_snipStart" => "_recordResume",
""",
    )

    changed |= replace_once(
        INPUT_HELPERS,
        """        _snippingLauncherMode = mode;
        _mode = CaptureMode.Rectangle;
        _activeToolId = "rect";
""",
        """        _snippingLauncherMode = mode;
        _mode = CaptureMode.Rectangle;
        _activeToolId = mode == SnippingLauncherMode.Recording ? "_snipArea" : "rect";
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """        Bitmap? screenshot = null;
        bool captureFlowHandedOff = false;
        RecordingCaptureTarget? pendingRecordingTarget = null;
""",
        """        Bitmap? screenshot = null;
        bool captureFlowHandedOff = false;
        RecordingCaptureTarget? pendingRecordingTarget = null;
        string? pendingToolbarActionId = null;
        bool pendingToolbarActionOpenResult = false;
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """            overlay.ToolbarActionRequested += actionId =>
            {
                overlay.Hide();
                overlay.Close();
                if (!TryPostToAppDispatcher(
                        () => LaunchToolbarActionFromOverlay(
                            actionId,
                            openResultWindow: snippingLauncherMode == SnippingLauncherMode.Screenshot),
                        DispatcherPriority.Background,
                        "capture.toolbar-action-post"))
                {
                    ResetCapturingWithoutUiRestore();
                }
            };
""",
        """            overlay.ToolbarActionRequested += actionId =>
            {
                // Window and monitor pickers are also topmost surfaces. Defer opening
                // them until this launcher and its layered toolbar have fully closed.
                captureFlowHandedOff = true;
                pendingToolbarActionId = actionId;
                pendingToolbarActionOpenResult = snippingLauncherMode.HasValue;
                overlay.Hide();
                overlay.Close();
            };
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """                if (pendingRecordingTarget is not null)
                {
                    LaunchGifRecording(pendingRecordingTarget, openResultWindow: true);
                    return;
                }

                if (captureFlowHandedOff)
                    return;
""",
        """                if (pendingRecordingTarget is not null)
                {
                    LaunchGifRecording(pendingRecordingTarget, openResultWindow: true);
                    return;
                }

                if (pendingToolbarActionId is not null)
                {
                    string actionId = pendingToolbarActionId;
                    bool openResultWindow = pendingToolbarActionOpenResult;
                    if (!TryPostToAppDispatcher(
                            () => LaunchToolbarActionFromOverlay(actionId, openResultWindow),
                            DispatcherPriority.Background,
                            "capture.toolbar-action-after-close-post"))
                    {
                        ResetCapturingWithoutUiRestore();
                    }
                    return;
                }

                if (captureFlowHandedOff)
                    return;
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """            case "_recordMonitor":
                LaunchGifRecording(RecordingCaptureTargetSelector.GetMonitorTargetAt(System.Windows.Forms.Cursor.Position));
                break;
""",
        """            case "_recordMonitor":
                LaunchGifRecording(
                    RecordingCaptureTargetSelector.GetMonitorTargetAt(System.Windows.Forms.Cursor.Position),
                    openResultWindow);
                break;
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """            case "_recordMonitor":
                LaunchGifRecording(
                    RecordingCaptureTargetSelector.GetMonitorTargetAt(System.Windows.Forms.Cursor.Position),
                    openResultWindow);
                break;
""",
        """            case "_recordMonitor":
            {
                var monitorTarget = RecordingCaptureTargetSelector.SelectMonitorAt(
                    System.Windows.Forms.Cursor.Position);
                if (monitorTarget is not null)
                    LaunchGifRecording(monitorTarget, openResultWindow);
                else
                    ResetCapturing();
                break;
            }
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """                if (windowTarget is not null)
                    LaunchGifRecording(windowTarget);
""",
        """                if (windowTarget is not null)
                    LaunchGifRecording(windowTarget, openResultWindow);
""",
    )

    changed |= replace_once(
        MONITOR_SELECTOR,
        """    public static RecordingCaptureTarget GetMonitorTargetAt(Point screenPoint)
""",
        """    public static RecordingCaptureTarget? SelectMonitorAt(Point screenPoint)
""",
    )

    changed |= replace_once(
        MONITOR_SELECTOR,
        """        if (selectedTarget is not null)
            return selectedTarget;

        using var fallbackDpiScope = DpiAwarenessScope.EnterPerMonitorV2();
        return CreateTargetForMonitor(FindMonitorAt(screenPoint, GetOrderedMonitors()));
    }

    private static MonitorDescriptor[] GetOrderedMonitors()
""",
        """        return selectedTarget;
    }

    public static RecordingCaptureTarget GetMonitorTargetAt(Point screenPoint)
    {
        var selectedTarget = SelectMonitorAt(screenPoint);
        if (selectedTarget is not null)
            return selectedTarget;

        using var fallbackDpiScope = DpiAwarenessScope.EnterPerMonitorV2();
        return CreateTargetForMonitor(FindMonitorAt(screenPoint, GetOrderedMonitors()));
    }

    private static MonitorDescriptor[] GetOrderedMonitors()
""",
    )

    print(
        "Recording launcher area/window/monitor target repair applied."
        if changed
        else "Recording launcher area/window/monitor target repair already applied."
    )


if __name__ == "__main__":
    main()
