using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Xml.Linq;

namespace IQMedia.Service.Domain
{
    [Serializable]
    public class HighlightedTWOutput : IXmlSerializable
    {
        [XmlElement(ElementName = "Text")]
        public string Highlights { get; set; }

        public string Message { get; set; }
        public int Status { get; set; }

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
            try
            {
                reader.MoveToContent();
                var outerXml = reader.ReadOuterXml();
                XElement root = XElement.Parse(outerXml);

                this.Highlights = root.Elements(XName.Get("Text")).First().Value;
                this.Message = root.Elements(XName.Get("Message")).First().Value;
                var strstatus = root.Elements(XName.Get("Status")).FirstOrDefault();
                this.Status = strstatus != null ? Convert.ToInt32(strstatus.Value) : 1;
            }
            catch (Exception)
            {
                this.Highlights = string.Empty;
                this.Message = string.Empty;
                this.Status = 1;
            }

        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteElementString("Text", Highlights);
            writer.WriteElementString("Message", Message);
            writer.WriteElementString("Status", Status.ToString());
        }
    }
}
