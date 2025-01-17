﻿/*
 * Copyright 2015 - 2019 EDDiscovery development team
 * Copyright 2020 Robbyxp1 @ github.com
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
 * 
 * 
 */

using GLOFC.Utils;
using OpenTK;
using System;
using System.Drawing;

namespace GLOFC
{
    // GL           World Space                         View Space                                  Clip Space
    //           p5----------p6		                  p5----------p6                     Zfar     p5----------p6 1 = far clip
    //          /|           /|                      /|           /|                             /|           /|
    //         / |          / | 	                / |          / |                            / |          / | 
    //        /  |         /  |                    /  |         /  |                           /  |         /  |
    //       /   p4-------/--p7  Z++              /   p4-------/--p7	                      /   p4-------/--p7	
    //      /   /        /   /                   /   /        /   /                          /   / +1     /   /
    //     p1----------p2   /	x ModelView     p1----------p2   /	    x Projection        p1----------p2   /	
    //     |  /         |  /                    |  /         |  /                           |  /         |  /  
    //     | /          | /			            | /          | /		                 -1 | /          | / +1		
    //     |/	        |/                      |/	         |/                             |/	         |/
    //     p0----------p3			            p0----------p3	  Viewer Pos      ZNear     p0----------p3  0 = near clip
    //     p0-p7 are in world coords                                                               -1
    // https://learnopengl.com/Getting-started/Coordinate-Systems

    // GL           GL Window                          View Port                                 Screen Coords Area
    //                                   Viewport in GL window coord, 0,0 top left  ScreenCoordClipSpace/Offset defines the area
    //    ----------------------------       ----------------------------               -----------------------------------
    //    |0,0                       |       |Defined in screen coords  |               |                                 |
    //    |                          |       |    -------------------   |               |  -----------------------------  |
    //    |                          |       |    |       +1        |   |               |  |Viewport                   |  |
    //    |                          |       |    |                 |   |               |  |    ====================== |  |
    //    |                          |       |    |                 |   |               |  |    |Screen Coord Area   | |  |
    //    |                          |       |    | -1 Clip Space +1|   |               |  |    |ScreenCoordMax      | |  |
    //    |                          |       |    |                 |   |               |  |    |Defines the logical | |  |
    //    |                          |       |    |                 |   |               |  |    |size of this        | |  |
    //    |                          |       |    |                 |   |               |  |    ====================== |  |
    //    |                          |       |    |       -1        |   |               |  |                           |  |
    //    |                          |       |    -------------------   |               |  -----------------------------  |
    //    ----------------------------       ----------------------------               -----------------------------------

    /// <summary>
    /// This class holds and computes the model and projection matrices, and the screen co-ord matrix
    /// </summary>

    public class GLMatrixCalc
    {
        /// <summary>Perspective mode </summary>
        public bool InPerspectiveMode { get; set; } = true;

        /// <summary> Model maximum z (corresponding to GL viewport Z=1)</summary>
        public float PerspectiveFarZDistance { get; set; } = 100000.0f;
        // 
        /// <summary> Model minimum z (corresponding to GL viewport Z=0) 
        /// Don't set this too small othersize depth testing starts going wrong as you exceed the depth buffer resolution
        /// </summary>
        public float PerspectiveNearZDistance { get; set; } = 1f;

        /// <summary> Orthographic, give scale</summary>
        public float OrthographicDistance { get; set; } = 5000.0f;

        /// <summary> Field of view, radians, in perspective mode</summary>
        public float Fov { get; set; } = (float)(Math.PI / 2.0f);              
        /// <summary> Fov in degrees </summary>
        public float FovDeg { get { return Fov.Degrees(); } }
        /// <summary> Fov Scaling factor </summary>
        public float FovFactor { get; set; } = 1.258925F;

        /// <summary> Screen size, total, of GL window. </summary>
        public Size ScreenSize { get; protected set; }

        /// <summary> Area of window GL is drawing to - note 0,0 is top left, not the GL method of bottom left </summary>
        public Rectangle ViewPort { get; protected set; }
        /// <summary> Depth range (near,far) of Z to apply to viewport transformation to screen coords from normalised device coords (0..1)</summary>
        public Vector2 DepthRange { get; set; } = new Vector2(0, 1);            

        /// <summary> Screen co-ords max. Does not have to match the screensize. If not, you get a fixed scalable area</summary>
        public Size ScreenCoordMax { get; set; }

