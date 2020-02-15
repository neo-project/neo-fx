using System;
using System.IO;

namespace NeoFx.TestNode
{
    public class NodeOptions
    {
        public string UserAgent { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;

        public string GetStoragePath() => StoragePath.Length > 0 ? StoragePath 
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".neofx-test-node");
    }
}
