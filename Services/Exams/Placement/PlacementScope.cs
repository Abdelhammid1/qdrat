namespace QdratNew.Services.Exams.Placement
{
    public enum PlacementScopeKind { Admin = 0, Partner = 1 }

    // PPL-S2: نطاق التنفيذ. البناء عبر Admin/Partner فقط حتى لا يوجد نطاق شريك بلا PartnerId
    // (مقارنة OwnerPartnerId بقيمة null كانت ستُرجع اختبارات المعهد).
    public sealed record PlacementScope
    {
        private PlacementScope(PlacementScopeKind kind, int? partnerId, string? userId, string? userName)
        {
            Kind = kind;
            PartnerId = partnerId;
            UserId = userId;
            UserName = userName;
        }

        public PlacementScopeKind Kind { get; }
        public int? PartnerId { get; }
        public string? UserId { get; }
        public string? UserName { get; }

        public bool IsPartner => Kind == PlacementScopeKind.Partner;

        public static PlacementScope Admin(string? userId, string? userName = null)
            => new(PlacementScopeKind.Admin, null, userId, userName);

        public static PlacementScope Partner(int partnerId, string? userId, string? userName = null)
        {
            if (partnerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(partnerId));

            return new(PlacementScopeKind.Partner, partnerId, userId, userName);
        }
    }
}
