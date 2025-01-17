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

using GLOFC.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace GLOFC.GL4.Controls
{
    /// <summary>
    /// Panel with scroll bar
    /// </summary>
    public class GLScrollPanelScrollBar : GLBaseControl
    {
        /// <summary> Scroll panelback color</summary>
        public Color ScrollBackColor { get { return scrollpanel.BackColor; } set { scrollpanel.BackColor = value; } }

        /// <summary> Scroll bar theme</summary>
        public GLScrollBarTheme ScrollBarTheme { get { return vertscrollbar.Theme; } }

        /// <summary> Controls of panel in Z order</summary>
        public override IList<GLBaseControl> ControlsZ { get { return scrollpanel.ControlsZ; } }      // read only
        /// <summary> Controls of panel in inverse Z order</summary>
        public override IList<GLBaseControl> ControlsIZ { get { return scrollpanel.ControlsIZ; } }      // read only

        /// <summary> Scroll bar width </summary>
        public int ScrollBarWidth { get { return Font?.ScalePixels(20) ?? 20; } }

        /// <summary> Enable horizontal scroll bar </summary>
        public bool EnableHorzScrolling { get { return horzscrollbar.Visible; } set { horzscrollbar.Visible = value; } }
        /// <summary> Enable vertical scroll bar</summary>
        public bool EnableVertScrolling { get { return vertscrollbar.Visible; } set { vertscrollbar.Visible = value; } }

        /// <summary> Disable autosize </summary>
        public new bool AutoSize { get { return false; } set { throw new NotImplementedException(); } }

        /// <summary> Construct with name and location </summary>
        public GLScrollPanelScrollBar(string name, Rectangle location) : base(name,location)
        {
            BorderColorNI = DefaultVerticalScrollPanelBorderColor;
            BackColorGradientAltNI = BackColorNI = DefaultVerticalScrollPanelBackColor;

            scrollpanel = new GLScrollPanel(name+"_VSP");
            scrollpanel.Dock = DockingType.Fill;
            scrollpanel.BackColor = BackColor;
            base.Add(scrollpanel);  // base because we don't want to use the overrides

            vertscrollbar = new GLVerticalScrollBar(name + "_SVert");
            vertscrollbar.Dock = DockingType.Right;
            vertscrollbar.Width = ScrollBarWidth;
            base.Add(vertscrollbar);     // last added always goes to top of z-order
            vertscrollbar.Scroll += VScrolled;

            horzscrollbar = new GLHorizontalScrollBar(name + "_SHorz");
            horzscrollbar.Dock = DockingType.Bottom;
            horzscrollbar.Height =ScrollBarWidth;
            base.Add(horzscrollbar);     // last added always goes to top of z-order
            horzscrollbar.Scroll += HScrolled;

            horzscrollbar.Theme = vertscrollbar.Theme;                  // set theme for horz scroll bar to same as vertical scroll bar
            vertscrollbar.Theme.Parents.Add(horzscrollbar);             // and add it to Parents so when it gets changed, we invalidate both
        }

        /// <summary> Empty Construct</summary>
        public GLScrollPanelScrollBar(string name = "SPSB?") : this(name, DefaultWindowRectangle)
        {
        }

        /// <inheritdoc cref="GLOFC.GL4.Controls.GLBaseControl.Add(GLBaseControl, bool)"/>
        public override void Add(GLBaseControl other, bool atback = false)           // we need to override, since we want controls added to the scroll panel not us
        {
            scrollpanel.Add(other, atback);
            InvalidateLayout();
        }

        private GLVerticalScrollBar vertscrollbar;
        private GLHorizontalScrollBar horzscrollbar;
        private GLScrollPanel scrollpanel;

        private void VScrolled(GLBaseControl c, GLScrollBar.ScrollEventArgs e)
        {
            scrollpanel.VertScrollPos = vertscrollbar.Value;
        }
        private void HScrolled(GLBaseControl c, GLScrollBar.ScrollEventArgs e)
        {
            scrollpanel.HorzScrollPos = horzscrollbar.Value;
        }
        
        /// <inheritdoc cref="GLOFC.GL4.Controls.GLBaseControl.PerformRecursiveLayout"/>
        protected override void PerformRecursiveLayout()
        {
            vertscrollbar.Width = ScrollBarWidth;
            horzscrollbar.Height = ScrollBarWidth;
            horzscrollbar.DockingMargin = new MarginType(0,0,vertscrollbar.Visible ? ScrollBarWidth : 0,0);

            base.PerformRecursiveLayout();   // the docking sorts out the positioning of the controls

            vertscrollbar.Maximum = scrollpanel.VertScrollRange + vertscrollbar.LargeChange;
            horzscrollbar.Maximum = scrollpanel.HorzScrollRange + horzscrollbar.LargeChange;
         //   System.Diagnostics.Debug.WriteLine($"Scroll panel range {horzscrollbar.Maximum} {vertscrollbar.Maximum}");
        }
    }
}

