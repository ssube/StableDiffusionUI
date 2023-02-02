﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace SD_FXUI
{
    internal class CMD
    {
        public static async Task ProcessRunner(string command, int TotalCount, Helper.UpscalerType Type, int UpSize)
        {
            Host ProcesHost = new Host();
            ProcesHost.Print("\n Startup generation..... \n");

            for (int i = 0; i < TotalCount; i++)
            {
                ProcesHost.Start();
                // FX: Dirty hack for cache 
                string WorkDir = MainWindow.CachePath;
                ProcesHost.Send(command);
                ProcesHost.SendExistCommand();
                ProcesHost.Wait();

                //  process.WaitForInputIdle();
                var Files = FS.GetFilesFrom(FS.GetModelDir(), new string[] { "png", "jpg" }, false);
                foreach (var file in Files)
                {
                    string NewFilePath = MainWindow.ImgPath + System.IO.Path.GetFileName(file);
                    System.IO.File.Move(file, MainWindow.ImgPath + System.IO.Path.GetFileName(file));

                    MainWindow.Form.UpdateViewImg(NewFilePath);

                    if (UpSize > 0)
                    {
                        await UpscalerRunner(Type, UpSize, NewFilePath);
                    }
                }
            }

            ProcesHost.Print("\n  Generation Done..... \n");
        }

        public static async Task UpscalerRunner(Helper.UpscalerType Type, int Size, string File)
        {
            string DopCmd = "4";
            DopCmd = " -s " + DopCmd;

            string FileName = FS.GetToolsDir();

            switch (Type)
            {
                case Helper.UpscalerType.ESRGAN:
                    {
                        FileName += @"\realesrgan\realesrgan-ncnn-vulkan.exe";
                        DopCmd += " -n realesrgan-x4plus";
                        break;
                    }

                case Helper.UpscalerType.ESRGAN_ANIME:
                    {
                        FileName += @"\realesrgan\realesrgan-ncnn-vulkan.exe";

                        DopCmd += " -n realesrgan-x4plus-anime";
                        break;
                    }
                case Helper.UpscalerType.SR:
                    {
                        FileName += @"\realsr.exe\realsr-ncnn-vulkan.exe";
                        break;
                    }
                case Helper.UpscalerType.SRMD:
                    {
                        FileName += @"\srmd\srmd-ncnn-vulkan.exe";
                        break;
                    }
                default:
                    return;
            }

            Host ProcesHost = new Host(FileName);
            ProcesHost.Print("\n Startup upscale..... \n");

            string OutFile = File.Substring(0, File.Length - 4) + "_upscale.png";
            ProcesHost.Start("-i " + File + " -o " + OutFile + DopCmd);

            ProcesHost.Wait();
            MainWindow.Form.UpdateViewImg(OutFile);
        }
    }
}