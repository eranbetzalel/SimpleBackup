using System.IO;
using System.Text;

namespace Betzalel.Infrastructure.Extensions
{
  public static class FileStreamExtensions
  {
    public static void Write(this FileStream fileStream, string text, Encoding encoding)
    {
      var textBytes = encoding.GetBytes(text);

      fileStream.Write(textBytes, 0, textBytes.Length);
    }
  }
}