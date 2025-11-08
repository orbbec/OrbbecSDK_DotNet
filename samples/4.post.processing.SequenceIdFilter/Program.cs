using Orbbec;
using Samples.Common;

namespace Samples.SequenceId
{
    class Program
    {
        private static volatile bool _isRunning = true;

        static void Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("SequenceId - Starting...");

            using var renderer = new OrbbecRenderer(title: "SequenceId");

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
                SequenceIdFilter? filter = null;
                foreach (var f in filterList)
                {
                    if (f.Name().Equals("SequenceIdFilter"))
                    {
                        filter = f.As<SequenceIdFilter>();
                    }
                }
                if (filter == null)
                {
                    Console.WriteLine("The current device does not support SequenceIdFilter!");
                    return;
                }
                filter.Enable(true);
                filter.SelectSequenceId(1);

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
                Console.WriteLine("SequenceId sample exited.");
            }
        }

        private static void StartStream(Pipeline pipeline, SequenceIdFilter filter, OrbbecRenderer renderer, int depthTextureIndex, int processedTextureIndex)
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