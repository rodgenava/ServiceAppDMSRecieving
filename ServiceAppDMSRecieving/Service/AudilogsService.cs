using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceAppDMSRecieving
{
    public class AudilogsService : IAudilogs
    {
        private readonly IConfiguration _configuration;
        public AudilogsService(IConfiguration configuration) 
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        public async void writeLogs(string logsdata = "") 
        {
            DateTime dateOnly = DateTime.Now;
            string fileName = String.Format("Audilogs{0}.txt", dateOnly.ToString("MMddyyyy"));
            string path = _configuration.GetSection("AuditLogs:links").Value;  // UNC path to the CSV file 
            string fullpath = Path.Combine(path, fileName);

            Directory.CreateDirectory(path);
            if (!File.Exists(fullpath))
            {
                File.Create(fileName);
            }

            using (var sw = new StreamWriter(fullpath, true))
            {
                DateTime localDate = DateTime.Now;
                sw.WriteLine(String.Format("{0} {1}", logsdata, localDate));
                sw.WriteLine("______________________________________________________________________________________________________");
            }
        }
    }
}
