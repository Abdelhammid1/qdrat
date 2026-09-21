using Microsoft.AspNetCore.Mvc.Filters;
using QdratNew.Enums;

namespace QdratNew.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class PartnerFeatureAttribute : Attribute
    {
        public PartnerFeatureType Feature { get; }

        public PartnerFeatureAttribute(PartnerFeatureType feature)
        {
            Feature = feature;
        }
    }
}
