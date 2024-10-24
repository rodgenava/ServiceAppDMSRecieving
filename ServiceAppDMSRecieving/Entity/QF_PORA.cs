using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ServiceAppDMSRecieving
{
    public class QF_PORA
    {
        public string RCRNumber { get; set; }
        public DateTime? PORADate { get; set; }
        public Decimal? PORAAmount { get; set; }
        public string PORAStatus { get; set; }
    }
}
