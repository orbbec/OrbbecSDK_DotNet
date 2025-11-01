using System.Runtime.InteropServices;
using Orbbec;
using Samples.Common;

namespace Samples.Playback
{
    class Program
    {
        private static volatile bool _isRunning = true;
        private static volatile bool _isExited = false;

        static void Main()
        {
            Console.Clear();
            Console.WriteLine("Playback - Starting...");

            bool result = GetRosbagPath(out string filePath);

            try
            {
                if (!result) return;

                using var playback = new PlaybackDevice(filePath);
                using var pipe = new Pipeline(playback);
                using var imuPipe = new Pipeline(playback);
                using var config = new Config();

                playback.SetPlaybackStatusChangeCallback(status =>
                {
                    if (status == PlaybackStatus.OB_PLAYBACK_STOPPED && !_isExited)
                    {
                        pipe.Stop();
                        Thread.Sleep(1000);
                        pipe.Start(config);
                    }
                });

                using var sensorList = playback.GetSensorList();
                for (uint i = 0; i < sensorList.SensorCount(); ++i)
                {
                    var sensorType = sensorList.SensorType(i);
                    if (sensorType == SensorType.OB_SENSOR_ACCEL || sensorType == SensorType.OB_SENSOR_GYRO)
                    {
                        config.EnableStream(sensorType);
                        continue;
                    }

                    var format = sensorType switch
                    {
                        SensorType.OB_SENSOR_COLOR => Format.OB_FORMAT_RGB,
                        SensorType.OB_SENSOR_DEPTH => Format.OB_FORMAT_Y16,
                        SensorType.OB_SENSOR_IR or SensorType.OB_SENSOR_IR_LEFT
                            or SensorType.OB_SENSOR_IR_RIGHT => Format.OB_FORMAT_Y8,
                        _ => Format.OB_FORMAT_UNKNOWN
                    };
                    config.EnableVideoStream(sensorType, 0, 0, 0, format);
                }

                pipe.Start(config);

                using (var renderer = new OrbbecRenderer(1280, 720, "Playback"))
                {
                    Dictionary<SensorType, int> textureIndices = [];
                    for (uint i = 0; i < sensorList.SensorCount(); ++i)
                    {
                        var sensorType = sensorList.SensorType(i);
                        if (sensorType == SensorType.OB_SENSOR_ACCEL || sensorType == SensorType.OB_SENSOR_GYRO)
                            continue;

                        textureIndices.Add(sensorType, renderer.AddVideoFrame());
                    }
                    renderer.Closing += (e) =>
                    {
                        Console.WriteLine("Window closing, stopping...");
                        _isRunning = false;
                    };

                    _ = Task.Run(() => StartStream(pipe, renderer, textureIndices));

                    renderer.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }

            Console.WriteLine("Playback sample exited.");
        }

        private static bool GetRosbagPath(out string rosbagPath)
        {
            rosbagPath = string.Empty;

            while (_isRunning)
            {
                Console.WriteLine("Please input the path of the Rosbag file (.bag) to playback:");
                Console.Write("Path: ");

                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                input = input.Trim('\'', '"');
                if (input.EndsWith(".bag", StringComparison.OrdinalIgnoreCase))
                {
                    rosbagPath = input;
                    Console.WriteLine($"Playback file confirmed: {rosbagPath}\n");
                    return true;
                }

                Console.Write("Invalid file format. Please provide a .bag file.\n");
            }

            return false;
        }

        private static void StartStream(Pipeline pipeline, OrbbecRenderer renderer, Dictionary<SensorType, int> textureIndices)
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
                            // var accelValue = accelFrame.GetAccelValue();
                            // var obFloat3d = new Float3D { x = accelValue.x, y = accelValue.y, z = accelValue.z };
                            var dataPtr = accelFrame.GetDataPtr();
                            if (dataPtr == IntPtr.Zero)
                                continue;
                            var obFloat3d = Marshal.PtrToStructure<Float3D>(dataPtr);
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
                            // var gyroValue = gyroFrame.GetGyroValue();
                            // var obFloat3d = new Float3D { x = gyroValue.x, y = gyroValue.y, z = gyroValue.z };
                            var dataPtr = gyroFrame.GetDataPtr();
                            if (dataPtr == IntPtr.Zero)
                                continue;
                            var obFloat3d = Marshal.PtrToStructure<Float3D>(dataPtr);
                            PrintImuValue(obFloat3d, gyroIndex, gyroTimeStampUs, gyroTemperature, gyroType, "rad/s");
                        }
                    }

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

                        var frame = frameSet.GetFrame(frameType);

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

                _isExited = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Start stream error: {ex.Message}");
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