        /// <summary> Clip space size to use for screen coords, override to set up another</summary>
        public virtual SizeF ScreenCoordClipSpaceSize { get; set; } = new SizeF(2, 2);
        /// <summary> Origin in clip space (-1,1) = top left, screen coord (0,0)</summary>
        public virtual PointF ScreenCoordClipSpaceOffset { get; set; } = new PointF(-1, 1);

        /// <summary> Eye Position (after CalculateModelMatrix)</summary>
        public Vector3 EyePosition { get; private set; }
        /// <summary> Look At Position (after CalculateModelMatrix)</summary>
        public Vector3 LookAt { get; private set; }
        /// <summary> Eye Distance (after CalculateModelMatrix)</summary>
        public float EyeDistance { get; private set; }
        /// <summary> Model Matrix (after CalculateModelMatrix)</summary>
        public Matrix4 ModelMatrix { get; private set; }
        /// <summary> Projection Position (after CalculateProjectionMatrix)</summary>
        public Matrix4 ProjectionMatrix { get; private set; }
        /// <summary> ModelProjection Position (after CalculateModelMatrix)</summary>
        public Matrix4 ProjectionModelMatrix { get; private set; }
        /// <summary> Res Mat. Used for calculating positions on the screen from pixel positions.  Remembering Apollo </summary>
        public Matrix4 GetResMat { get { return ProjectionModelMatrix; } }
        /// <summary>Projection Z Near (after CalculateProjectionMatrix) </summary>
        public float ZNear { get; private set; }

        /// <summary> Incremented when Model or Project matrix changed</summary>
        public int CountMatrixCalcs { get; private set;  } = 0;

        /// <summary> 
        /// OpenGL operates a +X to right, +Y upwards, and +Z towards the viewer.
        /// You may want to operate a +X to right, +Y upwards, and +Z away from viewer.  The majority of OFC is in this mode.
        /// Setting this to true enables this. 
        /// Note the winding of surfaces will be affected. The winding of surfaces will need changing or changing the FrontFace mode will be required. 
        /// This should be set at startup. Changing it requires new Model and Project matrix calculations
        /// </summary>
       
        public bool ModelAxisPositiveZAwayFromViewer { get; set; } = true;      

        /// <summary> Construct </summary>
        public GLMatrixCalc()
        {
            context = GLStatics.GetContext();
        }

        /// <summary>
        /// Calculate the model matrix, which is the model translated to world space then to view space, using a lookat, camera direction and distance
        /// </summary>
        /// <param name="lookat">The lookat position </param>
        /// <param name="cameradirection">The camera direction in degrees.
        /// * camera.x rotates around the x axis (elevation) and camera.y rotates around the y axis (aziumth)
        /// * Elevation: 0 is straignt up, 180 is straight down, 90 is level
        /// * Azimuth in opengl +Z towards the viewer: 180 is straight forward, 0 is straight back
        /// * Azimuth in +Z away from the viewer: 0 is straight forward, 180 is straight back
        /// </param>
        /// <param name="distance">Distance between eye and lookat</param>
        /// <param name="camerarotation">Camera rotation in degrees. 0 is upright</param>
        public void CalculateModelMatrix(Vector3 lookat, Vector2 cameradirection, float distance, float camerarotation)       
        {
            Vector3 eyeposition = lookat.CalculateEyePositionFromLookat(cameradirection, distance);
            CalculateModelMatrix(lookat, eyeposition,  camerarotation);
        }

