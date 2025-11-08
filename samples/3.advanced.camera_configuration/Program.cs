using Orbbec;

namespace Samples.CameraConfiguration
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("Camera Configuration - Starting...");

            Pipeline? pipe = null;
            try
            {
                pipe = new Pipeline();

                var profile_list = pipe.GetStreamProfileList(SensorType.OB_SENSOR_COLOR);
                // Get color_profile
                var color_profile = profile_list.GetVideoStreamProfile(0, 0, Format.OB_FORMAT_ANY, 0);
                profile_list = pipe.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH);
                //Get depth_profile
                var depth_profile = profile_list.GetVideoStreamProfile(0, 0, Format.OB_FORMAT_ANY, 0);
                //Get D2C external parameters
                var extrinsicD2C = depth_profile.GetExtrinsicTo(color_profile);
                Console.WriteLine($"D2C extrinsic：\n  -[rot={string.Join(", ", extrinsicD2C.rot)}, trans={string.Join(", ", extrinsicD2C.trans)}]");
                //Get depth inernal parameters
                var depth_intrinsics = depth_profile.GetIntrinsic();
                Console.WriteLine($"depth_intrinsics：\n  -[cx={depth_intrinsics.cx}, cy={depth_intrinsics.cy}, fx={depth_intrinsics.fx}" +
                    $" fy={depth_intrinsics.fy}, width={depth_intrinsics.width}, height={depth_intrinsics.height}]");
                //Get depth distortion parameter
                var depth_distortion = depth_profile.GetDistortion();
                Console.WriteLine($"depth_distortion：\n  -[k1={depth_distortion.k1}, k2={depth_distortion.k2}, k3={depth_distortion.k3}, k4={depth_distortion.k4}" +
                    $" k5={depth_distortion.k5}, k6={depth_distortion.k6}, p1={depth_distortion.p1}, p2={depth_distortion.p2}]");
                //Get C2D external parameters
                var extrinsicC2D = color_profile.GetExtrinsicTo(depth_profile);
                Console.WriteLine($"C2D extrinsic：\n  -[rot={string.Join(", ", extrinsicC2D.rot)}, trans={string.Join(", ", extrinsicC2D.trans)}]");
                //Get color internala parameters
                var color_intrinsics = color_profile.GetIntrinsic();
                Console.WriteLine($"color_intrinsics：\n  -[cx={color_intrinsics.cx}, cy={color_intrinsics.cy}, fx={color_intrinsics.fx}" +
                    $" fy={color_intrinsics.fy}, width={color_intrinsics.width}, height={color_intrinsics.height}]");
                //Get color distortion parameter
                var color_distortion = color_profile.GetDistortion();
                Console.WriteLine($"color_distortion：\n  -[k1={color_distortion.k1}, k2={color_distortion.k2}, k3={color_distortion.k3}, k4={color_distortion.k4}" +
                    $" k5={color_distortion.k5}, k6={color_distortion.k6}, p1={color_distortion.p1}, p2={color_distortion.p2}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
            finally
            {
                pipe?.Stop();
                Console.WriteLine("CameraConfiguration sample exited.");
            }
        }
    }
}