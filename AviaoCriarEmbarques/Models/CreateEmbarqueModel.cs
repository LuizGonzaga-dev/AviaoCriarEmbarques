using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AviaoCriarEmbarques.Models
{
    public class CreateEmbarqueModel
    {
        public string Nome { get; set; }
        public string Assento { get; set; }
        public string Portao { get; set; }
        public Guid GuidAviao { get; set; }

        public CreateEmbarqueModel()
        {
            
        }
    }
}
