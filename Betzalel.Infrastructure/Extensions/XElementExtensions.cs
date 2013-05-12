using System;
using System.Xml.Linq;

namespace Betzalel.Infrastructure.Extensions
{
  public static class XElementExtensions
  {
    public static XAttribute NotNullAttribute(this XElement element, string name)
    {
      var attribute = element.Attribute(name);
      
      if(attribute == null)
        throw new Exception("Could not find \"" + name + "\" attribute.");

      return attribute;
    }
  }
}