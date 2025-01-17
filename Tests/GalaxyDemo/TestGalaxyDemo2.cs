﻿/*
 * Copyright 2019-2021 Robbyxp1 @ github.com
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 */
using GLOFC;
using GLOFC.Controller;
using GLOFC.GL4;
using GLOFC.GL4.Shaders;
using GLOFC.GL4.Shaders.Vertex;
using GLOFC.GL4.Shaders.Basic;
using GLOFC.GL4.Shaders.Compute;
using GLOFC.GL4.Shaders.Fragment;
using GLOFC.Utils;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Drawing;
using System.Windows.Forms;
using GLOFC.GL4.Shaders.Sprites;
using GLOFC.GL4.ShapeFactory;
using GLOFC.GL4.Textures;

namespace TestOpenTk
{
    public partial class TestGalaxyDemo2 : Form
    {
        private GLOFC.WinForm.GLWinFormControl glwfc;
        private Controller3D gl3dcontroller;

        private Timer systemtimer = new Timer();

        public TestGalaxyDemo2()
        {
            InitializeComponent();

            glwfc = new GLOFC.WinForm.GLWinFormControl(glControlContainer,null,4,6);

        }

        GLRenderProgramSortedList rObjects = new GLRenderProgramSortedList();
        GLItemsList items = new GLItemsList();
        Vector4[] boundingbox;
        GLVolumetricUniformBlock volumetricblock;
        GLRenderableItem galaxy;
        GLTexture2DArray gridtexcoords;
        float lasteyedistance = 100000000;
        int lastgridwidth;

        /// ////////////////////////////////////////////////////////////////////////////////////////////////////


        private void ShaderTest_Closed(object sender, EventArgs e)
        {
            items.Dispose();
        }

