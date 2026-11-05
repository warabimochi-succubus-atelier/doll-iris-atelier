using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace DollIrisGenerator.Services
{
    public static class SvgValidator
    {
        /// <summary>Checks that hand-edited SVG is well-formed XML. DTDs are refused (no external entity loading).</summary>
        public static bool IsWellFormed(string text, out string error)
        {
            error = "";
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using var reader = XmlReader.Create(new StringReader(text), settings);
                var doc = XDocument.Load(reader);
                if (doc.Root == null || doc.Root.Name.LocalName != "svg")
                {
                    error = "ルート要素が <svg> ではありません。";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
