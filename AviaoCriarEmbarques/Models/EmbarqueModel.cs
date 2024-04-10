using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AviaoCriarEmbarques.Models
{
    public class EmbarqueModel
    {
        public Guid? EmbarqueGuid { get; set; }
        public String Assento {  get; set; }
        public String Portao { get; set; }
        public Guid AviaoGuid { get; set; }
        public String AviaoName { get; set; }
        public EmbarqueModel()
        {
            
        }
    }
}
