using Samples.Common;
using Orbbec;

namespace Samples.Infrared
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
            Console.WriteLine("Infrared - Starting...");

            var pipeline = new Pipeline();
            var device = pipeline.GetDevice();

            try
            {
                using (var renderer = new OrbbecRenderer(1280, 720, "Infrared"))
                {
                    var availableIrTypes = GetAvailableIrTypes(device);
                    var textureIndices = new Dictionary<SensorType, int>();
                    foreach (var sensorType in availableIrTypes)
                    {
                        int textureIndex = renderer.AddVideoFrame();
                        textureIndices[sensorType] = textureIndex;
                    }
                    renderer.Closing += (e) =>
                    {
                        Console.WriteLine("Window closing, stopping...");
                        _isRunning = false;
                    };

                    _ = Task.Run(() => StartStream(pipeline, renderer, textureIndices));

                    renderer.Run();
                }
            }
            finally
            {
                pipeline?.Stop();
                pipeline?.Dispose();
                device?.Dispose();
            }

            Console.WriteLine("Infrared sample exited.");
        }

        private static List<SensorType> GetAvailableIrTypes(Device device)
        {
            var availableTypes = new List<SensorType>();

            try
            {
                using var sensorList = device.GetSensorList();

                for (int i = 0; i < sensorList.SensorCount(); ++i)
                {
                    var sensorType = sensorList.SensorType((uint)i);
                    if (sensorType == SensorType.OB_SENSOR_IR ||
                        sensorType == SensorType.OB_SENSOR_IR_LEFT ||
                        sensorType == SensorType.OB_SENSOR_IR_RIGHT)
                    {
                        availableTypes.Add(sensorType);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get available IR sensor types: {ex.Message}");
            }

            return availableTypes;
        }

        private static void StartStream(Pipeline pipeline, OrbbecRenderer renderer, Dictionary<SensorType, int> textureIndices)
        {
            try
            {
                using var config = new Config();

                foreach (var sensorType in textureIndices.Keys)
                {
                    config.EnableVideoStream(sensorType, 0, 0, 0, Format.OB_FORMAT_Y8);
                    Console.WriteLine($"Enabled stream for: {sensorType}");
                }

                pipeline.Start(config);

                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    foreach (var (sensorType, textureIndex) in textureIndices)
                    {
                        var frameType = sensorType switch
                        {
                            SensorType.OB_SENSOR_IR => FrameType.OB_FRAME_IR,
                            SensorType.OB_SENSOR_IR_LEFT => FrameType.OB_FRAME_IR_LEFT,
                            SensorType.OB_SENSOR_IR_RIGHT => FrameType.OB_FRAME_IR_RIGHT,
                            _ => FrameType.OB_FRAME_IR
                        };

                        var irFrame = frameSet?.GetFrame(frameType);

                        if (irFrame != null)
                        {
                            var vf = irFrame.As<VideoFrame>();
                            byte[] data = new byte[vf.GetDataSize()];
                            vf.CopyData(ref data);
                            renderer.UpdateVideoFrame(textureIndex, (int)vf.GetWidth(), (int)vf.GetHeight(), vf.GetFormat(), data);
                            vf.Dispose();
                            irFrame.Dispose();
                        }
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