        /// <summary>
        /// Calculate the model matrix, which is the model translated to world space then to view space, using a lookat, camera direction and distance
        /// </summary>
        /// <param name="lookat">The lookat position </param>
        /// <param name="eyeposition">Where the eye is </param>
        /// <param name="camerarotation">Camera rotation in degrees. 0 is upright</param>
        public void CalculateModelMatrix(Vector3 lookat, Vector3 eyeposition, float camerarotation)
        {
            //System.Diagnostics.Debug.WriteLine($"Calculate model matrix lookat {lookat} from {eyeposition} rot {camerarotation} ");

            LookAt = lookat;      // record for shader use
            EyePosition = eyeposition;
            EyeDistance = (lookat - eyeposition).Length;

            // 18/1/22 verified in opengl/+Z mode in both perspective and ortho mode

            if (InPerspectiveMode)
            {
                // In opengl mode, the camera normal is (0,1,0) and there is no perspective Y flip.
                // in +Z away mode, the camera normal is (0,-1,0) to flip the axis, then we flip the perspective Y to compensate. This rotates the system so +Z is away

                // we set the up vector, (0,1,0) for opengl mode, (0,-1,0) for +Z away mode (the perspective flips it back)

                Vector3 camnormal = new Vector3(0, ModelAxisPositiveZAwayFromViewer ? -1 : 1, 0);
                
                if ( camerarotation!=0)     // if we have camera rotation, then rotate the up vector
                {
                    Matrix3 rot = Matrix3.CreateRotationZ((float)camerarotation.Radians());
                    camnormal = Vector3.Transform(camnormal, rot);
                }

                ModelMatrix = Matrix4.LookAt(EyePosition, LookAt, camnormal);
            }
            else
            {
                Size scr = ViewPort.Size;
                float orthoheight = (OrthographicDistance / 5.0f) * scr.Height / scr.Width;  // this comes from the projection calculation, and allows us to work out the scale factor the eye vs lookat has

                Matrix4 scale = Matrix4.CreateScale(orthoheight/EyeDistance);    // create a scale based on eyedistance compensated for by the orth projection scaling

                Matrix4 mat = Matrix4.CreateTranslation(-lookat.X, 0, -lookat.Z);        // we offset by the negative of the position to give the central look
                mat = Matrix4.Mult(mat, scale);          // translation world->View = scale + offset

                Matrix4 rotcam = Matrix4.CreateRotationX((float)((90) * Math.PI / 180.0f));        // flip 90 along the x axis to give the top down view
                ModelMatrix = Matrix4.Mult(mat, rotcam);
            }

            //System.Diagnostics.Debug.WriteLine("MM\r\n{0}", ModelMatrix);

            ProjectionModelMatrix = Matrix4.Mult(ModelMatrix, ProjectionMatrix);        // order order order ! so important.
            CountMatrixCalcs++;
        }

        /// <summary>Calculate the Projection matrix - projects the 3d model space to the 2D screen</summary> 
        public void CalculateModelMatrix(Vector3d lookatd, Vector3d eyepositiond, double camerarotation)
        {
            LookAt = new Vector3((float)lookatd.X, (float)lookatd.Y, (float)lookatd.Z);      // record for shader use
            EyePosition = new Vector3((float)eyepositiond.X, (float)eyepositiond.Y, (float)eyepositiond.Z);
            EyeDistance = (float)((lookatd - eyepositiond).Length);

            //  System.Diagnostics.Debug.WriteLine($"CMM {lookat} {eyeposition} dist {EyeDistance} {cameradirection} {camerarotation}");

            Matrix4d mat;

            if (InPerspectiveMode)
            {
                Vector3d camnormal = new Vector3d(0, ModelAxisPositiveZAwayFromViewer ? -1 : 1, 0);

                if (camerarotation != 0)     // if we have camera rotation, then rotate the up vector
                {
                    Matrix4d rot = Matrix4d.CreateRotationZ((float)camerarotation.Radians());
                    camnormal = Vector3d.Transform(camnormal, rot);
                }

                mat = Matrix4d.LookAt(eyepositiond, lookatd, camnormal);   // from eye, look at target, with normal giving the rotation of the look
            }
            else
            {
                Size scr = ViewPort.Size;
                double orthoheight = (OrthographicDistance / 5.0f) * scr.Height / scr.Width;  // this comes from the projection calculation, and allows us to work out the scale factor the eye vs lookat has
                double scaler = orthoheight / EyeDistance;

                // no create scale, do it manually.
                Matrix4d scale = new Matrix4d(new Vector4d(scaler, 0, 0, 0), new Vector4d(0, scaler, 0, 0),new Vector4d(0, 0, scaler, 0),new Vector4d(0, 0, 0, 1));

                mat = Matrix4d.CreateTranslation(-lookatd.X, 0, -lookatd.Z);        // we offset by the negative of the position to give the central look
                mat = Matrix4d.Mult(mat, scale);          // translation world->View = scale + offset

                Matrix4d rotcam = Matrix4d.CreateRotationX(90.0 * Math.PI / 180.0f);        // flip 90 along the x axis to give the top down view
                mat = Matrix4d.Mult(mat, rotcam);
            }

            // and turn it to float model matrix
            ModelMatrix = new Matrix4((float)mat.M11, (float)mat.M12, (float)mat.M13, (float)mat.M14, (float)mat.M21, (float)mat.M22, (float)mat.M23, (float)mat.M24, (float)mat.M31, (float)mat.M32, (float)mat.M33, (float)mat.M34, (float)mat.M41, (float)mat.M42, (float)mat.M43, (float)mat.M44);

            //System.Diagnostics.Debug.WriteLine("MM\r\n{0}", ModelMatrix);

            ProjectionModelMatrix = Matrix4.Mult(ModelMatrix, ProjectionMatrix);        // order order order ! so important.
            CountMatrixCalcs++;
        }

