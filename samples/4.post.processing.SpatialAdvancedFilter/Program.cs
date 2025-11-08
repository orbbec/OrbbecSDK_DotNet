using Orbbec;
using Samples.Common;

namespace Samples.SpatialAdvanced
{
    class Program
    {
        private static volatile bool _isRunning = true;

        static void Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("SpatialAdvanced - Starting...");

            using var renderer = new OrbbecRenderer(title: "SpatialAdvanced");

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                renderer.Close();
            };

            Pipeline? pipe = null;
            Device? device = null;
            try
            {
                pipe = new Pipeline();
                using var config = new Config();

                config.EnableStream(StreamType.OB_STREAM_DEPTH);
                pipe.Start(config);

                device = pipe.GetDevice();
                var sensor = device.GetSensor(SensorType.OB_SENSOR_DEPTH);
                var filterList = sensor.CreateRecommendedFilters();
                SpatialAdvancedFilter? filter = null;
                foreach (var f in filterList)
                {
                    if (f.Name().Equals("SpatialAdvancedFilter"))
                    {
                        filter = f.As<SpatialAdvancedFilter>();
                    }
                }
                if (filter == null)
                {
                    Console.WriteLine("The current device does not support SpatialAdvancedFilter!");
                    return;
                }
                filter.Enable(true);
                var filterParams = new SpatialAdvancedFilterParams
                {
                    magnitude = 1,
                    alpha = 0.5f,
                    disp_diff = 160,
                    radius = 1
                };
                filter.SetFilterParams(filterParams);

                int depthTextureIndex = renderer.AddVideoFrame();
                int processedTextureIndex = renderer.AddVideoFrame();
                renderer.Closing += (e) =>
                {
                    Console.WriteLine("Window closing, stopping...");
                    _isRunning = false;
                };

                _ = Task.Run(() => StartStream(pipe, filter, renderer, depthTextureIndex, processedTextureIndex));

                renderer.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
            finally
            {
                pipe?.Stop();
                device?.Dispose();
                Console.WriteLine("SpatialAdvanced sample exited.");
                Environment.Exit(0);
            }
        }

        private static void StartStream(Pipeline pipeline, SpatialAdvancedFilter filter, OrbbecRenderer renderer, int depthTextureIndex, int processedTextureIndex)
        {
            try
            {
                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    using var depthFrame = frameSet.GetDepthFrame();

                    if (depthFrame == null)
                        continue;

                    using var newProcessedFrame = filter.Process(depthFrame);
                    using var processedFrame = newProcessedFrame.As<DepthFrame>();

                    byte[] depthData = new byte[depthFrame.GetDataSize()];
                    depthFrame.CopyData(ref depthData);
                    renderer.UpdateVideoFrame(depthTextureIndex, (int)depthFrame.GetWidth(),
                        (int)depthFrame.GetHeight(), depthFrame.GetFormat(), depthData);

                    byte[] processedData = new byte[processedFrame.GetDataSize()];
                    processedFrame.CopyData(ref processedData);
                    renderer.UpdateVideoFrame(processedTextureIndex, (int)processedFrame.GetWidth(),
                        (int)processedFrame.GetHeight(), processedFrame.GetFormat(), processedData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Start stream error: {ex.Message}");
            }
        }
    }
}