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

using OpenTK.Graphics.OpenGL4;
using System;
using System.Drawing;

namespace GLOFC.GL4.Textures
{
    /// <summary>
    /// These classes handle textures
    /// </summary>
    internal static class NamespaceDoc { } // just for documentation purposes

    /// <summary>
    /// A bindless texture buffer
    /// This is a Data Block (a Buffer) with ArbID handles in it.
    /// </summary>

    public class GLBindlessTextureHandleBlock : GLDataBlock
    {
        /// <summary> Create a bindless texture handle block on this binding index </summary>
        public GLBindlessTextureHandleBlock(int bindingindex) : base(bindingindex, false, BufferRangeTarget.UniformBuffer)
        {
        }

        /// <summary> Create a bindless texture handle block on this binding index with this list of textures. Block is filled with ARBIDs </summary>
        public GLBindlessTextureHandleBlock(int bindingindex, IGLTexture[] textures) : base(bindingindex, false, BufferRangeTarget.UniformBuffer)
        {
            WriteHandles(textures);
        }

        /// <summary> Write ARBIDs to the bindless texture handle block </summary>
        public void WriteHandles(IGLTexture[] textures)
        {
            AllocateStartWrite(sizeof(long) * textures.Length * 2);

            for (int i = 0; i < textures.Length; i++)
            {
                Write(textures[i].AcquireArbId());    // ARBS are stored as 128 bit numbers, so two longs
                Write((long)0);
            }

            StopReadWrite();
            System.Diagnostics.Debug.Assert(GLOFC.GLStatics.CheckGL(out string glasserterr), glasserterr);
        }
    }
}

