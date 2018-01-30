using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Logging;

namespace XMoat.Service
{
    public class HotfixLoader
    {
        public HotfixLoader()
        {
            appdomain = new ILRuntime.Runtime.Enviorment.AppDomain();
        }

        public async Task Load()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Hotfix");
            await LoadILRuntime(path);
        }

        ILRuntime.Runtime.Enviorment.AppDomain appdomain;

        private async Task LoadILRuntime(string path)
        {
            var dllPath = Path.Combine(path, "XMoat.Hotfix.dll");
            var pdbPath = Path.Combine(path, "XMoat.Hotfix.pdb");
            var dll = await File.ReadAllBytesAsync(dllPath);
            if (dll != null)
            {
                var pdb = await File.ReadAllBytesAsync(pdbPath);
                if (pdb != null)
                {
                    try
                    {
                        using (System.IO.MemoryStream fs = new MemoryStream(dll))
                        {
                            using (System.IO.MemoryStream p = new MemoryStream(pdb))
                            {
                                appdomain.LoadAssembly(fs, p, new Mono.Cecil.Pdb.PdbReaderProvider());
                                Log.Info($"Hotfix.LoadILRuntime: {dllPath}, {pdbPath}");
                            }
                        }
                        OnILRuntimeInitialized();
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"Hotfix.LoadILRuntime Exception: {ex.Message}");
                    }
                }
                else
                {
                    Log.Error($"Hotfix.LoadILRuntime Error: pdb NOT FOUND: {pdbPath}");
                }
            }
            else
            {
                Log.Error($"Hotfix.LoadILRuntime Error: dll NOT FOUND: {dllPath}");
            }
        }

        void OnILRuntimeInitialized()
        {
            appdomain.Invoke("XMoat.Hotfix.Init", "Start", null, null);
            Log.Info($"Hotfix.OnILRuntimeInitialized: Invoke = XMoat.Hotfix.Init.Start");
        }
    }
}
