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

using System;
using System.Drawing;

namespace GLOFC.GL4.Controls
{
    /// <summary>
    /// Flow Layout panel
    /// </summary>
    public class GLFlowLayoutPanel : GLPanel
    {
        /// <summary> Flow direction if panel </summary>
        public enum ControlFlowDirection {
            /// <summary> Flow left to right </summary>
            Right, 
            /// <summary> Flow down </summary>
            Down
        };

        /// <summary> Flow direction of panel</summary>
        public ControlFlowDirection FlowDirection { get { return flowDirection; } set { flowDirection = value; InvalidateLayout(); } }
        /// <summary> Flow in Z order if true, else flow in inverse Z order </summary>
        public bool FlowInZOrder { get; set; } = true;
        /// <summary> If set, autosizes both width and height, else just only one of its width/height dependent on flow direction. AutoSize must be set </summary>
        public bool AutoSizeBoth { get; set; } = true;     
        /// <summary> If set, keep flow within parent bounds, else it will flow without regard to parent bounds</summary>
        public bool KeepWithinParent { get; set; } = true;  
        /// <summary> Padding around flow positions to space out the flow</summary>
        public PaddingType FlowPadding { get { return flowPadding; } set { flowPadding = value; InvalidateLayout(); } }

        /// <summary> Construct with name and bounds </summary>
        public GLFlowLayoutPanel(string name, Rectangle location) : base(name, location)
        {
            BorderColorNI = DefaultFlowLayoutBorderColor;
            BackColorGradientAltNI = BackColorNI = DefaultFlowLayoutBackColor;
        }

        /// <summary> Construct with name, docking type and docking percent </summary>
        public GLFlowLayoutPanel(string name, DockingType type, float dockpercent) : base(name, DefaultWindowRectangle)
        {
            Dock = type;
            DockPercent = dockpercent;
        }

        /// <summary> Construct with name, size, docking type and docking percentage </summary>
        public GLFlowLayoutPanel(string name, Size sizep, DockingType type, float dockpercentage) : base(name, DefaultWindowRectangle)
        {
            Dock = type;
            DockPercent = dockpercentage;
            SetNI(size: sizep);
        }

        /// <summary> Default Constructor</summary>
        public GLFlowLayoutPanel() : this("TLP?",DefaultWindowRectangle)
        {
        }

        /// <inheritdoc cref="GLOFC.GL4.Controls.GLBaseControl.SizeControlPostChild(Size)"/>
        protected override void SizeControlPostChild(Size parentsize)
        {
            base.SizeControlPostChild(parentsize);

            // Sizing has been recursively done for all children, now size us
            // pick the PostChild hook

            if (AutoSize)       // width stays the same, height changes, width based on what parent says we can have (either our width, or docked width)
            {
                Size arealeft = new Size(parentsize.Width - Left, parentsize.Height - Top);
                //System.Diagnostics.Debug.WriteLine("Flow {0} location {1} parentsize {2} left {3} panelsize {4}", Name, Location, parentsize, arealeft , Size);

                var flowsize = Flow(arealeft, KeepWithinParent); // includes flow padding
                if (flowsize.IsEmpty)
                    flowsize = DefaultWindowRectangle.Size;     // emergency min for no controls

                //System.Diagnostics.Debug.WriteLine("..children measured {0} ", flowsize);

                if (AutoSizeBoth)
                {
                    SetNI(clientsize: new Size( flowsize.Width , flowsize.Height ));
                }
                else if (FlowDirection == ControlFlowDirection.Right)
                {
                    SetNI(clientsize: new Size(ClientWidth, flowsize.Height ));
                }
                else
                {
                    SetNI(clientsize: new Size(flowsize.Width , ClientHeight));
                }

               // System.Diagnostics.Debug.WriteLine($"Flow panel {Name} size now {Size}");
                //foreach (var cc in ControlsIZ) System.Diagnostics.Debug.WriteLine($"  children size {cc.Size}");
            }
        }

        /// <inheritdoc cref="GLOFC.GL4.Controls.GLBaseControl.PerformRecursiveLayout"/>
        protected override void PerformRecursiveLayout()
        {
            //System.Diagnostics.Debug.WriteLine("Flow Laying out " + Name + " In client size " + ClientSize);

            Flow(ClientSize, true, (c, p) => 
            {
              //  System.Diagnostics.Debug.WriteLine(".. set pos Control " + c.Name + " to " + p + c.Size);
                c.SetNI(location:p);
                c.CallPerformRecursiveLayout();
              //  System.Diagnostics.Debug.WriteLine(".... " + c.Size);
            });

            ClearLayoutFlags();
        }

        // flow in size and perform action if required
        private Size Flow(Size area, bool usearea, Action<GLBaseControl, Point> action = null)
        {
            Point flowpos = new Point(0, 0);        // in client co-ords
            Size max = new Size(0, 0);

           // System.Diagnostics.Debug.WriteLine($"Flow in {Name} {area} {usearea} dir {FlowDirection}");
            foreach (GLBaseControl c in (FlowInZOrder ? ControlsZ: ControlsIZ))
            {
                if (c.Visible == false)     // invisibles don't flow
                    continue;

             //   System.Diagnostics.Debug.WriteLine($".. {c.Name} {c.Size}");

                Point pos;

                int controlwidth = c.Width + c.FlowOffsetPosition.X;        // including any flow offsets
                int controlheight = c.Height + c.FlowOffsetPosition.Y;
                //System.Diagnostics.Debug.WriteLine(".. flow {0} cw {1} ch {2} fop {3} flowpos {4}", c.Name, controlwidth, controlheight, FlowOffsetPosition, flowpos);

                if (FlowDirection == ControlFlowDirection.Right)
                {
                    if (usearea && flowpos.X + controlwidth + flowPadding.TotalWidth > area.Width)    // if beyond client right, more down
                    {
                        flowpos = new Point(ClientLeftMargin, max.Height);
                    }

                    pos = new Point(flowpos.X + flowPadding.Left + c.FlowOffsetPosition.X, flowpos.Y + flowPadding.Top + c.FlowOffsetPosition.Y);

                    flowpos.X += controlwidth + flowPadding.TotalWidth;                 // move x right
                    int y = flowpos.Y + controlheight + flowPadding.TotalHeight;        // calculate bottom of control

                    max = new Size(Math.Max(max.Width, flowpos.X), Math.Max(max.Height, y));
                }
                else
                {
                    if ( usearea && flowpos.Y + controlheight + flowPadding.TotalHeight > area.Height )
                    {
                        flowpos = new Point(max.Width, ClientTopMargin);
                    }

                    pos = new Point(flowpos.X + flowPadding.Left + c.FlowOffsetPosition.X, flowpos.Y + flowPadding.Top + c.FlowOffsetPosition.Y);

                    flowpos.Y += controlheight + flowPadding.TotalHeight;
                    int x = flowpos.X + controlwidth + flowPadding.TotalWidth;

                    max = new Size(Math.Max(max.Width, x),  Math.Max(max.Height, flowpos.Y));
                }

               // System.Diagnostics.Debug.WriteLine("  Position {0} to {1} s {2}", c.Name, pos, c.Size);

                action?.Invoke(c, pos);
            }

            //System.Diagnostics.Debug.WriteLine("Flow END Max " + max);
            return max;
        }

        private GL4.Controls.GLBaseControl.PaddingType flowPadding { get; set; } = new PaddingType(1);
        private ControlFlowDirection flowDirection = ControlFlowDirection.Right;
    }
}

