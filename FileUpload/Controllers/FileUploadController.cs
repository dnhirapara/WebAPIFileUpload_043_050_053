using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using NLog;

namespace FileUpload.Controllers
{
    public class FileUploadController : ApiController
    {
        private static Logger logger;
        private const string connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=FileShareingSystemDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
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

        private void StoreDB()
        {

        }

        public Task<string> GetFileUrl(int id)
        {
            return Task.FromResult("Hello User Id: " + id);
        }

        public async Task<List<string>> Upload()
        {
            if (Request.Content.IsMimeMultipartContent("form-data"))
            {
                try
                {
                    string commonid = Guid.NewGuid().ToString("N");

                    string folder = ConfigurationManager.AppSettings["FileUploadLocation"] + commonid + "\\";
                    var path = System.Web.HttpContext.Current.Server.MapPath(folder);
                    logger.Info(ConfigurationManager.AppSettings["FileUploadLocation"]);
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
                    string uploadingFileName = provider.FileData.Select(x => x.LocalFileName).FirstOrDefault();
                    foreach (MultipartFileData i in provider.FileData)
                    {
                        logger.Info(i.LocalFileName);
                    }
                    logger.Info("Debugging.....");
                    foreach (HttpContent i in provider.Contents)
                    {
                        logger.Info(i.Headers.ContentDisposition.FileName);
                    }
                    string originalFileName = Path.Combine(fileuploadPath, (provider.Contents[0].Headers.ContentDisposition.FileName).Trim(new Char[] { '"' }));
                    logger.Info(originalFileName);
                    //string originalFileName = String.Concat(fileuploadPath, "\\" + (provider.Contents[0].Headers.ContentDisposition.FileName).Trim(new Char[] { '"' }));
                    logger.Info("Uploading File Name: " + uploadingFileName);
                    logger.Info("Original File Name: " + originalFileName);
                    if (File.Exists(originalFileName))
                    {
                        File.Delete(originalFileName);
                    }

                    File.Move(uploadingFileName, originalFileName);

                    //foreach (var singleFile in FileUpload1.PostedFiles)
                    //{
                    //string file_name = commonid + "_" + singleFile.FileName;
                    string file_name = originalFileName;
                    //singleFile.SaveAs(Server.MapPath(folder + "/") + file_name);
                    //}
                    try
                    {
                        SqlConnection sqlConnection = new SqlConnection(connectionString);
                        SqlCommand cmd = new SqlCommand();
                        cmd.Connection = sqlConnection;
                        cmd.CommandText = "INSERT INTO FileDetail (UplodeDate,FileName,UserId,ComonID)VALUES(@UplodeDate,@FileName,@UserId,@ComonID) ";
                        SqlParameter UplodeDate = new SqlParameter("@UplodeDate", DateTime.Now);
                        logger.Info(file_name);
                        logger.Info(commonid);
                        logger.Info(file_name.ToString(), commonid.ToString());
                        SqlParameter FileName = new SqlParameter("@FileName", file_name.ToString());
                        SqlParameter UserId = new SqlParameter("@UserId", 1);
                        SqlParameter comomid = new SqlParameter("@ComonID", commonid.ToString());
                        cmd.Parameters.Add(UplodeDate);
                        cmd.Parameters.Add(FileName);
                        cmd.Parameters.Add(UserId);
                        cmd.Parameters.Add(comomid);

                        sqlConnection.Open();
                        cmd.ExecuteNonQuery();
                        sqlConnection.Close();
                    }
                    catch (Exception e)
                    {
                        logger.Info(e);
                    }
                    return new List<string>() { "Success!!!" };
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
