﻿using System.Collections.Generic;
using System.Management;

namespace SD_FXUI
{
    internal class GPUInfo
    {
        public List<string> GPUs = new List<string>();
        public bool GreenGPU = false;
        public bool RedGPU = false;
        public bool BlueGPU = false;
        public bool NormalBlueGPU = false;

        public GPUInfo()
        {
            using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string GPUName = (string)obj["Name"];
                    Host.Print("Name  -  " + GPUName);
                    Host.Print("DeviceID  -  " + obj["DeviceID"]);
                    Host.Print("AdapterRAM  -  " + obj["AdapterRAM"]);
                    Host.Print("AdapterDACType  -  " + obj["AdapterDACType"]);
                    Host.Print("Monochrome  -  " + obj["Monochrome"]);
                    Host.Print("InstalledDisplayDrivers  -  " + obj["InstalledDisplayDrivers"]);
                    Host.Print("DriverVersion  -  " + obj["DriverVersion"]);
                    Host.Print("VideoProcessor  -  " + obj["VideoProcessor"]);
                    Host.Print("VideoArchitecture  -  " + obj["VideoArchitecture"]);
                    Host.Print("VideoMemoryType  -  " + obj["VideoMemoryType"]);

                    GPUs.Add(GPUName);

                    if (GPUName.Contains("AMD"))
                    {
                        RedGPU = true;
                    }
                    else if (GPUName.Contains("Intel"))
                    {
                        BlueGPU = true;

                        // Intel ARC... Interesting...
                        NormalBlueGPU = GPUName.ToLower().Contains("arc");
                    }
                    else
                    {
                        GreenGPU = true;
                    }
                }
            }
        }
    }
}
