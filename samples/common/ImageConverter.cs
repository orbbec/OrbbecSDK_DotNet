namespace Samples.Common
{
    public class ImageUtils
    {
        public static byte[] Y8ToRgb(int width, int height, ReadOnlySpan<byte> y8Data)
        {
            if (y8Data == null)
                throw new ArgumentException("Y8 data is null");

            int pixelCount = width * height;
            int rgbSize = pixelCount * 3;

            if (y8Data.Length < pixelCount)
                throw new ArgumentException("Invalid Y8 data length");

            byte[] rgbData = new byte[rgbSize];

            for (int i = 0, j = 0; i < pixelCount; ++i, j += 3)
            {
                byte y = y8Data[i];
                rgbData[j] = y;
                rgbData[j + 1] = y;
                rgbData[j + 2] = y;
            }

            return rgbData;
        }

        public static byte[] Y16ToRgb(int width, int height, ReadOnlySpan<byte> y16Data, bool normalize = false)
        {
            if (y16Data == null)
                throw new ArgumentException("Y16 data is null");

            int pixelCount = width * height;
            int rgbSize = pixelCount * 3;

            if (y16Data.Length < pixelCount * 2)
                throw new ArgumentException("Invalid Y16 data length");

            byte[] rgbData = new byte[rgbSize];

            if (normalize)
            {
                for (int i = 0, j = 0; i < pixelCount; ++i, j += 3)
                {
                    ushort y = (ushort)(y16Data[i * 2] | (y16Data[i * 2 + 1] << 8));
                    byte value = (byte)(y >> 8);
                    rgbData[j] = value;
                    rgbData[j + 1] = value;
                    rgbData[j + 2] = value;
                }
            }
            else
            {
                for (int i = 0, j = 0; i < pixelCount; ++i, j += 3)
                {
                    byte value = y16Data[i * 2];
                    rgbData[j] = value;
                    rgbData[j + 1] = value;
                    rgbData[j + 2] = value;
                }
            }

            return rgbData;
        }

        public static byte[] DepthAlignToColor(int colorW, int colorH, ReadOnlySpan<byte> colorData,
            int depthW, int depthH, ReadOnlySpan<byte> depthData, float alpha)
        {
            if (colorData.Length == 0 || depthData.Length == 0)
                throw new ArgumentException("Data is null");

            if (alpha <= 0)
                return colorData.ToArray();
            else if (alpha > 1)
                return depthData.ToArray();

            float scaleW = (float)colorW / depthW;
            float scaleH = (float)colorH / depthH;

            int colorDataSize = colorW * colorH * 3;
            int depthDataSize = depthW * depthH * 3;

            byte[] output = new byte[colorData.Length];
            colorData.CopyTo(output);

            int colorIndex, depthIndex;
            int srcX, srcY;

            for (int j = 0; j < colorH; ++j)
            {
                for (int i = 0; i < colorW; ++i)
                {
                    srcX = (int)(i / scaleW);
                    srcY = (int)(j / scaleH);

                    colorIndex = (j * colorW + i) * 3;
                    depthIndex = (srcY * depthW + srcX) * 3;

                    if (depthIndex + 3 > depthDataSize || colorIndex + 3 > colorDataSize)
                        continue;

                    byte d0 = depthData[depthIndex];
                    byte d1 = depthData[depthIndex + 1];
                    byte d2 = depthData[depthIndex + 2];

                    if (d0 == 0 && d1 == 0 && d2 == 0)
                        continue;

                    output[colorIndex] = (byte)(output[colorIndex] * (1.0f - alpha) + d0 * alpha);
                    output[colorIndex + 1] = (byte)(output[colorIndex + 1] * (1.0f - alpha) + d1 * alpha);
                    output[colorIndex + 2] = (byte)(output[colorIndex + 2] * (1.0f - alpha) + d2 * alpha);
                }
            }

            return output;
        }

        public static byte[] DepthAlignToColor(ReadOnlySpan<byte> colorData, ReadOnlySpan<byte> depthData, float alpha)
        {
            if (colorData.Length == 0 || depthData.Length == 0)
                throw new ArgumentException("Data is null");

            alpha = Math.Clamp(alpha, 0f, 1f);
            if (alpha <= 0f) return colorData.ToArray();
            if (alpha >= 1f) return depthData.ToArray();

            byte[] output = new byte[colorData.Length];
            colorData.CopyTo(output);
            float ialpha = 1f - alpha;
            for (int i = 0; i < output.Length; i++)
            {
                byte d = depthData[i];
                if (d != 0)
                    output[i] = (byte)(output[i] * ialpha + d * alpha);
            }
            return output;
        }
    }
}