﻿/*
 * Copyright 2019-2021 Robbyxp1 @ github.com
 * Part of the EDDiscovery Project
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
using GLOFC.GL4.Shaders.Basic;
using GLOFC.GL4.ShapeFactory;
using GLOFC.GL4.Textures;
using GLOFC.Utils;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Drawing;
using System.Windows.Forms;

// Demonstration without gl3dcontroller - of shader setting the render state not the RI's

namespace TestOpenTk
{
    public partial class TestShaderRenderState : Form
    {
        private GLOFC.WinForm.GLWinFormControl glwfc;

        GLRenderProgramSortedList rObjects = new GLRenderProgramSortedList();
        GLItemsList items = new GLItemsList();
        GLMatrixCalc matrixcalc;

        public TestShaderRenderState()
        {
            InitializeComponent();

            glwfc = new GLOFC.WinForm.GLWinFormControl(glControlContainer,null,4,6);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Closed += ShaderTest_Closed;

            // operate matrixcalc in +Z away mode (default), in perspective mode (default)
   
            matrixcalc = new GLMatrixCalc();                            // must make after GL window is made
            matrixcalc.ResizeViewPort(this, glwfc.Size);                // inform matrix calc of window size
            matrixcalc.CalculateModelMatrix(new Vector3(0, 0, 0), new Vector2(135, 0), 50, 0);  // set up the lookat position, the camera direction, the distance and rotation
            matrixcalc.CalculateProjectionMatrix();                     // and set the project matrix

            glwfc.Paint += ControllerDraw;    // register for draw

            // disposable items are stored in GLItemsList, so they can be cleanly disposed of at the end

            // make three stock shaders with names

            items.Add(new GLColorShaderWorld(), "COSW");
            items.Add(new GLColorShaderObjectTranslation(), "COSOT");
            items.Add( new GLTexturedShaderObjectTranslation(),"TEXOT");

            // make a texture from resources called dotted2

            items.Add(new GLTexture2D(Properties.Resources.dotted2, SizedInternalFormat.Rgba8), "dotted2");

            // render state for lines
            GLRenderState lines = GLRenderState.Lines();

            // make a set of vertices from the shape factory
            var rs1 = GLShapeObjectFactory.CreateLines(new Vector3(-100, -0, -100), new Vector3(-100, -0, 100), new Vector3(10, 0, 0), 21);

            // make a array of colours for the vertexes (note do not need a full set, the render creator will repeat them automatically)
            var rc1 = new Color4[] { Color.Red, Color.Red, Color.DarkRed, Color.DarkRed };

            // make a render item, indicating type (Lines), vertexes (rs1) and colours (rc1)
            var ri1 = GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Lines, lines, rs1, rc1);

            // add to render list - paint with this shader, and this render
            //            rObjects.Add(items.Shader("COSW"), ri1);

            // do more..

            rObjects.Add(items.Shader("COSW"),
                            GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Lines, lines,
                                GLShapeObjectFactory.CreateLines(new Vector3(-100, -0, -100), new Vector3(100, -0, -100), new Vector3(0, 0, 10), 21),
                                                    new Color4[] { Color.Red, Color.Red, Color.DarkRed, Color.DarkRed }));

            rObjects.Add(items.Shader("COSOT"), "p1",
                    GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Points, GLRenderState.Points(10),
                                   GLCubeObjectFactory.CreateVertexPointCube(10f), new Color4[] { Color.Red },
                             new GLRenderDataTranslationRotation(new Vector3(0, 5, 0))
                             ));

            rObjects.Add(items.Shader("COSOT"), "p2",
                    GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Points, GLRenderState.Points(5),
                                   GLCubeObjectFactory.CreateVertexPointCube(10f), new Color4[] { Color.Purple },
                             new GLRenderDataTranslationRotation(new Vector3(20, 5, 0))
                             ));

            {
                var shader = new GLColorShaderObjectTranslation();
                items.Add(shader);
                shader.RenderState = GLRenderState.Points(20);          // demonstrate render state set by shader for all ris

                rObjects.Add(shader, "pbig",
                          GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Points, null,
                                         GLCubeObjectFactory.CreateVertexPointCube(10f), new Color4[] { Color.Yellow },
                                   new GLRenderDataTranslationRotation(new Vector3(-20, 10, 0))
                                   ));

                rObjects.Add(shader, "pbig2",
                          GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Points, null,
                                         GLCubeObjectFactory.CreateVertexPointCube(10f), new Color4[] { Color.Magenta },
                                   new GLRenderDataTranslationRotation(new Vector3(-40, 10, 0))
                                   ));


            }

            rObjects.Add(items.Shader("COSOT"), "p3",
                    GLRenderableItem.CreateVector4Color4(items, PrimitiveType.Points, GLRenderState.Points(2),
                                   GLCubeObjectFactory.CreateVertexPointCube(10f), new Color4[] { Color.White },
                             new GLRenderDataTranslationRotation(new Vector3(40, 5, 0))
                             ));


            // make a Uniformblock to hold matrix info

            items.Add(new GLMatrixCalcUniformBlock(),"MCUB");     // def binding of 0
        }

        private void ShaderTest_Closed(object sender, EventArgs e)
        {
            items.Dispose();
            GLStatics.VerifyAllDeallocated();
        }

        // called on Paint of scene
        private void ControllerDraw(ulong unused)
        {
            //System.Diagnostics.Debug.WriteLine("Draw");

            var mcub = items.Get<GLMatrixCalcUniformBlock>("MCUB");
            mcub.SetFull(matrixcalc);       // need to store the matrixcalc information into the uniform block

            rObjects.Render(glwfc.RenderState, matrixcalc); // execute render
        }
    }
}


