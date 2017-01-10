using System;
using System.Configuration;
using System.Xml;
using System.Xml.Serialization;

namespace IQMedia.Service.Common.Config.Handlers
{
    public class XmlConfigurationSectionHandler : IConfigurationSectionHandler
    {
        #region IConfigurationSectionHandler Members

        public object Create(object parent, object configContext, XmlNode section)
        {
            object settings = null;

            if (section == null) return settings;

            var navigator = section.CreateNavigator();
            var typeName = (string)navigator.Evaluate("string(@type)");
            var sectionType = Type.GetType(typeName);
            var xs = new XmlSerializer(sectionType);
            var reader = new XmlNodeReader(section);
            settings = xs.Deserialize(reader);

            return settings;
        }

        #endregion
    }
}