﻿using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using GLOFC;
using GLOFC.Controller;
using GLOFC.GL4;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using GLOFC.GL4.Controls;
using EliteDangerousCore.EDSM;
using System.IO;
using Newtonsoft.Json.Linq;
using GLOFC.Utils;

namespace TestOpenTk
{
    public partial class TestGalaxy : Form
    {
        public TestGalaxy()
        {
            InitializeComponent();

            glwfc = new GLOFC.WinForm.GLWinFormControl(glControlContainer,null,4,6);

            systemtimer.Interval = 32;
            systemtimer.Tick += new EventHandler(SystemTick);
        }

        private GLOFC.WinForm.GLWinFormControl glwfc;

        private Timer systemtimer = new Timer();

        private GalacticMapping edsmmapping;
        private GalacticMapping eliteRegions;

        private Map map;
        private MapSaverTest mapdefaults;


        /// ////////////////////////////////////////////////////////////////////////////////////////////////////

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Closed += ShaderTest_Closed;

            edsmmapping = new GalacticMapping();
            string text = System.Text.Encoding.UTF8.GetString(Properties.Resources.galacticmapping);
            edsmmapping.ParseJson(text);                            // at this point, gal map data has been uploaded - get it into memory

            eliteRegions = new GalacticMapping();
            text = System.Text.Encoding.UTF8.GetString(Properties.Resources.EliteGalacticRegions);
            eliteRegions.ParseJson(text);                            // at this point, gal map data has been uploaded - get it into memory

            mapdefaults = new MapSaverTest();
            mapdefaults.ReadFromDisk(@"c:\code\mapdef.txt");

            map = new Map();
            map.Start(glwfc, edsmmapping, eliteRegions);
            map.LoadState(mapdefaults);

            systemtimer.Start();
        }

        private void ShaderTest_Closed(object sender, EventArgs e)
        {
            map.SaveState(mapdefaults);
            mapdefaults.WriteToDisk(@"c:\code\mapdef.txt");
            map.Dispose();
            GLStatics.VerifyAllDeallocated();
            glwfc.Dispose();
            glwfc = null;
        }

        private void SystemTick(object sender, EventArgs e)
        {
            PolledTimer.ProcessTimers();
            map.Systick();
        }
    }

    public class MapSaverTest : MapSaver
    {
        private Dictionary<string, Object> settings = new Dictionary<string, object>();

        public void ReadFromDisk(string file)
        {
            if ( File.Exists(file))
            {
                string s = File.ReadAllText(file);
                JToken jk = JToken.Parse(s);
                settings = jk.ToObject<Dictionary<string,Object>>();
            }
        }

        public void WriteToDisk(string file)
        {
            JToken jk = JToken.FromObject(settings);
            string s = jk.ToString(Newtonsoft.Json.Formatting.Indented);
            System.Diagnostics.Debug.WriteLine(s);
            File.WriteAllText(file, s);
        }
            
        public T GetSetting<T>(string id, T defaultvalue)
        {
            if (settings.ContainsKey(id))
            {
                return (T)settings[id];
            }
            else
                return defaultvalue;
        }

        public void PutSetting<T>(string id, T value)
        {
            settings[id] = value;
        }
    }


}


