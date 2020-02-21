using System;
using System.IO;

namespace NeoFx.TestNode
{
    public class NodeOptions
    {
        public string UserAgent { get; set; } = string.Empty;
        public string StoragePath { get; set; } = GetDefaultStoragePath();

        static string GetDefaultStoragePath() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".neofx-test-node");
    }
}
