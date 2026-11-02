using Microsoft.AspNetCore.Mvc.Filters;
using QdratNew.Enums;

namespace QdratNew.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class PartnerFeatureAttribute : Attribute
    {
        public PartnerFeatureType Feature { get; }

        /// <summary>
        /// PPL-S1.4: للتقارير فقط — يسمح بالقراءة بعد انتهاء العقد طالما HasDataAccess
        /// (حتى AccessUntilDate). لا يتجاوز عقدًا نشطًا لا يشمل الميزة.
        /// </summary>
        public bool AllowReadAfterExpiry { get; set; }

        public PartnerFeatureAttribute(PartnerFeatureType feature)
        {
            Feature = feature;
        }
    }
}
