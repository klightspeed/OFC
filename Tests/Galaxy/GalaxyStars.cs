﻿using GLOFC;
using GLOFC.GL4;
using OpenTK.Graphics.OpenGL4;
using OpenTK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using GLOFC.Utils;
using GLOFC.GL4.Shaders;
using GLOFC.GL4.Shaders.Vertex;
using GLOFC.GL4.Shaders.Geo;
using GLOFC.GL4.Shaders.Fragment;
using GLOFC.GL4.Shaders.Stars;
using GLOFC.GL4.Buffers;
using GLOFC.GL4.ShapeFactory;

namespace TestOpenTk
{
    class GalaxyStars
    {
        public Vector3 CurrentPos { get; set; } = new Vector3(-1000000, -1000000, -1000000);
        public Font Font { get; set; } = new Font("Ms Sans Serif", 14f);
        public Color ForeText { get; set; } = Color.White;
        public Color BackText { get; set; } = Color.Red;

        private const int MaxObjectsAllowed = 1000000;
        private const int MaxObjectsMargin = 1000;
        private const int SectorSize = 100;
        private const int MaxRequestedSectors = 10;

        public GalaxyStars(GLItemsList items, GLRenderProgramSortedList rObjects, float sunsize, GLStorageBlock findbufferresults)
        {
            sunvertex = new GLPLVertexShaderModelCoordWorldAutoscale(new Color[] { Color.FromArgb(255, 220, 220, 10), Color.FromArgb(255, 0,0,0) },
                autoscale: 50, autoscalemin: 1f, autoscalemax: 50f, useeyedistance: false);
            var sunfrag = new GLPLStarSurfaceFragmentShader();
            sunshader = items.NewShaderPipeline(null,sunvertex, sunfrag);

            shapebuf = items.NewBuffer(false);
            var shape = GLSphereObjectFactory.CreateSphereFromTriangles(2, sunsize);
            shapebuf.AllocateFill(shape);

            GLRenderState starrc = GLRenderState.Tri();     // render is triangles, with no depth test so we always appear
            starrc.DepthTest = true;
            starrc.DepthClamp = true;

            var textrc = GLRenderState.Tri();
            textrc.DepthTest = true;
            textrc.ClipDistanceEnable = 1;  // we are going to cull primitives which are deleted

            int texunitspergroup = 16;
            var textshader = items.NewShaderPipeline(null, new GLPLVertexShaderMatrixTriStripTexture(), new GLPLFragmentShaderTexture2DIndexMulti(0, 0, true, texunitspergroup));
 
            slset = new GLSetOfObjectsWithLabels("SLSet", rObjects, texunitspergroup, 100, 10,
                                                            sunshader, shapebuf, shape.Length, starrc, OpenTK.Graphics.OpenGL4.PrimitiveType.Triangles,
                                                            textshader, new Size(128, 32), textrc, SizedInternalFormat.Rgba8);

            items.Add(slset);

            var geofind = new GLPLGeoShaderFindTriangles(findbufferresults, 16);
            findshader = items.NewShaderPipeline(null, sunvertex, null, null, geofind , null, null, null);
        }

        public void Start()
        {
            requestorthread = new Thread(Requestor);
            requestorthread.Start();
        }

        public void Stop()
        {
            //System.Diagnostics.Debug.WriteLine("Request stop on gal stars");
            stop.Cancel();
            requestorthread.Join();
            while(subthreadsrunning > 0)
            {
                System.Diagnostics.Debug.WriteLine("Sub thread running");
                Thread.Sleep(100);
            }
            System.Diagnostics.Debug.WriteLine("Stopped on gal stars");
        }

        public void Request9BoxConditional(Vector3 newpos)
        {
            if ((CurrentPos - newpos).Length >= SectorSize && requestedsectors.Count < MaxRequestedSectors )
            {
                //if (CurrentPos.Z < -100000)
                //    CurrentPos = newpos;
                //newpos = new Vector3(CurrentPos.X, CurrentPos.Y, CurrentPos.Z + 300);

                Request9x3Box(newpos);
            }
        }