        public class GLFixedShader : GLShaderPipeline
        {
            public GLFixedShader(Color c, Action<IGLProgramShader, GLMatrixCalc> action = null) : base(action)
            {
                AddVertexFragment(new GLPLVertexShaderWorldCoord(), new GLPLFragmentShaderFixedColor(c));
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Closed += ShaderTest_Closed;

            gl3dcontroller = new Controller3D();
            gl3dcontroller.MatrixCalc.PerspectiveNearZDistance = 1f;
            gl3dcontroller.MatrixCalc.PerspectiveFarZDistance = 100000f;
            gl3dcontroller.ZoomDistance = 5000F;
            gl3dcontroller.PosCamera.ZoomMin = 0.1f;
            gl3dcontroller.PosCamera.ZoomScaling = 1.1f;
            gl3dcontroller.YHoldMovement = true;
            gl3dcontroller.PaintObjects = ControllerDraw;

            gl3dcontroller.KeyboardTravelSpeed = (ms,eyedist) =>
            {
                return (float)ms * 1.0f * Math.Min(eyedist / 1000, 10);
            };

            gl3dcontroller.MatrixCalc.InPerspectiveMode = true;
            gl3dcontroller.Start(glwfc, new Vector3(0, 0, 10000), new Vector3(140.75f, 0, 0), 0.5F);

            items.Add( new GLMatrixCalcUniformBlock(), "MCUB");     // create a matrix uniform block 

            int front = -20000, back = front + 90000, left = -45000, right = left + 90000, vsize = 2000;
            boundingbox = new Vector4[]
            {
                new Vector4(left,-vsize,front,1),
                new Vector4(left,vsize,front,1),
                new Vector4(right,vsize,front,1),
                new Vector4(right,-vsize,front,1),

                new Vector4(left,-vsize,back,1),
                new Vector4(left,vsize,back,1),
                new Vector4(right,vsize,back,1),
                new Vector4(right,-vsize,back,1),
            };

            Vector4[] displaylines = new Vector4[]
            {
                new Vector4(left,-vsize,front,1),   new Vector4(left,+vsize,front,1),
                new Vector4(left,+vsize,front,1),      new Vector4(right,+vsize,front,1),
                new Vector4(right,+vsize,front,1),     new Vector4(right,-vsize,front,1),
                new Vector4(right,-vsize,front,1),  new Vector4(left,-vsize,front,1),

                new Vector4(left,-vsize,back,1),    new Vector4(left,+vsize,back,1),
                new Vector4(left,+vsize,back,1),       new Vector4(right,+vsize,back,1),
                new Vector4(right,+vsize,back,1),      new Vector4(right,-vsize,back,1),
                new Vector4(right,-vsize,back,1),   new Vector4(left,-vsize,back,1),

                new Vector4(left,-vsize,front,1),   new Vector4(left,-vsize,back,1),
                new Vector4(left,+vsize,front,1),      new Vector4(left,+vsize,back,1),
                new Vector4(right,-vsize,front,1),  new Vector4(right,-vsize,back,1),
                new Vector4(right,+vsize,front,1),     new Vector4(right,+vsize,back,1),
            };

            {
                items.Add( new GLFixedShader(System.Drawing.Color.Yellow), "LINEYELLOW");
                GLRenderState rl = GLRenderState.Lines();
                rObjects.Add(items.Shader("LINEYELLOW"), GLRenderableItem.CreateVector4(items, PrimitiveType.Lines, rl, displaylines));
            }


            {
                items.Add(new GLTexture2D(Properties.Resources.golden, SizedInternalFormat.Rgba8), "solmarker");
                items.Add(new GLTexturedShaderObjectTranslation(), "TEX");
                GLRenderState rq = GLRenderState.Tri(cullface: false);
                rObjects.Add(items.Shader("TEX"),
                             GLRenderableItem.CreateVector4Vector2(items, PrimitiveType.TriangleStrip, rq,
                             GLShapeObjectFactory.CreateQuadTriStrip(1000.0f, 1000.0f, new Vector3(0, 0, 0)), GLShapeObjectFactory.TexTriStripQuad,
                             new GLRenderDataTranslationRotationTexture(items.Tex("solmarker"), new Vector3(0, 1000, 0))
                             ));
                rObjects.Add(items.Shader("TEX"),
                             GLRenderableItem.CreateVector4Vector2(items, PrimitiveType.TriangleStrip, rq,
                             GLShapeObjectFactory.CreateQuadTriStrip(1000.0f, 1000.0f, new Vector3(0, 0, 0)), GLShapeObjectFactory.TexTriStripQuad,
                             new GLRenderDataTranslationRotationTexture(items.Tex("solmarker"), new Vector3(0, -1000, 0))
                             ));
                items.Add(new GLTexture2D(Properties.Resources.dotted, SizedInternalFormat.Rgba8), "sag");
                rObjects.Add(items.Shader("TEX"),
                             GLRenderableItem.CreateVector4Vector2(items, PrimitiveType.TriangleStrip, rq,
                             GLShapeObjectFactory.CreateQuadTriStrip(1000.0f, 1000.0f, new Vector3(0, 0, 0)), GLShapeObjectFactory.TexTriStripQuad,
                             new GLRenderDataTranslationRotationTexture(items.Tex("sag"), new Vector3(25.2f, 2000, 25899))
                             ));
                rObjects.Add(items.Shader("TEX"),
                             GLRenderableItem.CreateVector4Vector2(items, PrimitiveType.TriangleStrip, rq,
                             GLShapeObjectFactory.CreateQuadTriStrip(1000.0f, 1000.0f, new Vector3(0, 0, 0)), GLShapeObjectFactory.TexTriStripQuad,
                             new GLRenderDataTranslationRotationTexture(items.Tex("sag"), new Vector3(25.2f, -2000, 25899))
                             ));
                items.Add(new GLTexture2D(Properties.Resources.dotted2, SizedInternalFormat.Rgba8), "bp");
                rObjects.Add(items.Shader("TEX"),
                             GLRenderableItem.CreateVector4Vector2(items, PrimitiveType.TriangleStrip, rq,
                             GLShapeObjectFactory.CreateQuadTriStrip(1000.0f, 1000.0f, new Vector3(0, 0, 0)), GLShapeObjectFactory.TexTriStripQuad,
                             new GLRenderDataTranslationRotationTexture(items.Tex("bp"), new Vector3(-1111f, 0, 65269))
                             ));

            }

            if (true) // galaxy
            {
                volumetricblock = new GLVolumetricUniformBlock();
                items.Add(volumetricblock, "VB");

                int sc = 1;
                GLTexture3D noise3d = new GLTexture3D(1024 * sc, 64 * sc, 1024 * sc, OpenTK.Graphics.OpenGL4.SizedInternalFormat.R32f); // red channel only
                items.Add(noise3d, "Noise");
                ComputeShaderNoise3D csn = new ComputeShaderNoise3D(noise3d.Width, noise3d.Height, noise3d.Depth, 128 * sc, 16 * sc, 128 * sc);       // must be a multiple of localgroupsize in csn
                csn.StartAction += (A,m) => { noise3d.BindImage(3); };
                csn.Run();      // compute noise

                GLTexture1D gaussiantex = new GLTexture1D(1024, OpenTK.Graphics.OpenGL4.SizedInternalFormat.R32f); // red channel only
                items.Add(gaussiantex, "Gaussian");

                // set centre=width, higher widths means more curve, higher std dev compensate.
                // fill the gaussiantex with data
                ComputeShaderGaussian gsn = new ComputeShaderGaussian(gaussiantex.Width, 2.0f, 2.0f, 1.4f, 4);
                gsn.StartAction += (A,m) => { gaussiantex.BindImage(4); };
                gsn.Run();      // compute noise

                GL.MemoryBarrier(MemoryBarrierFlags.AllBarrierBits);

                // load one upside down and horz flipped, because the volumetric co-ords are 0,0,0 bottom left, 1,1,1 top right
                GLTexture2D galtex = new GLTexture2D(Properties.Resources.Galaxy_L180, SizedInternalFormat.Rgba8);
                items.Add(galtex, "gal");
                GalaxyShader gs = new GalaxyShader();
                items.Add(gs, "Galaxy");
                // bind the galaxy texture, the 3dnoise, and the gaussian 1-d texture for the shader
                gs.StartAction = (a,m) => { galtex.Bind(1); noise3d.Bind(3); gaussiantex.Bind(4); };      // shader requires these, so bind using shader

                GLRenderState rt = GLRenderState.Tri();
                galaxy = GLRenderableItem.CreateNullVertex(PrimitiveType.Points, rt);   // no vertexes, all data from bound volumetric uniform, no instances as yet
                rObjects.Add(items.Shader("Galaxy"), galaxy);
            }

            if (true) // star points
            {
                int gran = 8;
                Bitmap img = Properties.Resources.Galaxy_L180;
                Bitmap heat = img.Function(img.Width / gran, img.Height / gran, mode: GLOFC.Utils.BitMapHelpers.BitmapFunction.HeatMap);
                heat.Save(@"c:\code\heatmap.jpg", System.Drawing.Imaging.ImageFormat.Jpeg);

                Random rnd = new Random(23);

                GLBuffer buf = new GLBuffer(16 * 500000);     // since RND is fixed, should get the same number every time.
                buf.StartWrite(0); // get a ptr to the whole schebang

                int xcw = (right - left) / heat.Width;
                int zch = (back - front) / heat.Height;

                int points = 0;

                for (int x = 0; x < heat.Width; x++)
                {
                    for (int z = 0; z < heat.Height; z++)
                    {
                        int i = heat.GetPixel(x, z).R;
                        int ii = i * i * i;
                        if (ii > 32 * 32 * 32)
                        {
                            int gx = left + x * xcw;
                            int gz = front + z * zch;

                            float dx = (float)Math.Abs(gx) / 45000;
                            float dz = (float)Math.Abs(25889 - gz) / 45000;
                            double d = Math.Sqrt(dx * dx + dz * dz);     // 0 - 0.1412
                            d = 1 - d;  // 1 = centre, 0 = unit circle
                            d = d * 2 - 1;  // -1 to +1
                            double dist = ObjectExtensionsNumbersBool.GaussianDist(d, 1, 1.4);

                            int c = Math.Min(Math.Max(ii / 140000, 0), 20);

                            dist *= 2000;

                            GLPointsFactory.RandomStars4(buf, c, gx, gx + xcw, gz, gz + zch, (int)dist, (int)-dist, rnd, w: i);
                            points += c;
                            System.Diagnostics.Debug.Assert(points < buf.Length / 16);
                        }
                    }
                }

                buf.StopReadWrite();

                items.Add(new GalaxyStarDots(), "SD");
                GLRenderState rp = GLRenderState.Points(1);
                rp.DepthTest = false;
                rObjects.Add(items.Shader("SD"), GLRenderableItem.CreateVector4(items, PrimitiveType.Points, rp, buf, points));
                System.Diagnostics.Debug.WriteLine("Stars " + points);
            }

            if (true)  // point sprite
            {
                items.Add(new GLTexture2D(Properties.Resources.star_grey64, SizedInternalFormat.Rgba8), "lensflare");
                items.Add(new GLPointSpriteShader(items.Tex("lensflare")), "PS1");
                int dist = 20000;
                var p = GLPointsFactory.RandomStars4(100, -dist, dist, 25899 - dist, 25899 + dist, 2000, -2000);

                GLRenderState rps = GLRenderState.PointsByProgram();
                rps.DepthTest = false;

                rObjects.Add(items.Shader("PS1"), GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Points, rps, p, new Color4[] { Color.White }));

            }

