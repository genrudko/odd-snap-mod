using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using OddSnap.Native;

namespace OddSnap.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HOTKEY_CAPTURE = 9001;
    private const int HOTKEY_OCR = 9002;
    private const int HOTKEY_PICKER = 9003;
    private const int HOTKEY_SCAN = 9004;
    private const int HOTKEY_RULER = 9005;
    private const int HOTKEY_STICKER = 9006;
    private const int HOTKEY_UPSCALE = 9007;
    private const int HOTKEY_GIF = 9008;
    private const int HOTKEY_FULLSCREEN = 9009;
    private const int HOTKEY_ACTIVE_WINDOW = 9010;
    private const int HOTKEY_SCROLL_CAPTURE = 9011;
    private const int HOTKEY_AI_REDIRECT = 9012;
    private const int HOTKEY_CENTER = 9013;

    private readonly User32.LowLevelKeyboardProc _printScreenProc;
    private IntPtr _printScreenHook;
    private int _printScreenKeyDown;
    private int _printScreenPosted;

    private bool _captureRegistered;
    private bool _ocrRegistered;
    private bool _pickerRegistered;
    private bool _scanRegistered;
    private bool _rulerRegistered;
    private bool _stickerRegistered;
    private bool _upscaleRegistered;
    private bool _gifRegistered;
    private bool _fullscreenRegistered;
    private bool _activeWindowRegistered;
    private bool _scrollCaptureRegistered;
    private bool _aiRedirectRegistered;
    private bool _centerRegistered;
    private bool _registered;

    public event Action? HotkeyPressed;
    public event Action? OcrHotkeyPressed;
    public event Action? PickerHotkeyPressed;
    public event Action? ScanHotkeyPressed;
    public event Action? RulerHotkeyPressed;
    public event Action? StickerHotkeyPressed;
    public event Action? UpscaleHotkeyPressed;
    public event Action? GifHotkeyPressed;
    public event Action? FullscreenHotkeyPressed;
    public event Action? ActiveWindowHotkeyPressed;
    public event Action? ScrollCaptureHotkeyPressed;
    public event Action? AiRedirectHotkeyPressed;
    public event Action? CenterHotkeyPressed;

    public HotkeyService()
    {
        _printScreenProc = PrintScreenHookProc;
    }

    private void EnsureMessageHook()
    {
        if (_registered)
            return;

        ComponentDispatcher.ThreadPreprocessMessage += OnMsg;
        _registered = true;
    }

    private bool RegisterHotkey(ref bool registeredFlag, int id, uint modifiers, uint key)
    {
        EnsureMessageHook();

        if (registeredFlag)
        {
            User32.UnregisterHotKey(IntPtr.Zero, id);
            registeredFlag = false;
        }

        if (key == 0 || IsUnsafeModifierlessHotkey(modifiers, key))
            return true;

        registeredFlag = User32.RegisterHotKey(
            IntPtr.Zero, id, modifiers | User32.MOD_NOREPEAT, key);
        return registeredFlag;
    }

    private static bool IsUnsafeModifierlessHotkey(uint modifiers, uint key) =>
        modifiers == 0 && key != User32.VK_SNAPSHOT;

    /// <summary>Force-unregister all hotkey IDs to clear any stale registrations from previous instances.</summary>
    public void UnregisterAll()
    {
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_CAPTURE);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_OCR);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_PICKER);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_SCAN);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_RULER);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_STICKER);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_UPSCALE);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_GIF);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_FULLSCREEN);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_ACTIVE_WINDOW);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_SCROLL_CAPTURE);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_AI_REDIRECT);
        User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_CENTER);
        RemovePrintScreenHook();
        _captureRegistered = false;
        _ocrRegistered = false;
        _pickerRegistered = false;
        _scanRegistered = false;
        _rulerRegistered = false;
        _stickerRegistered = false;
        _upscaleRegistered = false;
        _gifRegistered = false;
        _fullscreenRegistered = false;
        _activeWindowRegistered = false;
        _scrollCaptureRegistered = false;
        _aiRedirectRegistered = false;
        _centerRegistered = false;
    }

    public bool Register(uint modifiers, uint key)
    {
        if (_captureRegistered)
        {
            User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_CAPTURE);
            _captureRegistered = false;
        }
        RemovePrintScreenHook();

        if (key == 0)
            return true;

        if (modifiers == 0 && key == User32.VK_SNAPSHOT)
            return InstallPrintScreenHook();

        return RegisterHotkey(ref _captureRegistered, HOTKEY_CAPTURE, modifiers, key);
    }

    public bool RegisterOcr(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _ocrRegistered, HOTKEY_OCR, modifiers, key);
    }

    public bool RegisterPicker(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _pickerRegistered, HOTKEY_PICKER, modifiers, key);
    }

    public bool RegisterScan(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _scanRegistered, HOTKEY_SCAN, modifiers, key);
    }

    public bool RegisterRuler(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _rulerRegistered, HOTKEY_RULER, modifiers, key);
    }

    public bool RegisterSticker(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _stickerRegistered, HOTKEY_STICKER, modifiers, key);
    }

    public bool RegisterUpscale(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _upscaleRegistered, HOTKEY_UPSCALE, modifiers, key);
    }

    public bool RegisterGif(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _gifRegistered, HOTKEY_GIF, modifiers, key);
    }

    public bool RegisterFullscreen(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _fullscreenRegistered, HOTKEY_FULLSCREEN, modifiers, key);
    }

    public bool RegisterActiveWindow(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _activeWindowRegistered, HOTKEY_ACTIVE_WINDOW, modifiers, key);
    }

    public bool RegisterScrollCapture(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _scrollCaptureRegistered, HOTKEY_SCROLL_CAPTURE, modifiers, key);
    }

    public bool RegisterAiRedirect(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _aiRedirectRegistered, HOTKEY_AI_REDIRECT, modifiers, key);
    }

    public bool RegisterCenter(uint modifiers, uint key)
    {
        return RegisterHotkey(ref _centerRegistered, HOTKEY_CENTER, modifiers, key);
    }

    private bool InstallPrintScreenHook()
    {
        if (_printScreenHook != IntPtr.Zero)
            return true;

        IntPtr moduleHandle = IntPtr.Zero;
        try
        {
            string? moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
            if (!string.IsNullOrWhiteSpace(moduleName))
                moduleHandle = Kernel32.GetModuleHandle(moduleName);
        }
        catch
        {
            moduleHandle = IntPtr.Zero;
        }

        _printScreenHook = User32.SetWindowsHookEx(
            User32.WH_KEYBOARD_LL,
            _printScreenProc,
            moduleHandle,
            0);
        return _printScreenHook != IntPtr.Zero;
    }

    private IntPtr PrintScreenHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || lParam == IntPtr.Zero)
            return User32.CallNextHookEx(_printScreenHook, nCode, wParam, lParam);

        int vkCode = Marshal.ReadInt32(lParam);
        if (vkCode != User32.VK_SNAPSHOT)
            return User32.CallNextHookEx(_printScreenHook, nCode, wParam, lParam);

        int message = unchecked((int)wParam.ToInt64());
        if (message is User32.WM_KEYDOWN or User32.WM_SYSKEYDOWN)
        {
            if (HasPressedModifier())
                return User32.CallNextHookEx(_printScreenHook, nCode, wParam, lParam);

            if (Interlocked.Exchange(ref _printScreenKeyDown, 1) == 0)
                PostPrintScreenCapture();
            return (IntPtr)1;
        }

        if (message is User32.WM_KEYUP or User32.WM_SYSKEYUP &&
            Interlocked.Exchange(ref _printScreenKeyDown, 0) != 0)
        {
            return (IntPtr)1;
        }

        return User32.CallNextHookEx(_printScreenHook, nCode, wParam, lParam);
    }

    private static bool HasPressedModifier() =>
        IsKeyDown(User32.VK_SHIFT) ||
        IsKeyDown(User32.VK_CONTROL) ||
        IsKeyDown(User32.VK_MENU) ||
        IsKeyDown(User32.VK_LWIN) ||
        IsKeyDown(User32.VK_RWIN);

    private static bool IsKeyDown(int virtualKey) =>
        (User32.GetKeyState(virtualKey) & 0x8000) != 0;

    private bool PostPrintScreenCapture()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            return false;

        if (Interlocked.Exchange(ref _printScreenPosted, 1) != 0)
            return true;

        try
        {
            dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                try
                {
                    InvokeHandlersSafely(HotkeyPressed, "hotkey.print-screen");
                }
                finally
                {
                    Volatile.Write(ref _printScreenPosted, 0);
                }
            }));
            return true;
        }
        catch
        {
            Volatile.Write(ref _printScreenPosted, 0);
            return false;
        }
    }

    private void RemovePrintScreenHook()
    {
        Volatile.Write(ref _printScreenKeyDown, 0);
        Volatile.Write(ref _printScreenPosted, 0);
        var hook = Interlocked.Exchange(ref _printScreenHook, IntPtr.Zero);
        if (hook != IntPtr.Zero)
            User32.UnhookWindowsHookEx(hook);
    }

    public void Unregister()
    {
        if (_captureRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_CAPTURE); _captureRegistered = false; }
        if (_ocrRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_OCR); _ocrRegistered = false; }
        if (_pickerRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_PICKER); _pickerRegistered = false; }
        if (_scanRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_SCAN); _scanRegistered = false; }
        if (_rulerRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_RULER); _rulerRegistered = false; }
        if (_stickerRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_STICKER); _stickerRegistered = false; }
        if (_upscaleRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_UPSCALE); _upscaleRegistered = false; }
        if (_gifRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_GIF); _gifRegistered = false; }
        if (_fullscreenRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_FULLSCREEN); _fullscreenRegistered = false; }
        if (_activeWindowRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_ACTIVE_WINDOW); _activeWindowRegistered = false; }
        if (_scrollCaptureRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_SCROLL_CAPTURE); _scrollCaptureRegistered = false; }
        if (_aiRedirectRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_AI_REDIRECT); _aiRedirectRegistered = false; }
        if (_centerRegistered) { User32.UnregisterHotKey(IntPtr.Zero, HOTKEY_CENTER); _centerRegistered = false; }
        RemovePrintScreenHook();
        if (_registered)
        {
            ComponentDispatcher.ThreadPreprocessMessage -= OnMsg;
            _registered = false;
        }
    }

    private void OnMsg(ref MSG msg, ref bool handled)
    {
        if (msg.message != User32.WM_HOTKEY) return;
        int id = (int)msg.wParam;
        if (id == HOTKEY_CAPTURE) { InvokeHandlersSafely(HotkeyPressed, "hotkey.capture"); handled = true; }
        else if (id == HOTKEY_OCR) { InvokeHandlersSafely(OcrHotkeyPressed, "hotkey.ocr"); handled = true; }
        else if (id == HOTKEY_PICKER) { InvokeHandlersSafely(PickerHotkeyPressed, "hotkey.picker"); handled = true; }
        else if (id == HOTKEY_SCAN) { InvokeHandlersSafely(ScanHotkeyPressed, "hotkey.scan"); handled = true; }
        else if (id == HOTKEY_RULER) { InvokeHandlersSafely(RulerHotkeyPressed, "hotkey.ruler"); handled = true; }
        else if (id == HOTKEY_STICKER) { InvokeHandlersSafely(StickerHotkeyPressed, "hotkey.sticker"); handled = true; }
        else if (id == HOTKEY_UPSCALE) { InvokeHandlersSafely(UpscaleHotkeyPressed, "hotkey.upscale"); handled = true; }
        else if (id == HOTKEY_GIF) { InvokeHandlersSafely(GifHotkeyPressed, "hotkey.gif"); handled = true; }
        else if (id == HOTKEY_FULLSCREEN) { InvokeHandlersSafely(FullscreenHotkeyPressed, "hotkey.fullscreen"); handled = true; }
        else if (id == HOTKEY_ACTIVE_WINDOW) { InvokeHandlersSafely(ActiveWindowHotkeyPressed, "hotkey.active-window"); handled = true; }
        else if (id == HOTKEY_SCROLL_CAPTURE) { InvokeHandlersSafely(ScrollCaptureHotkeyPressed, "hotkey.scroll-capture"); handled = true; }
        else if (id == HOTKEY_AI_REDIRECT) { InvokeHandlersSafely(AiRedirectHotkeyPressed, "hotkey.ai-redirect"); handled = true; }
        else if (id == HOTKEY_CENTER) { InvokeHandlersSafely(CenterHotkeyPressed, "hotkey.center"); handled = true; }
    }

    private static void InvokeHandlersSafely(Action? handlers, string context)
    {
        if (handlers is null)
            return;

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                AppDiagnostics.LogError(context, ex);
            }
        }
    }

    public void Dispose() => Unregister();
}