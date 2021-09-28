﻿using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using GLOFC;
using GLOFC.Controller;
using GLOFC.GL4;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using GLOFC.GL4.Controls;
using System.Linq;
using System.Globalization;

// Demonstrate the volumetric calculations needed to compute a plane facing the user inside a bounding box done inside a geo shader
// this one add on tex coord calculation and using a single tight quad shows its working

namespace TestOpenTk
{
    public partial class TestControlsForm : Form
    {
        private GLOFC.WinForm.GLWinFormControl glwfc;
        private Controller3D gl3dcontroller;

        private Timer systemtimer = new Timer();

        public TestControlsForm()
        {
            InitializeComponent();

            glwfc = new GLOFC.WinForm.GLWinFormControl(glControlContainer);

            systemtimer.Interval = 25;
            systemtimer.Tick += new EventHandler(SystemTick);
            systemtimer.Start();
        }

        GLRenderProgramSortedList rObjects = new GLRenderProgramSortedList();
        GLItemsList items = new GLItemsList();
        GLControlDisplay displaycontrol;

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

        class MatrixCalcSpecial : GLMatrixCalc
        {
            public MatrixCalcSpecial()
            {
                ScreenCoordMax = new Size(2000, 1000);
                //ScreenCoordClipSpaceSize = new SizeF(1.8f, 1.8f);
                //ScreenCoordClipSpaceOffset = new PointF(-0.9f, 0.9f);
                //ScreenCoordClipSpaceSize = new SizeF(1f, 1f);
                //ScreenCoordClipSpaceOffset = new PointF(-0.5f, 0.5f);
            }

            public override void ResizeViewPort(object sender, Size newsize)            // override to change view port to a custom one
            {
                if (!(sender is Controller3D))         // ignore from 3dcontroller as it also sends it, but it will be reporting the size of the display window
                {
                    System.Diagnostics.Debug.WriteLine("Set GL Screensize {0}", newsize);
                    ScreenSize = newsize;
                    ScreenCoordMax = newsize;
                    int margin = 0;
                    ViewPort = new Rectangle(new Point(margin, margin), new Size(newsize.Width - margin * 2, newsize.Height - margin * 2));
                    SetViewPort();
                }
            }


        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Closed += ShaderTest_Closed;

            items.Add( new GLMatrixCalcUniformBlock(), "MCUB");     // create a matrix uniform block 

            int front = -20000, back = front + 90000, left = -45000, right = left + 90000, vsize = 2000;
  
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

            GLRenderState rl = GLRenderState.Lines(1);

            {
                items.Add(new GLFixedShader(System.Drawing.Color.Yellow), "LINEYELLOW");
                rObjects.Add(items.Shader("LINEYELLOW"),
                GLRenderableItem.CreateVector4(items, PrimitiveType.Lines, rl, displaylines));
            }

            float h = 0;
            if ( h != -1)
            {
                items.Add(new GLColorShaderWithWorldCoord(), "COS-1L");

                int dist = 1000;
                Color cr = Color.FromArgb(100, Color.White);
                rObjects.Add(items.Shader("COS-1L"),    // horizontal
                             GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Lines, rl,
                                                        GLShapeObjectFactory.CreateLines(new Vector3(left, h, front), new Vector3(left, h, back), new Vector3(dist, 0, 0), (back - front) / dist + 1),
                                                        new Color4[] { cr })
                                   );

                rObjects.Add(items.Shader("COS-1L"),
                             GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Lines, rl,
                                                        GLShapeObjectFactory.CreateLines(new Vector3(left, h, front), new Vector3(right, h, front), new Vector3(0, 0, dist), (right - left) / dist + 1),
                                                        new Color4[] { cr })
                                   );

            }

            MatrixCalcSpecial mc = new MatrixCalcSpecial();
            mc.PerspectiveNearZDistance = 1f;
            mc.PerspectiveFarZDistance = 500000f;

            mc.ResizeViewPort(this, glwfc.Size);          // must establish size before starting

            displaycontrol = new GLControlDisplay(items, glwfc,mc);       // hook form to the window - its the master, it takes its size fro mc.ScreenCoordMax
            displaycontrol.Focusable = true;          // we want to be able to focus and receive key presses.
            displaycontrol.Name = "displaycontrol";
            displaycontrol.SuspendLayout();

            GLForm pform;

