using System.Xml.Serialization;

namespace IQMedia.Service.DiscoveryArchiveMetaDataUpdate.Config.Sections
{
    public class OutputType
    {
        /// <summary>
        /// The file extension of this output type.
        /// </summary>
        [XmlAttribute("ext")]
        public string Ext { get; set; }

        /// <summary>
        /// The additional command line paramaters that should be 
        /// passed to ffmpeg for this file type.
        /// </summary>
        //[XmlAttribute("ffmpegParams")]
        //public string FormatParams { get; set; }
    }
}
