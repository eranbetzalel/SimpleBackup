using System.Collections.Generic;
using System.IO;

namespace Betzalel.Infrastructure.Extensions
{
  public static class DirectoryInfoExtensions
  {
    public static ICollection<FileInfo> GetAllFiles(this DirectoryInfo directoryInfo)
    {
      return GetAllFiles(directoryInfo, "*.*");
    }

    public static ICollection<FileInfo> GetAllFiles(this DirectoryInfo directoryInfo, string searchPattern)
    {
      var resultFiles = new List<FileInfo>();
      var unprocessedDirectories = new Queue<DirectoryInfo>();

      unprocessedDirectories.Enqueue(directoryInfo);

      while (!unprocessedDirectories.IsEmpty())
      {
        var tempDirectoryInfo = unprocessedDirectories.Dequeue();

        var directories = tempDirectoryInfo.GetDirectories();

        foreach (var directory in directories)
        {
          if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            continue;

          unprocessedDirectories.Enqueue(directory);
        }

        resultFiles.AddRange(tempDirectoryInfo.GetFiles(searchPattern));
      }

      return resultFiles;
    }
  }
}