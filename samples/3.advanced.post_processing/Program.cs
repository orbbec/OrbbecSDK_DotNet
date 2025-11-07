using System.Runtime.InteropServices;
using Orbbec;
using Samples.Common;

namespace Samples.PostProcessing
{
    class Program
    {
        private static volatile bool _isRunning = true;

        static void Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("Post Processing - Starting...");

            using var renderer = new OrbbecRenderer(title: "Post Processing");

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
                device = pipe.GetDevice();
                var sensor = device.GetSensor(SensorType.OB_SENSOR_DEPTH);
                var filterList = sensor.CreateRecommendedFilters();

                PrintFiltersInfo(filterList);

                using var config = new Config();
                config.EnableVideoStream(StreamType.OB_STREAM_DEPTH, 0, 0, 0, Format.OB_FORMAT_Y16);

                pipe.Start(config);

                int depthTextureIndex = renderer.AddVideoFrame();
                int processedTextureIndex = renderer.AddVideoFrame();
                renderer.Closing += (e) =>
                {
                    Console.WriteLine("Window closing, stopping...");
                    _isRunning = false;
                };

                _ = Task.Run(() => StartStream(pipe, filterList, renderer, depthTextureIndex, processedTextureIndex));
                _ = Task.Run(() => FilterControl(filterList));

                renderer.Run();
            }
            finally
            {
                pipe?.Stop();
                device?.Dispose();
                Console.WriteLine("Post Processing sample exited.");
                Environment.Exit(0);
            }
        }

        private static void PrintFiltersInfo(List<Filter> filterList)
        {
            Console.WriteLine("post processing filters recommended:");
            foreach (var filter in filterList)
            {
                Console.WriteLine($" - {filter.Name()}: {(filter.IsEnabled() ? "enabled" : "disabled")}");
                var configSchemaList = filter.GetConfigSchemaList();

                foreach (var configSchema in configSchemaList)
                {
                    Console.WriteLine($"    - {{ {Marshal.PtrToStringAnsi(configSchema.name)}, {configSchema.type}, " +
                                            $"{configSchema.min}, {configSchema.max}, {configSchema.step}, {configSchema.def}, " +
                                            $"{Marshal.PtrToStringAnsi(configSchema.desc)} }}");
                }
                filter.Enable(false);
            }
        }

        private static void FilterControl(List<Filter> filterList)
        {
            PrintHelp();

            while (_isRunning)
            {
                Console.WriteLine(new string('-', 27));
                Console.Write("Enter your input (h for help): ");

                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input.Equals("l", StringComparison.OrdinalIgnoreCase))
                {
                    PrintFiltersInfo(filterList);
                    continue;
                }
                else if (input.Equals("h", StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                    continue;
                }

                string[] tokens = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    continue;

                bool foundFilter = false;
                foreach (var filter in filterList)
                {
                    if (filter.Name() != tokens[0]) continue;

                    foundFilter = true;
                    if (tokens.Length == 1)
                    {
                        var configSchemaList = filter.GetConfigSchemaList();
                        Console.WriteLine($"Config values for {filter.Name()}:");
                        foreach (var configSchema in configSchemaList)
                        {
                            string name = Marshal.PtrToStringAnsi(configSchema.name) ?? string.Empty;
                            if (!string.IsNullOrEmpty(name))
                            {
                                Console.WriteLine($" - {name}: {filter.GetConfigValue(name)}");
                            }
                        }
                    }
                    else if (tokens.Length == 2 && (tokens[1] == "on" || tokens[1] == "off"))
                    {
                        filter.Enable(tokens[1] == "on");
                        Console.WriteLine($"Success: Filter {filter.Name()} is now {(filter.IsEnabled() ? "enabled" : "disabled")}");
                    }
                    else if (tokens.Length == 2 && tokens[1] == "list")
                    {
                        var configSchemaList = filter.GetConfigSchemaList();
                        Console.WriteLine($"Config values for {filter.Name()}:");
                        foreach (var configSchema in configSchemaList)
                        {
                            Console.WriteLine($" - {{ {Marshal.PtrToStringAnsi(configSchema.name)}, {configSchema.type}, " +
                                    $"{configSchema.min}, {configSchema.max}, {configSchema.step}, {configSchema.def}, " +
                                    $"{Marshal.PtrToStringAnsi(configSchema.desc)} }}");
                        }
                    }
                    else if (tokens.Length == 2)
                    {
                        var configSchemaList = filter.GetConfigSchemaList();
                        bool foundConfig = false;
                        foreach (var configSchema in configSchemaList)
                        {
                            string name = Marshal.PtrToStringAnsi(configSchema.name) ?? string.Empty;
                            if (!string.IsNullOrEmpty(name) && name.Equals(tokens[1], StringComparison.OrdinalIgnoreCase))
                            {
                                foundConfig = true;
                                Console.WriteLine($"Config values for {filter.Name()}@{name}: {filter.GetConfigValue(name)}");
                                break;
                            }
                        }
                        if (!foundConfig)
                        {
                            Console.WriteLine($"Error: Config {tokens[1]} not found for filter {filter.Name()}");
                        }
                    }
                    else if (tokens.Length == 3)
                    {
                        try
                        {
                            double value = double.Parse(tokens[2]);
                            filter.SetConfigValue(tokens[1], value);
                            Console.WriteLine($"Success: Config value of {tokens[1]} for filter {filter.Name()} is set to {value}.");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error: {e.Message}");
                            continue;
                        }
                    }
                    break;
                }
                if (!foundFilter)
                {
                    Console.WriteLine($"Error: Filter {tokens[0]} not found");
                }
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("- Enter `[Filter]` to list the config values for the filter");
            Console.WriteLine("- Enter `[Filter] on` or `[Filter] off` to enable/disable the filter");
            Console.WriteLine("- Enter `[Filter] list` to list the config schema for the filter");
            Console.WriteLine("- Enter `[Filter] [Config]` to show the config values for the filter");
            Console.WriteLine("- Enter `[Filter] [Config] [Value]` to set a config value");
            Console.WriteLine("- Enter `L`or `l` to list all available filters");
            Console.WriteLine("- Enter `H` or `h` to print this help message");
            Console.WriteLine("- Enter `Q` or `q` to quit");
        }

        private static void StartStream(Pipeline pipeline, List<Filter> filterList, OrbbecRenderer renderer, int depthTextureIndex, int processedTextureIndex)
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

                    var processedFrame = depthFrame;
                    foreach (var filter in filterList)
                    {
                        if (filter.IsEnabled())
                        {
                            using var newProcessedFrame = filter.Process(processedFrame);
                            processedFrame = newProcessedFrame.As<DepthFrame>();
                        }
                    }

                    byte[] depthData = new byte[depthFrame.GetDataSize()];
                    depthFrame.CopyData(ref depthData);
                    renderer.UpdateVideoFrame(depthTextureIndex, (int)depthFrame.GetWidth(),
                        (int)depthFrame.GetHeight(), Format.OB_FORMAT_Y16, depthData);

                    byte[] resultData = new byte[processedFrame.GetDataSize()];
                    processedFrame.CopyData(ref resultData);
                    renderer.UpdateVideoFrame(processedTextureIndex, (int)processedFrame.GetWidth(),
                        (int)processedFrame.GetHeight(), Format.OB_FORMAT_Y16, resultData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Start stream error: {ex.Message}");
            }
        }
    }
}