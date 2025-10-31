using Orbbec;
using Samples.Common;

namespace Samples.MultiDevices
{
    class Program
    {
        private static volatile bool _isRunning = true;
        private static readonly Dictionary<uint, Pipeline> _pipes = [];

        static void Main()
        {
            Console.Clear();
            Console.WriteLine("Multi Devices - Starting...");

            Context? ctx = null;
            try
            {
                ctx = new Context();

                using var deviceList = ctx.QueryDeviceList();
                uint count = deviceList.DeviceCount();
                for (uint i = 0; i < count; ++i)
                {
                    var device = deviceList.GetDevice(i);
                    var pipe = new Pipeline(device);
                    _pipes.Add(i, pipe);
                }

                foreach (var (i, pipe) in _pipes)
                {
                    using var config = new Config();
                    config.EnableVideoStream(StreamType.OB_STREAM_COLOR, 0, 0, 0, Format.OB_FORMAT_RGB);
                    config.EnableVideoStream(StreamType.OB_STREAM_DEPTH, 0, 0, 0, Format.OB_FORMAT_Y16);

                    pipe.Start(config);
                }

                using (var renderer = new OrbbecRenderer(1280, 720, "Multi Devices"))
                {
                    for (uint i = 0; i < count * 2; ++i)
                    {
                        renderer.AddVideoFrame();
                    }
                    renderer.Closing += (e) =>
                    {
                        Console.WriteLine("Window closing, stopping...");
                        _isRunning = false;
                    };

                    _ = Task.Run(() => StartStream(renderer));

                    renderer.Run();
                }
            }
            finally
            {
                foreach (var (_, pipe) in _pipes)
                {
                    pipe.Stop();
                }
                ctx?.Dispose();
            }

            Console.WriteLine("Multi Devices sample exited.");
        }

        private static void StartStream(OrbbecRenderer renderer)
        {
            try
            {
                while (_isRunning)
                {
                    foreach (var (i, pipe) in _pipes)
                    {
                        using var frameset = pipe.WaitForFrames(100);
                        if (frameset == null)
                            continue;

                        using var colorFrame = frameset.GetColorFrame();
                        using var depthFrame = frameset.GetDepthFrame();

                        if (colorFrame == null || depthFrame == null)
                        {
                            frameset.Dispose();
                            continue;
                        }

                        int index = (int)i * 2;

                        byte[] colorData = new byte[colorFrame.GetDataSize()];
                        colorFrame.CopyData(ref colorData);
                        renderer.UpdateVideoFrame(index, (int)colorFrame.GetWidth(),
                            (int)colorFrame.GetHeight(), colorFrame.GetFormat(), colorData);

                        byte[] depthData = new byte[depthFrame.GetDataSize()];
                        depthFrame.CopyData(ref depthData);
                        renderer.UpdateVideoFrame(index + 1, (int)depthFrame.GetWidth(),
                            (int)depthFrame.GetHeight(), depthFrame.GetFormat(), depthData);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Start stream error: {ex.Message}");
            }
        }
    }
}