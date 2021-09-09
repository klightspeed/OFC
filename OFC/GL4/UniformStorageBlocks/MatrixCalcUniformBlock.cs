﻿/*
 * Copyright 2019-2020 Robbyxp1 @ github.com
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
using OpenTK.Graphics.OpenGL4;
using System;

namespace GLOFC.GL4
{
    // Matrix calc UB block at 0 fixed. See matrixcalc.glsl for definition in glsl

    public class GLMatrixCalcUniformBlock : GLUniformBlock 
    {
        public int MatrixCalcUse { get; } = Mat4size * 3 + Vec4size * 2 + sizeof(float) * 4 + Mat4size; 

        public const int BindingPoint = 0;// 0 is the fixed binding block for matrixcal

        private int lastmccount = int.MinValue;

        public GLMatrixCalcUniformBlock() : base(BindingPoint)         
        {
        }

        public void SetMinimal(GLMatrixCalc c)
        {
            if (lastmccount != c.CountMatrixCalcs)
            {
                if (NotAllocated)
                    AllocateBytes(MatrixCalcUse, BufferUsageHint.DynamicCopy);

                StartWrite(0, Length);        // the whole schebang
                Write(c.ProjectionModelMatrix);
                StopReadWrite();                                // and complete..
                lastmccount = c.CountMatrixCalcs;
            }
        }

        public void Set(GLMatrixCalc c)
        {
            if (lastmccount != c.CountMatrixCalcs)
            {
                if (NotAllocated)
                    AllocateBytes(MatrixCalcUse, BufferUsageHint.DynamicCopy);

                StartWrite(0, Length);        // the whole schebang
                Write(c.ProjectionModelMatrix);     // 0- 63
                Write(c.ProjectionMatrix);          // 64-127
                Write(c.ModelMatrix);               // 128-191
                Write(c.TargetPosition, 0);         // 192-207
                Write(c.EyePosition, 0);            // 208-223
                Write(c.EyeDistance);               // 224-239
                StopReadWrite();                                // and complete..
                lastmccount = c.CountMatrixCalcs;
            }
        }

        public void SetText(GLMatrixCalc c) 
        {
            if (lastmccount != c.CountMatrixCalcs)
            {
                if (NotAllocated)
                    AllocateBytes(MatrixCalcUse, BufferUsageHint.DynamicCopy);
                StartWrite(0, Length);              // the whole schebang
                Write(c.ProjectionModelMatrix);     //0, 64 long
                Write(c.ProjectionMatrix);          //64, 64 long
                Write(c.ModelMatrix);               //128, 64 long
                Write(c.TargetPosition, 0);         //192, vec4, 16 long
                Write(c.EyePosition, 0);            // 208, vec4, 16 long
                Write(c.EyeDistance);               // 224, float, 4 long
                Write(c.MatrixScreenCoordToClipSpace());                // 240-303, into the project model matrix slot, used for text
                StopReadWrite();   // and complete..
                lastmccount = c.CountMatrixCalcs;
            }
        }
    }

}

