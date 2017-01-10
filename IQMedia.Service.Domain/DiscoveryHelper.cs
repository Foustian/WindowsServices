using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.ComponentModel;

namespace IQMedia.Service.Domain
{
    public class ClientSettings
    {
        public Guid ClientGUID { get; set; }

        public Int64 ClientID { get; set; }

        public List<int> CountryList { get; set; }

        public Int64 CustomerID { get; set; }

        public int Exportlimit { get; set; }

        public int FeedsExportLimit { get; set; }

        public bool IsCompeteData { get; set; }

        public bool IsNielsenData { get; set; }

        public List<int> LicenseList { get; set; }

        public List<int> RegionList { get; set; }

        public string TimeZone { get; set; }

        public SentimentSettings SentimentSettings { get; set; }

        public double GMTHours { get; set; }

        public double DSTHours { get; set; }

        public bool UseProminenceMediaValue { get; set; }

        public int? RawMediaExpiration { get; set; }
    }

    public class SearchCriteria
    {
        public string SearchTerm { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime ToDate { get; set; }

        [XmlArrayItem(ElementName = "SubMediaType")]
        public List<string> SubMediaTypes { get; set; }

        public string Market { get; set; }

        public DiscoveryAdvanceSearchModel AdvanceSearchSettings { get; set; }
    }

    public class DiscoveryMediaResult
    {
        public DateTime? Date { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Market { get; set; }
        public int? PositiveSentiment { get; set; }
        public int? NegativeSentiment { get; set; }
        public string ArticleURL { get; set; }
        public int? Audience { get; set; }
        public decimal? IQAdsharevalue { get; set; }
        public string Nielsen_Result { get; set; }
        public string SubMediaType { get; set; }
        public string IQ_CC_Key { get; set; }
        public string CompeteImage { get; set; }
        public string TimeZone { get; set; }
        public Int16 IQLicense { get; set; }
        public string Affiliate { get; set; }
        public int Hits { get; set; }
        public string Outlet { get; set; }
    }

    public class IQCompeteAll
    {
        public string CompeteURL { get; set; }

        public decimal? IQ_AdShare_Value { get; set; }

        public int? c_uniq_visitor { get; set; }

        public Boolean IsCompeteAll { get; set; }

        public Boolean IsUrlFound { get; set; }
    }

    [XmlRoot(ElementName = "AdvanceSearchSettings")]
    public class DiscoveryAdvanceSearchModel
    {
        public TVAdvanceSearchSettings TVSettings { get; set; }

        public NewsAdvanceSearchSettings NewsSettings { get; set; }

        public LexisNexisAdvanceSearchSettings LexisNexisSettings { get; set; }

        public BlogAdvanceSearchSettings BlogSettings { get; set; }

        public ForumAdvanceSearchSettings ForumSettings { get; set; }

        public ProQuestAdvanceSearchSettings ProQuestSettings { get; set; }
    }

    public class TVAdvanceSearchSettings
    {
        public string SearchTerm { get; set; }

        public string ProgramTitle { get; set; }

        public string Appearing { get; set; }

        [XmlArrayItem(ElementName = "Category")]
        public List<string> CategoryList { get; set; }

        [XmlArrayItem(ElementName = "IQDma")]
        public List<string> IQDmaList { get; set; }

        [XmlArrayItem(ElementName = "Station")]
        public List<string> StationList { get; set; }

        [XmlArrayItem(ElementName = "Affiliate")]
        public List<string> AffiliateList { get; set; }

        [XmlArrayItem(ElementName = "Region")]
        public List<string> RegionList { get; set; }

        [XmlArrayItem(ElementName = "Country")]
        public List<string> CountryList { get; set; }
    }

    public class ClientTVSearchSettings
    {
        public List<string> DmaList { get; set; }

        public List<Station> StationList { get; set; }

        public List<string> AffiliateList { get; set; }

        public List<string> ClassList { get; set; }

        public List<int> RegionList { get; set; }

        public List<int> CountryList { get; set; }
    }

