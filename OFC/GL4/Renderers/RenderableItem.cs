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
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GLOFC.GL4
{
    ///<summary>
    /// Standard renderable item supporting Instancing and draw count, vertex arrays, instance data, element indexes, indirect command buffers
    /// It has a primitive type for Render
    /// It may be associated with a RenderControl state, which is to be applied before executing. If this is null, then the render state is not changed.
    /// It is associated with an optional VertexArray which is bound using Bind()
    /// It is associated with an optional InstanceData which is instanced using Bind()
    /// It is associated with an optional ElementBuffer giving vertex indices for all vertex inputs - this then selects the draw type to use
    /// It is associated with an optional IndirectBuffer giving draw command groups - this then selected the draw type to use
    /// It is associated with an optional ParameterBuffer (4.6) giving draw data
    /// Renderable items are normally put into a GLRenderProgramSortedList by shader, but can be executed directly
    /// using Execute.  This is normally only done for renders which do not produce output but compute to a buffer.
    /// Supplied a large range of static creator functions which make a renderable item out of supplied vertex data
    /// We can draw either arrays (A); element index (E); indirect arrays (IA); indirect element index (IE)
    /// type is controlled by if ElementBuffer and/or IndirectBuffer is non null
    ///</summary> 

    [System.Diagnostics.DebuggerDisplay("RI {PrimitiveType} d{DrawCount} i{InstanceCount}")]
    public class GLRenderableItem : IGLRenderableItem
    {
        ///<summary>Visble flag</summary>
        public bool Visible { get; set; } = true;

        ///<summary>for Render, the primitive to issue</summary>
        public PrimitiveType PrimitiveType { get; set; }

        /// <summary> Render State. It may be null - if so no explicit render state to apply, use current state </summary>
        public GLRenderState RenderState { get; set; }

        /// <summary> Vertex Array to bind. It may be null - if so no vertex data. Does not own so it needs to be added to Items list on creation </summary>
        public IGLVertexArray VertexArray { get; set; }

        /// <summary> A+E : Draw count, IE+IA MultiDraw count, ICA+ICE Maximum draw count(don't exceed buffer size when setting this)</summary>
        public int DrawCount { get; set; } = 0;                           

        /// <summary> A+E: Instances (not used in indirect - this comes from the buffer)</summary>
        public int InstanceCount { get; set; } = 1;                       
        /// <summary>A+E: Base Instance, normally 0 (not used in indirect - this comes from the buffer) </summary>
        public int BaseInstance { get; set; } = 0;                         

        /// <summary> E+IE: if non null, we doing a draw using a element buffer to control indexes</summary>
        public GLBuffer ElementBuffer { get; set; }                        
        /// <summary>IA+IE: if non null, we doing a draw using a indirect buffer to control draws </summary>
        public GLBuffer IndirectBuffer { get; set; }                       
        /// <summary>ICA+ICE: if non null, we doing a draw using a parameter buffer to indicate count (needs indirect buffer) </summary>
        public GLBuffer ParameterBuffer { get; set; }                    

        /// <summary>E+IE: for element draws, its index type (byte/short/uint) </summary>
        public DrawElementsType ElementIndexSize { get; set; }            

        /// <summary> ICA+ICE: Where in the parameter buffer is the control data</summary>
        public int ParameterBufferOffset { get; set; }                     

        /// <summary> 
        /// E: for element draws, first index element in the element index buffer to use, offset to use different groups. 
        /// IE+IA+ICA+ICE: offset in buffer in bytes to first command entry 
        /// </summary>
        public int BaseIndexOffset { get; set; }

        /// <summary>E: for element draws (but not indirect) first vertex to use in the buffer (not used in indirect - this comes from the buffer) </summary>
        public int BaseVertex { get; set; }                                 

        /// <summary>IE+IA: distance between each command buffer entry (default is we use the maximum of elements+array structures) in bytes </summary>
        public int MultiDrawCountStride { get; set; } = 20;                 

        /// <summary>Called on bind, use to bind extra data such as textures or set up uniforms
        /// may be null - no specific render data. Does not own. </summary>
        public IGLRenderItemData RenderData { get; set; }

        /// <summary>TF: if set, do drawtransformfeedback.  Do not use any of the A,E,IA,IE,ICA,ICE variables </summary>
        public GLTransformFeedback TFObj { get; set; }                
        /// <summary>TFStream associated with transform feedback </summary>
        public int TFStream { get; set; } = 0;

        /// <summary>Create a renderable item.  </summary>
        /// <param name="pt">The primitive type to render</param>
        /// <param name="rc">The render state to enforce for this draw</param>
        /// <param name="drawcount">Number of draws</param>
        /// <param name="va">Vertex array to bind</param>
        /// <param name="id">Render data to bind at the draw</param>
        /// <param name="ic">Instance count</param>
        public GLRenderableItem(PrimitiveType pt, GLRenderState rc, int drawcount, IGLVertexArray va, IGLRenderItemData id = null, int ic = 1)
        {
            PrimitiveType = pt;
            RenderState = rc;
            DrawCount = drawcount;
            VertexArray = va;
            RenderData = id;
            InstanceCount = ic;
        }

        /// <summary> Called before Render (normally by RenderList::Render) to set up data for the render.
        /// currentstate may be null, meaning, don't apply
        /// RenderState may be null, meaning don't change</summary>
        public void Bind(GLRenderState currentstate, IGLProgramShader shader, GLMatrixCalc matrixcalc)      
        {
            if (currentstate != null && RenderState != null)    // if either null, it means the last render state applied is the same as our render state, so no need to apply
                currentstate.ApplyState(RenderState,shader.Name);           // else go to this state

            VertexArray?.Bind();                                // give the VA a chance to bind to GL
            RenderData?.Bind(this,shader,matrixcalc);           // optional render data supplied by the user to bind
            ElementBuffer?.BindElement();                       // if we have an element buffer, give it a chance to bind
            IndirectBuffer?.BindIndirect();                     // if we have an indirect buffer, give it a chance to bind
            ParameterBuffer?.BindParameter();                   // if we have a parameter buffer, give it a chance to bind

            System.Diagnostics.Debug.Assert(GLOFC.GLStatics.CheckGL(out string glasserterr2), glasserterr2+shader.Name);
        }

        /// <summary>Render - submit a draw to GL with the render data provided.  Parameters select the draw type submitted.
        /// Bind must have been called first.
        /// The shader must already be set up - RenderableLists sets the shader up before calling this</summary>
        public void Render()                                               
        {
            //System.Diagnostics.Debug.WriteLine("Draw " + RenderControl.PrimitiveType + " " + DrawCount + " " + InstanceCount);

            if ( TFObj != null)                                     // transform feedback call
            {
                GL.DrawTransformFeedbackStreamInstanced(PrimitiveType, TFObj.Id,TFStream,InstanceCount);
            }
            else if ( ElementBuffer != null )                            // we are picking the GL call, dependent on what is bound to the render
            {
                System.Diagnostics.Debug.Assert(GL.GetInteger(GetPName.ElementArrayBufferBinding) == ElementBuffer.Id);

                if (IndirectBuffer != null)                         // IE or ICE indirect element index
                {
                    System.Diagnostics.Debug.Assert(GL.GetInteger(GetPName.DrawIndirectBufferBinding) == IndirectBuffer.Id);

                    if (ParameterBuffer != null)                    // 4.6 feature ICE
                    {
                        // https://www.khronos.org/registry/OpenGL/extensions/ARB/ARB_indirect_parameters.txt
                        // Says the parameter buffer offset is an offset into the buffer
                        // same as MultiDrawElementsIndirect 
                       
                        GL.MultiDrawElementsIndirectCount(PrimitiveType,(All)ElementIndexSize, (IntPtr)BaseIndexOffset, (IntPtr)ParameterBufferOffset, DrawCount, MultiDrawCountStride);
                    }
                    else
                    {                                               // IE
                        // says indirect is an offset if Indirect buffer is bound 
                        GL.MultiDrawElementsIndirect(PrimitiveType, ElementIndexSize, (IntPtr)BaseIndexOffset, DrawCount, MultiDrawCountStride);
                        //int b = BaseIndexOffset;  GL.MultiDrawElementsIndirect(PrimitiveType, ElementIndexSize, ref b, DrawCount, MultiDrawCountStride); // fails..
                    }
                }
                else
                {                                                   // E element index
                    // says same as glDrawElementsInstanced, says same as DrawElements, DrawElements does not say
                    GL.DrawElementsInstancedBaseVertexBaseInstance(PrimitiveType, DrawCount, ElementIndexSize, (IntPtr)BaseIndexOffset, 
                                                                    InstanceCount, BaseVertex, BaseInstance);
                }
            }
            else
            {
                if (IndirectBuffer != null)                         // IA or ICA indirect buffer
                {
                    System.Diagnostics.Debug.Assert(GL.GetInteger(GetPName.DrawIndirectBufferBinding) == IndirectBuffer.Id);

                    if (ParameterBuffer != null)                    // 4.6 feature ICA
                    {
                        // https://www.khronos.org/registry/OpenGL/extensions/ARB/ARB_indirect_parameters.txt
                        // ParameterBufferOffset defines an offset into the parameter buffer
                        // behaves like MultiDrawArraysIndirect.
                        // MultiDrawArraysIndirect says if the buffer is bound, BaseIndexOffset is an offset. 
                        // this does not work: int b = BaseIndexOffset;  GL.MultiDrawArraysIndirectCount<int>(PrimitiveType, ref b, (IntPtr)ParameterBufferOffset, DrawCount, MultiDrawCountStride); // fails?
                        
                        GL.MultiDrawArraysIndirectCount(PrimitiveType, (IntPtr)BaseIndexOffset, (IntPtr)ParameterBufferOffset, DrawCount, MultiDrawCountStride);
                    }
                    else
                    {                                               // IA
                        // explicity states if a buffer is bound to INDIRECT_BUFFER Then IntPtr is an offset into buffer in base machine units
                        GL.MultiDrawArraysIndirect(PrimitiveType, (IntPtr)BaseIndexOffset, DrawCount, MultiDrawCountStride);
                    }
                }
                else
                {                                                   // A no indirect or element buffer, direct draw

                    GL.DrawArraysInstancedBaseInstance(PrimitiveType, 0, DrawCount, InstanceCount, BaseInstance);       // Type A
                }
            }
        }

        #region These create a new RI with vertex arrays into buffers, lots of them 

        /// <summary>Vector4, Color4, optional instance data and count
        /// in attribute 0 and 1 setup vector4 and vector4 colours</summary>
        public static GLRenderableItem CreateVector4Color4(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector4[] vectors, Color4[] colours, IGLRenderItemData id = null, int ic = 1)
        {
            var vb = items.NewBuffer();                                     // they all follow this pattern, grab a buffer (unless supplied)
            vb.AllocateBytes(GLBuffer.Vec4size * vectors.Length * 2);       // allocate
            vb.Fill(vectors);                                               // fill
            vb.Fill(colours, vectors.Length);

            var va = items.NewArray();                                      // vertex array
            vb.Bind(va, 0, vb.Positions[0], 16);                            // buffer bind to vertex array at bindingpoint 0, bufferpos, stride
            va.Attribute(0, 0, 4, VertexAttribType.Float);                  // bind bindingpoint 0 to attribute index 0 (in shader), 4 components, float

            vb.Bind(va, 1, vb.Positions[1], 16);                            // buffer bind to vertex array at bindingpoint 1, bufferpos, stride
            va.Attribute(1, 1, 4, VertexAttribType.Float);                  // bind bindingpoint 1 to attribute index 1 (in shader), 4 components, float

            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);    // create new RI
        }

        /// <summary>Vector4, in attribute 0</summary> 
        public static GLRenderableItem CreateVector4(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector4[] vectors, 
                                        IGLRenderItemData id = null, int ic = 1)
        {
            var vb = items.NewBuffer();
            vb.AllocateFill(vectors);

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 16);        // bind buffer to binding point 0
            va.Attribute(0, 0, 4, VertexAttribType.Float);  // bind binding point 0 to attribute point 0 with 4 float components
            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }

        /// <summary> Use a buffer for Vector4 elements in attribute 0</summary>
        public static GLRenderableItem CreateVector4(GLItemsList items, PrimitiveType prim, GLRenderState pt, GLBuffer vb, int drawcount, int pos = 0, 
                                        IGLRenderItemData id = null, int ic = 1)
        {
            var va = items.NewArray();
            vb.Bind(va, 0, pos, 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);
            return new GLRenderableItem(prim,pt, drawcount, va, id, ic);
        }

        /// <summary> Two vectors4 in same buffer. Second vector can be instance divided. Drawcount set to vectors length. In attributes 0,1</summary>
        public static GLRenderableItem CreateVector4Vector4(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector4[] vectors, Vector4[] secondvector,
                                        IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            var vb = items.NewBuffer();
            vb.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Vec4size * secondvector.Length);
            vb.Fill(vectors);
            vb.Fill(secondvector);

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);

            vb.Bind(va, 1, vb.Positions[1], 16, seconddivisor);
            va.Attribute(1, 1, 4, VertexAttribType.Float);
            return new GLRenderableItem(prim, pt, vectors.Length, va, id, ic);
        }
        /// <summary> Two Vector4s, in buffers. Second vector can be instance divided.  In attributes 0,1</summary>
        public static GLRenderableItem CreateVector4Vector4(GLItemsList items, PrimitiveType prim, GLRenderState pt, GLBuffer buf1, int drawcount, GLBuffer buf2, 
                                        IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            var va = items.NewArray();
            buf1.Bind(va, 0, buf1.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);

            buf2.Bind(va, 1, buf2.Positions[0], 16, seconddivisor);
            va.Attribute(1, 1, 4, VertexAttribType.Float);
            return new GLRenderableItem(prim,pt, drawcount, va, id, ic);
        }

        /// <summary> Two Vector4s, and a Vector2 in one buffer. Second and Third vector can be instance divided.  In attributes 0,1,2</summary>
        public static GLRenderableItem CreateVector4Vector4Vector2(GLItemsList items, PrimitiveType prim, GLRenderState pt,
                                        Vector4[] vectors, Vector4[] secondvector, Vector2[] thirdvector,
                                        IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0, int thirddivisor = 0)
        {
            var vb = items.NewBuffer();
            vb.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Vec4size * secondvector.Length + GLBuffer.Vec2size * thirdvector.Length);   
            vb.Fill(vectors);
            vb.Fill(secondvector);
            vb.Fill(thirdvector);

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);

            vb.Bind(va, 1, vb.Positions[1], 16, seconddivisor);
            va.Attribute(1, 1, 4, VertexAttribType.Float);

            vb.Bind(va, 2, vb.Positions[2], 8, thirddivisor);
            va.Attribute(2, 2, 2, VertexAttribType.Float);

            return new GLRenderableItem(prim, pt, vectors.Length, va, id, ic);
        }

        /// <summary> Two Vector4s, and a Vector2. Second given in buffer. First/Second in one buffer. Second and Third vector can be instance divided.  In attributes 0,1,2</summary>
        public static GLRenderableItem CreateVector4Vector4Vector2(GLItemsList items, PrimitiveType prim, GLRenderState pt,
                                        Vector4[] vectors, GLBuffer secondvector, int secondoffset, Vector2[] thirdvector,
                                        IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0, int thirddivisor = 0)
        {
            var vb = items.NewBuffer();
            vb.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Vec2size * thirdvector.Length);    // vec2 aligned vec4
            vb.Fill(vectors);
            vb.Fill(thirdvector);

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);

            secondvector.Bind(va, 1, secondoffset, 16, seconddivisor);
            va.Attribute(1, 1, 4, VertexAttribType.Float);

            vb.Bind(va, 2, vb.Positions[1], 8, thirddivisor);
            va.Attribute(2, 2, 2, VertexAttribType.Float);

            return new GLRenderableItem(prim, pt, vectors.Length, va, id, ic);
        }

        /// <summary> Two Vector4s, in separate buffers. Second vector can be instance divided.  In attributes 0,1</summary>
        public static GLRenderableItem CreateVector4Vector4Buf2(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector4[] vectors, Vector4[] secondvector, 
                                        IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            var v1 = items.NewBuffer();
            v1.AllocateBytes(GLBuffer.Vec4size * vectors.Length );
            v1.Fill(vectors);

            var v2 = items.NewBuffer();
            v2.AllocateBytes(GLBuffer.Vec4size * secondvector.Length);
            v2.Fill(secondvector);

            var va = items.NewArray();
            v1.Bind(va, 0, v1.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);

            v2.Bind(va, 1, v2.Positions[0], 16, seconddivisor);
            va.Attribute(1, 1, 4, VertexAttribType.Float);
            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }

        /// <summary> Two Vector4s, second in a given buffer. Second vector can be instance divided.  In attributes 0,1</summary>
        public static GLRenderableItem CreateVector4Vector4(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector4[] vectors, GLBuffer buf2, int bufoff = 0, 
                                        IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            var vb = items.NewBuffer();
            vb.AllocateBytes(GLBuffer.Vec4size * vectors.Length);
            vb.Fill(vectors);

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);

            buf2.Bind(va, 1, bufoff, 16, seconddivisor);
            va.Attribute(1, 1, 4, VertexAttribType.Float);
            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }

        /// <summary> Two Vector4s, both in buffers. Second vector can be instance divided.  In attributes 0,1</summary>
        public static GLRenderableItem CreateVector4Vector4(GLItemsList items, PrimitiveType prim, GLRenderState pt, GLBuffer buf1, int buf1off, int drawcount, GLBuffer buf2, int buf2off = 0, 
                                        IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            var va = items.NewArray();
            buf1.Bind(va, 0, buf1off, 16);          // binding index 0
            va.Attribute(0, 0, 4, VertexAttribType.Float);  // attribute 0

            buf2.Bind(va, 1, buf2off, 16, seconddivisor);   // binding index 1
            va.Attribute(1, 1, 4, VertexAttribType.Float);  // attribute 1
            return new GLRenderableItem(prim,pt,drawcount, va, id, ic);
        }

        /// <summary> Vector4 and Vector2. Second vector can be instance divided.  In attributes 0,1</summary>
        public static GLRenderableItem CreateVector4Vector2(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector4[] vectors, Vector2[] coords, 
                                        IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            var vb = items.NewBuffer();
            vb.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Vec2size * coords.Length);
            vb.Fill(vectors);
            vb.Fill(coords);

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);
            vb.Bind(va, 1, vb.Positions[1], 8, seconddivisor);
            va.Attribute(1, 1, 2, VertexAttribType.Float);
            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }

        /// <summary> Vector4 and Vector2 in one buffer, with buffer given and positions given. Second vector can be instance divided.  In attributes 0,1</summary>
        public static GLRenderableItem CreateVector4Vector2(GLItemsList items, PrimitiveType prim, GLRenderState pt, GLBuffer vb, int pos1, int pos2, int drawcount, 
                                            IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);
            vb.Bind(va, 1, vb.Positions[1], 8, seconddivisor);
            va.Attribute(1, 1, 2, VertexAttribType.Float);
            return new GLRenderableItem(prim,pt, drawcount, va, id, ic);
        }

        /// <summary> Vector4 and Vector2 in a tuple, into one buffer.  In attributes 0,1</summary>
        public static GLRenderableItem CreateVector4Vector2(GLItemsList items, PrimitiveType prim, GLRenderState pt, Tuple<Vector4[], Vector2[]> vectors, 
                                            IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            return CreateVector4Vector2(items, prim, pt, vectors.Item1, vectors.Item2, id, ic, seconddivisor);
        }

        /// <summary> Vector4 and Vector2 and index array in a tuple. Element index is created. In attributes 0,1. No primitive restart</summary>
        public static GLRenderableItem CreateVector4Vector2Indexed(GLItemsList items, PrimitiveType prim, GLRenderState pt, Tuple<Vector4[], Vector2[], uint[]> vectors, 
                                            IGLRenderItemData id = null, int ic = 1, int seconddivisor = 0)
        {
            var dt = GL4Statics.DrawElementsTypeFromMaxEID((uint)vectors.Item1.Length - 1);
            var ri = CreateVector4Vector2(items, prim, pt, vectors.Item1, vectors.Item2, id, ic, seconddivisor);
            ri.CreateElementIndex(items.NewBuffer(), vectors.Item3, dt);
            return ri;
        }

        /// <summary> Vector4 and Vector2 and Vector4 in a tuple. See Vector4Vector2Vector4</summary>
        public static GLRenderableItem CreateVector4Vector2Vector4(GLItemsList items, PrimitiveType prim, GLRenderState pt,
                                                                   Tuple<Vector4[],Vector2[]> pos, 
                                                                    Vector4[] instanceposition,
                                                                   IGLRenderItemData id = null, int ic = 1,
                                                                   bool separbuf = false, int divisorinstance = 1)
        {
            return CreateVector4Vector2Vector4(items, prim, pt, pos.Item1, pos.Item2, instanceposition, id, ic, separbuf, divisorinstance);
        }

        ///<summary> Vector4, Vector2 and Vector4 in buffers.  First is model, second is coords, third is instance positions
        /// if separbuffer = true it makes as separate buffer for instanceposition
        /// if separbuffer = true and instanceposition is null, you fill up the separbuffer yourself outside of this (maybe auto generated).  
        /// You can get this buffer using items.LastBuffer()
        ///Attributes 0,1,4 set up.  </summary>
        public static GLRenderableItem CreateVector4Vector2Vector4(GLItemsList items, PrimitiveType prim, GLRenderState pt,
                                                                   Vector4[] vectors, Vector2[] coords, Vector4[] instanceposition,
                                                                   IGLRenderItemData id = null, int ic = 1,
                                                                   bool separbuf = false, int divisorinstance = 1)
        {
            var va = items.NewArray();
            var vbuf1 = items.NewBuffer();
            var vbuf2 = vbuf1;
            int posi = 2;

            if (separbuf)
            {
                vbuf1.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Vec2size * coords.Length);
                vbuf2 = items.NewBuffer();

                if (instanceposition != null)
                    vbuf2.AllocateBytes(GLBuffer.Vec4size * instanceposition.Length);

                posi = 0;
            }
            else
            {
                vbuf1.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Vec2size * coords.Length + GLBuffer.Vec4size * instanceposition.Length + GLBuffer.Vec4size);    // due to alignment, add on a little
            }

            vbuf1.Fill(vectors);
            vbuf1.Fill(coords);
            if (instanceposition != null)
                vbuf2.Fill(instanceposition);

            vbuf1.Bind(va, 0, vbuf1.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);

            vbuf1.Bind(va, 1, vbuf1.Positions[1], 8);
            va.Attribute(1, 1, 2, VertexAttribType.Float);

            vbuf2.Bind(va, 2, instanceposition!=null ? vbuf2.Positions[posi] : 0, 16, divisorinstance);
            va.Attribute(2, 2, 4, VertexAttribType.Float);

            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }


        ///<summary> Vector4, Vector2 and Matrix4 in buffers.  First is model, second is coords, third is instance matrix translation
        /// if separbuffer = true and instancematrix is null, it makes a buffer for you to fill up externally.
        /// if separbuffer = true it makes as separate buffer for instancematrix
        /// if separbuffer = true and instancematrix is null, you fill up the separbuffer yourself outside of this (maybe auto generated).  
        /// You can get this buffer using items.LastBuffer()
        /// Attributes in 0,1,4-7 set up.</summary>

        public static GLRenderableItem CreateVector4Vector2Matrix4(GLItemsList items, PrimitiveType prim, GLRenderState pt, 
                                                                    Vector4[] vectors, Vector2[] coords, Matrix4[] instancematrix, 
                                                                    IGLRenderItemData id = null, int ic = 1, 
                                                                    bool separbuf = false, int matrixdivisor = 1)
        {
            var va = items.NewArray();
            GLBuffer vbuf1 = items.NewBuffer();
            GLBuffer vbuf2 = vbuf1;
            int posi = 2;

            if (separbuf)
            {
                vbuf1.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Vec2size * coords.Length);

                vbuf2 = items.NewBuffer();

                if (instancematrix != null)
                    vbuf2.AllocateBytes(GLBuffer.Mat4size * instancematrix.Length);

                posi = 0;
            }
            else
            { 
                vbuf1.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Vec2size * coords.Length + GLBuffer.Vec4size + GLBuffer.Mat4size * instancematrix.Length);    // due to alignment, add on a little
            }

            vbuf1.Fill(vectors);
            vbuf1.Fill(coords);
            if ( instancematrix != null )
                vbuf2.Fill(instancematrix);

            vbuf1.Bind(va, 0, vbuf1.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);      // bp 0 at 0 

            vbuf1.Bind(va, 1, vbuf1.Positions[1], 8);
            va.Attribute(1, 1, 2, VertexAttribType.Float);      // bp 1 at 1

            va.MatrixAttribute(2, 4);                           // bp 2 at 4-7
            vbuf2.Bind(va, 2, instancematrix != null ? vbuf2.Positions[posi]: 0, 64, matrixdivisor);     // use a binding 

            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }

        ///<summary> Vector4, Vector2 and Matrix4 in buffers.  First is model, second is coords, third is instance matrix translation
        /// All are supplied by buffer references
        /// Attributes in 0,1,4-7 set up.</summary>

        public static GLRenderableItem CreateVector4Vector2Matrix4(GLItemsList items, PrimitiveType prim, GLRenderState pt,
                                                                    GLBuffer vbuf1, GLBuffer vbuf2, GLBuffer vbuf3,
                                                                    int vertexcount,
                                                                    IGLRenderItemData id = null, int ic = 1,
                                                                    int matrixdivisor = 1,
                                                                    int buf1pos = 0, int buf2pos = 0, int buf3pos = 0)
        {
            var va = items.NewArray();

            vbuf1.Bind(va, 0, buf1pos, 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);      // bp 0 at 0 

            vbuf2.Bind(va, 1, buf2pos, 8);
            va.Attribute(1, 1, 2, VertexAttribType.Float);      // bp 1 at 1

            va.MatrixAttribute(2, 4);                           // bp 2 at 4-7
            vbuf3.Bind(va, 2, buf3pos, 64, matrixdivisor);            // use a binding 

            return new GLRenderableItem(prim, pt, vertexcount, va, id, ic);
        }


        ///<summary> Vector4 and Matrix4.  
        /// Attributes in 0,4-7 set up.</summary>
        public static GLRenderableItem CreateVector4Matrix4(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector4[] vectors, Matrix4[] matrix, 
                                            IGLRenderItemData id = null, int ic = 1, int matrixdivisor = 1)
        {
            var vb = items.NewBuffer();

            vb.AllocateBytes(GLBuffer.Vec4size * vectors.Length + GLBuffer.Mat4size * matrix.Length);
            vb.Fill(vectors);
            vb.Fill(matrix);

            var va = items.NewArray();

            vb.Bind(va, 0, vb.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);      // bp 0 at attrib 0

            vb.Bind(va, 2, vb.Positions[1], 64, matrixdivisor);     // use a binding 
            va.MatrixAttribute(2, 4);                           // bp 2 at attribs 4-7

            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }

        ///<summary> Vector4 and Matrix4. Matrix4 is a buffer reference.
        /// Attributes in 0,4-7 set up.</summary>
        public static GLRenderableItem CreateVector4Matrix4(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector4[] vectors, GLBuffer matrix, 
                                            IGLRenderItemData id = null, int ic = 1, int matrixdivisor = 1)
        {
            var vb = items.NewBuffer();
            vb.AllocateFill(vectors);       // push in model vectors

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);      // bp 0 at attrib 0

            matrix.Bind(va, 2, matrix.Positions[0], 64, matrixdivisor);     // use a binding 
            va.MatrixAttribute(2, 4);                           // bp 2 at attribs 4-7

            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }

        ///<summary> Vector4 and Matrix4. Both are buffer references.
        /// Attributes in 0,4-7 set up.</summary>
        public static GLRenderableItem CreateVector4Matrix4(GLItemsList items, PrimitiveType prim, GLRenderState pt, GLBuffer shape, GLBuffer matrix, int drawcount, 
                                            IGLRenderItemData id = null, int ic = 1, int matrixdivisor = 1)
        {
            var va = items.NewArray();
            shape.Bind(va, 0, shape.Positions[0], 16);
            va.Attribute(0, 0, 4, VertexAttribType.Float);      // bp 0 at attrib 0

            matrix.Bind(va, 2, matrix.Positions[0], 64, matrixdivisor);     // use a binding 
            va.MatrixAttribute(2, 4);                           // bp 2 at attribs 4-7

            return new GLRenderableItem(prim,pt, drawcount, va, id, ic);
        }

        ///<summary> Matrix4. 
        /// Attributes in 4-7 set up.</summary>
        public static GLRenderableItem CreateMatrix4(GLItemsList items, PrimitiveType prim, GLRenderState pt, Matrix4[] matrix, int drawcount, 
                                            IGLRenderItemData id = null, int ic = 1, int matrixdivisor = 1)
        {
            var vb = items.NewBuffer();

            vb.AllocateBytes(GLBuffer.Mat4size * matrix.Length);
            vb.Fill(matrix);

            var va = items.NewArray();

            vb.Bind(va, 2, vb.Positions[0], 64, matrixdivisor);     // use a binding 
            va.MatrixAttribute(2, 4);                           // bp 2 at attribs 4-7

            return new GLRenderableItem(prim,pt, drawcount, va, id, ic);
        }

        ///<summary> Matrix4 as a buffer reference.
        /// Attributes in 4-7 set up.</summary>
        public static GLRenderableItem CreateMatrix4(GLItemsList items, PrimitiveType prim, GLRenderState pt, GLBuffer vb, int bufoffset, int drawcount, 
                                            IGLRenderItemData id = null, int ic = 1, int matrixdivisor = 1)
        {
            var va = items.NewArray();
            vb.Bind(va, 2, bufoffset, 64, matrixdivisor);     // use a binding 
            va.MatrixAttribute(2, 4);                           // bp 2 at attribs 4-7
            return new GLRenderableItem(prim,pt, drawcount, va, id, ic);
        }

        ///<summary> Vector3 packed using GLBuffer.FillPacked2vec. Offsets and mult specify range
        /// Attributes in 0.</summary>
        public static GLRenderableItem CreateVector3Packed2(GLItemsList items, PrimitiveType prim, GLRenderState pt, Vector3[] vectors, Vector3 offsets, float mult, 
                                                IGLRenderItemData id = null, int ic = 1)
        {
            var vb = items.NewBuffer();
            vb.AllocateBytes(sizeof(uint) * 2 * vectors.Length);
            vb.FillPacked2vec(vectors, offsets, mult);

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], 8);
            va.AttributeI(0, 0, 2, VertexAttribType.UnsignedInt);

            return new GLRenderableItem(prim,pt, vectors.Length, va, id, ic);
        }

        ///<summary> Floats packed into buffer with configurable component number (1,2,4)
        /// Attributes in 0.</summary>
        public static GLRenderableItem CreateFloats(GLItemsList items, PrimitiveType prim, GLRenderState pt, float[] floats, int components, 
                                                IGLRenderItemData id = null, int ic = 1)
        {
            var vb = items.NewBuffer();
            vb.AllocateFill(floats);
            //float[] ouwt = vb.ReadFloats(0, floats.Length); // test read back

            var va = items.NewArray();
            vb.Bind(va, 0, vb.Positions[0], sizeof(float)*components);    
            va.Attribute(0, 0, components, VertexAttribType.Float);

            return new GLRenderableItem(prim,pt, floats.Length / components, va, id, ic);
        }

        ///<summary> Null Vertex without any data.  No data into GL pipeline.
        /// Used when a uniform buffer gives info for the vertex shader to create vertices
        /// Or when the vertex shader makes up its own vertexes from draw/instance counts </summary>

        public static GLRenderableItem CreateNullVertex(PrimitiveType prim, GLRenderState pt, IGLRenderItemData id = null, int drawcount =1,  int instancecount = 1)
        {
            return new GLRenderableItem(prim, pt, drawcount, null, id, instancecount);  // no vertex data.
        }

        #endregion

        #region Create element indexs for this RI. Normally called after Create..

        ///<summary> A set of rectangle indexes of reccount rectangles, with a restart after each rectangle, in Byte indexes </summary>

        public void CreateRectangleElementIndexByte(GLBuffer elementbuf, int reccount, uint restartindex = 0xff)
        {
            ElementBuffer = elementbuf;
            ElementBuffer.FillRectangularIndicesBytes(reccount, restartindex);
            ElementIndexSize = DrawElementsType.UnsignedByte;
            DrawCount = ElementBuffer.Length - 1;       // -1 because we do not need the last restart index to be processed
            //byte[] b = elementbuf.ReadBuffer(0, elementbuf.BufferSize); // test read back
        }

        ///<summary> A set of rectangle indexes of reccount rectangles, with a restart after each rectangle, in short indexes </summary>
        public void CreateRectangleElementIndexUShort(GLBuffer elementbuf, int reccount, uint restartindex = 0xffff)
        {
            ElementBuffer = elementbuf;
            ElementBuffer.FillRectangularIndicesShort(reccount, restartindex);
            ElementIndexSize = DrawElementsType.UnsignedShort;
            DrawCount = ElementBuffer.Length - 1;
        }

        ///<summary> Given a elementbuffer, fill with indexes from an array. Set ElementBuffer, ElementIndexSize, BaseIndexOffset, DrawCount </summary>
        public void CreateElementIndexByte(GLBuffer elementbuf, byte[] indexes, int base_index = 0)
        {
            ElementBuffer = elementbuf;
            ElementBuffer.AllocateFill(indexes);
            ElementIndexSize = DrawElementsType.UnsignedByte;
            BaseIndexOffset = base_index;
            DrawCount = indexes.Length;
        }

        ///<summary> Given a elementbuffer, fill with indexes from an array. Set ElementBuffer, ElementIndexSize, BaseIndexOffset, DrawCount </summary>
        public void CreateElementIndexUShort(GLBuffer elementbuf, ushort[] indexes, int base_index = 0)
        {
            ElementBuffer = elementbuf;
            ElementBuffer.AllocateFill(indexes);
            ElementIndexSize = DrawElementsType.UnsignedShort;
            BaseIndexOffset = base_index;
            DrawCount = indexes.Length;
        }

        ///<summary> Given a elementbuffer, fill with indexes from an array. Set ElementBuffer, ElementIndexSize, BaseIndexOffset, DrawCount </summary>
        public void CreateElementIndex(GLBuffer elementbuf, uint[] eids, int base_index = 0)
        {
            CreateElementIndex(elementbuf, eids, GL4Statics.DrawElementsTypeFromMaxEID(eids.Max()), base_index);
        }

        ///<summary> Given a elementbuffer, fill with indexes from an array. Drawtype sets the element index size. Set ElementBuffer, ElementIndexSize, BaseIndexOffset, DrawCount </summary>
        public void CreateElementIndex(GLBuffer elementbuf, uint[] eids, DrawElementsType drawtype, int base_index = 0)
        {
            ElementBuffer = elementbuf;

            if (drawtype == DrawElementsType.UnsignedByte)
            {
                ElementBuffer.AllocateFill(eids.Select(x => (byte)x).ToArray());
            }
            else if (drawtype == DrawElementsType.UnsignedShort)
            {
                ElementBuffer.AllocateFill(eids.Select(x => (ushort)x).ToArray());
                ElementIndexSize = DrawElementsType.UnsignedShort;
            }
            else
            {
                ElementBuffer.AllocateFill(eids.ToArray());
                ElementIndexSize = DrawElementsType.UnsignedInt;
            }

            ElementIndexSize = drawtype;
            BaseIndexOffset = base_index;
            DrawCount = eids.Length;
        }

        #endregion

        #region Execute directly outside of a render list

        ///<summary> Execute a RenderableItem. 
        /// Use to execute outside of the normal RenderableItemList system. For a compute shader for instance.  Or for a finder.
        /// appliedstate if not null overrides this.RenderState and uses that instead </summary>
        public void Execute(IGLProgramShader shader, GLRenderState glstate, GLMatrixCalc c = null, GLRenderState appliedstate = null, bool noshaderstart = false)
        {
            System.Diagnostics.Debug.Assert(glstate != null && shader != null);
            if (shader.Enable)
            {
                if (!noshaderstart)
                    shader.Start(c);

                GLRenderState curstate = RenderState;
                if ( appliedstate != null )
                    RenderState = appliedstate;

                Bind(glstate, shader, c);

                Render();

                if (!noshaderstart)
                    shader.Finish();

                RenderState = curstate;
            }
        }

        #endregion

    }

}

