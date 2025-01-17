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
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using GLOFC;
using GLOFC.Controller;
using GLOFC.GL4;
using System;
using System.Drawing;
using System.Collections.Generic;
using GLOFC.GL4.Controls;
using System.Linq;
using GLOFC.GL4.Shaders;
using GLOFC.GL4.Shaders.Vertex;
using GLOFC.GL4.Shaders.Basic;
using GLOFC.GL4.Shaders.Fragment;
using GLOFC.GL4.ShapeFactory;
using GLOFC.Utils;
using static GLOFC.GL4.Controls.GLBaseControl;

namespace TestOpenTk
{
    public partial class TestControlsDGV: System.Windows.Forms.Form
    {
        private GLOFC.WinForm.GLWinFormControl glwfc;
        private Controller3D gl3dcontroller;

        private System.Windows.Forms.Timer systemtimer = new System.Windows.Forms.Timer();

        public TestControlsDGV()
        {
            InitializeComponent();

            glwfc = new GLOFC.WinForm.GLWinFormControl(glControlContainer);
        }

        GLRenderProgramSortedList rObjects = new GLRenderProgramSortedList();
        GLItemsList items = new GLItemsList();
        GLControlDisplay displaycontrol;
        GLDataGridView dgv;
        GLForm pform;
        /// ////////////////////////////////////////////////////////////////////////////////////////////////////

