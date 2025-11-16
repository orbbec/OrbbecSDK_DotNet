using OpenTK.Graphics.OpenGL4;
using GLPixelType = OpenTK.Graphics.OpenGL4.PixelType;
using Orbbec;

namespace Samples.Common
{
    public class VideoTexture : IDisposable
    {
        public int TextureId { get; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private readonly bool _hasAlpha;
        private bool _isInitialized = false;
        private readonly Queue<TextureData> _updateQueue = new();
        private readonly object _queueLock = new();

        public VideoTexture(bool hasAlpha = false)
        {
            _hasAlpha = hasAlpha;

            TextureId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, TextureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        }

        public void UpdateQueue(int width, int height, Format format, byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            lock (_queueLock)
            {
                byte[] finalData = format switch
                {
                    Format.OB_FORMAT_RGB => data,
                    Format.OB_FORMAT_Y8 => ImageUtils.Y8ToRgb(width, height, data),
                    Format.OB_FORMAT_Y16 => ImageUtils.Y16ToRgb(width, height, data),
                    _ => throw new NotSupportedException($"Format {format} is not supported")
                };

                var updateData = new TextureData
                {
                    Width = width,
                    Height = height,
                    Data = finalData
                };

                _updateQueue.Enqueue(updateData);

                while (_updateQueue.Count > 3)
                {
                    _updateQueue.Dequeue();
                }
            }
        }

        public void ProcessFrames()
        {
            lock (_queueLock)
            {
                if (_updateQueue.Count == 0)
                    return;

                TextureData latestUpdate = _updateQueue.Dequeue();

                while (_updateQueue.Count > 0)
                {
                    latestUpdate = _updateQueue.Dequeue();
                }

                UpdateTextureInternal(latestUpdate.Width, latestUpdate.Height, latestUpdate.Data);
            }
        }

        private void UpdateTextureInternal(int width, int height, byte[] data)
        {
            GL.BindTexture(TextureTarget.Texture2D, TextureId);

            if (!_isInitialized || width != Width || height != Height)
            {
                Width = width;
                Height = height;

                GL.TexImage2D(TextureTarget.Texture2D, 0,
                    _hasAlpha ? PixelInternalFormat.Rgba : PixelInternalFormat.Rgb,
                    Width, Height, 0,
                    _hasAlpha ? PixelFormat.Rgba : PixelFormat.Rgb,
                    GLPixelType.UnsignedByte, data);

                _isInitialized = true;

                Console.WriteLine($"Texture initialized/updated: {width}x{height}, ID: {TextureId}");
            }
            else
            {
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width, Height,
                    _hasAlpha ? PixelFormat.Rgba : PixelFormat.Rgb,
                    GLPixelType.UnsignedByte, data);
            }
        }

        public void Dispose()
        {
            if (TextureId != 0)
            {
                GL.DeleteTexture(TextureId);
                _isInitialized = false;
            }
        }

        private struct TextureData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[] Data { get; set; }
        }
    }
}