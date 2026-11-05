using Abp.Reflection.Extensions;
using System;
using System.IO;

namespace Capstone;

public class AppVersionHelper
{
    public const string Version = "9.3.0";

    public static DateTime ReleaseDate => LzyReleaseDate.Value;

    private static readonly Lazy<DateTime> LzyReleaseDate = new Lazy<DateTime>(() => new FileInfo(typeof(AppVersionHelper).GetAssembly().Location).LastWriteTime);
}