            if (true)
            {
                items.Add(new DynamicGridVertexShader(Color.Cyan), "PLGRIDVertShader");
                items.Add(new GLPLFragmentShaderVSColor(), "PLGRIDFragShader");

                GLRenderState rl = GLRenderState.Lines();
                rl.DepthTest = false;

                items.Add(new GLShaderPipeline(items.PLShader("PLGRIDVertShader"), items.PLShader("PLGRIDFragShader")), "DYNGRID");
                rObjects.Add(items.Shader("DYNGRID"), "DYNGRIDRENDER", GLRenderableItem.CreateNullVertex(PrimitiveType.Lines, rl, drawcount: 2));

            }

            if (true)
            {
                items.Add(new DynamicGridCoordVertexShader(), "PLGRIDBitmapVertShader");
                items.Add(new GLPLFragmentShaderTexture2DIndexed(0), "PLGRIDBitmapFragShader");     // binding

                GLRenderState rl = GLRenderState.Tri(cullface: false);
                rl.DepthTest = false;

                gridtexcoords = new GLTexture2DArray();
                items.Add(gridtexcoords, "PLGridBitmapTextures");

                GLShaderPipeline sp = new GLShaderPipeline(items.PLShader("PLGRIDBitmapVertShader"), items.PLShader("PLGRIDBitmapFragShader"));

                items.Add(sp, "DYNGRIDBitmap");

                rObjects.Add(items.Shader("DYNGRIDBitmap"), "DYNGRIDBitmapRENDER", GLRenderableItem.CreateNullVertex(PrimitiveType.TriangleStrip, rl, drawcount: 4, instancecount: 9));
            }

