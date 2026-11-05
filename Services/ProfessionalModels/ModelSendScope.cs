namespace QdratNew.Services.ProfessionalModels
{
    public enum ModelSendScopeKind { Admin = 0, Partner = 1 }

    // PPM-S3.1: نطاق الإرسال. البناء عبر Admin/Partner فقط حتى لا يوجد نطاق شريك بلا PartnerId
    public sealed record ModelSendScope
    {
        private ModelSendScope(ModelSendScopeKind kind, int? partnerId, string? userId, string? userName)
        {
            Kind = kind;
            PartnerId = partnerId;
            UserId = userId;
            UserName = userName;
        }

        public ModelSendScopeKind Kind { get; }
        public int? PartnerId { get; }
        public string? UserId { get; }
        public string? UserName { get; }

        public bool IsPartner => Kind == ModelSendScopeKind.Partner;

        public static ModelSendScope Admin(string? userId, string? userName = null)
            => new(ModelSendScopeKind.Admin, null, userId, userName);

        public static ModelSendScope Partner(int partnerId, string? userId, string? userName = null)
        {
            if (partnerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(partnerId));

            return new(ModelSendScopeKind.Partner, partnerId, userId, userName);
        }
    }
}
