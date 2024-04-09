using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AviaoCriarEmbarques.Models
{
    public class EmbarqueModel
    {
        public Guid EmbarqueGuid { get; set; }
        public string Assento {  get; set; }
        public string Portao { get; set; }
    }
}