            if (true)
            {
                pform = new GLForm("Form1", "GL Form demonstration", new Rectangle(0, 0, 1000, 800));
                pform.BackColor = Color.FromArgb(200, Color.Red);
                pform.SuspendLayout();
                pform.BackColorGradientDir = 90;
                pform.BackColorGradientAlt = Color.FromArgb(200, Color.Yellow);
                //pform.ScaleWindow = new SizeF(0.75f, 0.75f);
                //pform.AlternatePos = new RectangleF(100, 100, 500, 400);
                //pform.AlternatePos = new RectangleF(100, 100, 1200, 1000);
                pform.ScaleWindow = new SizeF(0.0f, 0.0f);
                pform.Animators.Add(new AnimateScale(glwfc.ElapsedTimems + 100, glwfc.ElapsedTimems + 300, new SizeF(1, 1)));
                pform.Animators.Add(new AnimateTranslate(glwfc.ElapsedTimems + 100, glwfc.ElapsedTimems + 300, new Point(100,100)));
                displaycontrol.Add(pform);

                int taborder = 0;

                GLLabel lab1 = new GLLabel("Lab1", new Rectangle(400,0,0, 0), "From Check");
                pform.Add(lab1);

                if (true)
                {
                    GLButton b1 = new GLButton("B1", new Rectangle(5, 10, 80, 30), "Button 1");
                    b1.Margin = new Margin(2);
                    b1.TabOrder = taborder++;
                    b1.Padding = new GLOFC.GL4.Controls.Padding(5);
                    b1.Click += (c, ev) => { ConfDialog(); };
                    b1.ToolTipText = "Button 1 tip\r\nLine 2 of it";
                    pform.Add(b1);

                    GLButton b2 = new GLButton("B2", new Rectangle(5, 50, 0, 0), "Button 2");
                    b2.Image = Properties.Resources.ImportSphere;
                    b2.TabOrder = taborder++;
                    b2.ImageAlign = ContentAlignment.MiddleLeft;
                    b2.TextAlign = ContentAlignment.MiddleRight;
                    b2.Click += (c, ev) => { MsgDialog(); };
                    b2.ToolTipText = "Button 2 tip\r\nLine 2 of it";
                    pform.Add(b2);

                    GLButton b3 = new GLButton("B3", new Rectangle(100, 10, 80, 30), "Button 3");
                    b3.Margin = new Margin(2);
                    b3.TabOrder = taborder++;
                    b3.Padding = new GLOFC.GL4.Controls.Padding(5);
                    b3.ToolTipText = "Button 3 tip\r\nLine 2 of it";
                    b3.Enabled = false;
                    pform.Add(b3);
                }

                if (true)
                {
                    GLComboBox cb1 = new GLComboBox("CB", new Rectangle(0, 100, 0, 0), new List<string>() { "one", "two", "three" });
                    cb1.Margin = new Margin(16, 8, 16, 8);
                    cb1.TabOrder = taborder++;
                    cb1.ToolTipText = "Combo Box";
                    pform.Add(cb1);

                    GLCheckBox chk1 = new GLCheckBox("Checkbox", new Rectangle(0, 150, 0, 0), "CheckBox 1");
                    chk1.Margin = new Margin(16, 0, 0, 0);
                    chk1.TabOrder = taborder++;
                    pform.Add(chk1);
                    GLCheckBox chk2 = new GLCheckBox("Checkbox", new Rectangle(100, 150, 0, 0), "CheckBox 2");
                    chk2.Appearance = CheckBoxAppearance.Radio;
                    chk2.TabOrder = taborder++;
                    pform.Add(chk2);
                    GLCheckBox chk3 = new GLCheckBox("Checkbox", new Rectangle(200, 150, 0, 0), "CheckBox 3");
                    chk3.Appearance = CheckBoxAppearance.Button;
                    chk3.TabOrder = taborder++;
                    pform.Add(chk3);
                    GLCheckBox chk4 = new GLCheckBox("Checkbox", new Rectangle(300, 150, 0, 0), "");
                    chk4.TabOrder = taborder++;
                    pform.Add(chk4);
                    GLCheckBox chk5 = new GLCheckBox("Checkbox", new Rectangle(350, 150, 0, 0), "R1");
                    chk5.Appearance = CheckBoxAppearance.Radio;
                    chk5.GroupRadioButton = true;
                    chk5.TabOrder = taborder++;
                    pform.Add(chk5);
                    GLCheckBox chk6 = new GLCheckBox("Checkbox", new Rectangle(400, 150, 0, 0), "");
                    chk6.Appearance = CheckBoxAppearance.Radio;
                    chk6.GroupRadioButton = true;
                    chk6.TabOrder = taborder++;
                    pform.Add(chk6);
                }

                if (true)
                {
                    GLDateTimePicker dtp = new GLDateTimePicker("DTP", new Rectangle(0, 200, 300, 30), DateTime.Now);
                    dtp.Font = new Font("Ms Sans Serif", 11);
                    dtp.ShowCheckBox = dtp.ShowCalendar = true;
                    dtp.ShowUpDown = true;
                    //dtp.Culture = CultureInfo.GetCultureInfo("es");
                    dtp.TabOrder = taborder++;
                    pform.Add(dtp);
                }

                if (true)
                {
                    List<string> i1 = new List<string>() { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" };
                    GLListBox lb1 = new GLListBox("LB1", new Rectangle(0, 250, 200, 100), i1);
                    lb1.SetMarginBorderWidth(new Margin(2), 1, Color.Wheat, new GLOFC.GL4.Controls.Padding(2));
                    lb1.Font = new Font("Microsoft Sans Serif", 12f);
                    lb1.TabOrder = taborder++;
                    //lb1.FitToItemsHeight = false;
                    pform.Add(lb1);
                    lb1.SelectedIndexChanged += (s, si) => { System.Diagnostics.Debug.WriteLine("Selected index " + si); };
                }

                if (true)
                {
                    string l = "";
                    for (int i = 0; i < 20; i++)
                    {
                        string s = string.Format("Line " + i);
                        if (i == 0)
                            s += "And a much much longer Line which should break the width";
                        l += s + "\r\n";
                    }
                    l += "trail ";
                    // l = "";

                    GLMultiLineTextBox mtb = new GLMultiLineTextBox("mltb", new Rectangle(0, 400, 400, 90), l);
                    mtb.Font = new Font("Ms Sans Serif", 16);
                    mtb.LineColor = Color.Green;
                    mtb.EnableVerticalScrollBar = true;
                    mtb.EnableHorizontalScrollBar = true;
                    mtb.SetSelection(16 * 2 + 2, 16 * 3 + 4);
                    mtb.TabOrder = taborder++;
                    mtb.RightClickMenuFont = new Font("Euro Caps", 14f);
                    pform.Add(mtb);
                    //mtb.FlashingCursor = false;
                    //mtb.ReadOnly = true;
                }

                if (false)
                {
                    GLTextBox tb1 = new GLTextBox("TB1", new Rectangle(0, 500, 150, 40), "Text Data Which is a very long string of very many many characters");
                    tb1.Font = new Font("Arial", 12);
                    tb1.TabOrder = taborder++;
                    pform.Add(tb1);
                }

                if (false)
                {
                    GLUpDownControl upc1 = new GLUpDownControl("UPC1", new Rectangle(0, 550, 26, 46));
                    upc1.TabOrder = taborder++;
                    pform.Add(upc1);
                    upc1.Clicked += (s, upe) => System.Diagnostics.Debug.WriteLine("Up down control {0} {1}", s.Name, upe);
                }

                if (false)
                {
                    GLCalendar cal = new GLCalendar("Cal", new Rectangle(500, 10, 300, 200));
                    cal.TabOrder = taborder++;
                    //cal.Culture = CultureInfo.GetCultureInfo("es");
                    cal.AutoSize = true;
                    cal.Font = new Font("Arial", 10);
                    pform.Add(cal);
                }

                if ( false )
                { 
                    GLNumberBoxFloat glf = new GLNumberBoxFloat("FLOAT", new Rectangle(500, 250, 100, 25), 23.4f);
                    glf.TabOrder = taborder++;
                    glf.Font = new Font("Ms Sans Serif", 12);
                    glf.Minimum = -1000;
                    glf.Maximum = 1000;
                    pform.Add(glf);

                    GLTextBoxAutoComplete gla = new GLTextBoxAutoComplete("ACTB", new Rectangle(500, 300, 100, 25));
                    gla.TabOrder = taborder++;
                    gla.Font = new Font("Ms Sans Serif", 12);
                    gla.PerformAutoCompleteInThread += (s, a) =>
                    {
                        var r = new List<string>() { "one", "two", "three" };
                        return r.Where(x => x.StartsWith(s)).ToList();
                    };
                    pform.Add(gla);
                }

                pform.ResumeLayout();
            }

            if (false)
            {
                GLForm pform2 = new GLForm("Form2", "Form 2 GL Control demonstration", new Rectangle(1100, 0, 400, 400));
                pform2.BackColor = Color.FromArgb(200, Color.Red);
                pform2.Font = new Font("Ms sans serif", 10);
                pform2.SuspendLayout();
                pform2.BackColorGradientDir = 90;
                pform2.BackColorGradientAlt = Color.FromArgb(200, Color.Blue);
                displaycontrol.Add(pform2);

                GLButton b1 = new GLButton("F2B1", new Rectangle(5, 10, 80, 30), "F2B1");
                pform2.Add(b1);
            }

            {
                GLToolTip tip = new GLToolTip("ToolTip");
                displaycontrol.Add(tip);
            }

            displaycontrol.ResumeLayout();

            displaycontrol.GlobalMouseDown += (ctrl, ex) =>
            {
                if (ctrl == null || !pform.IsThisOrChildOf(ctrl))
                {
                    System.Diagnostics.Debug.WriteLine("Not on form");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Click on form");
                }
            };



            gl3dcontroller = new Controller3D();
            gl3dcontroller.ZoomDistance = 5000F;
            gl3dcontroller.YHoldMovement = true;
            gl3dcontroller.PaintObjects = Controller3dDraw;

            gl3dcontroller.KeyboardTravelSpeed = (ms,eyedist) =>
            {
                return (float)ms * 10.0f;
            };

            gl3dcontroller.MatrixCalc.InPerspectiveMode = true;

            if ( displaycontrol != null )
            {
                gl3dcontroller.Start(mc , displaycontrol, new Vector3(0, 0, 10000), new Vector3(140.75f, 0, 0), 0.5F);     // HOOK the 3dcontroller to the form so it gets Form events

                displaycontrol.Paint += (o,ts) =>        // subscribing after start means we paint over the scene, letting transparency work
                {                                 
                    displaycontrol.Render(glwfc.RenderState,ts);       // we use the same matrix calc as done in controller 3d draw
                };

            }
            else
                gl3dcontroller.Start(glwfc, new Vector3(0, 0, 10000), new Vector3(140.75f, 0, 0), 0.5F);     // HOOK the 3dcontroller to the form so it gets Form events

        }

