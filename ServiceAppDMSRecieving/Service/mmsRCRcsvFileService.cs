using CsvHelper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Formats.Asn1;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ServiceAppDMSRecieving
{
    public class mmsRCRcsvFileService : ImmsRCRcsvFile
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAudilogs _audilogs;
        public mmsRCRcsvFileService(ILogger<Worker> logger, IConfiguration configuration, IAudilogs audilogs)
        {
            _logger = logger;
            //_configuration = configuration;
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _audilogs = audilogs;
        }
        public async void CopyCSVfileMMS_RCR()
        {
            _logger.LogInformation("ConnectionStrings is  " + _configuration.GetSection("ConnectionStrings:DMS").Value);
            DateTime dateTime = DateTime.Now;
            string extendName = dateTime.ToString("MMddyyy_HHmmss");
            string sourceDirectory = _configuration.GetSection("FilePathforRCR:sourceDirectory").Value;  // UNC path to the CSV file 
            string destinationDirectory = _configuration.GetSection("FilePathforRCR:destinationDirectory").Value;
            string PDFdestinationDirectory = _configuration.GetSection("FilePathforRCR:PDFdestinationDirectory").Value;

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
                        _audilogs.writeLogs("===== csv File for RCR =====.");
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
                        bool isdone = await InsertCsvToDatabase(destinationPath1);

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
                        _audilogs.writeLogs("===== pdf File for RCR =====.");
                        // Get the first file from the source directory
                        string firstpdfFile = pdfFiles[0];

                        string RCRNumber = Path.GetFileNameWithoutExtension(firstpdfFile);

                        string pdffileName = String.Format("{0}_{1}{2}", Path.GetFileNameWithoutExtension(firstpdfFile), extendName, Path.GetExtension(firstpdfFile));

                        Directory.CreateDirectory(PDFdestinationDirectory);
                        // Create the destination path (the target file location)
                        string destinationpdfPath = Path.Combine(PDFdestinationDirectory, pdffileName);
                        string destinationPath1 = Path.Combine(destinationDirectory, pdffileName);

                        // Copy the file to the destination path
                        bool isSuccessCopyingpdf = false;
                        bool isSuccessCopyingpdfinlocal = false;
                        int retryCount = 0;
                        do
                        {
                            isSuccessCopyingpdf = await CopyFile(firstpdfFile, destinationpdfPath);
                            //isSuccessCopyingpdfinlocal = await CopyFile(firstpdfFile, destinationPath1);
                            retryCount++;
                            if (!isSuccessCopyingpdf)
                            {
                                _audilogs.writeLogs("firstpdfFile = " + firstpdfFile + " destinationpdfPath = " + destinationpdfPath);
                            }
                            //if (!isSuccessCopyingpdfinlocal)
                            //{
                            //    _audilogs.writeLogs("firstpdfFile = " + firstpdfFile + " destinationpdfPath = " + destinationPath1);
                            //}
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
                _audilogs.writeLogs(ex.Message);
            }
        }
        public async Task<bool> InsertCsvToDatabase(string csvFilePath)
        {
            try
            {
                using (var reader = new StreamReader(csvFilePath))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    string connectionString = _configuration.GetSection("ConnectionStrings:DMS").Value;

                    // Create a new GUID
                    string uniqueUser = "C287E52E-5A0C-4FDA-836A-2DCADE1E00C4";// Guid uniqueUser = GenerateGuidFromString("SystemGenerated");

                    var records = csv.GetRecords<QF_RCRform>(); // YourDataModel should represent the CSV structure

                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        foreach (var record in records)
                        {
                            bool isRCRexist = await IsRCRnumberexist("QF_RCRform", record.RCRNumber);
                            _audilogs.writeLogs("isRCRexist in insertion =>" + isRCRexist + " RCRNumber" + record.RCRNumber);
                            if (!isRCRexist)  //Insert New Data
                            {
                                // SQL command to insert the data
                                string query = "INSERT INTO QF_RCRform " +
                                              "(                                        " +
                                                "[ID]                                   " +
                                                ",[Createdby]                           " +
                                                ",[Datecreated]                         " +
                                                ",[Location]                            " +
                                                ",[PONumber]                            " +
                                                ",[VendorCode]                          " +
                                                ",[VendorName]                          " +
                                                ",[RCRDate]                             " +
                                                ",[RCRNumber]                           " +
                                                ",[RCRAmount]                           " +
                                                ",[RCRStatus]                           " +
                                                ",[Modifiedby]                          " +
                                                ",[VatCode]                             " +
                                                ",[PaymentTerms]                        " +
                                                ",[Tags]                                ";

                                        query += ")" +
                                                "VALUES                                   " +
                                                "(                                        " +
                                                "@ID                                    " +
                                                ",@Createdby                            " +
                                                ",@Datecreated                          " +
                                                ",@Location                             " +
                                                ",@PONumber                             " +
                                                ",@VendorCode                           " +
                                                ",@VendorName                           " +
                                                ",@RCRDate                              " +
                                                ",@RCRNumber                            " +
                                                ",@RCRAmount                            " +
                                                ",@RCRStatus                            " +
                                                ",@Modifiedby                           " +
                                                ",@VatCode                              " +
                                                ",@PaymentTerms                         " +
                                                ",@Tags                                 ";

                                        query += ")";

                                DateTime now = DateTime.Now;
                                //Guid uniqueId = GenerateGuidFromDateTime(now);
                                Guid uniqueId = Guid.NewGuid();

                                using (SqlCommand cmd = new SqlCommand(query, connection))
                                {
                                    cmd.Parameters.AddWithValue("@ID", uniqueId);
                                    cmd.Parameters.AddWithValue("@Createdby", uniqueUser);
                                    cmd.Parameters.AddWithValue("@Datecreated", now);

                                    cmd.Parameters.AddWithValue("@Location", record.LocationCode.Trim() + " " + record.Description.Trim());
                                    cmd.Parameters.AddWithValue("@PONumber", record.PONumber);
                                    cmd.Parameters.AddWithValue("@VendorCode", record.VendorCode);
                                    cmd.Parameters.AddWithValue("@VendorName", record.VendorName);
                                    cmd.Parameters.AddWithValue("@RCRDate", record.RCRDate);
                                    cmd.Parameters.AddWithValue("@RCRNumber", record.RCRNumber);
                                    cmd.Parameters.AddWithValue("@RCRAmount", record.RCRAmount);
                                    cmd.Parameters.AddWithValue("@RCRStatus", "Pending");
                                    cmd.Parameters.AddWithValue("@Modifiedby", "SystemGenerated");
                                    cmd.Parameters.AddWithValue("@VatCode", record.VatCode);
                                    cmd.Parameters.AddWithValue("@PaymentTerms", record.PaymentTerms);
                                    cmd.Parameters.AddWithValue("@Tags", true);

                                    // Execute the query
                                    int rowsAffected = cmd.ExecuteNonQuery();
                                    if (rowsAffected > 0)
                                    {
                                        _audilogs.writeLogs("successfully inserted " + record.RCRNumber);
                                    }
                                }
                            }
                            else   //Update Data
                            {
                                string queryUpdate = "UPDATE QF_RCRform SET " +
                                                                    "[Location] = @Location                        " +
                                                                    ",[PONumber] = @PONumber                        " +
                                                                    ",[VendorCode] = @VendorCode                    " +
                                                                    ",[VendorName] = @VendorName                    " +
                                                                    ",[RCRDate] = @RCRDate                          " +
                                                                    ",[RCRAmount] = @RCRAmount                      " +
                                                                    ",[RCRStatus] = @RCRStatus                      " +
                                                                    ",[Modifiedby] = @Modifiedby                    " +
                                                                    ",[VatCode] = @VatCode                          " +
                                                                    ",[PaymentTerms] = @PaymentTerms                " +
                                                                    ",[Tags] = @Tags                                " +
                                                                    " WHERE RCRNumber = @RCRNumber";

                                using (SqlCommand cmd = new SqlCommand(queryUpdate, connection))
                                {
                                    cmd.Parameters.AddWithValue("@Location", record.LocationCode.Trim() + " " + record.Description.Trim());
                                    cmd.Parameters.AddWithValue("@PONumber", record.PONumber);
                                    cmd.Parameters.AddWithValue("@VendorCode", record.VendorCode);
                                    cmd.Parameters.AddWithValue("@VendorName", record.VendorName);
                                    cmd.Parameters.AddWithValue("@RCRDate", record.RCRDate);
                                    cmd.Parameters.AddWithValue("@RCRAmount", record.RCRAmount);
                                    cmd.Parameters.AddWithValue("@RCRStatus", "Pending");
                                    cmd.Parameters.AddWithValue("@Modifiedby", "SystemGenerated");
                                    cmd.Parameters.AddWithValue("@VatCode", record.VatCode);
                                    cmd.Parameters.AddWithValue("@PaymentTerms", record.PaymentTerms);
                                    cmd.Parameters.AddWithValue("@Tags", true);
                                    cmd.Parameters.AddWithValue("@RCRNumber", record.RCRNumber);

                                    int rowsAffected = cmd.ExecuteNonQuery();
                                    if (rowsAffected > 0)
                                    {
                                        _audilogs.writeLogs(String.Format("{0} | {1}", "successfully updated ", record.RCRNumber));
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

                string PdfData = String.Format("<iframe src=uploads/mmsRCRpdf/{0} width=600 height=500></iframe>", Path.GetFileName(pdfFilePath));
                string PDFpath = String.Format("mmsRCRpdf;{0}", Path.GetFileName(pdfFilePath));

                // Create a new GUID
                Guid uniqueId = Guid.NewGuid();
                Guid uniqueUser = GenerateGuidFromString("SystemGenerated");
                DateTime now = DateTime.Now;
                bool IsNewData = false;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    bool isExist = await IsRCRnumberexist("DMS_RCRpdf", RCRNumber);
                    connection.Open();

                    if (!isExist)  //Insert New Data
                    {
                        string queryInsert = "INSERT INTO DMS_RCRpdf                     " +
                                              "(                                         " +
                                                "[ID]                                    " +
                                                ",[Created by]                           " +
                                                ",[Date created]                         " +
                                                ",[RCRNumber]                            " +
                                                ",[PDF]                                  " +
                                                ",[PDFpath]                              " +
                                              ")                                         " +
                                              "VALUES                                    " +
                                              "(                                         " +
                                                "@ID                                     " +
                                                ",@Createdby                             " +
                                                ",@Datecreated                           " +
                                                ",@RCRNumber                             " +
                                                ",@PDF                                   " +
                                                ",@PDFpath                               " +
                                              ")                                         ";

                        using (SqlCommand cmd = new SqlCommand(queryInsert, connection))
                        {
                            cmd.Parameters.AddWithValue("@ID", uniqueId);
                            cmd.Parameters.AddWithValue("@Createdby", uniqueUser);
                            cmd.Parameters.AddWithValue("@Datecreated", now);
                            cmd.Parameters.AddWithValue("@PDF", PdfData);
                            cmd.Parameters.AddWithValue("@PDFpath", PDFpath);
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
                        string queryUpdate = "UPDATE DMS_RCRpdf SET [PDF] = @PDF,[PDFpath] = @PDFpath  WHERE RCRNumber = @RCRNumber";

                        using (SqlCommand cmd = new SqlCommand(queryUpdate, connection))
                        {
                            cmd.Parameters.AddWithValue("@PDF", PdfData);
                            cmd.Parameters.AddWithValue("@PDFpath", PDFpath);
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
                _logger.LogInformation("InsertPDFToDatabase Error: {0}", ex.Message);
                _audilogs.writeLogs("InsertPDFToDatabase Error: {0}" + ex.Message);
                return false;
            }
        }
        public async Task<bool> CopyFile(string firstFile,string destinationPath1)
        {
            try
            {
                // Copy the file to the destination path
                File.Copy(firstFile, destinationPath1, true); // Overwrites if the file exists
                _audilogs.writeLogs("File copied successfully " + firstFile + " to new path " + destinationPath1);
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
        public async Task<bool> IsRCRnumberexist(string tableName, string rcrNumber)
        {
            try
            {
                // Connection string to your SQL Server database
                string connectionString = _configuration.GetSection("ConnectionStrings:DMS").Value;

                // SQL select query
                string selectquery = String.Format("select CASE WHEN Count(*) = 0 THEN 'False' ELSE 'True' END as IsExist from {0} where RCRNumber = '{1}'",tableName, rcrNumber);

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
                                    return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error: {0}", ex.Message);
                _audilogs.writeLogs(ex.Message);
                return false;
            }
        }
    }
}
