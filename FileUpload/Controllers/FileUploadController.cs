using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using NLog;

namespace FileUpload.Controllers
{
    public class ResFileInfo
    {
        public string FileName { get; set; }
        public string Extension { get; set; }
        public long Length { get; set; }
    }
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class FileUploadController : ApiController
    {
        private static Logger logger;
        private const string connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=FileShareingSystemDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
        private const string prefixURL = "https://localhost:44319";
        SqlConnection sqlConnection;

        public FileUploadController()
        {
            logger = LogManager.GetCurrentClassLogger();
            sqlConnection = new SqlConnection(connectionString);
        }
        ~FileUploadController()
        {
            sqlConnection.Close();
        }
        private bool sendMail(string subject, string body, string emailTo)
        {
            string FromMail = "noreply.saloncheckin@gmail.com";
            MailMessage mail = new MailMessage();
            SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
            mail.From = new MailAddress(FromMail);
            mail.To.Add("darshikhirapara@gmail.com");
            mail.To.Add(emailTo);
            mail.Subject = subject;
            mail.Body = body;
            SmtpServer.Port = 587;
            SmtpServer.Credentials = new System.Net.NetworkCredential("noreply.saloncheckin@gmail.com", "Saloncheckin@sdp");
            SmtpServer.EnableSsl = true;
            SmtpServer.Send(mail);
            return true;
        }
        private void StoreDB(string file_name, string commonid, string _email ="darshikhirapara@gmail.com")
        {
            using (SqlConnection sqlConnection = new SqlConnection(connectionString))
            {
                sqlConnection.Open();
                Int32 userid;
                SqlCommand fileCmd = new SqlCommand();
                using (SqlCommand userCmd = new SqlCommand())
                {
                    userCmd.Connection = sqlConnection;
                    userCmd.CommandText = "INSERT INTO UserDetail (username,email,password)VALUES(@username,@email,@password)";
                    userCmd.Parameters.AddWithValue("username", "guest");
                    userCmd.Parameters.AddWithValue("email", _email);
                    userCmd.Parameters.AddWithValue("password", "guest");
                    //SqlParameter username = new SqlParameter("@username", "guest");
                    //SqlParameter email = new SqlParameter("@email", _email);
                    //SqlParameter password = new SqlParameter("@password", "guest");
                    userid = Convert.ToInt32(userCmd.ExecuteScalar());
                    logger.Info(userid);
                }

                fileCmd.Connection = sqlConnection;
                fileCmd.CommandText = "INSERT INTO FileDetail (UplodeDate,FileName,UserId,ComonID)VALUES(@UplodeDate,@FileName,@UserId,@ComonID) ";
                SqlParameter UplodeDate = new SqlParameter("@UplodeDate", DateTime.Now);
                SqlParameter FileName = new SqlParameter("@FileName", file_name.ToString());
                SqlParameter UserId = new SqlParameter("@UserId", userid);
                SqlParameter comomid = new SqlParameter("@ComonID", commonid.ToString());
                fileCmd.Parameters.Add(UplodeDate);
                fileCmd.Parameters.Add(FileName);
                fileCmd.Parameters.Add(UserId);
                fileCmd.Parameters.Add(comomid);
                fileCmd.ExecuteNonQuery();
            }
        }
        [HttpGet]
        public List<ResFileInfo> GetFileUrl(string key)
        {
            string folder = ConfigurationManager.AppSettings["FileUploadLocation"] + key + "\\";
            var path = System.Web.HttpContext.Current.Server.MapPath(folder);
            string dir = Path.GetDirectoryName(path);
            List<string> fileList = Directory.GetFiles(path).ToList();
            List<ResFileInfo> resFileInfos = new List<ResFileInfo>();
            fileList.ForEach(file => {
                logger.Debug(file);
                FileInfo fileInfo = new FileInfo(file);
                ResFileInfo resFileInfo = new ResFileInfo(){
                    FileName = fileInfo.Name,
                    Extension = fileInfo.Extension,
                    Length = fileInfo.Length
                };
                resFileInfos.Add(resFileInfo);
            });
            return resFileInfos;
        }
        [HttpPost]
        public async Task<List<string>> Upload()
        {
            if (Request.Content.IsMimeMultipartContent("form-data"))
            {
                try
                {
                    string commonid = Guid.NewGuid().ToString("N");
                    string folder = ConfigurationManager.AppSettings["FileUploadLocation"] + commonid + "\\";
                    var path = System.Web.HttpContext.Current.Server.MapPath(folder);
                    if (Directory.Exists(path))
                    {
                        logger.Info("Path Already Exists.");
                    }
                    else
                    {
                        Directory.CreateDirectory(path);
                    }
                    var fileuploadPath = path;
                    var provider = new MultipartFormDataStreamProvider(fileuploadPath);
                    var content = new StreamContent(HttpContext.Current.Request.GetBufferlessInputStream(true));
                    foreach (var header in Request.Content.Headers)
                    {
                        logger.Info(header.Key + " " + header.Value);
                        content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    await content.ReadAsMultipartAsync(provider);
                    foreach (MultipartFileData i in provider.FileData)
                    {
                        logger.Error(i.LocalFileName);
                    }
                    string originalFileName="";
                    string uploadingFileName = "";
                    if (provider.Contents.Count == 0)
                    {
                        logger.Info("Provide Atleast Onefile.");
                        HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.BadRequest, "Provide Atleast Onefile.");
                        throw new HttpResponseException(response);
                    }
                    foreach(MultipartFileData file in provider.FileData)
                    {
                        uploadingFileName = file.LocalFileName;
                        originalFileName = Path.Combine(fileuploadPath, (file.Headers.ContentDisposition.FileName).Trim(new Char[] { '"' }));
                        logger.Debug(uploadingFileName + " " + originalFileName);
                        File.Move(uploadingFileName, originalFileName);
                    }
                    string file_name = originalFileName;
                    try
                    {
                        StoreDB(file_name, commonid);
                    }
                    catch (Exception e)
                    {
                        logger.Info(e);
                    }
                    string body = $"Please goto these link to download your files. {prefixURL + "/api/FileUpload/?key=" + commonid}";
                    sendMail("File Uploaded Successfully.",body, "yashgarala29@gmail.com");
                    return new List<string>() { prefixURL+ "/api/FileUpload/?key=" + commonid };
                }
                catch (Exception e)
                {
                    logger.Info(e);
                    HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.BadRequest, e);
                    throw new HttpResponseException(response);
                }
            }
            else
            {
                
                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.BadRequest, "Invalid Request!");
                throw new HttpResponseException(response);
            }
        }
    }
}