        private void ConfDialog()
        {
            GLFormConfigurable c1 = new GLFormConfigurable("test");
            c1.Add(new GLFormConfigurable.Entry("Lab1", typeof(GLLabel), "Label 1 ", new Point(10, 10), new Size(200, 24), "TT"));
            c1.Add(new GLFormConfigurable.Entry("But1", typeof(GLButton), "But 1", new Point(10, 40), new Size(200, 24), "TT"));
            c1.Add(new GLFormConfigurable.Entry("Com1", "two", new Point(10, 70), new Size(200, 24), "TT", new List<string>() { "one", "two", "three" }));
            c1.Add(new GLFormConfigurable.Entry("Textb", typeof(GLTextBox), "text box", new Point(10, 100), new Size(200, 24), "TT"));
            c1.Add(new GLFormConfigurable.Entry("OK", typeof(GLButton), "OK", new Point(160, 300), new Size(100, 24), "TT"));
            // c1.Size = new Size(400, 400);
            c1.Init(new Point(200, 200), "Config Form Test");
            c1.Trigger += (cb, en, ctrlname, args) =>
            {
                if (ctrlname == "OK")
                    c1.Close();
            };
            displaycontrol.Add(c1);
        }

        private void MsgDialog()
        {
            string t = "";
            for (int i = 0; i < 30; i++)
                t += "Line " + i + " is here" + Environment.NewLine;

            GLMessageBox msg = new GLMessageBox("MB", displaycontrol, new Point(100, 500), MsgReturn, t, "Caption", GLMessageBox.MessageBoxButtons.OKCancel);
        }

