﻿/*
 * Copyright 2021 - 2021 Robbyxp1 @ github.com
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

using GLOFC.Utils;
using OpenTK;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace GLOFC.Controller
{
    /// <summary>
    /// Base class for 3dControllers - handles mouse and keyboard 
    /// </summary>

    public abstract class Controller3DBase
    {
        /// <summary> Amount to rotate per pixel for mouse </summary>
        public float MouseRotateAmountPerPixel { get; set; } = 0.15f;         
        /// <summary> Amount to move up/down per pixel (at zoom 1) for mouse</summary>
        public float MouseUpDownAmountAtZoom1PerPixel { get; set; } = 5f;   
        /// <summary> Amount to translate per pixel (at zoom 1) for mouse </summary>
        public float MouseTranslateAmountAtZoom1PerPixel { get; set; } = 10.0f;  
        /// <summary> Set for Y Hold movement </summary>
        public bool YHoldMovement { get; set; } = true;                        

        /// <summary> Optional set to scale travel key commands given this time interval and camera distance </summary>
        public Func<int, float, float> KeyboardTravelSpeed;                     
        /// <summary> Optional set to scale camera key rotation commands given this time interval</summary>
        public Func<int, float> KeyboardRotateSpeed;                             
        /// <summary> Optional set to scale zoom speed commands given this time interval</summary>
        public Func<int, float> KeyboardZoomSpeed;

        /// <summary> Matrix Calc to use </summary>
        public GLMatrixCalc MatrixCalc { get; set; } = new GLMatrixCalc();

        /// <summary> Hook glwindow control to our handlers </summary>
        public void Hook(GLWindowControl win, bool registermouseui = true, bool registerkeyui = true)
        {
            if (registermouseui)
            {
                win.MouseDown += MouseDown;
                win.MouseUp += MouseUp;
                win.MouseMove += MouseMove;
                win.MouseWheel += MouseWheel;
            }

            if (registerkeyui)
            {
                win.KeyDown += KeyDown;
                win.KeyUp += KeyUp;
            }
        }

        /// <summary> Hook a GL base control to our handlers </summary>
        public void Hook(GL4.Controls.GLBaseControl c, GLWindowControl wc)
        {
            c.MouseDown += (bc, ev) => { MouseDown(wc, ev); };
            c.MouseUp += (bc, ev) => { MouseUp(wc, ev); };
            c.MouseMove += (bc, ev) => { MouseMove(wc, ev); };
            c.MouseWheel += (bc, ev) => { MouseWheel(wc, ev); };
            c.KeyDown += (bc, ka) => { KeyDown(wc, ka); };
            c.KeyUp += (bc, ka) => { KeyUp(wc, ka); };
        }

        /// <summary> Mouse Down handler - hook to GLWindowControl </summary>
        public void MouseDown(GLWindowControl sender, GLMouseEventArgs e)
        {
            mouseDownPos = MatrixCalc.AdjustWindowCoordToViewPortCoord(e.WindowLocation);
            //System.Diagnostics.Debug.WriteLine($"Mouse down {e.WindowLocation} -> {mouseDownPos}");
        }

        /// <summary> Mouse Up handler - hook to GLWindowControl</summary>
        public void MouseUp(GLWindowControl sender, GLMouseEventArgs e)
        {
            // System.Diagnostics.Debug.WriteLine($"Mouse Up");
            mouseDeltaPos = mouseDownPos = new Point(int.MinValue, int.MinValue);
        }

        /// <summary> How much has the mouse moved since mouse down? </summary>
        public int MouseMovedSq(GLMouseEventArgs e)     // square of mouse moved, -1 if was not down
        {
            if (mouseDownPos.X != int.MinValue)
            {
                var curpos = MatrixCalc.AdjustWindowCoordToViewPortCoord(e.WindowLocation);
                return (curpos.X - mouseDownPos.X) * (curpos.X - mouseDownPos.X) + (curpos.Y - mouseDownPos.Y) * (curpos.Y - mouseDownPos.Y);
            }
            else
                return -1;
        }

        /// <summary> Mouse Move handler - hook to GLWindowControl</summary>
        public void MouseMove(GLWindowControl sender, GLMouseEventArgs e)
        {
            if (mouseDownPos.X == int.MinValue)
                return;

            if (mouseDeltaPos.X == int.MinValue)
            {
                var delta = MouseMovedSq(e);

                System.Diagnostics.Debug.WriteLine($"Mouse Move delta {delta}");

                if (delta >= 4)
                {
                    mouseDeltaPos = mouseDownPos;
                }
                else
                    return;     // not enough movement
            }

            var mousepos = MatrixCalc.AdjustWindowCoordToViewPortCoord(e.WindowLocation);

            if (e.Button == GLMouseEventArgs.MouseButtons.Left)
            {
                //System.Diagnostics.Debug.WriteLine($"Mouse {MouseDownPos}");
                if (MatrixCalc.InPerspectiveMode && mouseDownPos.X != int.MinValue) // on resize double click resize, we get a stray mousemove with left, so we need to make sure we actually had a down event
                {
                    int dx = mousepos.X - mouseDeltaPos.X;
                    int dy = mousepos.Y - mouseDeltaPos.Y;
                    var vec = new Vector2((float)(dy * MouseRotateAmountPerPixel), (float)(dx * MouseRotateAmountPerPixel));

                    //  System.Diagnostics.Debug.WriteLine($"3dcontroller Mouse move left {mousepos} {e.WindowLocation} {dx} {dy} {vec}");

                    KillSlew();    // all slews

                    mouseDeltaPos = mousepos;        // we reset both since the other button may be clicked later

                    RotateCamera(vec, 0, true);
                }
            }
            else if (e.Button == GLMouseEventArgs.MouseButtons.Right)
            {
                //System.Diagnostics.Debug.WriteLine($"Mouse {MouseDownPos}");
                KillSlew();

                int dx = mousepos.X - mouseDeltaPos.X;
                int dy = mousepos.Y - mouseDeltaPos.Y;

                mouseDeltaPos = mousepos;

                Vector3 translation = new Vector3(dx * (1.0f / ZoomFactor) * MouseTranslateAmountAtZoom1PerPixel, -dy * (1.0f / ZoomFactor) * MouseTranslateAmountAtZoom1PerPixel, 0.0f);

                if (MatrixCalc.InPerspectiveMode)
                {
                    //System.Diagnostics.Trace.WriteLine("dx" + dx.ToString() + " dy " + dy.ToString() + " Button " + e.Button.ToString());

                    Matrix3 transform = Matrix3.CreateRotationZ((float)(CameraDirection.Y * Math.PI / 180.0f));
                    translation = Vector3.Transform(translation, transform);

                    Translate(new Vector3(translation.X, 0, translation.Y));
                }
                else
                    Translate(new Vector3(translation.X, 0, translation.Y));
            }
            else if (e.Button == (GLMouseEventArgs.MouseButtons.Left | GLMouseEventArgs.MouseButtons.Right))
            {
                if ( MatrixCalc.InPerspectiveMode)
                {
                    KillSlew();

                    int dy = mousepos.Y - mouseDeltaPos.Y;

                    mouseDeltaPos = mousepos;

                    var tx = new Vector3(0, -dy * (1.0f / ZoomFactor) * MouseUpDownAmountAtZoom1PerPixel, 0);

                    //System.Diagnostics.Trace.WriteLine($"Controller3d right click translate {e.WindowLocation} -> {mousepos} prev {mouseDownPos} dy {dy} Button {e.Button.ToString()} {tx}");

                    Translate(tx);
                }
            }
        }

        /// <summary> Mouse Wheel handler - hook to GLWindowControl</summary>
        public void MouseWheel(GLWindowControl sender, GLMouseEventArgs e)
        {
            if (e.Delta != 0)
            {
                if (keyboard.Ctrl)
                {
                    if (MatrixCalc.FovScale(e.Delta < 0))
                    {
                        MatrixCalc.CalculateProjectionMatrix();
                        Invalidate();
                    }
                }
                else
                {
                    ZoomScale(e.Delta > 0);
                }
            }
        }

        /// <summary> Key Down handler - hook to GLWindowControl </summary>
        public void KeyDown(GLWindowControl sender, GLKeyEventArgs e)
        {
            keyboard.KeyDown(e.Control, e.Shift, e.Alt, e.KeyCode);
        }
        /// <summary> Key Up handler - hook to GLWindowControl</summary>
        public void KeyUp(GLWindowControl sender, GLKeyEventArgs e)
        {
            keyboard.KeyUp(e.Control, e.Shift, e.Alt, e.KeyCode);
        }

        /// <summary> 
        /// Handle keyboard, handle other keys if required. Note does not call any GL functions 
        /// </summary>
        protected int HandleKeyboardSlews(ulong curtime, bool focused, bool activated, Action<KeyboardMonitor> handleotherkeys = null)
        {
            int interval = lastkeyintervalcount.HasValue ? (int)(curtime - lastkeyintervalcount) : 1;
            lastkeyintervalcount = curtime;

            if (activated && focused)                      // if we can accept keys
            {
                if (MatrixCalc.InPerspectiveMode)               // camera rotations are only in perspective mode
                {
                    CameraKeyboard(keyboard, KeyboardRotateSpeed?.Invoke(interval) ?? (0.02f * interval));
                }

                if (keyboard.IsAnyCurrentlyOrHasBeenPressed())
                {
                    PositionKeyboard(keyboard, MatrixCalc.InPerspectiveMode, KeyboardTravelSpeed?.Invoke(interval, MatrixCalc.EyeDistance) ?? (0.1f * interval));
                    ZoomKeyboard(keyboard, KeyboardZoomSpeed?.Invoke(interval) ?? (1.0f + ((float)interval * 0.002f)));      // zoom slew is not affected by the above

                    handleotherkeys?.Invoke(keyboard);

                    keyboard.ClearHasBeenPressed();
                }
            }
            else
            {
                keyboard.Reset();
            }

            return interval;
        }

        // Numpad 8+2+4+6, TGQE with none or shift
        // Numpad 9+3 tilt camera

        private bool CameraKeyboard(KeyboardMonitor kbd, float angle)
        {
            Vector3 cameraActionRotation = Vector3.Zero;

            if (kbd.Shift)
                angle *= 2.0F;

            bool movelookat = kbd.Ctrl == false;

            if (kbd.IsCurrentlyPressed(Keys.NumPad8, Keys.T) != null)           // pitch up, 
            {
                cameraActionRotation.X = -angle;
            }
            else if (kbd.IsCurrentlyPressed(Keys.NumPad2, Keys.G) != null)           // pitch down
            {
                cameraActionRotation.X = angle;
            }

            if (kbd.IsCurrentlyPressed(Keys.NumPad4, Keys.Q) != null)           // turn left
            {
                cameraActionRotation.Y = -angle;
            }
            else if (kbd.IsCurrentlyPressed(Keys.NumPad6, Keys.E) != null)      // turn right
            {
                cameraActionRotation.Y = angle;
            }

            if (kbd.IsCurrentlyPressed(Keys.NumPad9) != null)
            {
                cameraActionRotation.Z = angle;
            }
            if (kbd.IsCurrentlyPressed(Keys.NumPad3) != null)
            {
                cameraActionRotation.Z = -angle;
            }

            //System.Diagnostics.Debug.WriteLine($"Cam rot {cameraActionRotation} {movelookat}");

            if (cameraActionRotation.LengthSquared > 0)
            {
                RotateCamera(new Vector2(cameraActionRotation.X, cameraActionRotation.Y), cameraActionRotation.Z, movelookat);
                return true;
            }
            else
                return false;
        }

        // Keys MN, Ctrl1-9
        private bool ZoomKeyboard(KeyboardMonitor kbd, float adjustment)
        {
            bool changed = false;

            if (kbd.IsCurrentlyPressed(KeyboardMonitor.ShiftState.None, Keys.Add, Keys.M) >= 0)
            {
                ZoomBy(adjustment);
                changed = true;
            }

            if (kbd.IsCurrentlyPressed(KeyboardMonitor.ShiftState.None, Keys.Subtract, Keys.N) >= 0)
            {
                ZoomBy(1.0f / adjustment);
                changed = true;
            }

            float newzoom = 0;

            float dist = ZoomMax - ZoomMin;
            float div = 2048;
            float pow = 3;

            if (kbd.HasBeenPressed(Keys.D1, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMax;
            if (kbd.HasBeenPressed(Keys.D2, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin + dist * (float)(Math.Pow(7, pow) / div);
            if (kbd.HasBeenPressed(Keys.D3, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin + dist * (float)(Math.Pow(6, pow) / div);
            if (kbd.HasBeenPressed(Keys.D4, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin + dist * (float)(Math.Pow(5, pow) / div);
            if (kbd.HasBeenPressed(Keys.D5, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin + dist * (float)(Math.Pow(4, pow) / div);
            if (kbd.HasBeenPressed(Keys.D6, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin + dist * (float)(Math.Pow(3, pow) / div);
            if (kbd.HasBeenPressed(Keys.D7, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin + dist * (float)(Math.Pow(2, pow) / div);
            if (kbd.HasBeenPressed(Keys.D8, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin + dist * (float)(Math.Pow(1, pow) / div);
            if (kbd.HasBeenPressed(Keys.D9, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin;


            if (newzoom != 0)
            {
                GoToZoom(newzoom, -1);
                changed = true;
            }

            return changed;
        }

        // Keys WASD RF Left/Right/Up/Down/PageUp/Down  with none or shift or ctrl
        private bool PositionKeyboard(KeyboardMonitor kbd, bool inperspectivemode, float movedistance)
        {
            Vector3 positionMovement = Vector3.Zero;

            if (kbd.Shift)
                movedistance *= 2.0F;

            if (kbd.HasBeenPressed(Keys.M, KeyboardMonitor.ShiftState.Ctrl))
                YHoldMovement = !YHoldMovement;

            if (kbd.IsCurrentlyPressed(Keys.Left, Keys.A) != null)                // x axis
            {
                positionMovement.X = -movedistance;
            }
            else if (kbd.IsCurrentlyPressed(Keys.Right, Keys.D) != null)
            {
                positionMovement.X = movedistance;
            }

            if (kbd.IsCurrentlyPressed(Keys.PageUp, Keys.R) != null)              // y axis
            {
                positionMovement.Y = movedistance;
            }
            else if (kbd.IsCurrentlyPressed(Keys.PageDown, Keys.F) != null)
            {
                positionMovement.Y = -movedistance;
            }

            if (kbd.IsCurrentlyPressed(Keys.Up, Keys.W) != null)                  // z axis
            {
                positionMovement.Z = movedistance;
            }
            else if (kbd.IsCurrentlyPressed(Keys.Down, Keys.S) != null)
            {
                positionMovement.Z = -movedistance;
            }

            if (positionMovement.LengthSquared > 0)
            {
                if (inperspectivemode)
                {
                    Vector2 cameraDir = CameraDirection;

                    if (YHoldMovement)  // Y hold movement means only the camera rotation around the Y axis is taken into account. 
                    {
                        var cameramove = Matrix4.CreateTranslation(positionMovement);
                        var rotY = Matrix4.CreateRotationY(cameraDir.Y.Radians());      // rotate by Y, which is rotation around the Y axis, which is where your looking at horizontally
                        cameramove *= rotY;
                        Translate(cameramove.ExtractTranslation());
                    }
                    else
                    {
                        var cameramove = Matrix4.CreateTranslation(new Vector3(positionMovement.X, positionMovement.Z, -positionMovement.Y));
                        cameramove *= Matrix4.CreateRotationZ(CameraRotation.Radians());   // rotate the translation by the camera look angle
                        cameramove *= Matrix4.CreateRotationX(cameraDir.X.Radians());
                        cameramove *= Matrix4.CreateRotationY(cameraDir.Y.Radians());
                        Translate(cameramove.ExtractTranslation());
                    }
                }
                else
                {
                    positionMovement.Y = 0;
                    Translate(positionMovement);
                }

                return true;
            }
            else
                return false;
        }


        private Point mouseDeltaPos = new Point(int.MinValue, int.MinValue);  // when using the mouse move, last delta pos
        private Point mouseDownPos = new Point(int.MinValue, int.MinValue);
        private KeyboardMonitor keyboard = new KeyboardMonitor();        // needed to be held because it remembers key downs
        private ulong? lastkeyintervalcount = null;

        // abstract interface into inheritor
        private protected abstract void KillSlew();
        private protected abstract void RotateCamera(Vector2 dir, float addzrot, bool changelookat);
        private protected abstract void Translate(Vector3 dir);
        private protected abstract void ZoomBy(float v);
        private protected abstract float ZoomFactor { get; }
        private protected abstract void GoToZoom(float v, float time);
        private protected abstract float ZoomMin { get; }
        private protected abstract float ZoomMax { get; }
        private protected abstract void ZoomScale(bool dir);
        private protected abstract Vector2 CameraDirection { get; }
        private protected abstract float CameraRotation { get; }
        private protected abstract void Invalidate();
    }
}
