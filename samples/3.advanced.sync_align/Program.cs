using Orbbec;
using Samples.Common;

namespace Samples.SyncAlign
{
    class Program
    {
        private static volatile bool _isRunning = true;
        private static volatile bool _enableSync = false;
        private static volatile int _alignMode = 0;
        private static volatile float _alpha = 0.6f;

        static void Main()
        {
            Console.Clear();
            Console.WriteLine("Sync Align - Starting...");

            Pipeline? pipe = null;
            try
            {
                pipe = new Pipeline();

                using var config = new Config();
                config.EnableVideoStream(StreamType.OB_STREAM_COLOR, 0, 0, 0, Format.OB_FORMAT_RGB);
                config.EnableVideoStream(StreamType.OB_STREAM_DEPTH, 0, 0, 0, Format.OB_FORMAT_Y16);
                config.SetFrameAggregateOutputMode(FrameAggregateOutputMode.OB_FRAME_AGGREGATE_OUTPUT_ALL_TYPE_FRAME_REQUIRE);

                pipe.Start(config);

                // Create a filter to align depth frame to color frame
                using var d2cAlign = new AlignFilter(StreamType.OB_STREAM_COLOR);
                // Create a filter to align color frame to depth frame
                using var c2dAlign = new AlignFilter(StreamType.OB_STREAM_DEPTH);

                using (var renderer = new OrbbecRenderer(1280, 720, "Sync Align"))
                {
                    int syncAlignTextureIndex = renderer.AddVideoFrame();
                    renderer.Closing += (e) =>
                    {
                        Console.WriteLine("Window closing, stopping...");
                        _isRunning = false;
                    };

                    _ = Task.Run(() => HandleKeyPress(pipe, config));
                    _ = Task.Run(() => StartStream(pipe, d2cAlign, c2dAlign, renderer, syncAlignTextureIndex));

                    renderer.Run();
                }
            }
            finally
            {
                pipe?.Stop();
            }

            Console.WriteLine("Sync Align sample exited.");
        }

        private static async Task HandleKeyPress(Pipeline pipeline, Config config)
        {
            Console.WriteLine("'T': Switch Align Mode, 'F': Toggle Synchronization, '+/-': Adjust Transparency");
            while (_isRunning)
            {
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(50);
                    continue;
                }

                var keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.F)
                {
                    _enableSync = !_enableSync;
                    if (_enableSync)
                        pipeline.EnableFrameSync();
                    else
                        pipeline.DisableFrameSync();
                    Console.WriteLine("Sync: " + (_enableSync ? "On" : "Off"));
                }
                else if (keyInfo.Key == ConsoleKey.T)
                {
                    _alignMode = (_alignMode + 1) % 2;
                    Console.WriteLine("Align Mode: " + (_alignMode == 0 ? "Depth to Color" : "Color to Depth"));
                }
                else if (keyInfo.Key == ConsoleKey.Add || keyInfo.Key == ConsoleKey.OemPlus)
                {
                    _alpha = Math.Min(_alpha + 0.1f, 1.0f);
                    Console.WriteLine($"Adjust alpha to {_alpha:F2}");
                }
                else if (keyInfo.Key == ConsoleKey.Subtract || keyInfo.Key == ConsoleKey.OemMinus)
                {
                    _alpha = Math.Max(_alpha - 0.1f, 0.0f);
                    Console.WriteLine($"Adjust alpha to {_alpha:F2}");
                }
            }
        }

        private static void StartStream(Pipeline pipeline, AlignFilter d2cAlign, AlignFilter c2dAlign,
            OrbbecRenderer renderer, int syncAlignTextureIndex)
        {
            try
            {
                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    var align = _alignMode == 0 ? d2cAlign : c2dAlign;
                    using var frame = align.Process(frameSet);

                    if (frame == null) continue;

                    using var newFrameSet = frame.As<Frameset>();
                    using var colorFrame = newFrameSet?.GetColorFrame();
                    using var depthFrame = newFrameSet?.GetDepthFrame();

                    if (colorFrame == null || depthFrame == null)
                        continue;

                    byte[] alignData = new byte[colorFrame.GetDataSize()];
                    SyncAlignProcess(colorFrame, depthFrame, ref alignData);
                    renderer.UpdateVideoFrame(syncAlignTextureIndex, (int)colorFrame.GetWidth(),
                        (int)colorFrame.GetHeight(), Format.OB_FORMAT_RGB, alignData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Start stream error: {ex.Message}");
            }
        }

        private static void SyncAlignProcess(VideoFrame colorFrame, VideoFrame depthFrame, ref byte[] alignData)
        {
            try
            {
                int colorW = (int)colorFrame.GetWidth();
                int colorH = (int)colorFrame.GetHeight();
                byte[] colorData = new byte[colorFrame.GetDataSize()];
                colorFrame.CopyData(ref colorData);

                int depthW = (int)depthFrame.GetWidth();
                int depthH = (int)depthFrame.GetHeight();
                byte[] depthDataRaw = new byte[depthFrame.GetDataSize()];
                depthFrame.CopyData(ref depthDataRaw);
                byte[] depthData = ImageConverter.ConvertY16ToRgb(depthW, depthH, depthDataRaw);

                alignData = ImageConverter.DepthAlignToColor(colorW, colorH, colorData, depthW, depthH, depthData, _alpha);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Process error: {ex.Message}");
            }
        }
    }
}