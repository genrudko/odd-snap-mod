using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;

namespace OddSnap.Capture;

/// <summary>
/// Captures one HWND through Windows.Graphics.Capture and converts the newest
/// compositor frame into the fixed-size BGRA stream consumed by the existing
/// OddSnap encoders. Because the source is the window compositor surface rather
/// than a desktop rectangle, moving or covering the window does not change the
/// recorded content.
/// </summary>
internal sealed class WindowsGraphicsCaptureFrameSource : IRecordingFrameSource
{
    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid ID3D11Texture2DGuid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
    private readonly Size _outputSize;
    private readonly AutoResetEvent _frameAvailable = new(false);
    private readonly object _lastFrameLock = new();

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private ID3D11Texture2D? _stagingTexture;
    private IDirect3DDevice? _winrtDevice;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private byte[]? _lastFrameBuffer;
    private volatile bool _sourceClosed;
    private bool _disposed;

    public static bool IsSupported
    {
        get
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362))
                return false;

            try { return GraphicsCaptureSession.IsSupported(); }
            catch { return false; }
        }
    }

    public WindowsGraphicsCaptureFrameSource(nint windowHandle, Size outputSize, bool includeCursor)
    {
        if (windowHandle == nint.Zero)
            throw new ArgumentException("Native window capture requires a non-zero HWND.", nameof(windowHandle));
        if (outputSize.Width <= 0 || outputSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputSize), "Native window capture requires a positive output size.");
        if (!IsSupported)
            throw new PlatformNotSupportedException("Windows Graphics Capture is not supported on this system.");
        if (!IsWindow(windowHandle))
            throw new ArgumentException("The selected HWND is no longer valid.", nameof(windowHandle));

        _outputSize = outputSize;

        try
        {
            CreateDevices();
            _item = CreateItemForWindow(windowHandle);

            var initialSize = _item.Size;
            if (initialSize.Width <= 0 || initialSize.Height <= 0)
            {
                initialSize = new SizeInt32
                {
                    Width = outputSize.Width,
                    Height = outputSize.Height
                };
            }

            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winrtDevice!,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                3,
                initialSize);
            _session = _framePool.CreateCaptureSession(_item);

            if (ApiInformation.IsPropertyPresent(
                    "Windows.Graphics.Capture.GraphicsCaptureSession",
                    "IsCursorCaptureEnabled"))
            {
                try { _session.IsCursorCaptureEnabled = includeCursor; }
                catch { }
            }

            _framePool.FrameArrived += HandleFrameArrived;
            _item.Closed += HandleItemClosed;
            _session.StartCapture();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public int BufferByteCount => checked(_outputSize.Width * _outputSize.Height * 4);

    public byte[] CaptureToBuffer(byte[]? buffer)
    {
        ThrowIfDisposed();
        if (buffer is null || buffer.Length != BufferByteCount)
            buffer = new byte[BufferByteCount];

        using var frame = AcquireLatestFrame();
        if (frame is not null)
        {
            CopyFrameToBuffer(frame, buffer);
            lock (_lastFrameLock)
            {
                _lastFrameBuffer ??= new byte[BufferByteCount];
                Buffer.BlockCopy(buffer, 0, _lastFrameBuffer, 0, buffer.Length);
            }
            return buffer;
        }

        if (_sourceClosed)
            throw new OperationCanceledException("The captured window was closed.");

        lock (_lastFrameLock)
        {
            if (_lastFrameBuffer is null)
                throw new TimeoutException("Windows Graphics Capture did not provide its first frame in time.");

            Buffer.BlockCopy(_lastFrameBuffer, 0, buffer, 0, buffer.Length);
        }

        return buffer;
    }

    public Bitmap CaptureBitmap()
    {
        var buffer = CaptureToBuffer(null);
        return CreateBitmap(buffer);
    }

    public Bitmap CloneCurrentFrame()
    {
        ThrowIfDisposed();
        lock (_lastFrameLock)
        {
            if (_lastFrameBuffer is null)
                throw new InvalidOperationException("No native window frame has been captured yet.");

            return CreateBitmap(_lastFrameBuffer);
        }
    }

    private void CreateDevices()
    {
        var featureLevels = new[]
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0
        };

        D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels,
            out ID3D11Device device,
            out ID3D11DeviceContext context).CheckError();

        _device = device;
        _context = context;

        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out nint graphicsDevice);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            _winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevice);
        }
        finally
        {
            if (graphicsDevice != nint.Zero)
                Marshal.Release(graphicsDevice);
        }

        _stagingTexture = device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)_outputSize.Width,
            Height = (uint)_outputSize.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read
        });
    }

    private Direct3D11CaptureFrame? AcquireLatestFrame()
    {
        Direct3D11CaptureFrame? latest = DrainFrames();
        if (latest is not null)
            return latest;

        if (_sourceClosed)
            return null;

        int waitMs;
        lock (_lastFrameLock)
            waitMs = _lastFrameBuffer is null ? 1_500 : 80;

        _frameAvailable.WaitOne(waitMs);
        return DrainFrames();
    }

    private Direct3D11CaptureFrame? DrainFrames()
    {
        Direct3D11CaptureFrame? latest = null;
        try
        {
            while (_framePool is not null)
            {
                var next = _framePool.TryGetNextFrame();
                if (next is null)
                    break;

                latest?.Dispose();
                latest = next;
            }
        }
        catch (ObjectDisposedException)
        {
            latest?.Dispose();
            return null;
        }
        catch (COMException) when (_sourceClosed)
        {
            latest?.Dispose();
            return null;
        }

        return latest;
    }

    private void CopyFrameToBuffer(Direct3D11CaptureFrame frame, byte[] buffer)
    {
        var context = _context ?? throw new ObjectDisposedException(nameof(WindowsGraphicsCaptureFrameSource));
        var stagingTexture = _stagingTexture ?? throw new ObjectDisposedException(nameof(WindowsGraphicsCaptureFrameSource));

        using var sourceTexture = CreateTexture2D(frame.Surface);
        var sourceDescription = sourceTexture.Description;
        var contentSize = frame.ContentSize;
        int copyWidth = Math.Min(
            _outputSize.Width,
            Math.Min(Math.Max(0, contentSize.Width), checked((int)sourceDescription.Width)));
        int copyHeight = Math.Min(
            _outputSize.Height,
            Math.Min(Math.Max(0, contentSize.Height), checked((int)sourceDescription.Height)));

        if (copyWidth <= 0 || copyHeight <= 0)
            throw new InvalidOperationException("Windows Graphics Capture returned an empty frame.");

        context.CopySubresourceRegion(
            stagingTexture,
            0,
            0,
            0,
            0,
            sourceTexture,
            0,
            new Vortice.Mathematics.Box(0, 0, 0, copyWidth, copyHeight, 1));

        Array.Clear(buffer, 0, buffer.Length);
        var mapped = context.Map(stagingTexture, 0, MapMode.Read, MapFlags.None);
        try
        {
            int sourceRowPitch = checked((int)mapped.RowPitch);
            int destinationRowPitch = checked(_outputSize.Width * 4);
            int copiedRowBytes = checked(copyWidth * 4);
            for (int row = 0; row < copyHeight; row++)
            {
                Marshal.Copy(
                    IntPtr.Add(mapped.DataPointer, checked(row * sourceRowPitch)),
                    buffer,
                    checked(row * destinationRowPitch),
                    copiedRowBytes);
            }
        }
        finally
        {
            context.Unmap(stagingTexture, 0);
        }
    }

    private Bitmap CreateBitmap(byte[] buffer)
    {
        var bitmap = new Bitmap(_outputSize.Width, _outputSize.Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(Point.Empty, _outputSize),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            int sourceStride = checked(_outputSize.Width * 4);
            for (int row = 0; row < _outputSize.Height; row++)
            {
                Marshal.Copy(
                    buffer,
                    checked(row * sourceStride),
                    IntPtr.Add(data.Scan0, checked(row * data.Stride)),
                    sourceStride);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private void HandleFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        if (!_disposed)
            _frameAvailable.Set();
    }

    private void HandleItemClosed(GraphicsCaptureItem sender, object args)
    {
        _sourceClosed = true;
        _frameAvailable.Set();
    }

    private static GraphicsCaptureItem CreateItemForWindow(nint windowHandle)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        nint itemPointer = interop.CreateForWindow(windowHandle, GraphicsCaptureItemGuid);
        if (itemPointer == nint.Zero)
            throw new InvalidOperationException("Windows Graphics Capture did not create a capture item for the selected window.");

        try
        {
            return GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    private static ID3D11Texture2D CreateTexture2D(IDirect3DSurface surface)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        nint texturePointer = access.GetInterface(ID3D11Texture2DGuid);
        if (texturePointer == nint.Zero)
            throw new InvalidOperationException("The Windows Graphics Capture surface did not expose an ID3D11Texture2D.");

        return new ID3D11Texture2D(texturePointer);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _sourceClosed = true;
        _frameAvailable.Set();

        if (_framePool is not null)
            _framePool.FrameArrived -= HandleFrameArrived;
        if (_item is not null)
            _item.Closed -= HandleItemClosed;

        try { _session?.Dispose(); } catch { }
        try { _framePool?.Dispose(); } catch { }
        try { _winrtDevice?.Dispose(); } catch { }
        try { _stagingTexture?.Dispose(); } catch { }
        try { _context?.Dispose(); } catch { }
        try { _device?.Dispose(); } catch { }
        _frameAvailable.Dispose();

        _session = null;
        _framePool = null;
        _item = null;
        _winrtDevice = null;
        _stagingTexture = null;
        _context = null;
        _device = null;
        lock (_lastFrameLock)
            _lastFrameBuffer = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WindowsGraphicsCaptureFrameSource));
    }

    [ComImport]
    [System.Runtime.InteropServices.Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow([In] nint window, in Guid iid);
        nint CreateForMonitor([In] nint monitor, in Guid iid);
    }

    [ComImport]
    [System.Runtime.InteropServices.Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        nint GetInterface(in Guid iid);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(
        nint dxgiDevice,
        out nint graphicsDevice);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint windowHandle);
}