    public class NewsAdvanceSearchSettings
    {
        public string SearchTerm { get; set; }

        [XmlArrayItem(ElementName = "Publication")]
        public List<string> PublicationList { get; set; }

        [XmlArrayItem(ElementName = "Category")]
        public List<string> CategoryList { get; set; }

        [XmlArrayItem(ElementName = "PublicationCategory")]
        public List<int> PublicationCategoryList { get; set; }

        [XmlArrayItem(ElementName = "Market")]
        public List<string> MarketList { get; set; }

        [XmlArrayItem(ElementName = "Genre")]
        public List<string> GenreList { get; set; }

        [XmlArrayItem(ElementName = "Region")]
        public List<string> RegionList { get; set; }

        [XmlArrayItem(ElementName = "Country")]
        public List<string> CountryList { get; set; }

        [XmlArrayItem(ElementName = "Language")]
        public List<string> LanguageList { get; set; }

        [XmlArrayItem(ElementName = "ExcludeDomain")]
        public List<string> ExcludeDomainList { get; set; }
    }

    public class LexisNexisAdvanceSearchSettings
    {
        public string SearchTerm { get; set; }

        [XmlArrayItem(ElementName = "Publication")]
        public List<string> PublicationList { get; set; }

        [XmlArrayItem(ElementName = "Category")]
        public List<string> CategoryList { get; set; }

        [XmlArrayItem(ElementName = "PublicationCategory")]
        public List<int> PublicationCategoryList { get; set; }

        [XmlArrayItem(ElementName = "Genre")]
        public List<string> GenreList { get; set; }

        [XmlArrayItem(ElementName = "Region")]
        public List<string> RegionList { get; set; }

        [XmlArrayItem(ElementName = "Country")]
        public List<string> CountryList { get; set; }

        [XmlArrayItem(ElementName = "Language")]
        public List<string> LanguageList { get; set; }

        [XmlArrayItem(ElementName = "ExcludeDomain")]
        public List<string> ExcludeDomainList { get; set; }
    }

    public class BlogAdvanceSearchSettings
    {
        public string SearchTerm { get; set; }

        public string Author { get; set; }

        public string Title { get; set; }

        [XmlArrayItem(ElementName = "Source")]
        public List<string> SourceList { get; set; }

        [XmlArrayItem(ElementName = "ExcludeDomain")]
        public List<string> ExcludeDomainList { get; set; }
    }

    public class ForumAdvanceSearchSettings
    {
        public string SearchTerm { get; set; }

        public string Author { get; set; }

        public string Title { get; set; }

        [XmlArrayItem(ElementName = "Source")]
        public List<string> SourceList { get; set; }

        [XmlArrayItem(ElementName = "SourceType")]
        public List<string> SourceTypeList { get; set; }

        [XmlArrayItem(ElementName = "ExcludeDomain")]
        public List<string> ExcludeDomainList { get; set; }
    }

    public class ProQuestAdvanceSearchSettings
    {
        public string SearchTerm { get; set; }

        [XmlArrayItem(ElementName = "Publication")]
        public List<string> PublicationList { get; set; }

        [XmlArrayItem(ElementName = "Author")]
        public List<string> AuthorList { get; set; }

        [XmlArrayItem(ElementName = "Language")]
        public List<string> LanguageList { get; set; }
    }

    public partial class IQ_Dma
    {
        [XmlElement("name")]
        public string Name
        {
            get;
            set;
        }

        [XmlElement("num")]
        public string Num
        {
            get;
            set;
        }
    }
    public partial class Station_Affil
    {
        [XmlElement("name")]
        public string Name
        {
            get;
            set;
        }
    }

    public partial class Station
    {
        public string Station_Call_Sign { get; set; }

        public string StationID { get; set; }
    }

    public partial class IQ_Class
    {
        [XmlElement("name")]
        public string Name
        {
            get;
            set;
        }

        [XmlElement("num")]
        public string Num
        {
            get;
            set;
        }
    }
}
