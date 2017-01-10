using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace IQMedia.Service.Domain
{
    public class HighlightedPQOutput
    {
        [XmlArrayItem("Text")]
        public List<string> Highlights { get; set; }

        public string Message { get; set; }
        public int Status { get; set; }
    }
}