        /// <summary>Calculate the Projection matrix - projects the 3d model space to the 2D screen</summary> 
        public void CalculateProjectionMatrix()           // calculate and return znear.
        {
            Size scr = ViewPort.Size;

            if (InPerspectiveMode)
            {                                                                   // Fov, perspective, znear, zfar
                ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(Fov, (float)scr.Width / scr.Height, PerspectiveNearZDistance, PerspectiveFarZDistance);
                ZNear = PerspectiveNearZDistance;
            }
            else
            {
                ZNear = -OrthographicDistance;
                float orthoheight = (OrthographicDistance / 5.0f) * scr.Height / scr.Width;
                ProjectionMatrix = Matrix4.CreateOrthographic(OrthographicDistance * 2.0f / 5.0f, orthoheight * 2.0F, -OrthographicDistance, OrthographicDistance);

                Matrix4 zoffset = Matrix4.CreateTranslation(0,0,0.5f);     // we ensure all z's are based around 0.5f.  W = 1 in clip space, so Z must be <= 1 to be visible
                ProjectionMatrix = Matrix4.Mult(ProjectionMatrix, zoffset); // doing this means that a control can display with a Z around low 0.
            }

            //System.Diagnostics.Debug.WriteLine("PM\r\n{0}", ProjectionMatrix);

            if ( ModelAxisPositiveZAwayFromViewer )
            {
                ProjectionMatrix = ProjectionMatrix * Matrix4.CreateScale(new Vector3(1, -1, 1));   // flip y to make y+ up
            }

            ProjectionModelMatrix = Matrix4.Mult(ModelMatrix, ProjectionMatrix);
            CountMatrixCalcs++;
        }

        /// <summary> Scale the FOV in a direction</summary>
        public bool FovScale(bool direction)        // direction true is scale up FOV - need to tell it its changed
        {
            float curfov = Fov;

            if (direction)
                Fov = (float)Math.Min(Fov * FovFactor, Math.PI * 0.8);
            else
                Fov /= (float)FovFactor;

            return curfov != Fov;
        }

        /// <summary> Resize view port to newsize.</summary>
        public virtual void ResizeViewPort(object sender, Size newsize)            // override to change view port to a custom one
        {
            System.Diagnostics.Debug.Assert(context == GLStatics.GetContext(), "Context incorrect");     // safety

            //System.Diagnostics.Debug.WriteLine("Set GL Screensize {0}", newsize);
            ScreenSize = newsize;
            ScreenCoordMax = newsize;
            ViewPort = new Rectangle(new Point(0, 0), newsize);
            SetViewPort();
        }

        /// <summary> Set the view port into openGL</summary>
        public void SetViewPort()       
        {
            System.Diagnostics.Debug.Assert(context == GLStatics.GetContext(), "Context incorrect");     // safety

            //System.Diagnostics.Debug.WriteLine("Set GL Viewport {0} {1} w {2} h {3}", ViewPort.Left, ScreenSize.Height - ViewPort.Bottom, ViewPort.Width, ViewPort.Height);
            OpenTK.Graphics.OpenGL.GL.Viewport(ViewPort.Left, ScreenSize.Height - ViewPort.Bottom, ViewPort.Width, ViewPort.Height);
            OpenTK.Graphics.OpenGL.GL.DepthRange(DepthRange.X, DepthRange.Y);
        }

        /// <summary> Get the matrix4 to translate screen co-ords to clip space</summary>
        public virtual Matrix4 MatrixScreenCoordToClipSpace()             
        {
            Matrix4 screenmat = Matrix4.Zero;
            screenmat.Column0 = new Vector4(ScreenCoordClipSpaceSize.Width / ScreenCoordMax.Width , 0, 0, ScreenCoordClipSpaceOffset.X);      // transform of x = x * 2 / width - 1
            screenmat.Column1 = new Vector4(0.0f, -ScreenCoordClipSpaceSize.Height / ScreenCoordMax.Height, 0, ScreenCoordClipSpaceOffset.Y);  // transform of y = y * -2 / height +1, y = 0 gives +1 (top), y = sh gives -1 (bottom)
            screenmat.Column2 = new Vector4(0, 0, 1, 0);                  // transform of z = none
            screenmat.Column3 = new Vector4(0, 0, 0, 1);                  // transform of w = none
            return screenmat;
        }

