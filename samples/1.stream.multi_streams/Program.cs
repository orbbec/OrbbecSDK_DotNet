using Samples.Common;
using Orbbec;
using System.Runtime.InteropServices;

namespace Samples.MultiStreams
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
            Console.WriteLine("Multi Streams - Starting...");

            var pipeline = new Pipeline();
            var device = pipeline.GetDevice();
            var imuPipeline = new Pipeline(device);

            try
            {
                using (var renderer = new OrbbecRenderer(1280, 720, "Multi Streams"))
                {
                    var availableTypes = GetAvailableTypes(device);
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

                    _ = Task.Run(() => StartStream(pipeline, renderer, textureIndices));
                    _ = Task.Run(() => StartIMU(imuPipeline));

                    renderer.Run();
                }
            }
            finally
            {
                pipeline?.Stop();
                imuPipeline?.Stop();
                pipeline?.Dispose();
                device?.Dispose();
            }

            Console.WriteLine("Multi Streams sample exited.");
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
                using var config = new Config();

                foreach (var sensorType in textureIndices.Keys)
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

                pipeline.Start(config);

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

                        var frame = frameSet?.GetFrame(frameType);

                        if (frame != null)
                        {
                            var vf = frame.As<VideoFrame>();
                            byte[] data = new byte[vf.GetDataSize()];
                            vf.CopyData(ref data);
                            renderer.UpdateVideoFrame(textureIndex, (int)vf.GetWidth(), (int)vf.GetHeight(), vf.GetFormat(), data);
                            vf.Dispose();
                            frame.Dispose();
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
                using var config = new Config();
                config.EnableAccelStream();
                config.EnableGyroStream();
                pipeline.Start(config);

                while (_isRunning)
                {
                    using var frameSet = pipeline.WaitForFrames(100);
                    if (frameSet == null) continue;

                    using var accelFrameRaw = frameSet.GetFrame(FrameType.OB_FRAME_ACCEL);
                    using var accelFrame = accelFrameRaw.As<AccelFrame>();
                    var accelIndex = accelFrame.GetIndex();
                    var accelTimeStampUs = accelFrame.GetTimeStampUs();
                    var accelTemperature = accelFrame.GetTemperature();
                    var accelType = accelFrame.GetFrameType();
                    if (accelIndex % 50 == 0)
                    {
                        // print information every  50 frames.
                        // var accelValue = accelFrame.GetAccelValue();
                        // var obFloat3d = new Float3D { x = accelValue.x, y = accelValue.y, z = accelValue.z };
                        var dataPtr = accelFrame.GetDataPtr();
                        if (dataPtr == IntPtr.Zero)
                            continue;
                        var obFloat3d = Marshal.PtrToStructure<Float3D>(dataPtr);
                        PrintImuValue(obFloat3d, accelIndex, accelTimeStampUs, accelTemperature, accelType, "m/s^2");
                    }

                    using var gyroFrameRaw = frameSet.GetFrame(FrameType.OB_FRAME_GYRO);
                    using var gyroFrame = gyroFrameRaw.As<GyroFrame>();
                    var gyroIndex = gyroFrame.GetIndex();
                    var gyroTimeStampUs = gyroFrame.GetTimeStampUs();
                    var gyroTemperature = gyroFrame.GetTemperature();
                    var gyroType = gyroFrame.GetFrameType();
                    if (gyroIndex % 50 == 0)
                    {
                        // print information every 50 frames.
                        // var gyroValue = gyroFrame.GetGyroValue();
                        // var obFloat3d = new Float3D { x = gyroValue.x, y = gyroValue.y, z = gyroValue.z };
                        var dataPtr = gyroFrame.GetDataPtr();
                        if (dataPtr == IntPtr.Zero)
                            continue;
                        var obFloat3d = Marshal.PtrToStructure<Float3D>(dataPtr);
                        PrintImuValue(obFloat3d, gyroIndex, gyroTimeStampUs, gyroTemperature, gyroType, "rad/s");
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