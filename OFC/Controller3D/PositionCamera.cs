﻿/*
* Copyright 2015 - 2021 EDDiscovery development team + Robbyxp1 @ github.com
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

using OpenTK;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace GLOFC.Controller
{
    public class PositionCamera       // holds lookat and eyepositions and camera
    {
        #region Positions

        public Vector3 LookAt { get { return lookat; } set { KillSlew(); float d = EyeDistance; lookat = value; SetEyePositionFromLookat(cameradir, d);} }
        public Vector3 EyePosition { get { return eyeposition; } set { KillSlew(); float d = EyeDistance; eyeposition = value; SetLookatPositionFromEye(cameradir, d); } }

        public float EyeDistance { get { return (lookat - EyePosition).Length; } }

        public void Translate(Vector3 pos)
        {
            KillSlew();
            lookat += pos;
            eyeposition += pos;
        }

        // time <0 estimate, 0 instant >0 time
        public void GoTo(Vector3 gotopos, float timeslewsec = 0, float unitspersecond = 10000F)       // may pass a Nan Position - no action. Y is normal sense
        {
            if (!float.IsNaN(gotopos.X))
            {
                //System.Diagnostics.Debug.WriteLine("Goto " + gotopos + " in " + timeslewsec + " at " + unitspersecond);

                double dist = Math.Sqrt((lookat.X - gotopos.X) * (lookat.X - gotopos.X) + (lookat.Y - gotopos.Y) * (lookat.Y - gotopos.Y) + (lookat.Z - gotopos.Z) * (lookat.Z - gotopos.Z));
                Debug.Assert(!double.IsNaN(dist));      // had a bug due to incorrect signs!

                if (dist >= 1)
                {
                    Vector3 eyeoffset = eyeposition - lookat;

                    if (timeslewsec == 0)
                    {
                        lookat = gotopos;
                        eyeposition = gotopos + eyeoffset;
                        //System.Diagnostics.Debug.WriteLine("{0} Immediate Slew to {1}", Environment.TickCount % 10000, targetposSlewTarget);
                    }
                    else
                    {
                        targetposSlewTarget = gotopos;
                        targetposSlewProgress = 0.0f;
                        targetposSlewTime = (timeslewsec < 0) ? ((float)Math.Max(1.0, dist / unitspersecond)) : timeslewsec;            //10000 ly/sec, with a minimum slew
                        //System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 10000} Slew start to {gotopos} {targetposSlewTime}  eye {eyeposition} look {lookat} dir {CameraDirection} rot {CameraRotation}");
                    }
                }
            }
        }

        public void GoToZoom(Vector3 gotopos, float zoom, float timeslewsec = 0, float unitspersecond = 10000F)       // may pass a Nan Position - no action. Y is normal sense
        {
            GoTo(gotopos, timeslewsec, unitspersecond);
            GoToZoom(zoom, Math.Max(targetposSlewTime,1));
        }

        public void GoToZoomPan(Vector3 gotopos, Vector2 cameradir, float zoom, float timeslewsec = 0, float unitspersecond = 10000F)       // may pass a Nan Position - no action. Y is normal sense
        {
            GoTo(gotopos, timeslewsec, unitspersecond);
            float time = Math.Max(targetposSlewTime, 1);
            GoToZoom(zoom, time);
            Pan(cameradir, time);
        }

        #endregion

        #region Camera

        // camera is in degrees
        // camera.x rotates around X, counterclockwise, = 0 (up), 90 = (forward), 180 (down)
        // camera.y rotates around Y, counterclockwise, = 0 (forward), 90 = (to left), 180 (back), -90 (to right)
        public Vector2 CameraDirection { get { return cameradir; } set { KillSlew(); cameradir = value; SetLookatPositionFromEye(value, EyeDistance); } }
        
        public float CameraRotation { get { return camerarot; } set { KillSlew(); camerarot = value; } }       // rotation around Z

        public void RotateCamera(Vector2 addazel, float addzrot, bool changelookat)
        {
            KillSlew();
            //System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 1000} Rotate camera {addazel} z {addzrot} look {lookat} eye {eyeposition} camera dir {cameradir}");

            System.Diagnostics.Debug.Assert(!float.IsNaN(addazel.X) && !float.IsNaN(addazel.Y));
            Vector2 cameraDir = CameraDirection;

            cameraDir.X = cameraDir.X.AddBoundedAngle(addazel.X);
            cameraDir.Y = cameraDir.Y.AddBoundedAngle(addazel.Y);

            if (cameraDir.X < 0 && cameraDir.X > -90)                   // Limit camera pitch
                cameraDir.X = 0;
            if (cameraDir.X > 180 || cameraDir.X <= -90)
                cameraDir.X = 180;

            camerarot = camerarot.AddBoundedAngle(addzrot);

            if (changelookat)
                SetLookatPositionFromEye(cameraDir, EyeDistance);
            else
                SetEyePositionFromLookat(cameraDir, EyeDistance);
           // System.Diagnostics.Debug.WriteLine("{0} Camera moved to {1} Eye {2} Zoom Fact {3} Eye dist {4}", Environment.TickCount % 10000, lookat, eyeposition, ZoomFactor, EyeDistance);
        }

        // Pan to camera position, time = 0 immediate, <0 estimate, else time to slew in seconds
        public void Pan(Vector2 newcamerapos, float timeslewsec = 0) 
        {
            if (timeslewsec == 0)
            {
                SetLookatPositionFromEye(newcamerapos, EyeDistance);
            }
            else
            {
                if (timeslewsec < 0)       // auto estimate on log distance between them
                {
                    Vector2 diff = newcamerapos - cameradir;
                    timeslewsec = (float)(diff.Length / 60);
                   // System.Diagnostics.Debug.WriteLine($"camera diff {diff} {timeslewsec}");
                }

                cameraDirSlewStart = CameraDirection;
                cameraDirSlewTarget = newcamerapos;
                cameraDirSlewProgress = 0.0f;
                cameraDirSlewTime = (timeslewsec == 0) ? (1.0F) : timeslewsec;
            }
        }

        // Pan to target, time = 0 immeidate, else time to slew
        public void PanTo(Vector3 target, float timeslewsec = 0)            
        {
            Vector2 camera = EyePosition.AzEl(target, true);
            Pan(camera, timeslewsec);
        }

        // time = 0 estimate
        public void PanZoomTo(Vector3 target, float zoom, float time = 0) 
        {
            Vector2 camera = EyePosition.AzEl(target, true);
            Pan(camera, time);
            GoToZoom(zoom, time);
        }

        #endregion

        #region Zoom

        public float ZoomFactor { get { return Zoom1Distance / EyeDistance; } set { KillSlew(); Zoom(value); } }
        public float Zoom1Distance { get; set; } = 1000F;                     // distance that Current=1 will be from the Position, in the direction of the camera.
        public float ZoomMax = 300F;            // Default out Current
        public float ZoomMin = 0.01F;           // Iain special ;-) - this depends on znear (the clip distance - smaller you can Current in more) and Zoomdistance.
        public float ZoomScaling = 1.258925F;      // scaling

        public void ZoomScale(bool direction)
        {
            KillSlew();
            float newzoomfactor = ZoomFactor;
            if (direction)
            {
                newzoomfactor *= (float)ZoomScaling;
                if (newzoomfactor > ZoomMax)
                    newzoomfactor = (float)ZoomMax;
            }
            else
            {
                newzoomfactor /= (float)ZoomScaling;
                if (newzoomfactor < ZoomMin)
                    newzoomfactor = (float)ZoomMin;
            }

            Zoom(newzoomfactor);
        }

        // to to zoom, time 0 = immediate, <0 estimate, >0 in seconds
        public void GoToZoom(float z, float timetozoom = 0)        // <0 means auto estimate
        {
            z = Math.Max(Math.Min(z, ZoomMax), ZoomMin);

            if (timetozoom == 0)
            {
                Zoom(z);
            }
            else if (Math.Abs(z - ZoomFactor) > 0.01)
            {
                zoomSlewTarget = z;
                zoomSlewStart = ZoomFactor;

                if (timetozoom < 0)       // auto estimate on log distance between them
                {
                    timetozoom = (float)(Math.Abs(Math.Log10(zoomSlewTarget / ZoomFactor)) * 1.5);
                }

                zoomSlewTime = timetozoom;
                zoomSlewProgress = 0;
                //System.Diagnostics.Debug.WriteLine($"gotozoom to {zoomSlewTarget} from {ZoomFactor} {zoomSlewTime}");
            }
        }

        private void Zoom(float newzoomfactor)
        {
            newzoomfactor = Math.Max(Math.Min(newzoomfactor, ZoomMax), ZoomMin);
            SetEyePositionFromLookat(CameraDirection, Zoom1Distance / newzoomfactor);
        }

        #endregion

        #region More Position functions

        public string StringPositionCamera { get { return $"{lookat.X},{lookat.Y},{lookat.Z},{eyeposition.X},{eyeposition.Y},{eyeposition.Z},{camerarot}"; } }

        public bool SetPositionCamera(string s)     // from StringPositionCamera
        {
            string[] sparts = s.Split(',');
            if (sparts.Length == 7)
            {
                float[] dparts = sparts.Select(x => x.InvariantParseFloat(0)).ToArray();
                SetPositionCamera(new Vector3(dparts[0], dparts[1], dparts[2]), new Vector3(dparts[3], dparts[4], dparts[5]), dparts[6]);
                return true;
            }
            else
                return false;
        }

        public void SetPositionCamera(Vector3 lookp, Vector3 eyeposp, float camerarotp = 0)     // set lookat/eyepos, rotation
        {
            lookat = lookp;
            eyeposition = eyeposp;
            camerarot = camerarotp;
            cameradir = eyeposition.AzEl(lookat, true);
        }

        public void SetPositionDistance(Vector3 lookp, Vector2 cameradirdegreesp, float distance, float camerarotp = 0)     // set lookat, cameradir, zoom from, rotation
        {
            lookat = lookp;
            cameradir = cameradirdegreesp;
            camerarot = camerarotp;
            SetEyePositionFromLookat(cameradir, distance);
        }

        public void SetPositionZoom(Vector3 lookp, Vector2 cameradirdegreesp, float zoom, float camerarotp = 0)     // set lookat, cameradir, zoom from, rotation
        {
            lookat = lookp;
            cameradir = cameradirdegreesp;
            camerarot = camerarotp;
            SetEyePositionFromLookat(cameradir, Zoom1Distance / zoom);
        }

        public void SetEyePositionFromLookat(Vector2 cameradirdegreesp, float distance)              // from current lookat, set eyeposition, given a camera angle and a distance
        {
            eyeposition = lookat.CalculateEyePositionFromLookat(cameradirdegreesp, distance);
            cameradir = cameradirdegreesp;
        }

        public void SetLookatPositionFromEye(Vector2 cameradirdegreesp, float distance)              // from current eye position, set lookat, given a camera angle and a distance
        {
            lookat = eyeposition.CalculateLookatPositionFromEye(cameradirdegreesp, distance);
          //  System.Diagnostics.Debug.WriteLine($"setlookat {cameradirdegreesp} distance {distance} resulting distance {(lookat - eyeposition).Length}");
            cameradir = cameradirdegreesp;
        }


        #endregion

        #region Slew

        public bool InSlew { get { return (targetposSlewProgress < 1.0f || zoomSlewTarget > 0 || cameraDirSlewProgress < 1.0f); } }

        public void KillSlew()
        {
            if (targetposSlewProgress < 1)
            {
                //System.Diagnostics.Debug.WriteLine($"Kill target pos slew at {targetposSlewProgress}");
                targetposSlewProgress = 1.0f;
            }
            zoomSlewTarget = 0;
            cameraDirSlewProgress = 1.0f;
        }

        public void DoSlew(int msticks)
        {
            if (targetposSlewProgress < 1.0f)
            {
                Debug.Assert(targetposSlewTime > 0);
                var newprogress = targetposSlewProgress + msticks / (targetposSlewTime * 1000);

                if (newprogress >= 1.0f)        // limit
                    newprogress = 1.0f;

                var slewstart = Math.Sin((targetposSlewProgress - 0.5) * Math.PI);
                var slewend = Math.Sin((newprogress - 0.5) * Math.PI);
                Debug.Assert((1 - 0 - slewstart) != 0);
                var slewfact = (slewend - slewstart) / (1.0 - slewstart);

                var totvector = new Vector3((float)(targetposSlewTarget.X - lookat.X), (float)(targetposSlewTarget.Y - lookat.Y), (float)(targetposSlewTarget.Z - lookat.Z));

                var move = Vector3.Multiply(totvector, (float)slewfact);
                lookat += move;
                eyeposition += move;

                if (newprogress >= 1.0f)
                {
                    //System.Diagnostics.Debug.WriteLine($"{Environment.TickCount%1000} Slew complete at {lookat} {eyeposition}");
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 1000} Slew to {lookat} eye {eyeposition} dist {EyeDistance} prog {newprogress}");
                }

                targetposSlewProgress = (float)newprogress;
            }

            if ( zoomSlewProgress < 1.0f )
            {
                var newprogress = zoomSlewProgress + msticks / (zoomSlewTime * 1000);

                if ( newprogress >= 1.0f)
                {
                    SetEyePositionFromLookat(CameraDirection, Zoom1Distance / zoomSlewTarget);
                    //System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 10000} Zoom {zoomSlewTarget} over {ZoomFactor}");
                }
                else
                {
                    float newzoom = zoomSlewStart + (zoomSlewTarget - zoomSlewStart) * newprogress;
                   // System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 10000} Zoom {zoomSlewTarget} zoomfactor {ZoomFactor} -> set new {newzoom}");
                    SetEyePositionFromLookat(CameraDirection, Zoom1Distance / newzoom);
                }

                zoomSlewProgress = newprogress;
            }

            if (cameraDirSlewProgress < 1.0f)
            {
                var newprogress = cameraDirSlewProgress + msticks / (cameraDirSlewTime * 1000);

                if (newprogress >= 1.0f)
                {
                    SetLookatPositionFromEye(cameraDirSlewTarget, EyeDistance);
              //      System.Diagnostics.Debug.WriteLine($"Slew stop at {cameraDirSlewTarget}");
                }
                else
                {
                    Vector2 newpos = new Vector2(cameraDirSlewStart.X + (cameraDirSlewTarget.X - cameraDirSlewStart.X) * newprogress,
                                             cameraDirSlewStart.Y + (cameraDirSlewTarget.Y - cameraDirSlewStart.Y) * newprogress);
                    SetLookatPositionFromEye(newpos, EyeDistance);
             //       System.Diagnostics.Debug.WriteLine($"Slewing {cameraDirSlewProgress} to {newpos}");
                 }
                 cameraDirSlewProgress = newprogress;
            }
        }

        #endregion

        #region Different tracker

        private Vector3 lastlookat;
        private Vector3 lasteyepos;
        private float lastcamerarotation;

        public void ResetDifferenceTracker()
        {
            lasteyepos = EyePosition;
            lastlookat = Vector3.Zero;
            lastcamerarotation = 0;
        }

        public bool IsMoved(float minmovement = 0.1f, float cameramove = 1.0f)
        {
            bool moved = Vector3.Subtract(lastlookat, LookAt).Length >= minmovement;
            if (moved)
                lastlookat = LookAt;
            bool eyemoved = Vector3.Subtract(lasteyepos, EyePosition).Length >= minmovement;
            if (eyemoved)
                lasteyepos = EyePosition;
            bool rotated = Math.Abs(CameraRotation - lastcamerarotation) >= cameramove;
            if (rotated)
                lastcamerarotation = CameraRotation;
            return moved | eyemoved | rotated;
        }

        #endregion

        #region UI

        public bool YHoldMovement { get; set; } = true;

        // Keys WASD RF Left/Right/Up/Down/PageUp/Down  with none or shift or ctrl
        public bool PositionKeyboard(KeyboardMonitor kbd, bool inperspectivemode, float movedistance)
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
                        var cameramove = Matrix4.CreateTranslation(new Vector3(positionMovement.X,positionMovement.Z,-positionMovement.Y));
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

        // Numpad 8+2+4+6, TGQE with none or shift
        // Numpad 9+3 tilt camera
        public bool CameraKeyboard(KeyboardMonitor kbd, float angle)
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
        public bool ZoomKeyboard(KeyboardMonitor kbd, float adjustment)
        {
            bool changed = false;

            if (kbd.IsCurrentlyPressed(KeyboardMonitor.ShiftState.None, Keys.Add, Keys.M))
            {
                Zoom(adjustment);
                changed = true;
            }

            if (kbd.IsCurrentlyPressed(KeyboardMonitor.ShiftState.None, Keys.Subtract, Keys.N))
            {
                Zoom(1.0f / adjustment);
                changed = true;
            }

            float newzoom = 0;

            if (kbd.HasBeenPressed(Keys.D1, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMax;
            if (kbd.HasBeenPressed(Keys.D2, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = 100;                                                      // Factor 3 scale
            if (kbd.HasBeenPressed(Keys.D3, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = 33;
            if (kbd.HasBeenPressed(Keys.D4, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = 11F;
            if (kbd.HasBeenPressed(Keys.D5, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = 3.7F;
            if (kbd.HasBeenPressed(Keys.D6, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = 1.23F;
            if (kbd.HasBeenPressed(Keys.D7, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = 0.4F;
            if (kbd.HasBeenPressed(Keys.D8, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = 0.133F;
            if (kbd.HasBeenPressed(Keys.D9, KeyboardMonitor.ShiftState.Ctrl))
                newzoom = ZoomMin;

            if (newzoom != 0)
            {
                GoToZoom(newzoom, -1);
                changed = true;
            }

            return changed;
        }

        #endregion

        #region Privates

        private Vector3 lookat = Vector3.Zero;                // point where we are viewing. 
        private Vector3 eyeposition = new Vector3(10, 10, 10);  // and the eye position
        private Vector2 cameradir = Vector2.Zero;               // camera dir, kept in track
        private float camerarot = 0;                            // and rotation

        private Vector3 targetposSlewTarget;                    // where to slew to.
        private float targetposSlewProgress = 1.0f;             // 0 -> 1 slew progress
        private float targetposSlewTime;                        // how long to take to do the slew

        private float zoomSlewTarget = 0;
        private float zoomSlewStart = 0;
        private float zoomSlewProgress = 1.0f;
        private float zoomSlewTime = 0;

        private Vector2 cameraDirSlewTarget;                    // where to slew to.
        private Vector2 cameraDirSlewStart;                     // where it started
        private float cameraDirSlewProgress = 1.0f;             // 0 -> 1 slew progress
        private float cameraDirSlewTime;                        // how long to take to do the slew

        #endregion
    }
}

