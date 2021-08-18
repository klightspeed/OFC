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

using OFC.GL4;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OFC.GL4
{
    // class uses a GLVertexBufferIndirect to hold a vertex buffer and indirect commands, with multiple textures supplied to the shader
    // The object drawn is defined by its objectshader, and its model vertices are in objectbuffer (start of) of objectlength
    // Object shader will get vertex 0 = objectbuffer vector4s, and vertex 1 = worldpositions of items added (instance divided)
    // use with text shader GLShaderPipeline(new GLPLVertexShaderQuadTextureWithMatrixTranslation(), new GLPLFragmentShaderTexture2DIndexedMulti(0,0,true, texunitspergroup));
    // multiple textures can be bound to carry the text labels, the number given by textures.
    // dependent on the opengl, that gives the number of objects that can be produced (GetMaxTextureDepth())

    public class GLObjectsWithLabels : IDisposable
    {
        public Size LabelSize { get { return textures[0].Size; } }

        public int Blocks { get { return dataindirectbuffer.Indirects.Count > 0 ? dataindirectbuffer.Indirects[0].Positions.Count : 0; } }      // how many blocks allocated
        public int BlocksRemoved { get; private set; } = 0;     // how many have been removed
        public int Objects { get; private set; } = 0;           // total number of objects being drawn currently

        public GLRenderableItem ObjectRenderer { get; private set; }
        public GLRenderableItem TextRenderer { get; private set; }

        // get tag list. Removed blocks will have a null tag
        public List<object> Tags { get { return dataindirectbuffer.Indirects.Count > 0 ? dataindirectbuffer.Indirects[0].Tags : null; } }       

        private GLVertexBufferIndirect dataindirectbuffer;
        private GLTexture2DArray[] textures;
        private int objectvertexescount;
        private int textmapinuse = 0;

        GLItemsList items = new GLItemsList();      // our own item list to hold disposes

        public Tuple<GLRenderableItem,GLRenderableItem> Create(
                                int textures,       // number of 2D textures to allow maximum (limited by GL)
                                int estimateditemspergroup,      // estimated objects per group, this adds on vertext buffer space to allow for mat4 alignment. Smaller means more allowance.
                                int mingroups,     // minimum groups to have
                                GLBuffer objectbuffer, int objectvertexes , GLRenderControl objrc,   // object buffer, vertexes and its rendercontrol
                                Size texturesize, GLRenderControl textrc,   // texturesize and render control
                                int debuglimittexture = 0)  // use to limit texture map depth for debugging
        {
            this.objectvertexescount = objectvertexes;

            // Limit number of 2d textures in a single 2d array
            int maxtextper2darray = GL4Statics.GetMaxTextureDepth();
            if ( debuglimittexture > 0)
                maxtextper2darray = debuglimittexture;

            // set up number of textmaps bound
            int maxtexturesbound = GL4Statics.GetMaxFragmentTextures();
            int textmaps = Math.Min(textures, maxtexturesbound);

            // which then give us the number of stars we can do
            int objectcount = textmaps * maxtextper2darray;
            int groupcount = objectcount / estimateditemspergroup;      
            groupcount = Math.Max(mingroups, groupcount);               // min groups

            System.Diagnostics.Debug.WriteLine($"GLObjectWithLabels oc {objectcount} gc {groupcount}");

            // estimate maximum vert buffer needed, allowing for extra due to the need to align the mat4
            int vertbufsize = objectcount * (GLBuffer.Vec4size + GLBuffer.Mat4size) +       // for a vec4 + mat4 per object
                                groupcount * GLBuffer.Mat4size;         // and for groupcount Mat4 fragmentation per group

            // create the vertex indirect buffer
            dataindirectbuffer = new GLVertexBufferIndirect(items,vertbufsize, GLBuffer.WriteIndirectArrayStride * groupcount, true);

            // objects
            ObjectRenderer = GLRenderableItem.CreateVector4Vector4(items, objrc,
                                                                        objectbuffer, 0, 0,     // binding 0 is shapebuf, offset 0, no draw count yet
                                                                        dataindirectbuffer.Vertex, 0, // binding 1 is vertex's world positions, offset 0
                                                                        null, 0, 1);        // no ic, second divisor 1
            ObjectRenderer.BaseIndexOffset = 0;     // offset in bytes where commands are stored
            ObjectRenderer.MultiDrawCountStride = GLBuffer.WriteIndirectArrayStride;

            // text

            this.textures = new GLTexture2DArray[textmaps];

            for (int i = 0; i < this.textures.Length; i++)
            {
                int n = Math.Min(objectcount, maxtextper2darray);
                this.textures[i] = new GLTexture2DArray(texturesize.Width, texturesize.Height, n);
                items.Add(this.textures[i]);
                objectcount -= maxtextper2darray;
            }

            TextRenderer = GLRenderableItem.CreateMatrix4(items, textrc,
                                                                dataindirectbuffer.Vertex, 0, 0, //attach buffer with matrices, no draw count
                                                                new GLRenderDataTexture(this.textures,0),        // binding 0..N for textures
                                                                0, 1);     //no ic, and matrix divide so 1 matrix per vertex set
            TextRenderer.BaseIndexOffset = 0;     // offset in bytes where commands are stored
            TextRenderer.MultiDrawCountStride = GLBuffer.WriteIndirectArrayStride;


            return new Tuple<GLRenderableItem, GLRenderableItem>(ObjectRenderer, TextRenderer);
        }

        // array/text holds worldpositions and text of each object
        // tag gives a logical name to each group
        // returns position where it stopped, or -1 if all added

        public int Add(Object tag, Vector4[] array, string[] text, 
                                Font fnt, Color fore, Color back, 
                                Vector3 size, Vector3 rot, bool rotatetoviewer, bool rotateelevation,   // see GLPLVertexShaderQuadTextureWithMatrixTranslation.CreateMatrix
                                StringFormat fmt, float backscale, Vector3 textoffset)
        {
            var bmps = BitMapHelpers.DrawTextIntoFixedSizeBitmaps(LabelSize, text, fnt, System.Drawing.Text.TextRenderingHint.ClearTypeGridFit, fore, back, backscale, false, fmt);
            var mats = GLPLVertexShaderQuadTextureWithMatrixTranslation.CreateMatrices(array, textoffset, size, rot, rotatetoviewer, rotateelevation,0,0,0,true);
            int v = Add(tag, array, mats, bmps);
            BitMapHelpers.Dispose(bmps);
            return v;
        }

        // Add objects already drawn.  bitmaps are owned by caller, not by us
        // pos = indicates one to start from
        // -1 all added, else the pos where it failed on

        public int Add(Object tag, Vector4[] array, Matrix4[] matrix, Bitmap[] bitmaps, int pos = 0)
        {
            do
            {
                if (textmapinuse >= textures.Length)       // out of textures
                    return pos;

                // how many can we take..
                int touse = Math.Min(array.Length - pos, textures[textmapinuse].DepthLeftIndex);

                // fill in vertex array entries from pos .. pos+touse-1

                if (!dataindirectbuffer.Fill(array, pos, touse, 0, objectvertexescount, 0, touse, -1))  // indirect 0 holds object draws, objectvertexes long, touse objects, estimate base instance on position
                {
                    System.Diagnostics.Debug.WriteLine("GLObjectWithLabels failed to add object indirect");
                    return pos;
                }

                // now fill in the texture by loading bitmaps into each slot, and update the matrix image position
                for (int i = 0; i < touse; i++)
                {
                    int imgpos = textures[textmapinuse].DepthIndex + textmapinuse * 65536;      // bits 16+ has textmap
                   // System.Diagnostics.Debug.WriteLine($"Obj2 Write Mat {pos} {pos + i} tx {textmapinuse} = {imgpos}");
                    matrix[pos + i][0,3] = imgpos;
                    textures[textmapinuse].LoadBitmap(bitmaps[pos + i], textures[textmapinuse].DepthIndex++, false, 1);
                }

                // now write in the matrices to the vertex buffers
                dataindirectbuffer.Vertex.AlignMat4();          // instancing counts in mat4 sizes (mat4 0 @0, mat4 1 @ 64 etc) so align to it

                if (!dataindirectbuffer.Fill(matrix, pos, touse, 1, 4, 0, touse, -1))     // indirect 1 holds text draws, 4 vertices per draw, touse objects, estimate base instance on position
                {
                    System.Diagnostics.Debug.WriteLine("GLObjectWithLabels failed to add text indirect");
                    return pos;
                }

                dataindirectbuffer.Indirects[0].AddTag(tag);              // indirect draw buffer 0 holds the tags assigned by the user for identity purposes 
                dataindirectbuffer.Indirects[1].AddTag(array.Length);     // indirect draw buffer 1 holds the length

                ObjectRenderer.DrawCount = dataindirectbuffer.Indirects[0].Positions.Count;       // update draw count
                ObjectRenderer.IndirectBuffer = dataindirectbuffer.Indirects[0];                  // and buffer

                TextRenderer.DrawCount = dataindirectbuffer.Indirects[1].Positions.Count;
                TextRenderer.IndirectBuffer = dataindirectbuffer.Indirects[1];

                Objects += array.Length;                                                        // this more objects

                if (textures[textmapinuse].DepthLeftIndex == 0)                                 // out of bitmap space, next please!
                    textmapinuse++;

                pos += touse;

            } while (pos < array.Length);

            return -1;
        }

        public bool Remove(Predicate<object> test)
        {
            bool removed = false;

            if (dataindirectbuffer.Indirects.Count > 0)     // only if we added something..
            {
                for (int i = 0; i < dataindirectbuffer.Indirects[0].Tags.Count; i++)        // all blocks
                {
                    var tg = dataindirectbuffer.Indirects[0].Tags[i];           // get tag

                    if (tg != null)       // if not already removed
                    {
                        if (test(tg))       // if test passed, it wants it to be removed
                        {
                            int count = (int)dataindirectbuffer.Indirects[1].Tags[i];
                            // System.Diagnostics.Debug.WriteLine($"Found tag at {i}");
                            dataindirectbuffer.Remove(i, 0);        // clear draw of both text and object
                            dataindirectbuffer.Remove(i, 1);
                            Objects -= count;                   // reduce objects count
                            BlocksRemoved++;                    // increment blocks removed
                            dataindirectbuffer.Indirects[0].Tags[i] = null;     // remove tag so it can't be found again!
                            removed = true;
                        }
                    }
                }
            }

            return removed;
        }

        public void Dispose()
        {
            items.Dispose();
        }

    }
}