        private void MsgReturn(GLMessageBox msg, GLOFC.GL4.Controls.DialogResult res)
        {
            System.Diagnostics.Debug.WriteLine("!!! Message box " + res);
        }


        private void Controller3dDraw(Controller3D mc, ulong unused)
        {
            ((GLMatrixCalcUniformBlock)items.UB("MCUB")).SetText(gl3dcontroller.MatrixCalc);        // set the matrix unform block to the controller 3d matrix calc.

            rObjects.Render(glwfc.RenderState, gl3dcontroller.MatrixCalc);

            this.Text = "Looking at " + gl3dcontroller.MatrixCalc.TargetPosition + " eye@ " + gl3dcontroller.MatrixCalc.EyePosition + " dir " + gl3dcontroller.PosCamera.CameraDirection + " Dist " + gl3dcontroller.MatrixCalc.EyeDistance + " Zoom " + gl3dcontroller.PosCamera.ZoomFactor;
        }

        private void SystemTick(object sender, EventArgs e)
        {
            GLOFC.Timers.Timer.ProcessTimers();
            displaycontrol.Animate(glwfc.ElapsedTimems);
            if (displaycontrol != null && displaycontrol.RequestRender)
                glwfc.Invalidate();
            gl3dcontroller.HandleKeyboardSlewsAndInvalidateIfMoved(true, Otherkeys);
        }

        private void Otherkeys(KeyboardMonitor h)
        {
            if ( h.HasBeenPressed(Keys.F1))
            {
                displaycontrol.DumpTrees(0,null);
            }

        }
    }
}