        public void Request9x3Box(Vector3 pos)
        {
            CurrentPos = pos;
            //System.Diagnostics.Debug.WriteLine($"Request 9 box ${pos}");

            for (int i = 0; i <= 2; i++)
            {
                int y = i == 0 ? 0 : i == 1 ? SectorSize : -SectorSize;
                Request(new Vector3(pos.X , pos.Y + y, pos.Z));
                Request(new Vector3(pos.X + SectorSize, pos.Y + y, pos.Z));
                Request(new Vector3(pos.X - SectorSize, pos.Y + y, pos.Z));
                Request(new Vector3(pos.X, pos.Y+y, pos.Z + SectorSize));
                Request(new Vector3(pos.X, pos.Y + y, pos.Z - SectorSize));
                Request(new Vector3(pos.X + SectorSize, pos.Y + y, pos.Z + SectorSize));
                Request(new Vector3(pos.X + SectorSize, pos.Y + y, pos.Z - SectorSize));
                Request(new Vector3(pos.X - SectorSize, pos.Y + y, pos.Z + SectorSize));
                Request(new Vector3(pos.X - SectorSize, pos.Y + y, pos.Z - SectorSize));
            }
            //System.Diagnostics.Debug.WriteLine($"End 9 box");
        }

        // send the request to the requestor using a blocking queue
        private void Request(Vector3 pos)
        {
            int mm = 100000;
            pos.X = (int)(pos.X + mm) / SectorSize * SectorSize - mm;
            pos.Y = (int)(pos.Y + mm) / SectorSize * SectorSize - mm;
            pos.Z = (int)(pos.Z + mm) / SectorSize * SectorSize - mm;

            if (!slset.TagsToBlocks.ContainsKey(pos))
            {
                slset.ReserveTag(pos);      // important, stops repeated adds in the situation where it takes a while to add to set
                
                // use normally
                requestedsectors.Add(new Sector(pos));

            }
            else
            {
                //System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 100000} {pos} request rejected");
            }
        }

        // do this in a thread, as adding threads is computationally expensive so we don't want to do it in the foreground
        private void Requestor()
        {
            while (true)
            {
                try
                {
                    //  System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 100000} Requestor take");
                    var sector = requestedsectors.Take(stop.Token);       // blocks until take or told to stop

                    do
                    {
                        // reduce memory use first by bitmap cleaning 
                        while (cleanbitmaps.TryDequeue(out Sector sectoclean))
                        {
                            // System.Diagnostics.Debug.WriteLine($"Clean bitmap for {sectoclean.pos}");
                            GLOFC.Utils.BitMapHelpers.Dispose(sectoclean.bitmaps);
                            sectoclean.bitmaps = null;
                        }

                        System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 100000} {sector.pos} requestor accepts");

                        Interlocked.Add(ref subthreadsrunning, 1);      // commited to run this, so count subthreads, on shutdown, we need to wait until they all complete

                        Thread p = new Thread(FillSectorThread);
                        p.Start(sector);

                        while (subthreadsrunning > 16)
                            Thread.Sleep(100);


                    } while (requestedsectors.TryTake(out sector));     // until empty..

                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            //System.Diagnostics.Debug.WriteLine("Exit requestor");
        }

        // in a thread, look up the sector 
        private void FillSectorThread(Object seco)
        {
            Sector d = (Sector)seco;

          //  System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 100000} {d.pos} {tno} start");
            Thread.Sleep(10);

            Vector4[] array = new Vector4[500];
            string[] text = new string[array.Length];
            Random rnd = new Random((int)(d.pos.X * d.pos.Y) + 1);
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new Vector4(d.pos.X + rnd.Next(SectorSize), d.pos.Y + rnd.Next(SectorSize), d.pos.Z + rnd.Next(SectorSize), 0);
                text[i] = $"({d.pos.X},{d.pos.Y},{d.pos.Z})-{i}";
            }

            d.stars = array;       
            d.text = text;
            d.bitmaps = GLOFC.Utils.BitMapHelpers.DrawTextIntoFixedSizeBitmaps(slset.LabelSize, text, Font, System.Drawing.Text.TextRenderingHint.ClearTypeGridFit, ForeText, BackText, 0.5f);
            d.textpos = GLPLVertexShaderMatrixTriStripTexture.CreateMatrices(array, new Vector3(0, -2f, 0), new Vector3(2f, 0, 0.4f), new Vector3(-90F.Radians(), 0, 0), true, false);

            generatedsectors.Enqueue(d);       // d has been filled
            //System.Diagnostics.Debug.WriteLine($"{Environment.TickCount % 100000} {d.pos} {tno} end");

            Interlocked.Add(ref subthreadsrunning, -1);
        }

