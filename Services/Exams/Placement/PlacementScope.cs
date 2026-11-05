namespace QdratNew.Services.Exams.Placement
{
    public enum PlacementScopeKind { Admin = 0, Partner = 1, AdminForPartner = 2 }

    // PPL-S2: نطاق التنفيذ. البناء عبر Admin/Partner/AdminForPartner فقط حتى لا يوجد نطاق شريك بلا PartnerId
    // (مقارنة OwnerPartnerId بقيمة null كانت ستُرجع اختبارات المعهد).
    public sealed record PlacementScope
    {
        private PlacementScope(
            PlacementScopeKind kind,
            int? partnerId,
            string? userId,
            string? userName,
            bool contractOverridden = false,
            string? overrideReason = null,
            Guid? dispatchGroupId = null)
        {
            Kind = kind;
            PartnerId = partnerId;
            UserId = userId;
            UserName = userName;
            ContractOverridden = contractOverridden;
            OverrideReason = overrideReason;
            DispatchGroupId = dispatchGroupId;
        }

        public PlacementScopeKind Kind { get; }
        public int? PartnerId { get; }
        public string? UserId { get; }
        public string? UserName { get; }

        // APD-S3.1: بيانات سجل الإرسال، لنطاق AdminForPartner فقط
        public bool ContractOverridden { get; }
        public string? OverrideReason { get; }
        public Guid? DispatchGroupId { get; }

        // الحارس يعامل AdminForPartner كنطاق الشريك: اختباراته ودفعاته وطلابه فقط
        public bool IsPartner => Kind != PlacementScopeKind.Admin;

        // APD-S3.1: الأدمن يرسل باسم شريك: الإتاحة والعقد فُحصا في IDispatchAudienceService.ValidateAsync
        public bool IsAdminForPartner => Kind == PlacementScopeKind.AdminForPartner;

        public static PlacementScope Admin(string? userId, string? userName = null)
            => new(PlacementScopeKind.Admin, null, userId, userName);

        public static PlacementScope Partner(int partnerId, string? userId, string? userName = null)
        {
            if (partnerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(partnerId));

            return new(PlacementScopeKind.Partner, partnerId, userId, userName);
        }

        public static PlacementScope AdminForPartner(
            int partnerId,
            string userId,
            string? userName,
            bool contractOverridden,
            string? overrideReason,
            Guid dispatchGroupId)
        {
            if (partnerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(partnerId));

            // سجل الإرسال يتطلب المرسل والمجموعة، فالفشل هنا أوضح من فشله داخل الـ Transaction
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("الإرسال باسم شريك يتطلب المرسل.", nameof(userId));

            if (dispatchGroupId == Guid.Empty)
                throw new ArgumentException("الإرسال باسم شريك يتطلب معرّف مجموعة الإرسال.", nameof(dispatchGroupId));

            return new(PlacementScopeKind.AdminForPartner, partnerId, userId, userName, contractOverridden, overrideReason, dispatchGroupId);
        }
    }
}
