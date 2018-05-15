using System.Collections.Generic;

namespace Analyzer
{
    public class PretendingClass
    {
        public PretendingClass()
        {
            Methods = new List<Method>();
        }

        public string Name { get; set; }
        public List<Method> Methods { get; set; }
        public string NameHostMicroService { get; set; }
        public string Interface { get; set; }
        public string Classe { get; set; }
    }
}
