﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Neo.IronLua;
using PlasmaDownloader;
using ZkData;

namespace ChobbyLauncher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        
        static void Main(string[] args)
        {
            var rootPath = Path.GetFullPath(Application.ExecutablePath);

            var startupPath = Path.GetDirectoryName(rootPath);
            var paths = new SpringPaths(startupPath, false);
            var down = new PlasmaDownloader.PlasmaDownloader(new SpringScanner(paths), paths);

            var tag = "chobby:test";

            var chd = down.GetResource(DownloadType.MOD, tag);
            chd?.WaitHandle.WaitOne();

            string engineVersion = null;

            var ver = down.PackageDownloader.GetByTag(tag);
            if (ver != null)
            {
                var mi = ver.ReadFile(paths, "modinfo.lua");
                var lua = new Lua();
                var luaEnv = lua.CreateEnvironment();
                dynamic result = luaEnv.DoChunk(new StreamReader(mi), "dummy.lua");
                engineVersion = result.engine;
            }

            
            var eng = down.GetResource(DownloadType.ENGINE, engineVersion);
            eng?.WaitHandle.WaitOne();

            var chobbyla = new Chobbyla();
            chobbyla.LaunchChobby(paths, ver.InternalName, engineVersion);

        //    Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());

        }
    }
}
