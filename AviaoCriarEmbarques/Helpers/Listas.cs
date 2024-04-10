using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AviaoCriarEmbarques.Helpers
{
    public class Listas
    {
        public static List<string> Assentos = new List<string>() {
            "645870000",
            "645870001",
            "645870002",
            "645870003",
            "645870004",
            "645870005",
            "645870006",
            "645870007",
            "645870008",
            "645870009",
        };

        public static List<string> Portoes = new List<string>
        {
            "645870000",
            "645870001",
            "645870002"
        };

        public enum EventStage
        {
            PreValidation = 10, PreOperation = 20, PostOperation = 40,
        }
        public enum ExecutionMode
        {
            Synchronous = 0, Asynchronous = 1,
        }
    }
}
