using Orbbec;

namespace Samples.HotPlugin
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

            using var ctx = new Context();
            ctx.SetDeviceChangedCallback((removedList, deviceList) =>
            {
                PrintDeviceList("added", deviceList);
                PrintDeviceList("removed", removedList);
            });

            var currentList = ctx.QueryDeviceList();
            PrintDeviceList("connected", currentList);
            currentList.Dispose();

            Console.WriteLine("Press 'r' to reboot the connected devices to trigger the device disconnect and reconnect event, or manually unplug and plugin the device.");
            Console.WriteLine("Press 'Esc' to exit.");

            while (_isRunning)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);

                    if (keyInfo.Key == ConsoleKey.Escape)
                        break;

                    if (keyInfo.Key == ConsoleKey.R)
                    {
                        using (currentList = ctx.QueryDeviceList())
                        {
                            Console.WriteLine("Rebooting devices...");
                            RebootDevices(currentList);
                        }
                    }
                }

                Thread.Sleep(100);
            }
        }

        private static void PrintDeviceList(string prompt, DeviceList deviceList)
        {
            var count = deviceList.DeviceCount();
            if (count == 0) return;

            Console.WriteLine($"{count} device(s) {prompt}: ");
            for (uint i = 0; i < count; i++)
            {
                var uid = deviceList.Uid(i);
                var vid = deviceList.Vid(i);
                var pid = deviceList.Pid(i);
                var serialNumber = deviceList.SerialNumber(i);
                var connection = deviceList.ConnectionType(i);

                Console.WriteLine($" - uid: {uid}, vid: 0x{vid:X4}, pid: 0x{pid:X4}, serial number: {serialNumber}, connection: {connection}");
            }
            Console.WriteLine();
        }

        private static void RebootDevices(DeviceList deviceList)
        {
            for (int i = 0; i < deviceList.DeviceCount(); ++i)
            {
                var device = deviceList.GetDevice((uint)i);
                device.Reboot();
            }
        }
    }
}