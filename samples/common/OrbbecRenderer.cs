using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using GLErrorCode = OpenTK.Graphics.OpenGL4.ErrorCode;
using OpenTK.Mathematics;
using Orbbec;
using System.Diagnostics;

namespace Samples.Common
{
    public class OrbbecRenderer : GameWindow
    {
        private int _program;
        private int _vao;
        private int _vbo;
        private readonly List<VideoTexture> _videoTextures = new List<VideoTexture>{};
        private readonly object _textureLock = new();

        private static readonly float[] _vertices = new float[]
        {
            -1f, 1f, 0f, 0f,
            -1f, -1f, 0f, 1f,
            1f, -1f, 1f, 1f,
            1f, 1f, 1f, 0f,
        };

        public OrbbecRenderer(int width = 1280, int height = 720, string title = "")
            : base(GameWindowSettings.Default,
                new NativeWindowSettings()
                {
                    ClientSize = (width, height),
                    Title = title
                })
        {
            VSync = VSyncMode.On;
        }

        public int AddVideoFrame(bool hasAlpha = false)
        {
            var tex = new VideoTexture(hasAlpha);
            lock (_textureLock)
            {
                _videoTextures.Add(tex);
                return _videoTextures.Count - 1;
            }
        }

        public void AddText(int width, int height, string text)
        {

        }

        public void UpdateVideoFrame(int index, int width, int height, Format format, byte[] data)
        {
            if (index < 0 || index >= _videoTextures.Count)
            {
                Console.WriteLine($"Invalid texture index: {index}, available: {_videoTextures.Count}");
                return;
            }

            _videoTextures[index].UpdateQueue(width, height, format, data);
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(Color4.DarkGray);
            CheckGLError("Clear color setup");

            string vert = @"
                #version 330 core
                layout(location = 0) in vec2 aPos;
                layout(location = 1) in vec2 aTex;
                out vec2 TexCoord;
                void main() {
                    gl_Position = vec4(aPos, 0.0, 1.0);
                    TexCoord = aTex;
                }
                ";

            string frag = @"
                #version 330 core
                out vec4 FragColor;
                in vec2 TexCoord;
                uniform sampler2D tex;
                void main() {
                    FragColor = texture(tex, TexCoord);
                }
                ";

            _program = CreateShaderProgram(vert, frag);
            if (_program == 0) return;

            _vao = GL.GenVertexArray();
            CheckGLError("Generate VAO");
            if (_vao == 0)
            {
                Console.WriteLine("Failed to generate VAO");
                return;
            }
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            CheckGLError("Generate VBO");
            if (_vbo == 0)
            {
                Console.WriteLine("Failed to generate VBO");
                return;
            }
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            if (KeyboardState.IsKeyDown(Keys.Escape))
                Close();

            lock (_textureLock)
            {
                foreach (var texture in _videoTextures)
                {
                    texture.ProcessFrames();
                }
            }
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit);
            CheckGLError("Clear buffer");

            RenderVideoGrid(_videoTextures, Size.X, Size.Y);
            SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Size.X, Size.Y);
        }

        protected override void OnUnload()
        {
            lock (_textureLock)
            {
                foreach (var texture in _videoTextures)
                {
                    texture.Dispose();
                }
                _videoTextures.Clear();
            }

            if (_program != 0)
            {
                GL.DeleteProgram(_program);
                _program = 0;
            }

            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }

            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }
            base.OnUnload();
        }

        private int CreateShaderProgram(string vertSrc, string fragSrc)
        {
            int vert = GL.CreateShader(ShaderType.VertexShader);
            CheckGLError("Create vertex shader");
            if (vert == 0)
            {
                Console.WriteLine("Failed to create vertex shader");
                return 0;
            }
            GL.ShaderSource(vert, vertSrc);
            GL.CompileShader(vert);

            int frag = GL.CreateShader(ShaderType.FragmentShader);
            CheckGLError("Create fragment shader");
            if (frag == 0)
            {
                Console.WriteLine("Failed to create fragment shader");
                GL.DeleteShader(vert);
                return 0;
            }
            GL.ShaderSource(frag, fragSrc);
            GL.CompileShader(frag);

            int prog = GL.CreateProgram();
            CheckGLError("Create shader program");
            if (prog == 0)
            {
                Console.WriteLine("Failed to create shader program");
                GL.DeleteShader(vert);
                GL.DeleteShader(frag);
                return 0;
            }
            GL.AttachShader(prog, vert);
            GL.AttachShader(prog, frag);
            GL.LinkProgram(prog);

            GL.DeleteShader(vert);
            GL.DeleteShader(frag);
            CheckGLError("Cleanup shaders");

            return prog;
        }

        private void RenderVideoGrid(List<VideoTexture> textures, int winWidth, int winHeight)
        {
            if (textures.Count == 0) return;

            GL.UseProgram(_program);
            GL.BindVertexArray(_vao);

            try
            {
                int textureCount = textures.Count;
                int cols = (int)Math.Ceiling(Math.Sqrt(textureCount));
                int rows = (int)Math.Ceiling(textureCount / (float)cols);

                float cellWidth = winWidth / (float)cols;
                float cellHeight = winHeight / (float)rows;

                for (int i = 0; i < textureCount; ++i)
                {
                    var tex = textures[i];
                    if (tex.Width <= 0 || tex.Height <= 0)
                        continue;

                    int videoWidth = tex.Width;
                    int videoHeight = tex.Height;

                    float scale = Math.Min(cellWidth / videoWidth, cellHeight / videoHeight);
                    float displayW = videoWidth * scale;
                    float displayH = videoHeight * scale;

                    int col = i % cols;
                    int row = i / cols;

                    float offsetX = col * cellWidth + (cellWidth - displayW) / 2f;
                    float offsetY = winHeight - (row + 1) * cellHeight + (cellHeight - displayH) / 2f;

                    GL.Viewport((int)offsetX, (int)offsetY, (int)displayW, (int)displayH);

                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, tex.TextureId);
                    GL.Uniform1(GL.GetUniformLocation(_program, "tex"), 0);

                    GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
                    CheckGLError($"Draw call for texture {i}");
                }

            }
            finally
            {
                GL.BindVertexArray(0);
            }
        }

        [Conditional("DEBUG")]
        private void CheckGLError(string context)
        {
            GLErrorCode error = GL.GetError();
            if (error != GLErrorCode.NoError)
            {
                Console.WriteLine($"OpenGL Error in {context}: {error}");
            }
        }
    }
}