            systemtimer.Interval = 25;
            systemtimer.Tick += new EventHandler(SystemTick);
            systemtimer.Start();
        }

        private void ControllerDraw(Controller3D c3d, ulong unused)
        {
            ((GLMatrixCalcUniformBlock)items.UB("MCUB")).Set(gl3dcontroller.MatrixCalc);        // set the matrix unform block to the controller 3d matrix calc.

            IGLRenderableItem i = rObjects["DYNGRIDRENDER"];
            DynamicGridVertexShader s = items.PLShader("PLGRIDVertShader") as DynamicGridVertexShader;

            if (Math.Abs(lasteyedistance - gl3dcontroller.MatrixCalc.EyeDistance) > 10)     // a little histerisis
            {
                i.InstanceCount = s.ComputeGridSize(gl3dcontroller.MatrixCalc.EyeDistance, out lastgridwidth);
                lasteyedistance = gl3dcontroller.MatrixCalc.EyeDistance;
            }

            s.SetUniforms(gl3dcontroller.MatrixCalc.LookAt, lastgridwidth, i.InstanceCount);

            float dist = c3d.MatrixCalc.EyeDistance;
            float d1 = dist - lastgridwidth;
            float suc = d1 / (9.0f * lastgridwidth);
            float cf = 1.0f - suc.Clamp(0f, 1f);
            float a = 0.7f * cf;

            float coordfade = lastgridwidth == 10000 ? (0.7f - (c3d.MatrixCalc.EyeDistance / 20000).Clamp(0.0f, 0.7f)) : 0.7f;
            Color coordscol = Color.FromArgb(coordfade < 0.05 ? 0 : 150, Color.Cyan);

            System.Diagnostics.Debug.WriteLine("Dist {0} grid {1} suc {2} cf {3} a {4} coord {5} {6}", dist, lastgridwidth, suc, cf, a, coordfade, coordscol);

            DynamicGridCoordVertexShader bs = items.PLShader("PLGRIDBitmapVertShader") as DynamicGridCoordVertexShader;
            bs.ComputeUniforms(lastgridwidth, gl3dcontroller.MatrixCalc, gl3dcontroller.PosCamera.CameraDirection, coordscol, Color.Transparent);

            if (galaxy != null)
            {
                galaxy.InstanceCount = volumetricblock.Set(gl3dcontroller.MatrixCalc, boundingbox, 50.0f);        // set up the volumentric uniform

                IGLProgramShader p = items.Shader("Galaxy");
                var fsgalaxy = p.GetShader(OpenTK.Graphics.OpenGL4.ShaderType.FragmentShader) as GalaxyFragmentPipeline;
                fsgalaxy.SetFader(c3d.MatrixCalc.EyeDistance);
            }

            rObjects.Render(glwfc.RenderState, gl3dcontroller.MatrixCalc);


            this.Text = "Looking at " + gl3dcontroller.MatrixCalc.LookAt + " eye@ " + gl3dcontroller.MatrixCalc.EyePosition + " dir " + gl3dcontroller.PosCamera.CameraDirection + " Dist " + gl3dcontroller.MatrixCalc.EyeDistance + " Zoom " + gl3dcontroller.PosCamera.ZoomFactor;
        }
        private void SystemTick(object sender, EventArgs e)
        {
            var cdmt = gl3dcontroller.HandleKeyboardSlewsAndInvalidateIfMoved(true, OtherKeys);
        }

        private void OtherKeys(GLOFC.Controller.KeyboardMonitor kb)
        {
            if (kb.HasBeenPressed(Keys.F5, GLOFC.Controller.KeyboardMonitor.ShiftState.None))
            {
                IGLProgramShader ps = items.Shader("Galaxy");
                if (ps != null)
                {
                    ps.Enable = !ps.Enable;
                    glwfc.Invalidate();
                }
            }
            if (kb.HasBeenPressed(Keys.F6, GLOFC.Controller.KeyboardMonitor.ShiftState.None))
            {
                IGLProgramShader ps = items.Shader("SD");
                if (ps != null)
                {
                    ps.Enable = !ps.Enable;
                    glwfc.Invalidate();
                }
            }
        }

    }
}


