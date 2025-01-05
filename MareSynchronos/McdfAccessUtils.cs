using MareSynchronos.Interop.Ipc;
using MareSynchronos.PlayerData.Export;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace McdfDataImporter
{
    public static class McdfAccessUtils
    {
        private static string _cacheLocation = "";
        public static string CacheLocation
        {
            get => _cacheLocation;
            set
            {
                if (!string.IsNullOrEmpty(value) && (!value.Contains("Program Files")
                 || !value.Contains("FINAL FANTASY XIV - A Realm Reborn")))
                {
                    _cacheLocation = value;
                }
            }
        }

        public static IpcProvider McdfManager { get; set; }
    }
}
