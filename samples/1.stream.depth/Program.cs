using Samples.Common;
using Orbbec;

namespace Samples.Depth
{
    class Program
    {
        private static volatile bool _isRunning = true;

        static void Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("Depth - Starting...");

            using var renderer = new OrbbecRenderer(title: "Depth");

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                renderer.Close();
            };

            Pipeline? pipe = null;
            try
            {
                pipe = new Pipeline();
                using var config = new Config();

                using var depthProfileList = pipe.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH);
                using var depthProfile = depthProfileList.GetVideoStreamProfile(0, 0, Format.OB_FORMAT_Y16, 0);
                Console.WriteLine($"Depth Profile: {depthProfile.GetWidth()}x{depthProfile.GetHeight()}@{depthProfile.GetFormat()}");

                config.EnableStream(depthProfile);
                pipe.Start(config);

                int depthTextureIndex = renderer.AddVideoFrame();
                renderer.Closing += (e) =>
                {
                    Console.WriteLine("Window closing, stopping...");
                    _isRunning = false;
                };

                _ = Task.Run(() => StartStream(pipe, renderer, depthTextureIndex));

                renderer.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
            finally
            {
                pipe?.Stop();
                Console.WriteLine("Depth sample exited.");
            }
        }

        private static void StartStream(Pipeline pipeline, OrbbecRenderer renderer, int depthTextureIndex)
        {
            try
            {
                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    using var depthFrame = frameSet.GetDepthFrame();

                    if (depthFrame != null)
                    {
                        byte[] data = new byte[depthFrame.GetDataSize()];
                        depthFrame.CopyData(ref data);
                        renderer.UpdateVideoFrame(depthTextureIndex, (int)depthFrame.GetWidth(),
                            (int)depthFrame.GetHeight(), depthFrame.GetFormat(), data);
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