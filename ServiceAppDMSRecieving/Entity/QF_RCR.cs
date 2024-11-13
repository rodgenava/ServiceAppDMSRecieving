using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceAppDMSRecieving
{
    public class QF_RCRform
    {
        //public string ID { get; set; }  // Change the property types as per your CSV columns
        //public string Createdby { get; set; }
        //public DateTime Datecreated { get; set; }
        public string LocationCode { get; set; }
        public string Description { get; set; }
        public string PONumber { get; set; }
        public string VendorCode { get; set; }
        public string VendorName { get; set; }
        public DateTime? RCRDate { get; set; }
        public string RCRNumber { get; set; }
        public Decimal? RCRAmount { get; set; }
        public string VatCode { get; set; }
        public string PaymentTerms { get; set; }
        //public Decimal? AdjustedRCRAmount { get; set; }
        //public Decimal? SIAmount { get; set; }
        //public DateTime? PORADate { get; set; }
        //public Decimal? PORAAmount { get; set; }
        //public Decimal? FinalAmount { get; set; }
        //public string RCRStatus { get; set; }
        //public DateTime? DateProcessed { get; set; }

    }
}
