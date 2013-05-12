using System.Collections.Generic;
using System.Linq;

namespace Betzalel.Infrastructure.Extensions
{
  public static class EnumerableExtensions
  {
    public static bool IsEmpty<TSource>(this IEnumerable<TSource> source)
    {
      return source == null || !source.Any();
    }

    public static string ToStringList<TSource>(this IEnumerable<TSource> source)
    {
      var strings = source.Select(p => p.ToString()).ToArray();

      return strings.Length == 0 ? string.Empty : string.Join(",", strings);
    }
  }
}
