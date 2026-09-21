using System.ComponentModel.DataAnnotations.Schema;

namespace QdratNew.Entities
{
    public class ProfessionalModelPartner
    {
        public int Id { get; set; }

        public int ProfessionalModelId { get; set; }
        public ProfessionalModel ProfessionalModel { get; set; }

        public int PartnerId { get; set; }
        public Partner Partner { get; set; }
    }

}
