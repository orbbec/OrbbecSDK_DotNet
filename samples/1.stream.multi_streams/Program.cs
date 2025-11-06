using Samples.Common;
using Orbbec;

namespace Samples.MultiStreams
{
    class Program
    {
        private static volatile bool _isRunning = true;

        static void Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("Multi Streams - Starting...");

            using var renderer = new OrbbecRenderer(title: "Multi Streams");

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                renderer.Close();
            };

            Pipeline? pipe = null;
            Device? device = null;
            Pipeline? imuPipeline = null;
            try
            {
                pipe = new Pipeline();
                device = pipe.GetDevice();
                var availableTypes = GetAvailableTypes(device);
                using var config = new Config();

                foreach (var sensorType in availableTypes)
                {
                    var format = sensorType switch
                    {
                        SensorType.OB_SENSOR_COLOR => Format.OB_FORMAT_RGB,
                        SensorType.OB_SENSOR_DEPTH => Format.OB_FORMAT_Y16,
                        SensorType.OB_SENSOR_IR or SensorType.OB_SENSOR_IR_LEFT
                            or SensorType.OB_SENSOR_IR_RIGHT => Format.OB_FORMAT_Y8,
                        _ => Format.OB_FORMAT_UNKNOWN
                    };
                    config.EnableVideoStream(sensorType, 0, 0, 0, format);
                    Console.WriteLine($"Enabled stream for: {sensorType}");
                }
                pipe.Start(config);

                imuPipeline = new Pipeline(device);
                using var imuConfig = new Config();

                imuConfig.EnableAccelStream();
                imuConfig.EnableGyroStream();
                imuPipeline.Start(imuConfig);

                var textureIndices = new Dictionary<SensorType, int>();
                foreach (var sensorType in availableTypes)
                {
                    int textureIndex = renderer.AddVideoFrame();
                    textureIndices[sensorType] = textureIndex;
                }
                renderer.Closing += (e) =>
                {
                    Console.WriteLine("Window closing, stopping...");
                    _isRunning = false;
                };

                _ = Task.Run(() => StartStream(pipe, renderer, textureIndices));
                _ = Task.Run(() => StartIMU(imuPipeline));

                renderer.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
            finally
            {
                pipe?.Stop();
                imuPipeline?.Stop();
                device?.Dispose();
                Console.WriteLine("MultiStreams sample exited.");
                Environment.Exit(0);
            }
        }

        private static List<SensorType> GetAvailableTypes(Device device)
        {
            var availableTypes = new List<SensorType>();

            try
            {
                using var sensorList = device.GetSensorList();

                for (int i = 0; i < sensorList.SensorCount(); ++i)
                {
                    var sensorType = sensorList.SensorType((uint)i);
                    if (sensorType == SensorType.OB_SENSOR_ACCEL || sensorType == SensorType.OB_SENSOR_GYRO)
                        continue;

                    availableTypes.Add(sensorType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get available sensor types: {ex.Message}");
            }

            return availableTypes;
        }

        private static void StartStream(Pipeline pipeline, OrbbecRenderer renderer, Dictionary<SensorType, int> textureIndices)
        {
            try
            {
                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    foreach (var (sensorType, textureIndex) in textureIndices)
                    {
                        var frameType = sensorType switch
                        {
                            SensorType.OB_SENSOR_COLOR => FrameType.OB_FRAME_COLOR,
                            SensorType.OB_SENSOR_DEPTH => FrameType.OB_FRAME_DEPTH,
                            SensorType.OB_SENSOR_IR => FrameType.OB_FRAME_IR,
                            SensorType.OB_SENSOR_IR_LEFT => FrameType.OB_FRAME_IR_LEFT,
                            SensorType.OB_SENSOR_IR_RIGHT => FrameType.OB_FRAME_IR_RIGHT,
                            _ => FrameType.OB_FRAME_UNKNOWN
                        };

                        using var frame = frameSet.GetFrame(frameType);

                        if (frame != null)
                        {
                            using var vf = frame.As<VideoFrame>();
                            byte[] data = new byte[vf.GetDataSize()];
                            vf.CopyData(ref data);
                            renderer.UpdateVideoFrame(textureIndex, (int)vf.GetWidth(), (int)vf.GetHeight(), vf.GetFormat(), data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Start stream error: {ex.Message}");
            }
        }

        private static void StartIMU(Pipeline pipeline)
        {
            try
            {
                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    using var accelFrameRaw = frameSet.GetFrame(FrameType.OB_FRAME_ACCEL);
                    if (accelFrameRaw != null)
                    {
                        using var accelFrame = accelFrameRaw.As<AccelFrame>();
                        var accelIndex = accelFrame.GetIndex();
                        var accelTimeStampUs = accelFrame.GetTimeStampUs();
                        var accelTemperature = accelFrame.GetTemperature();
                        var accelType = accelFrame.GetFrameType();
                        if (accelIndex % 50 == 0)
                        {
                            // print information every  50 frames.
                            var accelValue = accelFrame.GetAccelValue();
                            var obFloat3d = new Float3D { x = accelValue.x, y = accelValue.y, z = accelValue.z };
                            PrintImuValue(obFloat3d, accelIndex, accelTimeStampUs, accelTemperature, accelType, "m/s^2");
                        }
                    }

                    using var gyroFrameRaw = frameSet.GetFrame(FrameType.OB_FRAME_GYRO);
                    if (gyroFrameRaw != null)
                    {
                        using var gyroFrame = gyroFrameRaw.As<GyroFrame>();
                        var gyroIndex = gyroFrame.GetIndex();
                        var gyroTimeStampUs = gyroFrame.GetTimeStampUs();
                        var gyroTemperature = gyroFrame.GetTemperature();
                        var gyroType = gyroFrame.GetFrameType();
                        if (gyroIndex % 50 == 0)
                        {
                            // print information every 50 frames.
                            var gyroValue = gyroFrame.GetGyroValue();
                            var obFloat3d = new Float3D { x = gyroValue.x, y = gyroValue.y, z = gyroValue.z };
                            PrintImuValue(obFloat3d, gyroIndex, gyroTimeStampUs, gyroTemperature, gyroType, "rad/s");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Start IMU error: {ex.Message}");
            }
        }

        private static void PrintImuValue(Float3D obFloat3d, ulong index, ulong timeStampUs, float temperature, FrameType type, string unit)
        {
            Console.WriteLine("frame index: " + index);
            string typeStr = type.ToString();
            Console.WriteLine($"{typeStr} Frame: ");
            Console.WriteLine("{");
            Console.WriteLine($"  tsp = {timeStampUs}");
            Console.WriteLine($"  temperature = {temperature}");
            Console.WriteLine($"  {typeStr}.x = {obFloat3d.x}{unit}");
            Console.WriteLine($"  {typeStr}.y = {obFloat3d.y}{unit}");
            Console.WriteLine($"  {typeStr}.z = {obFloat3d.z}{unit}");
            Console.WriteLine("}");
            Console.WriteLine();
        }
    }
}