using CsvHelper;
using ServiceAppDMSRecieving;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ServiceAppDMSRecieving
{
    public class mmsPORAcsvFileService : ImmsPORAcsvFile
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAudilogs _audilogs;
        private readonly ImmsRCRcsvFile _mmsRCRcsvFile;

        public mmsPORAcsvFileService(ILogger<Worker> logger, IConfiguration configuration, IAudilogs audilogs, ImmsRCRcsvFile mmsRCRcsvFile)
        {
            _logger = logger;
            //_configuration = configuration;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _audilogs = audilogs;
            _mmsRCRcsvFile = mmsRCRcsvFile;
        }
        public async void CopyCSVfileMMS_PORA()
        {
            DateTime dateTime = DateTime.Now;
            string extendName = dateTime.ToString("MMddyyy_HHmmss");
            string sourceDirectory = _configuration.GetSection("FilePathforPORA:sourceDirectory").Value;  // UNC path to the CSV file 
            string destinationDirectory = _configuration.GetSection("FilePathforPORA:destinationDirectory").Value;
            string PDFdestinationDirectory = _configuration.GetSection("FilePathforPORA:PDFdestinationDirectory").Value;

            try
            {
                // Get all files in the source directory
                string[] files = Directory.GetFiles(sourceDirectory);
                //string[] files = Directory.GetFiles(sourceDirectory, "*.csv");

                if (files.Length > 0)
                {
                    // Ensure the destination directory exists
                    Directory.CreateDirectory(destinationDirectory);

                    List<string> csvFiles = files.Where(a => a.Contains(".csv")).ToList();

                    if (csvFiles.Count() > 0)
                    {
                        _audilogs.writeLogs("===== csv File for PORA =====.");
                        // Get the first file from the source directory
                        string firstFile = csvFiles[0];

                        string fileName = Path.GetFileName(firstFile); // Extract the file name

                        //extend csv filename
                        //string newfileName = string.Concat(Path.GetFileNameWithoutExtension(firstFile), "_", extendName, Path.GetExtension(firstFile));
                        string newfileName = String.Format("{0}_{1}{2}", Path.GetFileNameWithoutExtension(firstFile), extendName, Path.GetExtension(firstFile));

                        // Create the destination path (the target file location)
                        string destinationPath1 = Path.Combine(destinationDirectory, newfileName);

                        // Copy the file to the destination path
                        bool isdoneCopy = await CopyFile(firstFile, destinationPath1);

                        //save to database
                        bool isdone = await UpdateCsvToDatabase(destinationPath1);

                        // After the file is copied, delete the source file
                        if (isdone)
                        {
                            string deletesourcePath = Path.Combine(sourceDirectory, fileName);
                            File.Delete(deletesourcePath);
                            _audilogs.writeLogs("Source csv file deleted successfully.");
                        }
                    }

                    List<string> pdfFiles = files.Where(a => a.Contains(".pdf")).ToList();

                    if (pdfFiles.Count() > 0)
                    {
                        _audilogs.writeLogs("===== pdf File for PORA =====.");
                        // Get the first file from the source directory
                        string firstpdfFile = pdfFiles[0];

                        string RCRNumber = Path.GetFileNameWithoutExtension(firstpdfFile);

                        string pdffileName = String.Format("{0}_{1}{2}", Path.GetFileNameWithoutExtension(firstpdfFile), extendName, Path.GetExtension(firstpdfFile));

                        Directory.CreateDirectory(PDFdestinationDirectory);
                        // Create the destination path (the target file location)
                        string destinationpdfPath = Path.Combine(PDFdestinationDirectory, pdffileName);

                        // Copy the file to the destination path
                        bool isSuccessCopyingpdf = false;
                        int retryCount = 0;
                        do
                        {
                            isSuccessCopyingpdf = await CopyFile(firstpdfFile, destinationpdfPath);
                            retryCount++;
                        }
                        while (retryCount < 5 && !isSuccessCopyingpdf); //if failed retry copy file up to 5x 

                        //save to database
                        bool isdone = await InsertPDFToDatabase(destinationpdfPath, RCRNumber);

                        // After the file is copied, delete the source file
                        if (isdone)
                        {
                            File.Delete(firstpdfFile);
                            _audilogs.writeLogs("Source pdf file deleted successfully.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error: {0}", ex.Message);
            }
        }
        public async Task<bool> UpdateCsvToDatabase(string csvFilePath)
        {
            try
            {
                using (var reader = new StreamReader(csvFilePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    string connectionString = _configuration.GetSection("ConnectionStrings:DMS").Value;

                    // Create a new GUID
                    string uniqueUser = "C287E52E-5A0C-4FDA-836A-2DCADE1E00C4";// Guid uniqueUser = GenerateGuidFromString("SystemGenerated");

                    var records = csv.GetRecords<QF_PORA>(); // YourDataModel should represent the CSV structure

                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        foreach (var record in records)
                        {
                            bool isRCRNumberExist = await _mmsRCRcsvFile.IsRCRnumberexist("QF_RCRform", record.RCRNumber);

                            if (!isRCRNumberExist) //Insert
                            {
                                string query = "INSERT INTO QF_RCRform " +
                                              "(                                        " +
                                                "[ID]                                   " +
                                                ",[Createdby]                           " +
                                                ",[Datecreated]                         " +
                                                ",[RCRNumber]                           ";
                                                if (record.PORADate != null)
                                                {
                                                    query += ",[PORADate]               ";
                                                }
                                                if (record.PORADate != null)
                                                {
                                                    query += ",[PORAAmount]             ";
                                                }
                                                query += ")                             " +
                                                            "VALUES                     " +
                                                        "(                              " +
                                                            "@ID                        " +
                                                            ",@Createdby                " +
                                                            ",@Datecreated              " +
                                                            ",@RCRNumber                ";
                                                if (record.PORADate != null)
                                                {
                                                    query += ",@PORADate                ";
                                                }
                                                if (record.PORAAmount != null)
                                                {
                                                    query += ",@PORAAmount              ";
                                                }
                                                query += ")                             ";

                                DateTime now = DateTime.Now;
                                Guid uniqueId = Guid.NewGuid();

                                using (SqlCommand cmd = new SqlCommand(query, connection))
                                {
                                    cmd.Parameters.AddWithValue("@ID", uniqueId);
                                    cmd.Parameters.AddWithValue("@Createdby", uniqueUser);
                                    cmd.Parameters.AddWithValue("@Datecreated", now);
                                    cmd.Parameters.AddWithValue("@RCRNumber", record.RCRNumber);
                                    if (record.PORADate != null)
                                        cmd.Parameters.AddWithValue("@PORADate", record.PORADate);
                                    if (record.PORAAmount != null)
                                        cmd.Parameters.AddWithValue("@PORAAmount", record.PORAAmount);

                                    // Execute the query
                                    int rowsAffected = cmd.ExecuteNonQuery();
                                    if (rowsAffected > 0)
                                    {
                                        _audilogs.writeLogs("successfully inserted " + record.RCRNumber);
                                    }
                                }
                            }
                            else //Update
                            {
                                if (record.PORAAmount != null || record.PORADate != null)
                                {
                                    string queryUpdate = "UPDATE QF_RCRform SET ";

                                    if (record.PORAAmount != null && record.PORADate == null)
                                    {
                                        queryUpdate += "[PORAAmount] = @PORAAmount ";
                                    }
                                    else if (record.PORAAmount == null && record.PORADate != null)
                                    {
                                        queryUpdate += "[PORADate] = @PORADate ";
                                    }
                                    else
                                    {
                                        queryUpdate += "[PORAAmount] = @PORAAmount, [PORADate] = @PORADate ";
                                    }

                                    queryUpdate += "WHERE RCRNumber = @RCRNumber ";

                                    using (SqlCommand cmd = new SqlCommand(queryUpdate, connection))
                                    {
                                        if (record.PORAAmount != null && record.PORADate == null)
                                        {
                                            cmd.Parameters.AddWithValue("@PORAAmount", record.PORAAmount);
                                        }
                                        else if (record.PORAAmount == null && record.PORADate != null)
                                        {
                                            cmd.Parameters.AddWithValue("@PORADate", record.PORADate);
                                        }
                                        else
                                        {
                                            cmd.Parameters.AddWithValue("@PORAAmount", record.PORAAmount);
                                            cmd.Parameters.AddWithValue("@PORADate", record.PORADate);
                                        }
                                        cmd.Parameters.AddWithValue("@RCRNumber", record.RCRNumber);

                                        int rowsAffected = cmd.ExecuteNonQuery();
                                        if (rowsAffected > 0)
                                        {
                                            _audilogs.writeLogs("successfully update " + record.RCRNumber);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                _audilogs.writeLogs("Done for InsertCsvToDatabase method " + csvFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error: {0}", ex.Message);
                _audilogs.writeLogs(ex.Message);
                return false;
            }
        }

        public async Task<bool> InsertPDFToDatabase(string pdfFilePath, string RCRNumber)
        {
            try
            {
                // Connection string to your SQL Server database
                string connectionString = _configuration.GetSection("ConnectionStrings:DMS").Value;

                // Reading the new PDF file into a byte array
                byte[] newPdfData = File.ReadAllBytes(pdfFilePath);

                // SQL select query
                string selectquery = String.Format("select CASE WHEN Count(*) = 0 THEN 'True' ELSE 'False'END as IsExist from DMS_PORApdf where RCRNumber = '{0}'", RCRNumber);

                string PdfData = String.Format("mmsPORApdf;{0}", Path.GetFileName(pdfFilePath));

                // Create a new GUID
                Guid uniqueId = Guid.NewGuid();
                string uniqueUser = "C287E52E-5A0C-4FDA-836A-2DCADE1E00C4";// Guid uniqueUser = GenerateGuidFromString("SystemGenerated");
                DateTime now = DateTime.Now;
                bool IsNewData = false;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Create a SqlCommand
                    using (SqlCommand command = new SqlCommand(selectquery, connection))
                    {
                        // Execute the query and get a SqlDataReader
                        using (SqlDataReader sreader = command.ExecuteReader())
                        {
                            // Loop through the rows
                            while (sreader.Read())
                            {
                                if (sreader[0].ToString() == "True")
                                    IsNewData = true;
                            }
                        }
                    }

                    if (IsNewData)  //Insert New Data
                    {
                        string queryInsert = "INSERT INTO DMS_PORApdf                     " +
                                              "(                                         " +
                                                "[ID]                                    " +
                                                ",[Created by]                           " +
                                                ",[Date created]                         " +
                                                ",[RCRNumber]                            " +
                                                ",[PDF]                                  " +
                                              ")                                         " +
                                              "VALUES                                    " +
                                              "(                                         " +
                                                "@ID                                     " +
                                                ",@Createdby                             " +
                                                ",@Datecreated                           " +
                                                ",@RCRNumber                             " +
                                                ",@PDF                                   " +
                                              ")                                         ";

                        using (SqlCommand cmd = new SqlCommand(queryInsert, connection))
                        {
                            cmd.Parameters.AddWithValue("@ID", uniqueId);
                            cmd.Parameters.AddWithValue("@Createdby", uniqueUser);
                            cmd.Parameters.AddWithValue("@Datecreated", now);
                            cmd.Parameters.AddWithValue("@PDF", PdfData);
                            cmd.Parameters.AddWithValue("@RCRNumber", RCRNumber);

                            int rowsAffected = cmd.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                _audilogs.writeLogs(String.Format("{0} | {1} | {2}", "successfully inserted ", PdfData, RCRNumber));
                            }
                        }
                    }
                    else   //Update Data
                    {
                        string queryUpdate = "UPDATE DMS_PORApdf SET [PDF] = @PDF WHERE RCRNumber = @RCRNumber";

                        using (SqlCommand cmd = new SqlCommand(queryUpdate, connection))
                        {
                            cmd.Parameters.AddWithValue("@PDF", PdfData);
                            cmd.Parameters.AddWithValue("@RCRNumber", RCRNumber);

                            int rowsAffected = cmd.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                _audilogs.writeLogs(String.Format("{0} | {1} | {2}", "successfully updated ", PdfData, RCRNumber));
                            }
                        }
                    }

                }

                _audilogs.writeLogs("Done for InsertPDFToDatabase method " + pdfFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error: {0}", ex.Message);
                _audilogs.writeLogs(ex.Message);
                return false;
            }
        }
        public async Task<bool> CopyFile(string firstFile, string destinationPath1)
        {
            try
            {
                // Copy the file to the destination path
                File.Copy(firstFile, destinationPath1, true); // Overwrites if the file exists
                _audilogs.writeLogs("File copied successfully " + firstFile + " to new path");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error: {0}", ex.Message);
                _audilogs.writeLogs(ex.Message);
                return false;
            }
        }

        static Guid GenerateGuidFromString(string input)
        {
            // Use MD5 hash to get a 128-bit hash value from the string
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                return new Guid(hashBytes);
            }
        }
    }
}
