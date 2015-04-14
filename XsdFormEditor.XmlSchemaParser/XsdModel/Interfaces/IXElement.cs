using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace XmlSchemaParser.XsdModel.Interfaces
{
    [Guid("30DF0297-9EB6-4254-A4E2-34A1A5231016")]
    public interface IXElement : IXmlSerializable
    {
        /// <summary>
        /// Element name.
        /// </summary>
        [DispId(1)]
        string Name { get; set; }

        [DispId(2)]
        string Prompt { get; set; }
  
    }


    public interface IXElement<T> : IXElement
    {
        /// <summary>
        /// Element value.
        /// </summary>
        [DispId(1)]
        T Value { get; set; }
    }
}