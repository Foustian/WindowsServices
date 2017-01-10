using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace IQMedia.Service.Domain
{
    public class FeedsMediaResult
    {
        public DateTime MediaDate { get; set; }
        public string TimeZone { get; set; }
        public string AgentName { get; set; }
        public long NumHits { get; set; }
        public string Source { get; set; }
        public string Title { get; set; }
        public string Outlet { get; set; }
        public string Market { get; set; }
        public string Url { get; set; }
        public int? Audience { get; set; }
        public string AudienceSource { get; set; }
        public decimal? MediaValue { get; set; }
        public long? NationalAudience { get; set; }
        public decimal? NationalMediaValue { get; set; }
        public long? TwitterFollowers { get; set; }
        public long? TwitterFriends { get; set; }
        public long? KloutScore { get; set; }
        public short PositiveSentiment { get; set; }
        public short NegativeSentiment { get; set; }
        public ArticleStatsModel ArticleStats { get; set; }
        public string HighlightingText { get; set; }
    }

    public class ArticleStatsModel
    {
        public int Likes { get; set; }
        public int Shares { get; set; }
        public int Comments { get; set; }
        public bool IsVerified { get; set; }
    }

    public class FeedsSearchCriteria
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Keyword { get; set; }
        public short? Sentiment { get; set; }
        public string Dma { get; set; }
        public string Station { get; set; }
        public string CompeteUrl { get; set; }
        public string TwitterHandle { get; set; }
        public string Publication { get; set; }
        public string Author { get; set; }
        public bool? IsRead { get; set; }
        public short? ProminenceValue { get; set; }
        public bool IsProminenceAudience { get; set; }
        public bool IsOnlyParents { get; set; }
        public bool IsHeard { get; set; }
        public bool IsSeen { get; set; }
        public bool IsPaid { get; set; }
        public bool IsEarned { get; set; }
        public string ShowTitle { get; set; }
        [XmlArrayItem(ElementName = "DayOfWeek")]
        public List<int> DaysOfWeek { get; set; }
        [XmlArrayItem(ElementName = "TimeOfDay")]
        public List<int> TimesOfDay { get; set; }
        public long? SinceID { get; set; }
        [XmlArrayItem(ElementName = "SubMediaType")]
        public List<string> SubMediaTypes { get; set; }
        [XmlArrayItem(ElementName = "SearchRequestID")]
        public List<string> SearchRequestIDs { get; set; }
        [XmlArrayItem(ElementName = "DmaID")]
        public List<string> DmaIDs { get; set; }
        public bool? useGMT { get; set; }
    }
}
