using System;
using System.IO;
using MagnumOpus.Core.Strings;
using Xunit;

namespace MagnumOpus.Tests
{
    /// <summary>
    /// The shipped lang/ files against the strings table. For now only the English template exists:
    /// lang/en.txt is <see cref="Strings.DumpTemplate"/>, so a translator's starting point never lags
    /// the table. After adding or rewording a string, regenerate it by running the tests once with
    /// MAGNUMOPUS_WRITE_LANG_TEMPLATE=1. Per-language completeness checks join this when the first
    /// translation does (the Guildrun pattern).
    /// </summary>
    public class LangFilesTests : IDisposable
    {
        public void Dispose() => Strings.LoadTranslation(null);

        private static string LangDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
            return Path.Combine(dir.FullName, "lang");
        }

        [Fact]
        public void The_English_file_is_the_generated_template()
        {
            string path = Path.Combine(LangDir(), "en.txt");
            if (Environment.GetEnvironmentVariable("MAGNUMOPUS_WRITE_LANG_TEMPLATE") == "1")
            {
                Directory.CreateDirectory(LangDir());
                File.WriteAllText(path, Strings.DumpTemplate());
            }
            Assert.Equal(Strings.DumpTemplate(), File.ReadAllText(path).Replace("\r\n", "\n"));
        }
    }
}
