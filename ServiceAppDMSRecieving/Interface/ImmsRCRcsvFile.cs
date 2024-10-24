using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceAppDMSRecieving
{
    public interface ImmsRCRcsvFile
    {
        void CopyCSVfileMMS_RCR();
        Task<bool> IsRCRnumberexist(string rcrNumber, string tableName);
    }
}
