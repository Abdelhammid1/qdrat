namespace QdratNew.Services.ProfessionalModels
{
    public enum ModelSendScopeKind { Admin = 0, Partner = 1, AdminForPartner = 2 }

    // PPM-S3.1: نطاق الإرسال. البناء عبر Admin/Partner/AdminForPartner فقط حتى لا يوجد نطاق شريك بلا PartnerId
    public sealed record ModelSendScope
    {
        private ModelSendScope(
            ModelSendScopeKind kind,
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

        public ModelSendScopeKind Kind { get; }
        public int? PartnerId { get; }
        public string? UserId { get; }
        public string? UserName { get; }

        // APD-S2.1: بيانات سجل الإرسال، لنطاق AdminForPartner فقط
        public bool ContractOverridden { get; }
        public string? OverrideReason { get; }
        public Guid? DispatchGroupId { get; }

        // الشريك نفسه يرسل من منطقته: الإتاحة والعقد يُفحصان داخل خدمة الإرسال
        public bool IsPartner => Kind == ModelSendScopeKind.Partner;

        // APD-S2.1: الأدمن يرسل باسم شريك: الإتاحة والعقد فُحصا في IDispatchAudienceService.ValidateAsync
        public bool IsAdminForPartner => Kind == ModelSendScopeKind.AdminForPartner;

        // ما يُنشأ بهذا النطاق يملكه الشريك ويخص دفعاته وطلابه
        public int? OwnerPartnerId => Kind != ModelSendScopeKind.Admin ? PartnerId : null;

        public static ModelSendScope Admin(string? userId, string? userName = null)
            => new(ModelSendScopeKind.Admin, null, userId, userName);

        public static ModelSendScope Partner(int partnerId, string? userId, string? userName = null)
        {
            if (partnerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(partnerId));

            return new(ModelSendScopeKind.Partner, partnerId, userId, userName);
        }

        public static ModelSendScope AdminForPartner(
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

            return new(ModelSendScopeKind.AdminForPartner, partnerId, userId, userName, contractOverridden, overrideReason, dispatchGroupId);
        }
    }
}