        private void ShaderTest_Closed(object sender, EventArgs e)
        {
            items.Dispose();
            GLStatics.VerifyAllDeallocated();
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

            GLRenderState rl = GLRenderState.Lines();

            {
                items.Add(new GLFixedShader(System.Drawing.Color.Yellow), "LINEYELLOW");
                rObjects.Add(items.Shader("LINEYELLOW"),
                GLRenderableItem.CreateVector4(items, PrimitiveType.Lines, rl, displaylines));
            }

            float h = 0;
            if ( h != -1)
            {
                items.Add(new GLColorShaderWorld(), "COS-1L");

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

            GLMatrixCalc mc = new GLMatrixCalc();
            mc.PerspectiveNearZDistance = 1f;
            mc.PerspectiveFarZDistance = 500000f;

            mc.ResizeViewPort(this, glwfc.Size);          // must establish size before starting

            displaycontrol = new GLControlDisplay(items, glwfc, mc);     // start class but don't hook
            displaycontrol.Focusable = true;          // we want to be able to focus and receive key presses.
            displaycontrol.Font = new Font("Times", 8);
            displaycontrol.Paint += (ts) => { System.Diagnostics.Debug.WriteLine("Paint controls"); displaycontrol.Render(glwfc.RenderState, ts); };

            gl3dcontroller = new Controller3D();
            gl3dcontroller.ZoomDistance = 5000F;
            gl3dcontroller.YHoldMovement = true;
            gl3dcontroller.PaintObjects = Controller3dDraw;
            gl3dcontroller.KeyboardTravelSpeed = (ms, eyedist) => { return (float)ms * 10.0f; };
            gl3dcontroller.MatrixCalc.InPerspectiveMode = true;

            // start hooks the glwfc paint function up, first, so it gets to go first
            // No ui events from glwfc.
            gl3dcontroller.Start(glwfc, new Vector3(0, 0, 10000), new Vector3(140.75f, 0, 0), 0.5F, false, false);
            gl3dcontroller.Hook(displaycontrol, glwfc); // we get 3dcontroller events from displaycontrol, so it will get them when everything else is unselected
            displaycontrol.Hook();  // now we hook up display control to glwin, and paint

            pform = new GLForm("Form1", "GL Control demonstration", new Rectangle(10, 10, 700, 800));

            displaycontrol.Add(pform);

            if (true)
            {
                dgv = new GLDataGridView("DGV-1", new Rectangle(10, 10, 600, 500));
                dgv.Dock = DockingType.Fill;
                dgv.DefaultAltRowCellStyle.BackColor = Color.FromArgb(255, 240, 240, 240);
                dgv.DefaultAltRowCellStyle.ForeColor = Color.DarkBlue;
                dgv.SelectRowOnRightClick = true;

                // dgv.ColumnFillMode = GLDataGridView.ColFillMode.FillWidth;
                var col0 = dgv.CreateColumn();
                var col1 = dgv.CreateColumn();
                var col2 = dgv.CreateColumn();
                var col3 = dgv.CreateColumn();
                col0.Width = 20;
                col0.MinimumWidth = 30;
                col0.Text = "Col0";
                col1.Width = 150;
                col1.Text = "Col1";
                col1.MinimumWidth = 50;
                col2.Width = 150;
                col2.Text = "Col2";
                col3.Width = 150;
                col3.Text = "Col3";
                dgv.AddColumn(col0);
                dgv.AddColumn(col1);
                dgv.AddColumn(col2);
                dgv.AddColumn(col3);

                pform.BackColor = Color.FromArgb(128, 128, 128, 128);
                pform.ForeColor = Color.DarkOrange;

                dgv.DefaultCellStyle.Padding = new PaddingType(5);

                dgv.BackColor = Color.FromArgb(128, 60, 60, 0);
                dgv.DefaultColumnHeaderStyle.ForeColor = dgv.DefaultRowHeaderStyle.ForeColor =
                dgv.DefaultCellStyle.ForeColor = dgv.DefaultAltRowCellStyle.ForeColor = Color.DarkOrange;

                dgv.UpperLeftBackColor =
                dgv.DefaultColumnHeaderStyle.BackColor = dgv.DefaultRowHeaderStyle.BackColor =  Color.FromArgb(192, 64, 64, 64);

                dgv.DefaultCellStyle.BackColor = Color.FromArgb(200, 40, 40, 40);
                dgv.DefaultAltRowCellStyle.BackColor = Color.FromArgb(200, 50, 50, 50);

                dgv.ScrollBarTheme.BackColor = Color.Transparent;
                dgv.ScrollBarTheme.SliderColor = Color.FromArgb(0, 64, 64, 64);
                dgv.ScrollBarTheme.ThumbButtonColor = Color.DarkOrange;
                dgv.ScrollBarTheme.MouseOverButtonColor = Color.Orange;
                dgv.ScrollBarTheme.MousePressedButtonColor = Color.FromArgb(255, 255, 192, 0);
                dgv.ScrollBarTheme.ArrowButtonColor = Color.Transparent;
                dgv.ScrollBarTheme.ArrowColor = Color.DarkOrange;

                col2.SortCompare = GLDataGridViewSorts.SortCompareNumeric;

                for (int i = 0; i < 200; i++)
                {
                    var row = dgv.CreateRow();
                    if (i < 2 || i > 5) row.AutoSize = true;
                    string prefix = char.ConvertFromUtf32(i + 65);
                    var imgcell = new GLDataGridViewCellImage(TestControls.Properties.Resources.GoBackward);
                    imgcell.Style.ContentAlignment = ContentAlignment.MiddleLeft;
                    imgcell.Size = new Size(16, 16);
                    row.AddCell(imgcell);
                    row.AddCell(new GLDataGridViewCellText($"{prefix} R{i,2}C1 long bit of text for it to wrap again and again and again"));
                    var but = new GLButton("EmbBut" + i, new Rectangle(0, 0, 30, 15), "But" + i);
                    row.AddCell(new GLDataGridViewCellText($"{i}"));
                    dgv.AddRow(row);
                }

                var butcel = new GLDataGridViewCellButton(new Rectangle(0, 0, 80, 24), "Buttext");
                butcel.MouseClick += (e2, e3) => { System.Diagnostics.Debug.WriteLine("Click on grid button"); };
                butcel.Style.Padding = new PaddingType(3);
                butcel.Style.ContentAlignment = ContentAlignment.MiddleLeft;
                dgv.Rows[0].AddCell(butcel);

                dgv.Rows[1].Height = 40;

                //  dgv.Rows[1].Cells[0].Selected = true;

                {
                    GLContextMenu cm = new GLContextMenu("CMContent");
                    GLMenuItem cm1 = new GLMenuItem("CM1A", "Menu-1");
                    GLMenuItem cm2 = new GLMenuItem("CM1B", "Menu-2");
                    cm2.CheckOnClick = true;
                    GLMenuItem cm3 = new GLMenuItem("CM1C", "Menu-3");
                    cm.Add(cm1);
                    cm.Add(cm2);
                    cm.Add(cm3);
                    cm.Opening += (e1, tag) =>
                    {
                        GLDataGridView.RowColPos g = (GLDataGridView.RowColPos)tag;
                        System.Diagnostics.Debug.WriteLine($"Open menu content at {g.Row} {g.Column} {g.Location}");
                    };

                    dgv.ContextMenuGrid = cm;
                }

                {
                    GLContextMenu cm = new GLContextMenu("CMColheader");
                    GLMenuItem cm1 = new GLMenuItem("CM1A", "Colheader1");
                    GLMenuItem cm2 = new GLMenuItem("CM1B", "ColHeader2");
                    cm.Add(cm1);
                    cm.Add(cm2);
                    cm.Opening += (e1, tag) =>
                    {
                        GLDataGridView.RowColPos g = (GLDataGridView.RowColPos)tag;
                        System.Diagnostics.Debug.WriteLine($"Open menu col header at {g.Row} {g.Column} {g.Location}");
                    };

                    dgv.ContextMenuColumnHeaders = cm;
                }

                {
                    GLContextMenu cm = new GLContextMenu("CMRowheader");
                    GLMenuItem cm1 = new GLMenuItem("CM1A", "RowHeader-1");
                    cm1.Click = (ctrlb) =>
                    {
                        GLMessageBox msg = new GLMessageBox("Confirm", displaycontrol, new Point(int.MinValue, 0), "Ag", "Warning",
                                        GLMessageBox.MessageBoxButtons.OKCancel);
                    };
                    GLMenuItem cm2 = new GLMenuItem("CM1B", "RowHeader-2");
                    cm.Add(cm1);
                    cm.Add(cm2);
                    cm.Opening += (e1, tag) =>
                    {
                        GLDataGridView.RowColPos g = (GLDataGridView.RowColPos)tag;
                        System.Diagnostics.Debug.WriteLine($"Open menu row header at {g.Row} {g.Column} {g.Location}");
                    };

                    dgv.ContextMenuRowHeaders = cm;
                }

                dgv.MouseClickOnGrid += (r, c, e1) => { System.Diagnostics.Debug.WriteLine($"Mouse click on grid {r} {c}"); };

                dgv.SelectedRow += (rw, state) => {
                    System.Diagnostics.Debug.WriteLine($"Row Selected {rw.Index} {state}");
                    var rowset = dgv.GetSelectedRows();
                    foreach (var r in rowset)
                        System.Diagnostics.Debug.WriteLine($".. Row {r.Index} selected");

                };
                dgv.SelectedCell += (cell, state) => {
                    System.Diagnostics.Debug.WriteLine($"Cell Selected {cell.RowParent.Index} {cell.Index} ");
                    var cellset = dgv.GetSelectedCells();
                    foreach (var c in cellset)
                        System.Diagnostics.Debug.WriteLine($".. Cell {c.RowParent.Index} {c.Index} ");
                };
                dgv.SelectionCleared += () => { System.Diagnostics.Debug.WriteLine($"Selection cleared"); };
                pform.Add(dgv);
            }

            {
                GLToolTip tip = new GLToolTip("ToolTip");
                displaycontrol.Add(tip);
            }

            systemtimer.Interval = 25;
            systemtimer.Tick += new EventHandler(SystemTick);
            systemtimer.Start();
        }

        private void SystemTick(object sender, EventArgs e)
        {
            PolledTimer.ProcessTimers();
            displaycontrol.Animate(glwfc.ElapsedTimems);
            if (displaycontrol != null && displaycontrol.RequestRender)
                glwfc.Invalidate();
            gl3dcontroller.HandleKeyboardSlewsAndInvalidateIfMoved(true);
        }

        private void Controller3dDraw(Controller3D mc, ulong unused)
        {
            ((GLMatrixCalcUniformBlock)items.UB("MCUB")).SetFull(gl3dcontroller.MatrixCalc);        // set the matrix unform block to the controller 3d matrix calc.

            rObjects.Render(glwfc.RenderState, gl3dcontroller.MatrixCalc);

            this.Text = "Looking at " + gl3dcontroller.MatrixCalc.LookAt + " eye@ " + gl3dcontroller.MatrixCalc.EyePosition + " dir " + gl3dcontroller.PosCamera.CameraDirection + " Dist " + gl3dcontroller.MatrixCalc.EyeDistance + " Zoom " + gl3dcontroller.PosCamera.ZoomFactor;
        }

        private void buttonAddRow_Click(object sender, EventArgs e)
        {
            var row = dgv.CreateRow();
            // row.AutoSize = true;
            int i = dgv.Rows.Count;
            row.AddCell(new GLDataGridViewCellText($"R{i}C0"));
            row.AddCell(new GLDataGridViewCellText($"R{i}C1"));
            dgv.AddRow(row);

        }

        private void buttonInsertRow1_Click(object sender, EventArgs e)
        {
            var row = dgv.CreateRow();
            // row.AutoSize = true;
            int i = dgv.Rows.Count;
            row.AddCell(new GLDataGridViewCellText($"R{i}C0"));
            row.AddCell(new GLDataGridViewCellText($"R{i}C1"));
            dgv.AddRow(row,1);
        }

        private void buttonRemoveRow1_Click(object sender, EventArgs e)
        {
            dgv.RemoveRow(1);

        }

        private void buttonRemoveCol0_Click(object sender, EventArgs e)
        {
            dgv.RemoveColumn(0);
        }

        private void buttonAddCol0_Click(object sender, EventArgs e)
        {
            var col0 = dgv.CreateColumn();
            col0.Width = 200;
            col0.Text = "Col0";
            dgv.AddColumn(col0);

        }

        private void buttonAddCell_Click(object sender, EventArgs e)
        {
            for( int r = 1; r < 10; r++)
            {
                var row = dgv.Rows[r];
                var cell = new GLDataGridViewCellText($"R{r}CX long bit of text for it to wrap again and again and again over and over again until it takes a long number of lines");
                cell.Style.TextFormat = (r % 2 == 0) ? StringFormatFlags.NoWrap : 0;
                row.AddCell(cell);
            }
        }

        private void buttonSizeA_Click(object sender, EventArgs e)
        {
            pform.Size = new Size(800, 600);
        }

        private void buttonSizeB_Click(object sender, EventArgs e)
        {
            pform.Size = new Size(800, 700);
        }

        private void buttonDisableTextCol0_Click(object sender, EventArgs e)
        {
            dgv.Columns[0].ShowHeaderText = !dgv.Columns[0].ShowHeaderText;
        }

        private void buttonDisableTextRow1_Click(object sender, EventArgs e)
        {
            dgv.Rows[1].ShowHeaderText = !dgv.Rows[1].ShowHeaderText;

        }

        private void buttonToggleColumnWidthAdjust_Click(object sender, EventArgs e)
        {
            dgv.AllowUserToResizeColumns = !dgv.AllowUserToResizeColumns;
        }

        private void buttonToggleFillMode_Click(object sender, EventArgs e)
        {
            dgv.ColumnFillMode = dgv.ColumnFillMode == GLDataGridView.ColFillMode.FillWidth ? GLDataGridView.ColFillMode.Width : GLDataGridView.ColFillMode.FillWidth;
        }

        private void buttonColumnHeightAdjust_Click(object sender, EventArgs e)
        {
            dgv.AllowUserToResizeColumnHeight = !dgv.AllowUserToResizeColumnHeight;

        }

        private void buttonSelR1C1_Click(object sender, EventArgs e)
        {
            dgv.Rows[1].Cells[1].Selected = !dgv.Rows[1].Cells[1].Selected;
        }

        private void buttonToggleR1Sel_Click(object sender, EventArgs e)
        {
            dgv.Rows[1].Selected = !dgv.Rows[1].Selected;
        }

        private void buttonSelRows_Click(object sender, EventArgs e)
        {
            dgv.AllowUserToSelectRows = !dgv.AllowUserToSelectRows;
            buttonSelRows.ForeColor = dgv.AllowUserToSelectRows ? Color.Green : Color.Red;
        }

        private void buttonDragRows_Click(object sender, EventArgs e)
        {
            dgv.AllowUserToSelectMultipleRows = !dgv.AllowUserToSelectMultipleRows;
            buttonDragRows.ForeColor = dgv.AllowUserToSelectMultipleRows ? Color.Green : Color.Red;
        }

        private void buttonSelectCells_Click(object sender, EventArgs e)
        {
            dgv.AllowUserToSelectCells = !dgv.AllowUserToSelectCells;
            buttonSelectCells.ForeColor = dgv.AllowUserToSelectCells ? Color.Green : Color.Red;
        }

        private void buttonToggleDragCells_Click(object sender, EventArgs e)
        {
            dgv.AllowUserToDragSelectCells = !dgv.AllowUserToDragSelectCells;
            buttonToggleDragCells.ForeColor = dgv.AllowUserToDragSelectCells ? Color.Green : Color.Red;
        }

        private void buttonClearSel_Click(object sender, EventArgs e)
        {
            dgv.ClearSelection();
        }

        private void buttonSelCellSelRows_Click(object sender, EventArgs e)
        {
            dgv.SelectCellSelectsRow = !dgv.SelectCellSelectsRow;
            buttonSelCellSelRows.ForeColor = dgv.SelectCellSelectsRow ? Color.Green : Color.Red;
        }

        private void buttonToggleSort_Click(object sender, EventArgs e)
        {
            dgv.AllowUserToSortColumns = !dgv.AllowUserToSortColumns;
            buttonToggleSort.ForeColor = dgv.AllowUserToSortColumns ? Color.Green : Color.Red;
        }

        private void buttonDumpSelection_Click(object sender, EventArgs e)
        {
            foreach( var c in dgv.GetSelectedCells())
            {
                System.Diagnostics.Debug.WriteLine($"Selected {c.RowParent.Index} {c.Index}");
            }

        }

        private void buttonColHeader_Click(object sender, EventArgs e)
        {
            dgv.ColumnHeaderEnable = !dgv.ColumnHeaderEnable;

        }

        private void buttonRowHeader_Click(object sender, EventArgs e)
        {
            dgv.RowHeaderEnable = !dgv.RowHeaderEnable;
        }

        private void buttonHorzScroll_Click(object sender, EventArgs e)
        {
            dgv.HorizontalScrollVisible = !dgv.HorizontalScrollVisible;
            buttonHorzScroll.ForeColor = dgv.HorizontalScrollVisible ? Color.Green : Color.Red;
        }

        private void buttonVertScroll_Click(object sender, EventArgs e)
        {
            dgv.VerticalScrollVisible = !dgv.VerticalScrollVisible;
            buttonVertScroll.ForeColor = dgv.VerticalScrollVisible ? Color.Green : Color.Red;

        }
    }
}