        ulong timelastadded = 0;

        // foreground, called on each frame, allows update of shader and queuing of new objects
        public void Update(ulong time, float eyedistance)
        {
            if (time - timelastadded > 50)
            {
                if (generatedsectors.Count > 0)
                {
                    int max = 5;
                    while (max-- > 0 && generatedsectors.TryDequeue(out Sector d) )      // limit fill rate..
                    {
                        System.Diagnostics.Debug.WriteLine($"Add to set {d.pos}");
                        slset.Add(d.pos, d.text, d.stars, d.textpos, d.bitmaps);
                        System.Diagnostics.Debug.WriteLine($"..add complete {d.pos} {slset.Objects}" );
                        cleanbitmaps.Enqueue(d);            // ask for cleaning of these bitmaps
                        timelastadded = time;
                    }
                }

                if ( slset.Objects > MaxObjectsAllowed )
                {
                    slset.RemoveUntil(MaxObjectsAllowed-MaxObjectsMargin);
                }
            }

            const int rotperiodms = 10000;
            time = time % rotperiodms;
            float fract = (float)time / rotperiodms;
            float angle = (float)(2 * Math.PI * fract);
            sunvertex.ModelTranslation = Matrix4.CreateRotationY(-angle);
            float scale = Math.Max(1, Math.Min(4, eyedistance / 5000));
            //     System.Diagnostics.Debug.WriteLine("Scale {0}", scale);
            sunvertex.ModelTranslation *= Matrix4.CreateScale(scale);           // scale them a little with distance to pick them out better
        }

        public SystemClass Find( Point loc, GLRenderState rs, Size viewportsize ,out float z)
        {
            z = 0;
            var find = slset.FindBlock(findshader, rs, loc, viewportsize);
            if ( find != null )
            {
                System.Diagnostics.Debug.WriteLine($"SLSet {find.Item2} {find.Item3} {find.Item4} {find.Item5}");
                var userdata = slset.UserData[find.Item1[0].tag] as string[];

                System.Diagnostics.Debug.WriteLine($"... {userdata[find.Item4]}");
                return new SystemClass() { Name = userdata[find.Item4] };
            }
            else
                return null;
        }


        private GLSetOfObjectsWithLabels slset; // main class holding drawing

        private GLShaderPipeline sunshader;     // sun drawer
        private GLPLVertexShaderModelCoordWorldAutoscale sunvertex;
        private GLBuffer shapebuf;

        private GLShaderPipeline findshader;    // find shader for lookups

        private class Sector
        {
            public Vector3 pos;
            public Sector(Vector3 pos) { this.pos = pos; }

            // generated by thread, passed to update, bitmaps pushed to cleanbitmaps and deleted by requestor
            public Vector4[] stars;
            public string[] text;
            public Matrix4[] textpos;
            public Bitmap[] bitmaps;
        }

        // requested sectors from foreground to requestor
        private BlockingCollection<Sector> requestedsectors = new BlockingCollection<Sector>();

        // added to by subthread when sector is ready, picked up by foreground update. ones ready for final foreground processing
        private ConcurrentQueue<Sector> generatedsectors = new ConcurrentQueue<Sector>();

        // added to by update when cleaned up bitmaps, requestor will clear these for it
        private ConcurrentQueue<Sector> cleanbitmaps = new ConcurrentQueue<Sector>();

        private Thread requestorthread;
        private CancellationTokenSource stop =  new CancellationTokenSource();
        private int subthreadsrunning = 0;
    }

}
//}
