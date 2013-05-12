using System.IO;
using System.Reflection;

namespace Betzalel.Infrastructure.Utilities
{
  public class PathUtil
  {
    private static readonly string _executablePath;

    static PathUtil()
    {
      _executablePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
    }

    public static string MapToExecutable(string relativePath)
    {
      return Path.Combine(_executablePath, relativePath);
    }
  }
}