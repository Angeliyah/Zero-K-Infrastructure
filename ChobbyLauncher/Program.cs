﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ZkData;

namespace ChobbyLauncher
{
    internal static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());


            string chobbyTag = null;
            string engineOverride = null;
            if (args.Length > 0)
            {
                if (args[0] == "--help" || args[0] == "-h" || args[0] == "/?")
                {
                    MessageBox.Show("chobby.exe [rapid_tag] [engine_override] \n\nUse chobby:stable or chobby:test\nTo run local dev version use chobby.exe dev");
                }
                chobbyTag = args[0];
                if (args.Length > 1) engineOverride = args[1];
            }

            var startupPath = Path.GetDirectoryName(Path.GetFullPath(Application.ExecutablePath));
            if (!SpringPaths.IsDirectoryWritable(startupPath))
            {
                MessageBox.Show("Please move this program to a writable folder", "Cannot write to startup folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();

            try
            {

                var chobbyla = new Chobbyla(startupPath, chobbyTag, engineOverride);
                var cf = new ChobbylaForm(chobbyla) { StartPosition = FormStartPosition.CenterScreen };
                if (cf.ShowDialog() == DialogResult.OK)
                {
                    chobbyla.Run().Wait();
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error starting chobby: {0}", ex);
                MessageBox.Show(ex.ToString(), "Error starting Chobby", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }
    }
}