        /// <summary> Adjust x and y to view port co-ordinates
        /// p is in windows coords (gl_Control), adjust to view port/clip space taking into account view port
        /// </summary>
        public virtual Point AdjustWindowCoordToViewPortCoord(int x, int y)     
        {
            return new Point(x - ViewPort.Left, y - ViewPort.Top);
        }

        /// <summary> Adjust point to view port co-ordinates
        /// p is in windows coords (gl_Control), adjust to view port/clip space
        /// </summary>
        public virtual Point AdjustWindowCoordToViewPortCoord(Point p)          
        {
            return new Point(p.X - ViewPort.Left, p.Y - ViewPort.Top);
        }

        /// <summary> Adjust window coord point to screen co-ordinates
        /// p is in window co-ords (gl_control), adjust to clip space taking into account view port and scaling
        /// </summary>
        public virtual Point AdjustWindowCoordToScreenCoord(Point p)            
        {
            return AdjustViewSpaceToScreenCoord(AdjustWindowCoordToViewPortClipSpace(p));
        }

        /// <summary> Adjust window coord point to view port coords
        /// p is in window co-ords (gl_control), adjust to clip space taking into account view port and scaling
        /// </summary>
        public virtual PointF AdjustWindowCoordToViewPortClipSpace(Point p)    
        {
            float x = p.X - ViewPort.Left;
            float y = p.Y - ViewPort.Top;
            var f = new PointF(-1 + x / ViewPort.Width * 2.0f, 1 - y / ViewPort.Height * 2.0f);
            //  System.Diagnostics.Debug.WriteLine("SC {0} -> CS {1}", p, f);
            return f;
        }

        /// <summary>
        /// Adjust view space co-ordinate to screen co-ordinate
        /// </summary>
        public virtual Point AdjustViewSpaceToScreenCoord(PointF cs)            
        {
            Size scr = ScreenCoordMax;
            SizeF clipsize = ScreenCoordClipSpaceSize;
            PointF clipoff = ScreenCoordClipSpaceOffset;

            float xo = (cs.X - clipoff.X) / clipsize.Width;         // % thru the clipspace
            float yo = (clipoff.Y - cs.Y) / clipsize.Height;

            Point np = new Point((int)(scr.Width * xo), (int)(scr.Height * yo));
            //System.Diagnostics.Debug.WriteLine("cs {0} -> {1} {2} -> {3}", cs, xo,yo, np);
            return np;
        }

        /// <summary>
        /// World pos -> normalised clip space
        /// </summary>

        public Vector4 WorldToNormalisedClipSpace(Vector4 worldpos)      
        {
            Vector4 m = Vector4.Transform(worldpos, ProjectionModelMatrix);  // go from world-> viewspace -> clip space
            Vector4 c = m / m.W;                                            // to normalised clip space
            return c;
        }

        /// <summary> Is in normalised clip space?</summary>
        public bool IsNormalisedClipSpaceInView(Vector4 clipspace)
        {
            return !(clipspace.X <= -1 || clipspace.X >= 1 || clipspace.Y <= -1 || clipspace.Y >= 1 || clipspace.Z >= 1);
        }

        /// <summary> World->viewport co-ord, W = 0 if in view. 0,0 = top left to match normal windows co-ords</summary>
        public Vector4 NormalisedClipSpaceToViewPortScreenCoord(Vector4 clipspace)   
        {
            bool inview = IsNormalisedClipSpaceInView(clipspace);
            return new Vector4((clipspace.X + 1) / 2 * ViewPort.Width, (-clipspace.Y + 1) / 2 * ViewPort.Height, 0, inview ? 0 : 1);
        }

        /// <summary> World->window co-ord, W = 0 if in view. 0,0 = top left to match normal windows co-ords</summary>

        public Vector4 NormalisedClipSpaceToWindowCoord(Vector4 clipspace)   
        {
            Vector4 viewportscreencoord = NormalisedClipSpaceToViewPortScreenCoord(clipspace);
            return new Vector4(viewportscreencoord.X + ViewPort.Left, viewportscreencoord.Y + ViewPort.Top, viewportscreencoord.Z, viewportscreencoord.W);
        }

        private IntPtr context;
    }
}
