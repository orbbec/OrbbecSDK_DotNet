using Samples.Common;
using Orbbec;

namespace Samples.Depth
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
            Console.WriteLine("Depth - Starting...");

            using (var renderer = new OrbbecRenderer(1280, 720, "Depth"))
            {
                int depthTextureIndex = renderer.AddVideoFrame();
                renderer.Closing += (e) =>
                {
                    Console.WriteLine("Window closing, stopping...");
                    _isRunning = false;
                };

                _ = Task.Run(() => StartStream(renderer, depthTextureIndex));

                renderer.Run();
            }

            Console.WriteLine("Depth sample exited.");
        }

        private static void StartStream(OrbbecRenderer renderer, int depthTextureIndex)
        {
            try
            {
                using var pipeline = new Pipeline();
                using var config = new Config();

                using var depthProfileList = pipeline.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH);
                using var depthProfile = depthProfileList.GetVideoStreamProfile(0, 0, Format.OB_FORMAT_Y16, 0);

                Console.WriteLine($"Depth Profile: {depthProfile.GetWidth()}x{depthProfile.GetHeight()}@{depthProfile.GetFormat()}");
                config.EnableStream(depthProfile);
                pipeline.Start(config);

                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    var depthFrame = frameSet?.GetDepthFrame();

                    if (depthFrame != null)
                    {
                        byte[] data = new byte[depthFrame.GetDataSize()];
                        depthFrame.CopyData(ref data);
                        renderer.UpdateVideoFrame(depthTextureIndex, (int)depthFrame.GetWidth(),
                            (int)depthFrame.GetHeight(), depthFrame.GetFormat(), data);
                        depthFrame.Dispose();
                    }
                    else
                    {
                        Console.WriteLine("No depth frame received - timeout or error");
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