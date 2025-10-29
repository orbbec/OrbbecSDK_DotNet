using Samples.Common;
using Orbbec;

namespace Samples.Color
{
    class Program
    {
        private static volatile bool _isRunning = true;

        static void Main()
        {
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _isRunning = false;
                Console.WriteLine("Exiting...");
                Environment.Exit(0);
            };

            Console.Clear();
            Console.WriteLine("Color - Starting...");

            using (var renderer = new OrbbecRenderer(1280, 720, "Color"))
            {
                int colorTextureIndex = renderer.AddVideoFrame();
                renderer.Closing += (e) =>
                {
                    Console.WriteLine("Window closing, stopping...");
                    _isRunning = false;
                };

                _ = Task.Run(() => StartStream(renderer, colorTextureIndex));

                renderer.Run();
            }

            Console.WriteLine("Color sample exited.");
        }

        private static void StartStream(OrbbecRenderer renderer, int colorTextureIndex)
        {
            try
            {
                using var pipeline = new Pipeline();
                using var config = new Config();

                using var colorProfileList = pipeline.GetStreamProfileList(SensorType.OB_SENSOR_COLOR);
                using var colorProfile = colorProfileList.GetVideoStreamProfile(0, 0, Format.OB_FORMAT_RGB, 0);

                Console.WriteLine($"Color Profile: {colorProfile.GetWidth()}x{colorProfile.GetHeight()}@{colorProfile.GetFormat()}");
                config.EnableStream(colorProfile);
                pipeline.Start(config);

                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    var colorFrame = frameSet?.GetColorFrame();

                    if (colorFrame != null)
                    {
                        byte[] data = new byte[colorFrame.GetDataSize()];
                        colorFrame.CopyData(ref data);
                        renderer.UpdateVideoFrame(colorTextureIndex, (int)colorFrame.GetWidth(),
                            (int)colorFrame.GetHeight(), colorFrame.GetFormat(), data);
                        colorFrame.Dispose();
                    }
                    else
                    {
                        Console.WriteLine("No color frame received - timeout or error");
                    }
                }

                pipeline.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Start stream error: {ex.Message}");
            }
        }
    }
}